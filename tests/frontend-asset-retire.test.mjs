import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';
import { assetDetailPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/assets.js';
import { assetRetireDialog, canRetireAsset } from '../src/ItAssetManagement.Api/wwwroot/js/components/asset-retire-dialog.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const version = 'AQEBAQEBAQEBAQEBAQEBAQ==';
const asset = () => ({ id: 5, assetCode: 'AST-FIXTURE', name: 'Synthetic asset', status: 'InStock', rowVersion: version });
const grants = ['assets.read', 'assets.status.manage'];
async function api(handler) {
  const calls = []; const services = createApiServices({ fetchImpl: async (url, options) => {
    calls.push({ url, options }); if (url.endsWith('/auth/login')) return Response.json({ accessToken: 'fixture', tokenType: 'Bearer',
      expiresAt: new Date(Date.now() + 60000).toISOString(), user: { id: 7, roles: [], permissions: grants } });
    return handler(url, options);
  } });
  await services.auth.login({ email: 'fixture@example.test', password: 'fixture-password' }); return { services, calls };
}
function source(handler = async () => ({ ...asset(), status: 'Retired' })) {
  const calls = []; return { mode: 'api', calls, auth: { session: { id: 7, permissions: [...grants] } }, assets: {
    get: async () => asset(), retire: async (...args) => { calls.push(args); return handler(...args); }
  } };
}
const confirm = dom => descendants(dom.body).find(n => n.tagName === 'BUTTON' && n.textContent === 'Xác nhận ngừng sử dụng');
const reason = dom => descendants(dom.body).find(n => n.tagName === 'TEXTAREA');
test('retire adapter sends only current Retired status, trimmed reason and exact optimistic version', async () => {
  const { services, calls } = await api(() => Response.json({ ...asset(), status: 'Retired' }));
  try {
    await services.assets.retire(5, { reason: '  Hết vòng đời  ', rowVersion: version });
    assert.equal(calls[1].url, '/api/v1/assets/5/status'); assert.equal(calls[1].options.method, 'PATCH');
    assert.equal(calls[1].options.cache, 'no-store'); assert.equal(calls[1].options.headers.Authorization, 'Bearer fixture');
    assert.deepEqual(JSON.parse(calls[1].options.body), { status: 'Retired', reason: 'Hết vòng đời', rowVersion: version });
  } finally { await services.auth.logout(); }
});
test('invalid retire commands fail before network, including mass assignment and noncanonical Base64', async () => {
  const { services, calls } = await api(() => Response.json(asset()));
  try {
    const valid = { reason: 'Fixture', rowVersion: version };
    for (const id of [0, '5', -1, 1.5]) await assert.rejects(services.assets.retire(id, valid), { status: 400 });
    for (const dto of [null, [], {}, { ...valid, status: 'InUse' }, { ...valid, quantity: 1 }, { ...valid, reason: ' ' },
      { ...valid, reason: 'x'.repeat(1001) }, { ...valid, rowVersion: 'not-base64' }, { ...valid, rowVersion: 'AQEBAQEBAQEBAQEBAQEBA==' },
      { ...valid, rowVersion: version.slice(0, -2) }, { ...valid, rowVersion: 'AQEBAQEBAQEBAQEBAQEBAR==' }]) {
      await assert.rejects(services.assets.retire(5, dto), { status: 400 });
    }
    assert.equal(calls.length, 1);
  } finally { await services.auth.logout(); }
});
test('retire transport never retries 409/503/network and preserves status errors', async () => {
  for (const status of [401, 403, 409, 503, 0]) {
    const { services, calls } = await api(() => { if (!status) throw Error('fixture'); return Response.json({ code: 'FIXTURE_ERROR' }, { status }); });
    try { await assert.rejects(services.assets.retire(5, { reason: 'Fixture', rowVersion: version }), { status }); assert.equal(calls.length, 2); }
    finally { await services.auth.logout(); }
  }
});
test('retire UI is API-only, grant dependent and restricted to InStock/Broken, never workflow status editing', async () => {
  const dom = installDom(); try {
    const services = source(); assert.equal(canRetireAsset(services, asset()), true);
    for (const status of ['InUse', 'Maintenance', 'Retired', undefined]) assert.equal(canRetireAsset(services, { ...asset(), status }), false);
    assert.equal(canRetireAsset(services, { ...asset(), status: 'Broken' }), true);
    assert.equal(canRetireAsset(services, { ...asset(), isArchived: true }), false);
    services.mode = 'mock'; assert.equal(assetRetireDialog(services, asset()), null); services.mode = 'api';
    services.auth.session.permissions = ['assets.read']; assert.equal(assetRetireDialog(services, asset()), null);
    const root = await assetDetailPage(services, 5); assert.ok(!descendants(root).some(n => n.tagName === 'BUTTON' && n.textContent === 'Ngừng sử dụng'));
    services.auth.session.permissions = [...grants]; const permitted = await assetDetailPage(services, 5); dom.body.append(permitted);
    descendants(permitted).find(n => n.tagName === 'BUTTON' && n.textContent === 'Ngừng sử dụng').fire('click');
    assert.equal(descendants(dom.body).filter(n => n.tagName === 'DIALOG').length, 1);
    assert.match(dom.body.textContent, /Chưa có thao tác hoàn tác/); assert.equal(services.calls.length, 0);
  } finally { dom.restore(); }
});
test('explicit confirmation requires a reason, single-flights and refreshes only after successful completion', async () => {
  const dom = installDom(); try {
    let finish, completed = 0; const services = source(() => new Promise(resolve => { finish = resolve; }));
    assetRetireDialog(services, asset(), () => completed++);
    confirm(dom).fire('click'); assert.match(dom.body.textContent, /Vui lòng nhập lý do/); assert.equal(services.calls.length, 0);
    reason(dom).value = '  Hết vòng đời  '; const submit = confirm(dom); submit.fire('click'); submit.fire('click');
    assert.deepEqual(services.calls, [[5, { reason: 'Hết vòng đời', rowVersion: version }]]); assert.equal(reason(dom).readOnly, true);
    finish({ ...asset(), status: 'Retired' }); await until(() => completed === 1);
    assert.equal(descendants(dom.body).filter(n => n.tagName === 'DIALOG').length, 0);
  } finally { dom.restore(); }
});
test('409 and unknown write outcomes keep reason and block resubmit until explicit discard/read refresh', async () => {
  const dom = installDom(); try {
    for (const code of ['CONCURRENCY_CONFLICT', 'ASSET_ACTIVE_WORKFLOW', 'ASSET_STATUS_CONFLICT', 'NETWORK_ERROR']) {
      let refreshes = 0; window.dispatchEvent = () => { refreshes++; }; const services = source(async () => { throw Object.assign(new Error('synthetic-secret'), { code, status: code === 'NETWORK_ERROR' ? 0 : 409 }); });
      assetRetireDialog(services, asset()); reason(dom).value = 'Giữ bản nháp'; const submit = confirm(dom); submit.fire('click');
      await until(() => submit.disabled && submit.getAttribute('aria-busy') === null);
      assert.equal(reason(dom).value, 'Giữ bản nháp'); assert.equal(reason(dom).readOnly, false); assert.equal(services.calls.length, 1);
      assert.doesNotMatch(dom.body.textContent, /synthetic-secret/); assert.equal(refreshes, 0); submit.fire('click'); assert.equal(services.calls.length, 1);
      if (code === 'CONCURRENCY_CONFLICT') assert.ok(descendants(dom.body).some(n => n.classList.contains('concurrency-feedback')));
      const refresh = descendants(dom.body).find(n => n.tagName === 'BUTTON' && /Bỏ.*tải/.test(n.textContent)); refresh.fire('click');
      await until(() => refreshes === 1); assert.equal(services.calls.length, 1);
    }
  } finally { dom.restore(); }
});
test('session/grant change before confirmation blocks write and changes during write suppress stale success', async () => {
  const dom = installDom(); try {
    for (const change of ['logout', 'revoke', 'account']) {
      const services = source(); const modal = assetRetireDialog(services, asset()); reason(dom).value = 'Fixture';
      if (change === 'logout') services.auth.session = null; else if (change === 'revoke') services.auth.session.permissions = ['assets.read']; else services.auth.session.id = 8;
      confirm(dom).fire('click'); assert.equal(services.calls.length, 0); assert.match(dom.body.textContent, /Phiên hoặc quyền/); modal.close();
    }
    let finish, completed = 0; const services = source(() => new Promise(resolve => { finish = resolve; }));
    const modal = assetRetireDialog(services, asset(), () => completed++); reason(dom).value = 'Fixture'; confirm(dom).fire('click');
    services.auth.session = null; finish({ ...asset(), status: 'Retired' }); await until(() => dom.body.textContent.includes('Phiên hoặc quyền'));
    assert.equal(completed, 0); assert.doesNotMatch(dom.body.textContent, /Đã ngừng sử dụng/); modal.close();
  } finally { dom.restore(); }
});
test('closing a pending retire dialog discards late success and never resubmits or refreshes another page', async () => {
  const dom = installDom(); try {
    let finish, completed = 0; const services = source(() => new Promise(resolve => { finish = resolve; }));
    const modal = assetRetireDialog(services, asset(), () => completed++); reason(dom).value = 'Fixture'; confirm(dom).fire('click');
    modal.close(); finish({ ...asset(), status: 'Retired' }); await new Promise(resolve => setImmediate(resolve));
    assert.equal(services.calls.length, 1); assert.equal(completed, 0); assert.doesNotMatch(dom.body.textContent, /Đã ngừng sử dụng/);
  } finally { dom.restore(); }
});
test('retire dialog takes one session snapshot so expiry between getter reads cannot crash setup', () => {
  const dom = installDom(); try {
    const services = source(); const initial = services.auth.session; let reads = 0;
    services.auth = { get session() { return ++reads === 1 ? initial : null; } };
    const modal = assetRetireDialog(services, asset()); assert.ok(modal); assert.equal(reads, 1);
    reason(dom).value = 'Fixture'; confirm(dom).fire('click');
    assert.equal(services.calls.length, 0); assert.match(dom.body.textContent, /Phiên hoặc quyền/); modal.close();
  } finally { dom.restore(); }
});
