import test from 'node:test';
import assert from 'node:assert/strict';
import { reportsPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/reports.js';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';
import { installDom, descendants } from './support/test-dom.mjs';

const all = ['reports.assets.read', 'reports.assignments.read', 'reports.maintenance.read', 'reports.cost.read'];
const pageOf = query => ({ items: [], page: query.page || 1, pageSize: query.pageSize || 20, totalItems: 60, totalPages: 3 });
function fixture(permissions = all) {
  const calls = [];
  const services = { reportingEnabled: true, auth: { session: { id: 7, permissions } }, reports: {} };
  for (const name of ['assetInventory', 'assignmentHistory', 'maintenanceHistory'])
    services.reports[name] = async query => { calls.push({ name, query }); return pageOf(query); };
  for (const name of ['assetsByDepartment', 'assetsByType', 'assetStatus'])
    services.reports[name] = async query => { calls.push({ name, query }); return [{ label: '<fixture>', count: 12, totalCost: 999999 }]; };
  services.reports.maintenanceCost = async query => { calls.push({ name: 'maintenanceCost', query }); return { totalCost: 0, groups: [] }; };
  services.reports.replacementBudget = async query => { calls.push({ name: 'replacementBudget', query }); return { year: query.year }; };
  return { services, calls };
}
const byLabel = (root, tag, label) => descendants(root).find(n => n.tagName === tag.toUpperCase() && n.getAttribute('aria-label') === label);
const field = (root, name) => descendants(root).find(n => n.getAttribute('name') === name);
const params = () => new URLSearchParams(window.location.hash.split('?')[1]);

test('inventory passes all filters and sort, keeps them during paging, resets page on submit', async () => {
  const dom = installDom('/reports?tab=inventory&page=2&pageSize=20&keyword=Dell&status=IN_USE&departmentId=7&assetTypeId=2&purchasedFrom=2026-01-01&purchasedTo=2026-10-21&sortBy=purchaseDate&sortDirection=asc');
  try {
    const { services, calls } = fixture(); let root = await reportsPage(services); dom.body.append(root);
    assert.deepEqual(calls[0].query, { page: 2, pageSize: 20, keyword: 'Dell', status: 'IN_USE', departmentId: 7, assetTypeId: 2,
      purchasedFrom: '2026-01-01', purchasedTo: '2026-10-21', sortBy: 'purchaseDate', sortDirection: 'asc' });
    const form = byLabel(root, 'form', 'Lọc kiểm kê tài sản');
    assert.equal(field(form, 'departmentId').value, '7'); assert.equal(field(form, 'sortBy').value, 'purchaseDate');
    const ids = descendants(form).filter(n => ['INPUT', 'SELECT'].includes(n.tagName)).map(n => n.getAttribute('id'));
    assert.equal(new Set(ids).size, ids.length);
    assert.equal(descendants(form).filter(n => n.tagName === 'LABEL').length, ids.length);
    byLabel(root, 'button', 'Trang sau').fire('click');
    assert.equal(params().get('page'), '3'); assert.equal(params().get('departmentId'), '7');
    assert.equal(params().get('purchasedFrom'), '2026-01-01'); assert.equal(params().get('sortDirection'), 'asc');
    root.remove(); root = await reportsPage(services); dom.body.append(root);
    const updated = byLabel(root, 'form', 'Lọc kiểm kê tài sản');
    field(updated, 'keyword').value = '  Lenovo  '; field(updated, 'pageSize').value = '50'; updated.fire('submit');
    assert.equal(params().get('page'), '1'); assert.equal(params().get('pageSize'), '50'); assert.equal(params().get('keyword'), 'Lenovo');
    assert.equal(params().get('assetTypeId'), '2'); assert.equal(params().get('purchasedTo'), '2026-10-21');
  } finally { dom.restore(); }
});

test('inventory invalid submitted range stays inline with draft and no new request', async () => {
  const dom = installDom('/reports?tab=inventory');
  try {
    const { services, calls } = fixture(); const root = await reportsPage(services); dom.body.append(root);
    const form = byLabel(root, 'form', 'Lọc kiểm kê tài sản');
    field(form, 'purchasedFrom').value = '2026-10-22'; field(form, 'purchasedTo').value = '2026-10-21'; form.fire('submit');
    assert.equal(params().toString(), 'tab=inventory'); assert.equal(calls.length, 1);
    assert.match(form.textContent, /bắt đầu không được sau/); assert.equal(field(form, 'purchasedFrom').value, '2026-10-22');
  } finally { dom.restore(); }
});

test('nonfinancial inventory reader has no cost column or cost sort option and crafted sort is denied before fetch', async () => {
  const dom = installDom('/reports?tab=inventory');
  try {
    const { services, calls } = fixture(['reports.assets.read']); const root = await reportsPage(services);
    assert.doesNotMatch(root.textContent, /Giá mua/);
    assert.equal(field(root, 'sortBy').children.some(n => n.getAttribute('value') === 'purchaseCost'), false);
    window.location.hash = '#/reports?tab=inventory&sortBy=purchaseCost';
    await assert.rejects(reportsPage(services), e => e.status === 403); assert.equal(calls.length, 1);
  } finally { dom.restore(); }
});

test('financial inventory reader can request purchase-cost sorting', async () => {
  const dom = installDom('/reports?tab=inventory&sortBy=purchaseCost&sortDirection=desc');
  try { const { services, calls } = fixture(); const root = await reportsPage(services);
    assert.match(root.textContent, /Giá mua/); assert.equal(calls[0].query.sortBy, 'purchaseCost'); }
  finally { dom.restore(); }
});

test('reset inventory clears filters/sorting/pages without calling any write API', async () => {
  const dom = installDom('/reports?tab=inventory&departmentId=7&page=2&sortBy=name');
  try {
    const { services, calls } = fixture(); const root = await reportsPage(services); dom.body.append(root);
    descendants(byLabel(root, 'form', 'Lọc kiểm kê tài sản')).find(n => n.tagName === 'BUTTON' && n.textContent === 'Đặt lại').fire('click');
    assert.equal(params().toString(), 'tab=inventory'); assert.equal(calls.length, 1);
  } finally { dom.restore(); }
});

for (const [tab, formName, from, to, idName, permission] of [
  ['assignments', 'Lọc lịch sử cấp phát', 'assignmentFrom', 'assignmentTo', 'userId', 'reports.assignments.read'],
  ['maintenance', 'Lọc báo cáo bảo trì', 'maintenanceFrom', 'maintenanceTo', 'technicianId', 'reports.maintenance.read']
]) test(`${tab} sends UTC and IDs, masks cost, keeps filters on paging and resets on submit`, async () => {
  const dom = installDom(`/reports?tab=${tab}&page=2&assetId=5&${idName}=7&${from}=2026-10-01T00%3A00&${to}=2026-10-21T12%3A30` +
    (tab === 'maintenance' ? '&maintenanceStatus=RESOLVED' : '&departmentId=2'));
  try {
    const { services, calls } = fixture([permission]); const root = await reportsPage(services); dom.body.append(root);
    assert.equal(calls.length, 1); const q = calls[0].query;
    assert.equal(q.assetId, 5); assert.equal(q[idName], 7); assert.equal(q.fromUtc, '2026-10-01T00:00:00.000Z');
    assert.equal(q.toUtc, '2026-10-21T12:30:00.000Z');
    if (tab === 'maintenance') { assert.equal(q.status, 'RESOLVED'); assert.doesNotMatch(root.textContent, /Chi phí/); }
    else assert.equal(q.departmentId, 2);
    byLabel(root, 'button', 'Trang sau').fire('click'); assert.equal(params().get('page'), '3'); assert.equal(params().get(from), '2026-10-01T00:00');
    // Old view callbacks must be inert once its hash changes.
    const form = byLabel(root, 'form', formName); field(form, idName).value = '8'; form.fire('submit'); assert.equal(params().get(idName), '7');
    root.remove(); const current = await reportsPage(services); dom.body.append(current);
    byLabel(current, 'form', formName).fire('submit'); assert.equal(params().get('page'), '1');
  } finally { dom.restore(); }
});

test('cost UTC window only reaches maintenance-cost and not the selected budget year', async () => {
  const dom = installDom('/reports?tab=cost&year=2029&costFrom=2026-01-01T00%3A00&costTo=2026-10-21T12%3A30&groupBy=department');
  try {
    const { services, calls } = fixture(); const root = await reportsPage(services);
    assert.equal(calls[0].query.fromUtc, '2026-01-01T00:00:00.000Z'); assert.equal(calls[0].query.toUtc, '2026-10-21T12:30:00.000Z');
    assert.equal(calls[1].query.year, 2029); assert.equal(calls[1].query.fromUtc, undefined);
    assert.match(root.textContent, /Khoảng hoàn tất/); assert.match(root.textContent, /không lọc thời gian/);
  } finally { dom.restore(); }
});

test('group report calls only three aggregate GET readers with shared filters and never exposes aggregate financial fields', async () => {
  const dom = installDom('/reports?tab=groups&departmentId=7&assetTypeId=2&status=IN_USE');
  try {
    const { services, calls } = fixture(['reports.assets.read']); const root = await reportsPage(services);
    assert.deepEqual(calls.map(c => c.name), ['assetsByDepartment', 'assetsByType', 'assetStatus']);
    assert.ok(calls.every(c => JSON.stringify(c.query) === JSON.stringify({ departmentId: 7, assetTypeId: 2, status: 'IN_USE' })));
    assert.match(root.textContent, /Tài sản theo phòng ban/); assert.match(root.textContent, /Tài sản theo loại/); assert.match(root.textContent, /Tài sản theo trạng thái/);
    assert.doesNotMatch(root.textContent, /999999/); assert.match(root.textContent, /<fixture>/); assert.equal(root.querySelectorAll('script').length, 0);
  } finally { dom.restore(); }
});

for (const tab of ['inventory', 'groups', 'assignments', 'maintenance', 'cost']) test(`${tab} propagates read failure instead of rendering invented empty/zero success`, async () => {
  const dom = installDom('/reports?tab=' + tab);
  try {
    const { services } = fixture();
    const name = { inventory: 'assetInventory', groups: 'assetsByDepartment', assignments: 'assignmentHistory', maintenance: 'maintenanceHistory', cost: 'maintenanceCost' }[tab];
    services.reports[name] = async () => { throw new Error('synthetic API unavailable'); };
    await assert.rejects(reportsPage(services), /synthetic API unavailable/);
  } finally { dom.restore(); }
});

for (const scenario of ['detached', 'actor', 'grant', 'cost-grant', 'flag']) test(`new inventory controls are inert after ${scenario}`, async () => {
  const dom = installDom('/reports?tab=inventory');
  try {
    const { services, calls } = fixture(); const root = await reportsPage(services); dom.body.append(root);
    if (scenario === 'detached') root.remove();
    if (scenario === 'actor') services.auth.session = { ...services.auth.session, id: 9 };
    if (scenario === 'grant') services.auth.session.permissions = ['reports.cost.read'];
    if (scenario === 'cost-grant') services.auth.session.permissions = ['reports.assets.read'];
    if (scenario === 'flag') services.reportingEnabled = false;
    const form = byLabel(root, 'form', 'Lọc kiểm kê tài sản'); field(form, 'departmentId').value = '8'; form.fire('submit');
    byLabel(root, 'button', 'Trang sau').fire('click');
    assert.equal(window.location.hash, '#/reports?tab=inventory'); assert.equal(calls.length, 1);
  } finally { dom.restore(); }
});

for (const query of ['purchasedFrom=2026-02-30', 'purchasedFrom=0000-01-01', 'purchasedFrom=2026-10-22&purchasedTo=2026-10-21',
  'departmentId=0', 'assetTypeId=2.5', 'assetId=9007199254740992', 'userId=-1', 'technicianId=0', 'maintenanceStatus=COMPLETED',
  'assignmentFrom=2026-10-22T00%3A00&assignmentTo=2026-10-21T00%3A00', 'maintenanceFrom=2026-10-21T25%3A00',
  'costFrom=2026-10-22T00%3A00&costTo=2026-10-21T00%3A00', 'sortBy=rawSql', 'sortDirection=DROP', 'tab=unknown'])
  test(`new malformed filters stop before any report read: ${query}`, async () => {
    const dom = installDom('/reports?' + query);
    try { const { services, calls } = fixture(); await assert.rejects(reportsPage(services), e => e.status === 400); assert.equal(calls.length, 0); }
    finally { dom.restore(); }
  });

test('real API adapter preserves inclusive business dates/IDs/sorting and emits only a no-store authenticated GET', async () => {
  const dom = installDom('/reports?tab=inventory&departmentId=7&assetTypeId=2&purchasedFrom=0001-01-01&purchasedTo=2026-10-21&sortBy=name&sortDirection=asc');
  const calls = [];
  try {
    const services = createApiServices({ reportingEnabled: true, fetchImpl: async (url, options) => {
      calls.push({ url, options });
      if (url.endsWith('/auth/login')) return Response.json({ accessToken: 'synthetic-reports-token', tokenType: 'Bearer', expiresAt: new Date(Date.now() + 60000).toISOString(),
        user: { id: 7, roles: [], permissions: ['reports.assets.read'] } });
      return Response.json(pageOf({ page: 1 }));
    } });
    await services.auth.login({ email: 'fixture@test.invalid', password: 'synthetic-password' }); await reportsPage(services);
    const { url, options } = calls[1], q = new URL(url, 'https://fixture.invalid').searchParams;
    assert.equal(q.get('departmentId'), '7'); assert.equal(q.get('assetTypeId'), '2'); assert.equal(q.get('purchasedFrom'), '0001-01-01');
    assert.equal(q.get('purchasedTo'), '2026-10-21'); assert.equal(q.get('sortBy'), 'name'); assert.equal(q.get('sortDirection'), 'asc');
    assert.equal(options.method, 'GET'); assert.equal(options.cache, 'no-store'); assert.equal(options.body, undefined);
    assert.equal(options.headers.Authorization, 'Bearer synthetic-reports-token'); assert.doesNotMatch(url, /token|password/iu);
    await services.auth.logout();
  } finally { dom.restore(); }
});
