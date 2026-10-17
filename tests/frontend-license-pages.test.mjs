import test from 'node:test';
import assert from 'node:assert/strict';
import { licenseListPage, licenseDetailPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/licenses.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const item = () => ({ id: 4, licenseCode: 'FIXTURE-LICENSE', software: { id: 2, name: 'Synthetic software' },
  licenseType: 'VOLUME', totalQuantity: 5, isActive: true, maskedKey: '****-ABCD' });
const services = (row = item()) => ({ mode: 'api', auth: { session: { id: 7, roles: ['SYSTEM_MANAGER'], permissions: ['licenses.read'] } },
  licenses: { list: async () => ({ items: [row], page: 1, pageSize: 20, totalItems: 1, totalPages: 1 }), get: async () => row } });

test('prepared pages default disabled and make no API call even with grants', async () => {
  const dom = installDom(); try {
    const source = services(); source.licenses.list = source.licenses.get = async () => { throw new Error('must not fetch'); };
    assert.match((await licenseListPage(source)).textContent, /PLANNED/);
    assert.match((await licenseDetailPage(source, 4)).textContent, /PLANNED/);
  } finally { dom.restore(); }
});
test('read permission/API mode are required before fetching when explicitly preparing a page', async () => {
  const source = services(); source.auth.session.permissions = [];
  await assert.rejects(licenseListPage(source, { enabled: true }), error => error.status === 403);
  await assert.rejects(licenseDetailPage({ ...services(), mode: 'mock' }, 4, { enabled: true }), error => error.status === 403);
});
test('list/detail only display masked key, unknown capacity stays unknown, no cost or reveal controls', async () => {
  const dom = installDom(); try {
    const source = services({ ...item(), purchaseCost: 123456, notes: 'ignored detail' });
    const list = await licenseListPage(source, { enabled: true });
    const detail = await licenseDetailPage(source, 4, { enabled: true });
    for (const root of [list, detail]) {
      assert.match(root.textContent, /FIXTURE-LICENSE/); assert.match(root.textContent, /\*\*\*\*-ABCD/);
      assert.match(root.textContent, /Chưa có dữ liệu/); assert.doesNotMatch(root.textContent, /123456|ignored detail/);
    }
    assert.match(detail.textContent, /Phân bổ.*PLANNED/);
  } finally { dom.restore(); }
});
test('missing/rejected backend data never becomes a fake empty list or zero totals', async () => {
  const dom = installDom(); try {
    const source = services(); source.licenses.list = async () => { throw Object.assign(new Error('not ready'), { status: 503 }); };
    await assert.rejects(licenseListPage(source, { enabled: true }), error => error.status === 503);
    await assert.rejects(licenseDetailPage(services({ ...item(), licenseKey: 'synthetic-secret' }), 4, { enabled: true }), error => error.status === 502);
  } finally { dom.restore(); }
});
test('search preserves page size, encodes text and resets page without mutation requests', async () => {
  const dom = installDom('/licenses'); try {
    const root = await licenseListPage(services(), { enabled: true, query: { page: 1, pageSize: 20 } });
    const nodes = descendants(root);
    nodes.find(n => n.getAttribute('name') === 'keyword').value = ' a & b ';
    nodes.find(n => n.getAttribute('name') === 'vendor').value = ' Fixture vendor ';
    nodes.find(n => n.tagName === 'FORM').fire('submit');
    const query = new URLSearchParams(location.hash.split('?')[1]);
    assert.equal(query.get('keyword'), 'a & b'); assert.equal(query.get('vendor'), 'Fixture vendor');
    assert.equal(query.get('page'), '1'); assert.equal(query.get('pageSize'), '20');
    nodes.find(n => n.tagName === 'BUTTON' && n.textContent === 'Đặt lại').fire('click');
    assert.doesNotMatch(location.hash, /keyword|vendor/);
  } finally { dom.restore(); }
});
test('empty result uses a standalone readable status instead of an off-screen wide table cell', async () => {
  const dom = installDom(); try {
    const source = services(); source.licenses.list = async () => ({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 });
    const root = await licenseListPage(source, { enabled: true });
    assert.match(root.textContent, /Chưa có license phù hợp/);
    assert.equal(descendants(root).filter(n => n.tagName === 'TABLE').length, 0);
    assert.ok(descendants(root).some(n => n.getAttribute('role') === 'status' && n.textContent === 'Chưa có license phù hợp.'));
  } finally { dom.restore(); }
});
test('allocation preparation is separately opt-in and read-only caller still gets metadata', async () => {
  const dom = installDom(); try {
    const source = services(); const root = await licenseDetailPage(source, 4, { enabled: true, allocationPreparation: { enabled: true } });
    assert.match(root.textContent, /FIXTURE-LICENSE/); assert.match(root.textContent, /không có quyền chuẩn bị/);
    assert.equal(descendants(root).filter(n => n.tagName === 'FORM').length, 0); root.dispose();
    source.auth.session.permissions.push('license-assignments.assign', 'users.lookup');
    const closed = await licenseDetailPage(source, 4, { enabled: true });
    assert.equal(descendants(closed).filter(n => n.tagName === 'FORM').length, 0); closed.dispose();
  } finally { dom.restore(); }
});
test('detail composes draft form with lookup and local callback without an allocation API or route activation', async () => {
  const dom = installDom(); try {
    const source = services({ ...item(), licenseType: 'PER_USER' }); source.auth.session.permissions.push('license-assignments.assign', 'users.lookup');
    source.users = { lookup: async () => ({ items: [{ id: 9, displayName: 'Synthetic recipient' }] }) };
    const prepared = []; const root = await licenseDetailPage(source, 4, { enabled: true,
      allocationPreparation: { enabled: true, onPrepared: draft => prepared.push(draft) } }); dom.body.append(root);
    const nodes = descendants(root); const search = nodes.find(n => n.tagName === 'BUTTON' && n.textContent === 'Tìm target'); search.fire('click');
    const selection = nodes.find(n => n.tagName === 'SELECT' && n.getAttribute('id')?.endsWith('-selection'));
    await until(() => !selection.disabled); selection.value = '9'; selection.fire('change');
    nodes.find(n => n.tagName === 'FORM').fire('submit'); await until(() => prepared.length === 1);
    assert.deepEqual(prepared[0], { licenseId: 4, userId: 9, assetId: null, note: null });
    assert.match(root.textContent, /PLANNED/); root.dispose();
  } finally { dom.restore(); }
});
test('license read revoked while awaiting metadata cannot render the formerly authorized response', async () => {
  const source = services(); let release; source.licenses.list = () => new Promise(resolve => { release = resolve; });
  const pending = licenseListPage(source, { enabled: true }); const rejected = assert.rejects(pending, error => error.status === 403);
  source.auth.session.permissions = []; release({ items: [item()], page: 1, pageSize: 20, totalItems: 1, totalPages: 1 });
  await rejected;
});
test('a different account cannot receive the previous account detail response', async () => {
  const source = services(); let release; source.licenses.get = () => new Promise(resolve => { release = resolve; });
  const pending = licenseDetailPage(source, 4, { enabled: true });
  const rejected = assert.rejects(pending, error => error.status === 401 && error.code === 'SESSION_CHANGED');
  source.auth.session = { id: 8, roles: ['SYSTEM_MANAGER'], permissions: ['licenses.read'] }; release(item());
  await rejected;
});
test('list request and rendered filters use an immutable snapshot while caller changes its query object', async () => {
  const dom = installDom(); try {
    const source = services(); let release, received;
    source.licenses.list = query => { received = query; return new Promise(resolve => { release = resolve; }); };
    const query = { page: 1, pageSize: 20, keyword: 'Original keyword' };
    const pending = licenseListPage(source, { enabled: true, query });
    query.keyword = 'Changed while pending'; query.page = 9;
    release({ items: [item()], page: 1, pageSize: 20, totalItems: 1, totalPages: 1 });
    const root = await pending;
    assert.ok(Object.isFrozen(received)); assert.equal(received.keyword, 'Original keyword'); assert.equal(received.page, 1);
    assert.equal(descendants(root).find(n => n.getAttribute('name') === 'keyword').value, 'Original keyword');
  } finally { dom.restore(); }
});
test('session expiry between access checks returns a typed session error, not a null-property crash', async () => {
  const source = services(); const initial = source.auth.session; let reads = 0;
  Object.defineProperty(source.auth, 'session', { get: () => ++reads === 1 ? initial : null });
  await assert.rejects(licenseListPage(source, { enabled: true }), error => error.status === 401 && error.code === 'SESSION_CHANGED');
});
