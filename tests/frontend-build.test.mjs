import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import { stat, readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { buildFrontend, frontendOutput, projectRoot } from '../scripts/frontend-build.mjs';
import { createStaticServer, contentSecurityPolicy } from '../scripts/frontend-server.mjs';
import { swaggerOutput } from '../scripts/swagger-build.mjs';
import vm from 'node:vm';

let server;
let origin;

before(async () => {
  await buildFrontend();
  server = await createStaticServer();
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  origin = `http://127.0.0.1:${server.address().port}`;
});

after(async () => {
  if (server) await new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
});

function rawRequest(requestPath, method = 'GET') {
  return new Promise((resolve, reject) => {
    const request = http.request(`${origin}/`, { path: requestPath, method }, (response) => {
      let body = '';
      response.setEncoding('utf8');
      response.on('data', (chunk) => { body += chunk; });
      response.on('end', () => resolve({ status: response.statusCode, headers: response.headers, body }));
    });
    request.on('error', reject);
    request.end();
  });
}

test('build creates local CSS, six font assets and a font license', async () => {
  assert((await stat(path.join(frontendOutput, 'css', 'app.css'))).size > 1000);
  for (const subset of ['latin', 'vietnamese']) {
    for (const weight of [400, 500, 600]) {
      assert((await stat(path.join(frontendOutput, 'assets', 'fonts', `inter-${subset}-${weight}-normal.woff2`))).size > 1000);
    }
  }
  assert.match(await readFile(path.join(frontendOutput, 'assets', 'fonts', 'OFL.txt'), 'utf8'), /SIL OPEN FONT LICENSE/iu);
  await assert.rejects(stat(path.join(frontendOutput, 'css', 'input.css')), { code: 'ENOENT' });
});

test('Swagger distribution is built locally outside the application/static-preview output', async () => {
  for (const name of ['index.html', 'init.js', 'site.css', 'swagger-ui-bundle.js', 'swagger-ui.css', 'LICENSE']) {
    assert((await stat(path.join(swaggerOutput, name))).size > 0);
  }
  assert.equal((await rawRequest('/swagger/index.html')).status, 404);
  const html = await readFile(path.join(swaggerOutput, 'index.html'), 'utf8');
  assert(!/<script(?![^>]*\bsrc=)[^>]*>/iu.test(html));
  for (const [, reference] of html.matchAll(/\b(?:src|href)=["']([^"']+)["']/gu)) {
    if (reference === '/') continue;
    assert(!/^(?:https?:)?\/\//u.test(reference));
    assert((await stat(path.resolve(swaggerOutput, reference))).isFile());
  }
});

test('Swagger does not persist tokens or validate against remote services and rejects external targets', async () => {
  let config;
  const bundle = value => { config = value; };
  bundle.presets = { apis: {} };
  const window = { SwaggerUIBundle: bundle, location: { origin: 'http://localhost:5080' } };
  vm.runInNewContext(await readFile(path.join(swaggerOutput, 'init.js'), 'utf8'), { window, URL });
  assert.equal(config.persistAuthorization, false);
  assert.equal(config.queryConfigEnabled, false);
  assert.equal(config.validatorUrl, null);
  assert.equal(config.url, '/openapi/v1.json');
  assert.equal(config.requestInterceptor({ url: '/api/v1/assets' }).url, '/api/v1/assets');
  assert.throws(() => config.requestInterceptor({ url: 'https://example.test/api' }), /cùng origin/u);
});

test('entry point and query-string demo navigation are served with strict CSP', async () => {
  const response = await rawRequest('/?demo=1');
  assert.equal(response.status, 200);
  assert.match(response.headers['content-type'], /text\/html/u);
  assert.equal(response.headers['content-security-policy'], contentSecurityPolicy);
  assert(!contentSecurityPolicy.includes('unsafe-inline'));
  assert(!contentSecurityPolicy.includes('unsafe-eval'));
  assert.equal(response.headers['cache-control'], 'no-store');
  assert.equal(response.headers['x-content-type-options'], 'nosniff');
  assert.equal(response.headers['x-frame-options'], 'DENY');
});

test('built entry point and ES module graph reference existing local files', async () => {
  const html = await readFile(path.join(frontendOutput, 'index.html'), 'utf8');
  for (const [, reference] of html.matchAll(/\b(?:src|href)=["']([^"']+)["']/gu)) {
    if (reference.startsWith('#')) continue;
    assert(!/^(?:https?:)?\/\//u.test(reference), 'Built entry assets must not depend on a CDN.');
    assert((await stat(path.resolve(frontendOutput, reference))).isFile(), reference);
  }
  async function inspect(directory) {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const file = path.join(directory, entry.name);
      if (entry.isDirectory()) await inspect(file);
      else if (entry.name.endsWith('.js')) {
        const source = await readFile(file, 'utf8');
        for (const [, reference] of source.matchAll(/\b(?:import|export)\s+(?:[^;]*?\s+from\s+)?["'](\.[^"']+)["']/gu)) {
          assert((await stat(path.resolve(directory, reference))).isFile(), `${file}: ${reference}`);
        }
      }
    }
  }
  await inspect(path.join(frontendOutput, 'js'));
});

test('CSS and WOFF2 are self-hosted with correct media types', async () => {
  assert.equal((await rawRequest('/css/app.css')).headers['content-type'], 'text/css; charset=utf-8');
  assert.equal((await rawRequest('/assets/fonts/inter-vietnamese-400-normal.woff2')).headers['content-type'], 'font/woff2');
});

test('HEAD sends headers but no body', async () => {
  const response = await rawRequest('/', 'HEAD');
  assert.equal(response.status, 200);
  assert.equal(response.body, '');
  assert(Number(response.headers['content-length']) > 0);
});

test('API endpoints never fall back to frontend HTML or a fake backend', async () => {
  const response = await rawRequest('/api/v1/assets');
  assert.equal(response.status, 404);
  assert.match(response.body, /does not host the API/iu);
  assert.equal((await rawRequest('/api/v1/auth/login', 'POST')).status, 405);
});

test('only read-only GET and HEAD requests are accepted', async () => {
  for (const method of ['POST', 'PUT', 'PATCH', 'DELETE']) {
    const response = await rawRequest('/', method);
    assert.equal(response.status, 405);
    assert.equal(response.headers.allow, 'GET, HEAD');
  }
});

test('unknown files and directory listing return 404', async () => {
  assert.equal((await rawRequest('/unknown-route')).status, 404);
  assert.equal((await rawRequest('/assets/')).status, 404);
});

test('malformed, traversal, hidden and multiply encoded paths are rejected', async () => {
  for (const requestPath of ['/../README.md', '/%2e%2e/README.md', '/%252e%252e/README.md', '/%5c..%5cREADME.md', '/.env', '/%00file', '/bad%XX']) {
    assert.equal((await rawRequest(requestPath)).status, 400, requestPath);
  }
});

test('original-export relaxed preview cannot serve the repository or final build', async () => {
  await assert.rejects(createStaticServer({ root: projectRoot, previewOriginal: true }), /outside the repository/u);
  await assert.rejects(createStaticServer({ root: frontendOutput, previewOriginal: true }), /outside the repository/u);
});
