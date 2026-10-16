import test from 'node:test';
import assert from 'node:assert/strict';
import { archiveDialog } from '../src/ItAssetManagement.Api/wwwroot/js/pages/shared.js';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';
import { assetListPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/assets.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const version = 'AQEBAQEBAQEBAQEBAQEBAQ==';
const asset = () => ({ id: 5, assetCode: 'AST-FIXTURE', name: 'Synthetic asset', status: 'InStock', rowVersion: version });
const grants = ['assets.read', 'assets.archive'];
function source(handler = async () => undefined) {
  const calls = []; return { mode: 'api', calls, auth: { session: { id: 7, permissions: [...grants] } }, assets: {
    archive: async (...args) => { calls.push(args); return handler(...args); }
  } };
}
const confirm = dom => descendants(dom.body).find(n => n.tagName === 'BUTTON' && n.textContent === 'Archive tài sản');

test('archive is default-deny for missing read/archive, actor, adapter or archived record', () => {
  const dom = installDom('/assets'); try {
    for (const permissions of [[], ['assets.read'], ['assets.archive'], 'assets.archive']) {
      const services = source(); services.auth.session.permissions = permissions;
      assert.equal(archiveDialog(services, asset()), null); assert.equal(services.calls.length, 0);
    }
    const services = source(); services.auth.session = null; assert.equal(archiveDialog(services, asset()), null);
    services.auth.session = { id: 7, permissions: grants }; assert.equal(archiveDialog(services, { ...asset(), isArchived: true }), null);
    services.assets.archive = undefined; assert.equal(archiveDialog(services, asset()), null);
  } finally { dom.restore(); }
});

test('archive captures reviewed identity/version and single-flights one explicit confirmation', async () => {
  const dom = installDom('/assets'); try {
    let finish, completed = 0; const services = source(() => new Promise(resolve => { finish = resolve; })); const reviewed = asset();
    archiveDialog(services, reviewed, () => completed++); reviewed.id = 99; reviewed.rowVersion = 'changed';
    const submit = confirm(dom); submit.fire('click'); submit.fire('click');
    assert.deepEqual(services.calls, [[5, version]]); assert.equal(submit.disabled, true);
    finish(); await until(() => completed === 1);
    assert.equal(descendants(dom.body).filter(n => n.tagName === 'DIALOG').length, 0);
    assert.match(dom.body.textContent, /Đã archive tài sản/);
  } finally { dom.restore(); }
});

test('archive conflicts and unknown outcomes never echo details or permit blind replay', async () => {
  const dom = installDom('/assets'); try {
    for (const [status, code] of [[409, 'CONCURRENCY_CONFLICT'], [409, 'ASSET_ACTIVE_WORKFLOW'], [0, 'NETWORK_ERROR'],
      [503, 'API_ERROR'], [401, 'SESSION_CHANGED'], [403, 'FORBIDDEN'], [404, 'ASSET_NOT_FOUND']]) {
      let completed = 0, refreshes = 0; window.dispatchEvent = () => { refreshes++; };
      const services = source(async () => { throw Object.assign(new Error('synthetic-secret'), { status, code }); });
      archiveDialog(services, asset(), () => completed++); const submit = confirm(dom); submit.fire('click');
      await until(() => submit.getAttribute('aria-busy') === null);
      assert.equal(submit.disabled, true); submit.fire('click'); assert.equal(services.calls.length, 1); assert.equal(completed, 0);
      assert.doesNotMatch(dom.body.textContent, /synthetic-secret/); assert.equal(refreshes, 0);
      const refresh = descendants(dom.body).find(n => n.tagName === 'BUTTON' && n.textContent === 'Đóng và tải lại danh sách');
      assert.ok(refresh); refresh.fire('click'); assert.equal(refreshes, 1);
    }
  } finally { dom.restore(); }
});

test('archive rechecks session/grants before write and suppresses success after an actor change', async () => {
  const dom = installDom('/assets'); try {
    for (const mutate of [s => { s.auth.session = null; }, s => { s.auth.session.id = 8; }, s => { s.auth.session.permissions = ['assets.read']; }]) {
      const services = source(); const modal = archiveDialog(services, asset()); mutate(services); confirm(dom).fire('click');
      assert.equal(services.calls.length, 0); assert.match(dom.body.textContent, /Phiên hoặc quyền/); modal.close();
    }
    let finish, completed = 0; const services = source(() => new Promise(resolve => { finish = resolve; }));
    const modal = archiveDialog(services, asset(), () => completed++); const submit = confirm(dom); submit.fire('click');
    services.auth.session.id = 8; finish(); await until(() => submit.getAttribute('aria-busy') === null);
    assert.equal(completed, 0); assert.doesNotMatch(dom.body.textContent, /Đã archive tài sản/); assert.equal(submit.disabled, true); modal.close();
  } finally { dom.restore(); }
});

test('archive close or hash change discards late success without refreshing another page', async () => {
  const dom = installDom('/assets'); try {
    for (const change of ['close', 'hash']) {
      let finish, completed = 0; const services = source(() => new Promise(resolve => { finish = resolve; })); location.hash = '#/assets';
      const modal = archiveDialog(services, asset(), () => completed++); const submit = confirm(dom); submit.fire('click');
      if (change === 'close') modal.close(); else location.hash = '#/users';
      finish(); await until(() => submit.getAttribute('aria-busy') === null);
      assert.equal(completed, 0); assert.equal(services.calls.length, 1); assert.doesNotMatch(dom.body.textContent, /Đã archive tài sản/); modal.close();
    }
  } finally { dom.restore(); }
});

test('archive snapshots a single session read and preserves explicit synthetic demo behavior', async () => {
  const dom = installDom('/assets'); try {
    const services = source(); const initial = services.auth.session; let reads = 0;
    services.auth = { get session() { return ++reads === 1 ? initial : null; } };
    const modal = archiveDialog(services, asset()); assert.ok(modal); assert.equal(reads, 1); confirm(dom).fire('click');
    assert.equal(services.calls.length, 0); modal.close();
    let completed = 0; const demo = source(); demo.mode = 'mock'; archiveDialog(demo, asset(), () => completed++); confirm(dom).fire('click');
    await until(() => completed === 1); assert.match(dom.body.textContent, /dữ liệu minh họa/);
  } finally { dom.restore(); }
});

test('archive adapter requires canonical 16-byte strong version before DELETE and sends no body', async () => {
  const calls = []; const services = createApiServices({ fetchImpl: async (url, options) => {
    calls.push({ url, options }); return url.endsWith('/auth/login') ? Response.json({ accessToken: 'fixture', tokenType: 'Bearer',
      expiresAt: new Date(Date.now() + 60000).toISOString(), user: { id: 7, roles: [], permissions: grants } }) : new Response(null, { status: 204 });
  } });
  try {
    await services.auth.login({ email: 'fixture@example.test', password: 'fixture' });
    for (const invalid of ['*', 'AQEBAQEBAQEBAQEBAQEBAR==', version.slice(0, -2), 'not-base64']) await assert.rejects(services.assets.archive(5, invalid), { status: 400 });
    assert.equal(calls.length, 1); await services.assets.archive(5, version); assert.equal(calls.length, 2);
    assert.equal(calls[1].options.method, 'DELETE'); assert.equal(calls[1].url, '/api/v1/assets/5');
    assert.equal(calls[1].options.headers['If-Match'], `"${version}"`); assert.equal(calls[1].options.body, undefined);
    assert.equal(calls[1].options.headers.Authorization, 'Bearer fixture'); assert.equal(calls[1].options.cache, 'no-store');
  } finally { await services.auth.logout(); }
});

async function mountList(dom, services, handler) {
  const catalog = { list: async () => ({ items: [], totalPages: 0 }) }; services.departments = services.assetTypes = catalog;
  services.assets.list = async () => ({ items: [asset()], totalItems: 1, totalPages: 1, page: 1, pageSize: 10 });
  let reads = 0; services.assets.get = async () => { reads++; return reads === 1 ? asset() : handler(); };
  const root = await assetListPage(services); dom.body.append(root);
  return { root, reads: () => reads, trigger: descendants(root).find(n => n.getAttribute('aria-label') === 'Archive AST-FIXTURE') };
}

test('list confirmation read single-flights and cannot open a late dialog after detach/hash/account/revoke', async () => {
  const dom = installDom('/assets'); try {
    for (const change of ['detach', 'hash', 'account', 'revoke', 'none']) {
      location.hash = '#/assets'; let finish; const services = source();
      const view = await mountList(dom, services, () => new Promise(resolve => { finish = resolve; }));
      view.trigger.fire('click'); view.trigger.fire('click'); assert.equal(view.reads(), 2); assert.equal(view.trigger.disabled, true);
      if (change === 'detach') view.root.remove(); else if (change === 'hash') location.hash = '#/users';
      else if (change === 'account') services.auth.session.id = 8; else if (change === 'revoke') services.auth.session.permissions = ['assets.read'];
      finish(asset()); await until(() => !view.trigger.disabled);
      const dialogs = descendants(dom.body).filter(n => n.tagName === 'DIALOG'); assert.equal(dialogs.length, change === 'none' ? 1 : 0);
      assert.equal(services.calls.length, 0); dialogs.forEach(n => n.remove()); view.root.remove();
    }
  } finally { dom.restore(); }
});

test('failed confirmation read stays generic and never sends archive; revoked grant blocks read before request', async () => {
  const dom = installDom('/assets'); try {
    const services = source(); const view = await mountList(dom, services, async () => { throw Error('synthetic-secret'); });
    services.auth.session.permissions = ['assets.read']; view.trigger.fire('click'); assert.equal(view.reads(), 1);
    services.auth.session.permissions = [...grants]; view.trigger.fire('click'); await until(() => !view.trigger.disabled);
    assert.match(dom.body.textContent, /Chưa gửi thao tác archive/); assert.doesNotMatch(dom.body.textContent, /synthetic-secret/);
    assert.equal(services.calls.length, 0); assert.equal(descendants(dom.body).filter(n => n.tagName === 'DIALOG').length, 0);
  } finally { dom.restore(); }
});
