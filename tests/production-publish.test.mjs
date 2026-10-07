import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdir, mkdtemp, rm, symlink, unlink, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { verifyProductionPublish } from '../scripts/verify-production-publish.mjs';

const prefix = 'itam-production-publish-test-';

async function put(root, relative, content) {
  const destination = path.resolve(root, relative);
  const nested = path.relative(root, destination);
  assert(nested && nested !== '..' && !nested.startsWith(`..${path.sep}`) && !path.isAbsolute(nested));
  await mkdir(path.dirname(destination), { recursive: true });
  await writeFile(destination, content);
}

async function withFixture(run) {
  const temporaryParent = path.resolve(os.tmpdir());
  const root = await mkdtemp(path.join(temporaryParent, prefix));
  try {
    const fixture = {
      'ItAssetManagement.Api.dll': 'synthetic release file; not a runnable assembly',
      'frontend-production.marker': 'synthetic API-only package marker',
      'appsettings.json': JSON.stringify({ Logging: { LogLevel: { Default: 'Warning' } } }),
      'wwwroot/index.html': '<!doctype html><html><head><link rel="stylesheet" href="./css/app.css"><script type="module" src="./js/app.js"></script></head><body></body></html>',
      'wwwroot/css/app.css': 'body { color: black; }',
      'wwwroot/js/app.js': "import services from './services/index.js'; export default services;",
      'wwwroot/js/services/index.js': "import { createApiServices } from './api-services.js'; export function createServices({ mode = 'api' } = {}) { if (mode !== 'api') throw new TypeError('API only'); return createApiServices(); } export default createServices();",
      'wwwroot/js/services/api-services.js': "export function createApiServices() { return { mode: 'api' }; }",
    };
    for (const [relative, content] of Object.entries(fixture)) await put(root, relative, content);
    return await run(root);
  } finally {
    // Cleanup is restricted to this exact mkdtemp child, never a system/workspace root.
    assert.equal(path.dirname(path.resolve(root)), temporaryParent);
    assert(path.basename(root).startsWith(prefix));
    await rm(root, { recursive: true, force: true });
  }
}

test('release verifier accepts a self-contained synthetic API-only publish fixture', async () => {
  await withFixture(async root => {
    assert.deepEqual(await verifyProductionPublish(root), { files: 8, publicAssets: 5 });
  });
});

for (const forbidden of ['appsettings.Development.json', 'wwwroot/js/mock/seed.js', 'wwwroot/js/services/mock-services.js', 'wwwroot/swagger/index.html']) {
  test(`release verifier rejects forbidden development file ${forbidden}`, async () => {
    await withFixture(async root => {
      await put(root, forbidden, '{}');
      await assert.rejects(verifyProductionPublish(root), /Forbidden production file/u);
    });
  });
}

test('release verifier rejects a JWT signing key in published configuration', async () => {
  await withFixture(async root => {
    await put(root, 'appsettings.json', JSON.stringify({ Jwt: { SigningKey: 'synthetic-test-key-not-for-signing-real-tokens' } }));
    await assert.rejects(verifyProductionPublish(root), /JWT signing key/u);
  });
});

test('release verifier rejects any configured DefaultConnection credential', async () => {
  await withFixture(async root => {
    await put(root, 'appsettings.json', JSON.stringify({ ConnectionStrings: { DefaultConnection: 'Host=synthetic.invalid;Database=fixture;Username=test;Password=synthetic' } }));
    await assert.rejects(verifyProductionPublish(root), /database credential/u);
  });
});

for (const [name, settings, message] of [
  ['lowercase nested JWT key', { jwt: { signingkey: 'synthetic-test-key' } }, /JWT signing key/u],
  ['colon-flattened JWT key', { 'Jwt:SigningKey': 'synthetic-test-key' }, /JWT signing key/u],
  ['lowercase nested database key', { connectionstrings: { defaultconnection: 'Host=synthetic.invalid;Password=synthetic' } }, /database credential/u],
  ['colon-flattened database key', { 'ConnectionStrings:DefaultConnection': 'Host=synthetic.invalid;Password=synthetic' }, /database credential/u],
]) {
  test(`release verifier rejects ${name} using .NET configuration semantics`, async () => {
    await withFixture(async root => {
      await put(root, 'appsettings.json', JSON.stringify(settings));
      await assert.rejects(verifyProductionPublish(root), message);
    });
  });
}

test('release verifier rejects database URI credential markers in a public asset', async () => {
  await withFixture(async root => {
    await put(root, 'wwwroot/fixture.txt', 'postgresql://synthetic:synthetic@fixture.invalid/example');
    await assert.rejects(verifyProductionPublish(root), /Database credential marker/u);
  });
});

test('release verifier rejects a Neon password-shaped marker in a public asset', async () => {
  await withFixture(async root => {
    await put(root, 'wwwroot/fixture.txt', 'npg_syntheticfixtureonly');
    await assert.rejects(verifyProductionPublish(root), /Database credential marker/u);
  });
});

test('release verifier rejects a package without the explicit frontend marker', async () => {
  await withFixture(async root => {
    await unlink(path.join(root, 'frontend-production.marker'));
    await assert.rejects(verifyProductionPublish(root), /Missing production file: frontend-production.marker/u);
  });
});

test('release verifier rejects an unresolved relative import', async () => {
  await withFixture(async root => {
    await put(root, 'wwwroot/js/app.js', "import './missing.js';");
    await assert.rejects(verifyProductionPublish(root), error => error.code === 'ENOENT');
  });
});

test('release verifier rejects an import escaping the public frontend root', async () => {
  await withFixture(async root => {
    await put(root, 'wwwroot/js/app.js', "import '../../../outside.js';");
    await assert.rejects(verifyProductionPublish(root), /Published import escapes frontend/u);
  });
});

test('release verifier rejects external modules and unreviewed dynamic imports', async () => {
  await withFixture(async root => {
    await put(root, 'wwwroot/js/app.js', "import 'https://synthetic.invalid/module.js';");
    await assert.rejects(verifyProductionPublish(root), /Published modules must be local/u);
    await put(root, 'wwwroot/js/app.js', "import('./services/index.js');");
    await assert.rejects(verifyProductionPublish(root), /Dynamic imports require a production review/u);
  });
});

test('release verifier rejects published directory links rather than following them', async () => {
  await withFixture(async root => {
    await symlink(path.join(root, 'wwwroot', 'js'), path.join(root, 'linked-js'), 'junction');
    await assert.rejects(verifyProductionPublish(root), /Published files must not be links/u);
  });
});

test('release verifier rejects a linked publish root', async () => {
  await withFixture(async root => {
    const link = path.join(root, 'linked-root');
    await symlink(path.join(root, 'wwwroot'), link, 'junction');
    await assert.rejects(verifyProductionPublish(link), /Published root must not be a link/u);
  });
});
