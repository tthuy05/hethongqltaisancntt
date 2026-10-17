import test from 'node:test';
import assert from 'node:assert/strict';
import { assetListPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/assets.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const asset = { id: 5, assetCode: 'AST-FIXTURE', name: 'Synthetic asset', status: 'InStock',
  assetType: { id: 2, name: 'Loại trên Asset DTO' }, owningDepartment: { id: 3, name: 'Phòng ban trên Asset DTO' } };
function source(grants = ['assets.read']) {
  const calls = { list: [], detail: 0, department: 0, type: 0 }; return { mode: 'api', calls, auth: { session: { id: 7, permissions: grants } },
    assets: { list: async q => { calls.list.push(q); return { items: [asset], totalItems: 1, totalPages: 1, page: 1, pageSize: 10 }; }, get: async () => { calls.detail++; return asset; } },
    departments: { list: async () => { calls.department++; throw Object.assign(Error('private-detail'), { status: 403 }); } },
    assetTypes: { list: async () => { calls.type++; throw Object.assign(Error('private-detail'), { status: 403 }); } } };
}
const byName = (root, name) => descendants(root).find(n => n.getAttribute('name') === name);

test('asset-only reader sees Asset DTO reference names without requesting forbidden catalogs', async () => {
  const dom = installDom('/assets'); try {
    const services = source(); const root = await assetListPage(services);
    for (const text of ['Synthetic asset', 'Loại trên Asset DTO', 'Phòng ban trên Asset DTO']) assert.ok(root.textContent.includes(text));
    assert.equal(services.calls.department, 0); assert.equal(services.calls.type, 0); assert.equal(services.calls.detail, 1);
    assert.equal(byName(root, 'departmentId').disabled, true); assert.equal(byName(root, 'assetTypeId').disabled, true);
    assert.match(root.textContent, /Không có quyền tải danh mục/); assert.doesNotMatch(root.textContent, /private-detail/);
  } finally { dom.restore(); }
});

test('catalog grants are independent; catalog data is not fetched by role-name inference', async () => {
  const dom = installDom('/assets'); try {
    const services = source(['assets.read', 'asset-types.read']);
    services.auth.session.roles = ['ADMIN_IT']; services.assetTypes.list = async () => { services.calls.type++; return { items: [{ id: 2, name: 'Catalog fixture' }], totalPages: 1 }; };
    const root = await assetListPage(services); assert.equal(services.calls.department, 0); assert.equal(services.calls.type, 1);
    assert.equal(byName(root, 'departmentId').disabled, true); assert.equal(byName(root, 'assetTypeId').disabled, false);
  } finally { dom.restore(); }
});

test('optional catalog 403/network/503 failures remain visible without replacing asset results or retrying', async () => {
  const dom = installDom('/assets'); try {
    for (const status of [403, 0, 503]) {
      const services = source(['assets.read', 'departments.read', 'asset-types.read']);
      services.departments.list = async () => { services.calls.department++; throw Object.assign(Error('private-detail'), { status }); };
      const root = await assetListPage(services); assert.match(root.textContent, /Synthetic asset/); assert.doesNotMatch(root.textContent, /private-detail/);
      assert.match(root.textContent, status === 403 ? /Không có quyền tải danh mục/ : /Không tải được danh mục/);
      assert.equal(byName(root, 'departmentId').disabled, true); assert.equal(services.calls.department, 1); assert.equal(services.calls.type, 1);
    }
  } finally { dom.restore(); }
});

test('unavailable catalog preserves existing URL filter while other filters/search are submitted', async () => {
  const dom = installDom('/assets?departmentId=3&assetTypeId=2&keyword=fixture'); try {
    const services = source(); const root = await assetListPage(services); dom.body.append(root);
    assert.equal(byName(root, 'departmentId').value, '3'); assert.equal(byName(root, 'assetTypeId').value, '2');
    byName(root, 'keyword').value = 'new keyword'; descendants(root).find(n => n.tagName === 'FORM').fire('submit');
    const query = new URLSearchParams(location.hash.split('?')[1]); assert.equal(query.get('departmentId'), '3'); assert.equal(query.get('assetTypeId'), '2');
    assert.equal(query.get('keyword'), 'new keyword'); assert.match(root.textContent, /ID phòng ban: 3/);
    assert.doesNotMatch(root.textContent, /Không có dữ liệu tài sản/);
  } finally { dom.restore(); }
});

test('expired/stale session from an optional lookup must propagate, never masquerade as missing catalog', async () => {
  const dom = installDom('/assets'); try {
    for (const code of ['UNAUTHENTICATED', 'SESSION_CHANGED']) {
      const services = source(['assets.read', 'departments.read']); services.departments.list = async () => { throw Object.assign(Error('private-detail'), { status: 401, code }); };
      await assert.rejects(assetListPage(services), { status: 401, code });
    }
  } finally { dom.restore(); }
});

test('asset list requires asset-read before any call and rejects stale actor/grant before returning data', async () => {
  const dom = installDom('/assets'); try {
    const denied = source([]); await assert.rejects(assetListPage(denied), { status: 403 }); assert.equal(denied.calls.list.length, 0);
    for (const change of ['account', 'revoke']) {
      let finish; const services = source(); services.assets.get = () => new Promise(resolve => { finish = resolve; });
      const result = assetListPage(services); await until(() => typeof finish === 'function');
      if (change === 'account') services.auth.session.id = 8; else services.auth.session.permissions = [];
      const rejected = assert.rejects(result, { status: change === 'account' ? 401 : 403 }); finish(asset); await rejected;
    }
  } finally { dom.restore(); }
});

test('catalog grant revoked during detail load discards previously loaded lookup values, not Asset DTO references', async () => {
  const dom = installDom('/assets'); try {
    let finish; const services = source(['assets.read', 'departments.read', 'asset-types.read']);
    services.departments.list = async () => ({ items: [{ id: 3, name: 'Catalog-only private name' }], totalPages: 1 });
    services.assetTypes.list = async () => ({ items: [], totalPages: 0 }); services.assets.get = () => new Promise(resolve => { finish = resolve; });
    const pending = assetListPage(services); await until(() => typeof finish === 'function'); services.auth.session.permissions = ['assets.read'];
    finish(asset); const root = await pending; assert.doesNotMatch(root.textContent, /Catalog-only private name/);
    assert.match(root.textContent, /Phòng ban trên Asset DTO/); assert.equal(byName(root, 'departmentId').disabled, true);
  } finally { dom.restore(); }
});
