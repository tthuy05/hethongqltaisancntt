import test, { before } from 'node:test';
import assert from 'node:assert/strict';
import { readFile, stat, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { buildFrontend, frontendProductionOutput, frontendOutput, assertWritableOutput, verifyProductionFrontend } from '../scripts/frontend-build.mjs';

before(async () => { await buildFrontend({ production: true }); });

test('production packages a complete local API-only frontend graph without development artifacts', async () => {
  const files = await verifyProductionFrontend();
  assert(files.includes('index.html'));
  assert(files.includes('js/app.js'));
  assert(files.includes('js/services/api-services.js'));
  assert(!files.some(file => /mock|swagger|production-index|private|credentials/u.test(file)));
  for (const missing of ['js/mock/seed.js', 'js/services/mock-services.js', 'js/services/production-index.js', 'css/input.css', 'swagger/index.html']) {
    await assert.rejects(stat(path.join(frontendProductionOutput, missing)), { code: 'ENOENT' });
  }
  const html = await readFile(path.join(frontendProductionOutput, 'index.html'), 'utf8');
  for (const [, reference] of html.matchAll(/\b(?:src|href)=["']([^"']+)["']/gu)) {
    if (reference.startsWith('#')) continue;
    assert((await stat(path.resolve(frontendProductionOutput, reference))).isFile());
  }
  assert((await stat(path.join(frontendProductionOutput, 'css/app.css'))).size > 1000);
  assert.equal(files.filter(file => file.endsWith('.woff2')).length, 6);
  assert(files.includes('assets/fonts/OFL.txt'));
});

test('production ignores localhost demo query and rejects explicit mock without fallback', async () => {
  const priorLocation = globalThis.location;
  globalThis.location = { hostname: 'localhost', search: '?demo=1' };
  try {
    const { services, createServices } = await import(pathToFileURL(path.join(frontendProductionOutput, 'js/services/index.js')).href);
    assert.equal(services.mode, 'api');
    assert.throws(() => createServices({ mode: 'mock', hostname: 'localhost' }), /Unsupported production service mode/u);
    const failingApi = createServices({ fetchImpl: async () => { throw new Error('offline'); } });
    await assert.rejects(failingApi.auth.login({ email: 'test@example.invalid', password: 'not-a-real-password' }), error => error.code === 'NETWORK_ERROR');
    assert.equal(failingApi.auth.session, null);
  } finally {
    if (priorLocation === undefined) delete globalThis.location;
    else globalThis.location = priorLocation;
  }
});

test('production destination guards reject paths outside the exact artifact root', async () => {
  await assert.rejects(assertWritableOutput(frontendOutput, frontendProductionOutput), /Outside frontend output/u);
  await assert.rejects(assertWritableOutput(path.join(frontendProductionOutput, '..', 'unexpected'), frontendProductionOutput), /Outside frontend output/u);
  await assert.rejects(assertWritableOutput(frontendProductionOutput, '/unexpected-root'), /Unknown frontend output/u);
});

test('a production rebuild removes obsolete files only from the disposable production output', async () => {
  await writeFile(path.join(frontendProductionOutput, 'obsolete-build.txt'), 'obsolete artifact');
  await buildFrontend({ production: true });
  await assert.rejects(stat(path.join(frontendProductionOutput, 'obsolete-build.txt')), { code: 'ENOENT' });
  assert((await verifyProductionFrontend()).includes('index.html'));
});
