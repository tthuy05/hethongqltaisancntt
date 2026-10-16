import test from 'node:test';
import assert from 'node:assert/strict';
import { assetFormPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/asset-form.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const asset = () => ({ id: 5, name: 'Synthetic asset', assetCode: 'AST-FIXTURE', assetTypeId: 1, owningDepartmentId: 1,
  assetType: { id: 1, name: 'Reference' }, owningDepartment: { id: 1, name: 'Reference' }, status: 'InStock', rowVersion: 'AQEBAQEBAQEBAQEBAQEBAQ==' });
const catalog = async () => ({ items: [{ id: 1, name: 'Reference', isActive: true }], page: 1, pageSize: 100, totalItems: 1, totalPages: 1 });
function source(handler) {
  const calls = []; return { mode: 'api', calls, auth: { session: { id: 7, permissions: ['assets.read', 'assets.create', 'assets.update'] } },
    assetTypes: { list: catalog }, departments: { list: catalog }, assets: { get: async () => asset(),
      create: async dto => { calls.push(dto); return handler(); }, update: async (_id, dto) => { calls.push(dto); return handler(); } } };
}
const control = (root, name) => descendants(root).find(n => n.getAttribute('name') === name);
const form = root => descendants(root).find(n => n.tagName === 'FORM');
const submit = root => descendants(root).find(n => n.tagName === 'BUTTON' && n.getAttribute('type') === 'submit');
async function mount(dom, services, id = null) {
  const root = await assetFormPage(services, id); dom.body.append(root);
  if (id == null) { control(root, 'name').value = 'New synthetic asset'; control(root, 'assetTypeId').value = control(root, 'owningDepartmentId').value = '1'; }
  return root;
}
test('save cannot begin without the correct grants or after actor/grants change', async () => {
  const dom = installDom(); try {
    const services = source(async () => asset()); services.auth.session.permissions = ['assets.read'];
    await assert.rejects(assetFormPage(services), { status: 403 });
    services.auth.session.permissions.push('assets.create'); const root = await mount(dom, services);
    services.auth.session = null; form(root).fire('submit'); assert.equal(services.calls.length, 0); assert.equal(submit(root).disabled, true);
    assert.match(root.textContent, /Phiên hoặc quyền đã thay đổi/);
  } finally { dom.restore(); }
});
test('late initial load cannot mount data under another account', async () => {
  const services = source(async () => asset()); let finish;
  services.assets.get = () => new Promise(resolve => { finish = resolve; });
  const pending = assetFormPage(services, 5); services.auth.session = { ...services.auth.session, id: 8 }; finish(asset());
  await assert.rejects(pending, { code: 'SESSION_CHANGED' }); assert.equal(services.calls.length, 0);
});
test('double submit sends one write and a detached save never redirects or toasts into another page', async () => {
  const dom = installDom('/assets/new'); try {
    let finish; const services = source(() => new Promise(resolve => { finish = resolve; })); const root = await mount(dom, services);
    form(root).fire('submit'); form(root).fire('submit'); assert.equal(services.calls.length, 1);
    root.remove(); location.hash = '#/dashboard'; finish(asset()); await new Promise(resolve => setImmediate(resolve));
    assert.equal(location.hash, '#/dashboard'); assert.doesNotMatch(dom.body.textContent, /Đã lưu tài sản/);
  } finally { dom.restore(); }
});
test('lost response or invalid saved ID blocks duplicate creation, keeps draft and opens only an explicit read tab', async () => {
  const dom = installDom('/assets/new'); try {
    for (const outcome of [0, 404, 503, 'invalid', 'same-account-relogin']) {
      const services = source(async () => { if (outcome === 'invalid') return { id: '5' }; throw Object.assign(new Error('synthetic-secret'),
        outcome === 'same-account-relogin' ? { status: 401, code: 'SESSION_CHANGED' } : { status: outcome }); });
      const root = await mount(dom, services); form(root).fire('submit');
      await until(() => root.textContent.includes('server có thể đã xử lý')); await until(() => submit(root).getAttribute('aria-busy') === null);
      assert.equal(submit(root).disabled, true); assert.equal(control(root, 'name').value, 'New synthetic asset');
      form(root).fire('submit'); assert.equal(services.calls.length, 1); assert.doesNotMatch(root.textContent, /synthetic-secret/);
      assert.equal(location.hash, '#/assets/new');
      const link = descendants(root).find(n => n.tagName === 'A' && n.textContent === 'Mở danh sách ở tab mới để đối chiếu');
      assert.equal(link.getAttribute('href'), '#/assets'); assert.equal(link.getAttribute('target'), '_blank'); assert.equal(link.getAttribute('rel'), 'noopener noreferrer');
      assert.match(root.textContent, /Tab mới cần đăng nhập lại/); root.remove();
    }
  } finally { dom.restore(); }
});
test('account/revoke during a pending save suppresses stale success and requires reconciliation', async () => {
  const dom = installDom('/assets/5/edit'); try {
    for (const change of ['logout', 'account', 'revoke']) {
      let finish; const services = source(() => new Promise(resolve => { finish = resolve; })); const root = await mount(dom, services, 5);
      form(root).fire('submit'); if (change === 'logout') services.auth.session = null;
      else if (change === 'account') services.auth.session.id = 8; else services.auth.session.permissions = ['assets.read'];
      finish(asset()); await until(() => root.textContent.includes('Phiên hoặc quyền đã thay đổi'));
      assert.equal(location.hash, '#/assets/5/edit'); assert.doesNotMatch(dom.body.textContent, /Đã lưu tài sản/); root.remove();
    }
  } finally { dom.restore(); }
});
test('known validation rejection remains editable while version conflict never blindly resubmits', async () => {
  const dom = installDom('/assets/5/edit'); try {
    for (const code of ['VALIDATION_ERROR', 'CONCURRENCY_CONFLICT']) {
      const services = source(async () => { throw Object.assign(new Error('Fixture rejection'), { code, status: code === 'VALIDATION_ERROR' ? 400 : 409, errors: { name: ['Fixture name error'] } }); });
      const root = await mount(dom, services, 5); form(root).fire('submit'); await until(() => root.textContent.includes('Fixture rejection'));
      await new Promise(resolve => setImmediate(resolve)); assert.equal(submit(root).disabled, code === 'CONCURRENCY_CONFLICT');
      assert.equal(control(root, 'name').value, 'Synthetic asset');
      if (code === 'CONCURRENCY_CONFLICT') { form(root).fire('submit'); assert.equal(services.calls.length, 1); }
      root.remove();
    }
  } finally { dom.restore(); }
});
test('a changed hash suppresses save success even before the router detaches the old form', async () => {
  const dom = installDom('/assets/new'); try {
    let finish; const services = source(() => new Promise(resolve => { finish = resolve; })); const root = await mount(dom, services);
    form(root).fire('submit'); location.hash = '#/dashboard'; finish(asset()); await new Promise(resolve => setImmediate(resolve));
    assert.equal(root.isConnected, true); assert.equal(location.hash, '#/dashboard'); assert.doesNotMatch(dom.body.textContent, /Đã lưu tài sản/);
  } finally { dom.restore(); }
});
test('cost grant revoked during initial read does not hydrate price, or start a write from a stale financial form', async () => {
  const dom = installDom('/assets/5/edit'); try {
    const services = source(async () => asset()); services.auth.session.permissions.push('assets.cost.read'); let finish;
    services.assets.get = () => new Promise(resolve => { finish = resolve; });
    const pending = assetFormPage(services, 5); services.auth.session = { id: 7, permissions: ['assets.read', 'assets.update'] };
    finish({ ...asset(), purchasePrice: 1200.25 }); const root = await pending; dom.body.append(root);
    assert.ok(!control(root, 'purchasePrice')); root.remove();
    services.auth.session.permissions.push('assets.cost.read'); services.assets.get = async () => ({ ...asset(), purchasePrice: 1200.25 });
    const financial = await mount(dom, services, 5); assert.ok(control(financial, 'purchasePrice'));
    services.auth.session.permissions = ['assets.read', 'assets.update']; form(financial).fire('submit');
    assert.equal(services.calls.length, 0); assert.equal(submit(financial).disabled, true);
  } finally { dom.restore(); }
});

test('pending Asset save locks the submitted inputs and restores prior states after validation rejection or detach', async () => {
  const dom = installDom('/assets/new'); try {
    for (const detached of [false, true]) {
      let reject; const services = source(() => new Promise((_resolve, fail) => { reject = fail; })); const root = await mount(dom, services);
      const note = control(root, 'note'); note.readOnly = true;
      form(root).fire('submit');
      try {
        assert.equal(control(root, 'name').readOnly, true); assert.equal(control(root, 'assetTypeId').disabled, true);
        assert.equal(control(root, 'owningDepartmentId').disabled, true); assert.equal(note.readOnly, true);
        assert.equal(services.calls[0].name, 'New synthetic asset'); if (detached) root.remove();
      } finally {
        reject(Object.assign(Error('Known fixture validation'), { status: 400, code: 'VALIDATION_ERROR' }));
        await until(() => submit(root).getAttribute('aria-busy') === null); await new Promise(resolve => setImmediate(resolve));
      }
      assert.equal(control(root, 'name').readOnly, false); assert.equal(control(root, 'assetTypeId').disabled, false);
      assert.equal(note.readOnly, true); assert.equal(control(root, 'name').value, 'New synthetic asset'); root.remove();
    }
  } finally { dom.restore(); }
});
