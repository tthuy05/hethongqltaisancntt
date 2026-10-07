import assert from 'node:assert/strict';
import { lstat, readdir, readFile, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export async function verifyProductionPublish(directory) {
  const root = path.resolve(directory);
  assert(!(await lstat(root)).isSymbolicLink(), 'Published root must not be a link.');
  const files = [];
  async function walk(current) {
    for (const entry of await readdir(current, { withFileTypes: true })) {
      assert(!entry.isSymbolicLink(), 'Published files must not be links.');
      const filename = path.join(current, entry.name);
      if (entry.isDirectory()) await walk(filename);
      else {
        assert(entry.isFile(), 'Unexpected published entry.');
        const relative = path.relative(root, filename).replaceAll(path.sep, '/');
        assert(!/(?:^|\/)(?:mock|swagger|private|node_modules|\.git)(?:\/|$)|(?:mock-services|production-index)\.js$|appsettings\.[^.]+\.json$|(?:credentials|secrets).*\.json$|\.(?:pfx|pem|key|user)$/iu.test(relative), `Forbidden production file: ${relative}`);
        files.push(relative);
        if (/\.(?:html|js|css|json|txt)$/u.test(filename)) {
          const content = await readFile(filename, 'utf8');
          assert(!/postgres(?:ql)?:\/\/[^\s"']+|\bnpg_[a-z0-9]+\b/iu.test(content), `Database credential marker in production package: ${relative}`);
        }
      }
    }
  }
  await walk(root);
  for (const required of ['ItAssetManagement.Api.dll', 'frontend-production.marker', 'wwwroot/index.html', 'wwwroot/css/app.css', 'wwwroot/js/app.js', 'wwwroot/js/services/index.js']) {
    assert(files.includes(required), `Missing production file: ${required}`);
  }
  const settings = JSON.parse(await readFile(path.join(root, 'appsettings.json'), 'utf8'));
  function inspectConfiguration(value, prefix = '') {
    if (value && typeof value === 'object') {
      for (const [key, child] of Object.entries(value)) inspectConfiguration(child, prefix ? `${prefix}:${key}` : key);
    } else {
      // .NET JSON keys are case-insensitive and may already contain ':' paths.
      const key = prefix.toLowerCase();
      const nonempty = value !== null && value !== undefined && value !== '';
      assert(!(key === 'connectionstrings:defaultconnection' && nonempty), 'Published configuration must not contain a database credential.');
      assert(!(key === 'jwt:signingkey' && nonempty), 'Published configuration must not contain a JWT signing key.');
    }
  }
  inspectConfiguration(settings);
  const frontend = path.join(root, 'wwwroot');
  const entry = await readFile(path.join(frontend, 'js/services/index.js'), 'utf8');
  assert(entry.includes("mode !== 'api'") && !/createMockServices|demoRequested/u.test(entry), 'Published UI must be API-only.');
  for (const relative of files.filter(file => file.startsWith('wwwroot/') && file.endsWith('.js'))) {
    const filename = path.join(root, relative);
    const content = await readFile(filename, 'utf8');
    assert(!/\bimport\s*\(/u.test(content), 'Dynamic imports require a production review.');
    for (const [, reference] of content.matchAll(/\b(?:import|export)\s+(?:[^;]*?\s+from\s+)?["']([^"']+)["']/gu)) {
      assert(reference.startsWith('.'), 'Published modules must be local.');
      const dependency = path.resolve(path.dirname(filename), reference);
      const nested = path.relative(frontend, dependency);
      assert(nested && nested !== '..' && !nested.startsWith(`..${path.sep}`) && !path.isAbsolute(nested), 'Published import escapes frontend.');
      assert((await stat(dependency)).isFile(), 'Published import is missing.');
    }
  }
  return { files: files.length, publicAssets: files.filter(file => file.startsWith('wwwroot/')).length };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (process.argv.length !== 3) throw new Error('Usage: node scripts/verify-production-publish.mjs <publish-directory>');
  verifyProductionPublish(process.argv[2]).then(result => {
    console.log(`PASS: production package (${result.files} files, ${result.publicAssets} public assets); API-only UI, closed imports, no Development settings/mock/Swagger/database-secret markers.`);
    console.log('This checks the release artifact, not Git history or live Neon credential rotation.');
  }).catch(error => { console.error(error.message); process.exitCode = 1; });
}
