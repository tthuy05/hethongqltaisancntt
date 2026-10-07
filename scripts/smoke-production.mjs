import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import net from 'node:net';
import http from 'node:http';
import path from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { verifyProductionPublish } from './verify-production-publish.mjs';

// Deliberately synthetic configuration. Smoke checks never invoke a DB endpoint.
// Do not replace these values with real Neon credentials or production JWT keys.
const fixture = {
  ASPNETCORE_ENVIRONMENT: 'Production', DOTNET_ENVIRONMENT: 'Production',
  ConnectionStrings__DefaultConnection: 'Host=offline-fixture.neon.tech;Database=deployment_fixture;Username=fixture;Password=synthetic-not-a-live-password',
  Jwt__SigningKey: 'synthetic-deployment-smoke-key-never-used-for-real-tokens-2026',
  AllowedHosts: 'localhost;127.0.0.1', Frontend__Enabled: 'true', Swagger__Enabled: 'false',
  RENDER: 'true', RENDER_EXTERNAL_HOSTNAME: 'itam-offline-fixture.onrender.com',
};

async function freePort() {
  const listener = net.createServer();
  await new Promise((resolve, reject) => { listener.once('error', reject); listener.listen(0, '127.0.0.1', resolve); });
  const port = listener.address().port;
  await new Promise(resolve => listener.close(resolve));
  return port;
}

async function check(origin) {
  const request = (target, options = {}) => fetch(`${origin}${target}`, { signal: AbortSignal.timeout(5000), ...options });
  // Node fetch intentionally ignores caller Host overrides. Raw HTTP is needed
  // to verify the real ASP.NET HostFiltering middleware, not a helper string.
  const hostStatus = hostname => new Promise((resolve, reject) => {
    const pending = http.get(`${origin}/health/live`, { headers: { Host: hostname } }, response => {
      response.resume();
      response.once('end', () => resolve(response.statusCode));
    });
    pending.once('error', reject);
    pending.setTimeout(5000, () => pending.destroy(new Error('Host filter check timed out.')));
  });
  let ready = false;
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline) {
    try { ready = (await request('/health/live', { signal: AbortSignal.timeout(1000) })).status === 200; }
    catch { /* Startup may still be binding its port. */ }
    if (ready) break;
    await delay(250);
  }
  assert(ready, 'Production liveness did not become ready.');
  let checks = 1;
  for (const target of ['/', '/?demo=1', '/index.html', '/css/app.css', '/js/app.js', '/js/services/index.js', '/assets/fonts/inter-vietnamese-400-normal.woff2']) {
    const response = await request(target);
    assert.equal(response.status, 200, `Production asset failed: ${target}`);
    assert.equal(response.headers.get('cache-control'), 'no-store');
    assert.equal(response.headers.get('x-content-type-options'), 'nosniff');
    assert(response.headers.get('content-security-policy')?.includes("default-src 'self'"));
    checks++;
  }
  const services = await (await request('/js/services/index.js')).text();
  assert(services.includes("mode !== 'api'") && !services.includes('createMockServices'));
  checks++;
  const head = await request('/', { method: 'HEAD' });
  assert.equal(head.status, 200); assert.equal(await head.text(), ''); checks++;
  assert.equal(await hostStatus(fixture.RENDER_EXTERNAL_HOSTNAME), 200); checks++;
  assert.equal(await hostStatus('unlisted-host.invalid'), 400); checks++;
  for (const target of ['/js/mock/seed.js', '/js/services/mock-services.js', '/js/services/production-index.js', '/css/input.css', '/swagger/', '/openapi/v1.json', '/health/ready', '/appsettings.json', '/appsettings.Development.json', '/frontend-production.marker', '/.env', '/not-a-route']) {
    assert.equal((await request(target)).status, 404, `Private/dev path exposed: ${target}`);
    checks++;
  }
  for (const target of ['/api/v1/assets', '/api/v1/auth/me', '/api/v1/audit-logs']) {
    assert.equal((await request(target)).status, 401, `Business endpoint exposed anonymously: ${target}`);
    checks++;
  }
  return checks;
}

async function main() {
  const [mode, target, ...rest] = process.argv.slice(2);
  assert(['--publish', '--image'].includes(mode) && target && rest.length === 0, 'Usage: node scripts/smoke-production.mjs (--publish <directory> | --image <docker-image>)');
  const port = await freePort();
  const origin = `http://127.0.0.1:${port}`;
  let child;
  let output = '';
  const container = `itam-smoke-${process.pid}-${Date.now()}`;
  try {
    if (mode === '--publish') {
      const directory = path.resolve(target);
      await verifyProductionPublish(directory);
      child = spawn('dotnet', [path.join(directory, 'ItAssetManagement.Api.dll')], {
        cwd: directory, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'],
        env: { ...process.env, ...fixture, PORT: String(port) },
      });
      child.on('error', error => { output += error.message; });
      child.stdout.on('data', data => { output += data.toString(); });
      child.stderr.on('data', data => { output += data.toString(); });
    } else {
      const arguments_ = ['run', '--detach', '--rm', '--name', container, '--publish', `127.0.0.1:${port}:8080`];
      for (const [key, value] of Object.entries({ ...fixture, PORT: '8080' })) arguments_.push('--env', `${key}=${value}`);
      arguments_.push(target);
      const result = spawnSync('docker', arguments_, { encoding: 'utf8', windowsHide: true, timeout: 30000 });
      assert.equal(result.status, 0, 'Could not start the synthetic Production smoke container.');
    }
    const checks = await check(origin);
    console.log(`PASS: ${checks} Production HTTP smoke checks (${mode === '--image' ? 'Linux Docker container' : 'published .NET application'}). No database query, seed or migration; live Neon login/data verification remains a separate manual gate.`);
  } catch (error) {
    if (mode === '--image') output = spawnSync('docker', ['logs', container], { encoding: 'utf8', windowsHide: true, timeout: 10000 }).stdout || '';
    // Only this synthetic test process is logged; real hosting secrets are never supplied.
    console.error(error.message);
    if (output) console.error(output.slice(-3000));
    process.exitCode = 1;
  } finally {
    if (child) {
      const finished = new Promise(resolve => child.once('exit', resolve));
      if (child.exitCode === null) child.kill('SIGTERM');
      await Promise.race([finished, delay(3000)]);
      if (child.exitCode === null) child.kill('SIGKILL');
    }
    if (mode === '--image') spawnSync('docker', ['stop', '--time', '5', container], { stdio: 'ignore', windowsHide: true, timeout: 15000 });
  }
}

main().catch(error => { console.error(error.message); process.exitCode = 1; });
