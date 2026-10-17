import test from 'node:test';
import assert from 'node:assert/strict';
import { assetDetailPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/assets.js';
import { installDom } from './support/test-dom.mjs';

const asset = { id: 5, assetCode: 'AST-FIXTURE', name: 'Synthetic private asset', status: 'InStock',
  purchasePrice: 987654, rowVersion: 'AAAAAAAAAAAAAAAAAAAAAA==' };
function source(permissions = ['assets.read']) {
  const calls = { get: 0 };
  return { mode: 'api', calls, auth: { session: { id: 7, permissions } },
    assets: { get: async () => { calls.get++; return asset; } } };
}

test('asset detail denies missing identity or read grant before requesting private data', async () => {
  const dom = installDom('/assets/5'); try {
    const missing = source(); missing.auth.session = null;
    await assert.rejects(assetDetailPage(missing, 5), { status: 401 }); assert.equal(missing.calls.get, 0);
    const denied = source([]);
    await assert.rejects(assetDetailPage(denied, 5), { status: 403 }); assert.equal(denied.calls.get, 0);
  } finally { dom.restore(); }
});

test('asset detail refuses a late response after actor, grant or route changes', async () => {
  const dom = installDom('/assets/5'); try {
    for (const change of ['actor', 'grant', 'route']) {
      location.hash = '#/assets/5'; let finish; const services = source();
      services.assets.get = () => { services.calls.get++; return new Promise(resolve => { finish = resolve; }); };
      const loading = assetDetailPage(services, 5); assert.equal(services.calls.get, 1);
      if (change === 'actor') services.auth.session.id = 8;
      if (change === 'grant') services.auth.session.permissions = [];
      if (change === 'route') location.hash = '#/users';
      const rejected = assert.rejects(loading, error => [401, 403].includes(error.status));
      finish(asset); await rejected;
    }
  } finally { dom.restore(); }
});

test('asset detail rejects a response for another asset while preserving the authorized detail view', async () => {
  const dom = installDom('/assets/5'); try {
    const services = source(); services.assets.get = async () => ({ ...asset, id: 6 });
    await assert.rejects(assetDetailPage(services, 5), { status: 502, code: 'INVALID_API_RESPONSE' });
    services.assets.get = async () => asset;
    const root = await assetDetailPage(services, 5);
    assert.match(root.textContent, /Synthetic private asset/);
    assert.doesNotMatch(root.textContent, /987654/);
  } finally { dom.restore(); }
});
