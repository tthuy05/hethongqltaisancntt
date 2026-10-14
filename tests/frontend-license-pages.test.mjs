import test from 'node:test';
import assert from 'node:assert/strict';
import { licenseListPage, licenseDetailPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/licenses.js';
import { installDom, descendants } from './support/test-dom.mjs';

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
