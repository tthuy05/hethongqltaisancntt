import test from 'node:test';
import assert from 'node:assert/strict';
import { assetDetailPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/assets.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const row = () => ({ id: 1, fromStatus: null, toStatus: 'InStock', source: 'SYSTEM', reason: '<script>literal text</script>',
  changedAtUtc: '2026-10-15T04:02:01Z', changedByUserId: 7, changedByDisplayName: 'Tên hiện tại' });
const result = () => ({ items: [row()], page: 1, pageSize: 20, totalItems: 1, totalPages: 1 });
function source(handler = async () => result()) {
  const calls = [];
  return { mode: 'api', calls, auth: { session: { id: 7, permissions: ['assets.read', 'assets.history.read'] } }, assets: {
    get: async id => ({ id, assetCode: 'AST-FIXTURE', name: 'Synthetic asset', status: 'InStock' }),
    history: async (...args) => { calls.push(args); return handler(...args); }
  } };
}
const tab = (root, key) => descendants(root).find(n => n.dataset.key === key);
const panel = root => descendants(root).find(n => n.getAttribute('role') === 'tabpanel');
async function mount(dom, services) { const root = await assetDetailPage(services, 5); dom.body.append(root); return root; }
test('actual asset detail lazily reads status history; other two tabs stay PLANNED and do not fetch', async () => {
  const dom = installDom(); try {
    const services = source(); const root = await mount(dom, services);
    assert.equal(services.calls.length, 0); assert.match(panel(root).textContent, /Assignment.*PLANNED/);
    tab(root, 'maintenance').fire('click'); assert.match(panel(root).textContent, /Maintenance.*PLANNED/); assert.equal(services.calls.length, 0);
    tab(root, 'status').fire('click'); await until(() => panel(root).textContent.includes('Tên hiện tại'));
    assert.deepEqual(services.calls, [[5, { page: 1, pageSize: 20 }]]);
    assert.match(panel(root).textContent, /Khởi tạo.*Trong kho.*Tên hiện tại/);
    assert.match(panel(root).textContent, /04:02:01/); assert.match(panel(root).textContent, /15\/10\/2026/); assert.match(panel(root).textContent, /\(UTC\)/);
    assert.match(panel(root).textContent, /<script>literal text<\/script>/);
    assert.equal(descendants(root).filter(n => n.tagName === 'SCRIPT').length, 0);
    assert.ok(descendants(root).some(n => n.classList.contains('asset-history-reason')));
    assert.equal(tab(root, 'status').getAttribute('aria-selected'), 'true');
    assert.equal(panel(root).getAttribute('aria-labelledby'), 'history-tab-status');
  } finally { dom.restore(); }
});
test('missing read/history grant, demo mode and absent adapter never query or invent empty history', async () => {
  const dom = installDom(); try {
    const noHistory = source(); noHistory.auth.session.permissions = ['assets.read']; const readable = await mount(dom, noHistory);
    tab(readable, 'status').fire('click'); assert.match(panel(readable).textContent, /không có quyền/);
    assert.equal(noHistory.calls.length, 0); readable.remove();
    const noAssetRead = source(); noAssetRead.auth.session.permissions = ['assets.history.read'];
    await assert.rejects(mount(dom, noAssetRead), { status: 403 }); assert.equal(noAssetRead.calls.length, 0);
    for (const mode of ['mock', 'missing']) {
      const services = source(); if (mode === 'mock') services.mode = 'mock'; else delete services.assets.history;
      const root = await mount(dom, services); tab(root, 'status').fire('click');
      assert.match(panel(root).textContent, /Chưa kết nối.*Không có dữ liệu mô phỏng/); assert.equal(services.calls.length, 0); root.remove();
    }
  } finally { dom.restore(); }
});
test('empty result is visible outside a wide table and missing actor is not fabricated', async () => {
  const dom = installDom(); try {
    const services = source(async () => ({ ...result(), items: [], totalItems: 0, totalPages: 0 }));
    const root = await mount(dom, services); tab(root, 'status').fire('click');
    await until(() => panel(root).textContent.includes('Chưa có lịch sử'));
    assert.equal(descendants(panel(root)).filter(n => n.tagName === 'TABLE').length, 0);
    services.assets.history = async () => ({ ...result(), items: [{ ...row(), changedByDisplayName: null }] });
    tab(root, 'status').fire('click'); await until(() => panel(root).textContent.includes('Chưa có tên người thực hiện'));
    services.assets.history = async () => ({ ...result(), items: [{ ...row(), changedByDisplayName: null, changedByUserId: null }] });
    tab(root, 'status').fire('click'); await until(() => panel(root).textContent.includes('Các lần') || descendants(panel(root)).some(n => n.tagName === 'TABLE'));
    assert.doesNotMatch(panel(root).textContent, /Chưa có tên người thực hiện/);
  } finally { dom.restore(); }
});
test('pagination remains local to history and preserves asset route, page size and newest-first contract', async () => {
  const dom = installDom('/assets/5'); try {
    const services = source(async (_id, query) => ({ items: Array.from({ length: query.page === 1 ? 20 : 1 }, (_, i) => ({ ...row(), id: i + 1 })),
      page: query.page, pageSize: 20, totalItems: 21, totalPages: 2 }));
    const root = await mount(dom, services); tab(root, 'status').fire('click'); await until(() => panel(root).textContent.includes('1–20'));
    descendants(panel(root)).find(n => n.getAttribute('aria-label') === 'Trang sau').fire('click');
    await until(() => panel(root).textContent.includes('21–21'));
    assert.deepEqual(services.calls.at(-1), [5, { page: 2, pageSize: 20 }]); assert.equal(location.hash, '#/assets/5');
  } finally { dom.restore(); }
});
test('HTTP failure is visible, only explicit read retry is allowed and server details are not echoed', async () => {
  const dom = installDom(); try {
    for (const status of [401, 403, 404, 503]) {
      const services = source(async () => { throw Object.assign(new Error('synthetic-secret'), { status }); });
      const root = await mount(dom, services); tab(root, 'status').fire('click'); await until(() => descendants(panel(root)).some(n => n.getAttribute('role') === 'alert'));
      assert.doesNotMatch(panel(root).textContent, /synthetic-secret|Hiển thị 0/); assert.equal(services.calls.length, 1);
      const retry = descendants(panel(root)).find(n => n.tagName === 'BUTTON' && n.textContent === 'Thử lại');
      assert.equal(Boolean(retry), status === 503);
      if (retry) { retry.fire('click'); await until(() => services.calls.length === 2); } root.remove();
    }
  } finally { dom.restore(); }
});
test('late responses cannot overwrite another tab or detached page', async () => {
  const dom = installDom(); try {
    let finish; const services = source(() => new Promise(resolve => { finish = resolve; }));
    const root = await mount(dom, services); tab(root, 'status').fire('click'); tab(root, 'maintenance').fire('click');
    finish(result()); await new Promise(resolve => setImmediate(resolve)); assert.match(panel(root).textContent, /Maintenance.*PLANNED/);
    tab(root, 'status').fire('click'); root.remove(); finish(result()); await new Promise(resolve => setImmediate(resolve));
    assert.doesNotMatch(panel(root).textContent, /Tên hiện tại/);
  } finally { dom.restore(); }
});
test('account or history permission change discards an in-flight response', async () => {
  const dom = installDom(); try {
    for (const change of ['account', 'permission', 'logout']) {
      let finish; const services = source(() => new Promise(resolve => { finish = resolve; })); const root = await mount(dom, services);
      tab(root, 'status').fire('click');
      if (change === 'account') services.auth.session = { ...services.auth.session, id: 8 };
      else if (change === 'permission') services.auth.session.permissions = ['assets.read']; else services.auth.session = null;
      finish(result()); await until(() => panel(root).textContent.includes('Phiên hoặc quyền đã thay đổi'));
      assert.doesNotMatch(panel(root).textContent, /Tên hiện tại/); root.remove();
    }
  } finally { dom.restore(); }
});
test('keyboard End/Home changes selected history tab with correct roving tabindex', async () => {
  const dom = installDom(); try {
    const services = source(); const root = await mount(dom, services);
    for (const handler of tab(root, 'assignment').handlers.get('keydown')) handler({ key: 'End', preventDefault() {} });
    await until(() => panel(root).textContent.includes('Tên hiện tại'));
    assert.equal(tab(root, 'status').tabIndex, 0); assert.equal(tab(root, 'assignment').tabIndex, -1);
    for (const handler of tab(root, 'status').handlers.get('keydown')) handler({ key: 'Home', preventDefault() {} });
    assert.match(panel(root).textContent, /Assignment.*PLANNED/); assert.equal(tab(root, 'assignment').tabIndex, 0);
  } finally { dom.restore(); }
});
