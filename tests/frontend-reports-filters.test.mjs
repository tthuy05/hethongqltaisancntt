import test from 'node:test';
import assert from 'node:assert/strict';
import { reportsPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/reports.js';
import { readReportQuery, validateReportQuery, reportUtc } from '../src/ItAssetManagement.Api/wwwroot/js/pages/reports-query.js';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';
import { installDom, descendants } from './support/test-dom.mjs';

const grants = ['reports.assets.read', 'reports.licenses.read', 'reports.cost.read', 'reports.replacement.read'];
const pageOf = (query, items = []) => ({ items, page: query.page || 1, pageSize: query.pageSize || 20, totalItems: 200, totalPages: 10 });
function fixture(permissions = grants) {
  const calls = [];
  const services = { reportingEnabled: true, auth: { session: { id: 7, permissions } }, reports: {} };
  for (const name of ['assetInventory', 'warrantyExpiration', 'licenseExpiration', 'replacementRecommendations'])
    services.reports[name] = async query => { calls.push({ name, query }); return pageOf(query); };
  services.reports.maintenanceCost = async query => { calls.push({ name: 'maintenanceCost', query }); return { currency: 'VND', totalCost: 0, groups: [] }; };
  services.reports.replacementBudget = async query => { calls.push({ name: 'replacementBudget', query }); return { year: query.year, totalBudget: 0, totalRecommendations: 0, missingEstimateCount: 0 }; };
  return { services, calls };
}
const byLabel = (root, tag, label) => descendants(root).find(n => n.tagName === tag.toUpperCase() && n.getAttribute('aria-label') === label);
const field = (root, name) => descendants(root).find(n => n.getAttribute('name') === name);
const params = () => new URLSearchParams(window.location.hash.split('?')[1]);

test('expiry reports send exact independent pages and warranty-date/UTC license filters', async () => {
  const dom = installDom('/reports?tab=expiry&warrantyPage=2&licensePage=3&pageSize=20&warrantyFrom=2026-10-01&warrantyTo=2026-11-01&departmentId=7&assetTypeId=2&licenseFrom=2026-10-21T12%3A30&licenseTo=2026-11-01T14%3A00&licenseStatus=Expiring&softwareId=4');
  try {
    const { services, calls } = fixture(); const root = await reportsPage(services); dom.body.append(root);
    const warranty = calls.find(c => c.name === 'warrantyExpiration').query;
    assert.equal(warranty.page, 2); assert.equal(warranty.fromDate, '2026-10-01'); assert.equal(warranty.toDate, '2026-11-01');
    assert.equal(warranty.departmentId, 7); assert.equal(warranty.assetTypeId, 2);
    const license = calls.find(c => c.name === 'licenseExpiration').query;
    assert.equal(license.page, 3); assert.equal(license.fromUtc, '2026-10-21T12:30:00.000Z'); assert.equal(license.toUtc, '2026-11-01T14:00:00.000Z');
    assert.equal(license.status, 'Expiring'); assert.equal(license.softwareId, 4);
    const section = byLabel(root, 'section', 'Báo cáo hạn bảo hành');
    byLabel(section, 'button', 'Trang sau').fire('click');
    assert.equal(params().get('warrantyPage'), '3'); assert.equal(params().get('licensePage'), '3');
    assert.equal(params().get('warrantyFrom'), '2026-10-01'); assert.equal(params().get('licenseStatus'), 'Expiring');
    const ids = descendants(root).map(n => n.getAttribute('id')).filter(Boolean);
    assert.equal(new Set(ids).size, ids.length, 'two filter forms must not duplicate input IDs');
  } finally { dom.restore(); }
});

test('license next page preserves warranty page and changing filters resets both counters', async () => {
  const dom = installDom('/reports?tab=expiry&warrantyPage=4&licensePage=2&pageSize=20&warrantyFrom=2026-10-01');
  try {
    const { services } = fixture(); let root = await reportsPage(services); dom.body.append(root);
    byLabel(byLabel(root, 'section', 'Báo cáo thời hạn License'), 'button', 'Trang sau').fire('click');
    assert.equal(params().get('warrantyPage'), '4'); assert.equal(params().get('licensePage'), '3');
    root.remove(); root = await reportsPage(services); dom.body.append(root);
    const form = byLabel(root, 'form', 'Lọc thời hạn License'); field(form, 'licenseStatus').value = 'Expired'; form.fire('submit');
    assert.equal(params().get('warrantyPage'), '1'); assert.equal(params().get('licensePage'), '1');
    assert.equal(params().get('licenseStatus'), 'Expired'); assert.equal(params().get('warrantyFrom'), '2026-10-01');
  } finally { dom.restore(); }
});

for (const permission of ['reports.assets.read', 'reports.licenses.read']) test(`expiry fetches/renders only the granted report: ${permission}`, async () => {
  const dom = installDom('/reports?tab=expiry');
  try {
    const { services, calls } = fixture([permission]); const root = await reportsPage(services);
    const assets = permission === 'reports.assets.read';
    assert.equal(calls.length, 1); assert.equal(calls[0].name, assets ? 'warrantyExpiration' : 'licenseExpiration');
    assert.equal(Boolean(byLabel(root, 'section', 'Báo cáo hạn bảo hành')), assets);
    assert.equal(Boolean(byLabel(root, 'section', 'Báo cáo thời hạn License')), !assets);
  } finally { dom.restore(); }
});

test('warranty days remain business-date values and perpetual licenses do not display zero days', async () => {
  const dom = installDom('/reports?tab=expiry');
  try {
    const { services } = fixture();
    services.reports.warrantyExpiration = async query => pageOf(query, [{ assetCode: 'AST-1', warrantyEndDate: '2026-11-01', daysRemaining: 11, isExpired: false }]);
    services.reports.licenseExpiration = async query => pageOf(query, [{ licenseCode: 'LIC-1', licenseKeyLast4: '1234', expiresAtUtc: null, daysRemaining: 0, isExpired: false }]);
    const root = await reportsPage(services);
    assert.match(root.textContent, /11 ngày/); assert.match(root.textContent, /Không có thời hạn/); assert.match(root.textContent, /••••-1234/);
    assert.doesNotMatch(byLabel(root, 'section', 'Báo cáo thời hạn License').querySelector('tbody').textContent, /0 ngày/);
  } finally { dom.restore(); }
});

test('budget year/group/department filters reach their proper read queries and the heading matches', async () => {
  const dom = installDom('/reports?tab=cost&year=2029&departmentId=7&assetTypeId=2&priority=HIGH&groupBy=type');
  try {
    const { services, calls } = fixture(); const root = await reportsPage(services); dom.body.append(root);
    assert.deepEqual(calls.find(c => c.name === 'replacementBudget').query, { year: 2029, departmentId: 7, priority: 'HIGH' });
    assert.deepEqual(calls.find(c => c.name === 'maintenanceCost').query, { groupBy: 'type', departmentId: 7, assetTypeId: 2 });
    assert.match(root.textContent, /Dự toán thay thế năm 2029/); assert.match(root.textContent, /theo loại tài sản/);
    const form = byLabel(root, 'form', 'Lọc chi phí và ngân sách'); field(form, 'year').value = '2030'; form.fire('submit');
    assert.equal(params().get('year'), '2030'); assert.equal(params().get('groupBy'), 'type');
  } finally { dom.restore(); }
});

test('invalid submitted year/date remains inline without navigating or issuing another request', async () => {
  const dom = installDom('/reports?tab=cost&year=2029');
  try {
    const { services, calls } = fixture(); const root = await reportsPage(services); dom.body.append(root);
    const form = byLabel(root, 'form', 'Lọc chi phí và ngân sách'), hash = window.location.hash;
    field(form, 'year').value = '2101'; form.fire('submit');
    assert.equal(window.location.hash, hash); assert.equal(calls.length, 2); assert.match(form.textContent, /2000 đến 2100/);
    field(form, 'year').value = ''; form.fire('submit'); assert.match(form.textContent, /Vui lòng nhập năm/);
  } finally { dom.restore(); }
});

test('replacement-only reader sees a read-only report with filters/paging and no financial columns', async () => {
  const dom = installDom('/reports?tab=replacement&page=2&pageSize=20&plannedYear=2029&priority=HIGH&disposition=ACTIVE&departmentId=7&generatedFrom=2026-10-01T00%3A00&generatedTo=2026-10-21T12%3A00');
  try {
    const { services, calls } = fixture(['reports.replacement.read']);
    services.reports.replacementRecommendations = async query => { calls.push({ name: 'replacementRecommendations', query }); return pageOf(query, [
      { id: 1, assetId: 2, assetCode: 'AST-2', assetName: '<script>synthetic</script>', priority: 'HIGH', disposition: 'ACTIVE', estimatedCost: 999999, plannedYear: 2029, isCurrent: true, recommendedAtUtc: '2026-10-21T12:00:00Z' }
    ]); };
    const root = await reportsPage(services); dom.body.append(root);
    assert.equal(calls.length, 1); assert.equal(calls[0].query.page, 2); assert.equal(calls[0].query.plannedYear, 2029);
    assert.equal(calls[0].query.generatedFromUtc, '2026-10-01T00:00:00.000Z');
    assert.match(root.textContent, /Đề xuất thay thế/); assert.match(root.textContent, /<script>synthetic<\/script>/);
    assert.doesNotMatch(root.textContent, /Chi phí dự kiến|999999/); assert.equal(root.querySelectorAll('script').length, 0);
    assert.equal(descendants(root).some(n => n.tagName === 'BUTTON' && /Đánh giá|Phê duyệt|Tạo đề xuất/.test(n.textContent)), false);
    byLabel(root, 'button', 'Trang sau').fire('click');
    assert.equal(params().get('page'), '3'); assert.equal(params().get('plannedYear'), '2029');
  } finally { dom.restore(); }
});

test('replacement estimated cost is shown only with the report cost grant', async () => {
  const dom = installDom('/reports?tab=replacement');
  try {
    const { services } = fixture(['reports.replacement.read', 'reports.cost.read']);
    services.reports.replacementRecommendations = async query => pageOf(query, [{ assetCode: 'AST-2', estimatedCost: null }]);
    const root = await reportsPage(services); assert.match(root.textContent, /Chi phí dự kiến/); assert.match(root.textContent, /Chưa có thông tin/);
  } finally { dom.restore(); }
});

for (const scenario of ['detached', 'actor', 'route', 'grant', 'flag']) test(`stale report filter/navigation callback is inert after ${scenario} change`, async () => {
  const dom = installDom('/reports?tab=cost&year=2029');
  try {
    const { services, calls } = fixture(); const root = await reportsPage(services); dom.body.append(root);
    const form = byLabel(root, 'form', 'Lọc chi phí và ngân sách'); field(form, 'year').value = '2030';
    if (scenario === 'detached') root.remove();
    if (scenario === 'actor') services.auth.session = { ...services.auth.session, id: 99 };
    if (scenario === 'route') window.location.hash = '#/assets';
    if (scenario === 'grant') services.auth.session.permissions = [];
    if (scenario === 'flag') services.reportingEnabled = false;
    const hash = window.location.hash; form.fire('submit');
    const tab = descendants(root).find(n => n.tagName === 'BUTTON' && n.textContent === 'Hạn bảo hành & License'); tab.fire('click');
    assert.equal(window.location.hash, hash); assert.equal(calls.length, 2);
  } finally { dom.restore(); }
});

for (const suffix of ['page=0', 'page=1.2', 'page=2147483647&pageSize=100', 'year=2101', 'plannedYear=1999', 'licenseStatus=ACTIVE',
  'licenseFrom=2026-02-30T12%3A00', 'warrantyFrom=0000-01-01', 'licenseFrom=0000-01-01T12%3A00',
  'warrantyFrom=2026-11-01&warrantyTo=2026-10-01', 'page=1&page=2', 'accessToken=synthetic'])
  test(`invalid report URL is rejected before a read: ${suffix}`, async () => {
    const dom = installDom('/reports?tab=expiry&' + suffix);
    try { const { services, calls } = fixture(); await assert.rejects(reportsPage(services), e => e.status === 400); assert.equal(calls.length, 0); }
    finally { dom.restore(); }
  });

test('UTC filter normalization is strict and does not depend on the browser timezone', () => {
  assert.equal(reportUtc('2026-10-21T12:30'), '2026-10-21T12:30:00.000Z');
  assert.equal(reportUtc('2026-10-21T12:30:05.12'), '2026-10-21T12:30:05.120Z');
  assert.throws(() => validateReportQuery({ licenseFrom: '2026-10-22T00:00', licenseTo: '2026-10-21T00:00' }), { status: 400 });
  assert.throws(() => reportUtc('2026-10-21T24:00'), { status: 400 });
});

test('asset detail links require assets.read and become inert after the page is detached', async () => {
  const dom = installDom('/reports?tab=replacement');
  try {
    const { services } = fixture(['reports.replacement.read', 'assets.read']);
    services.reports.replacementRecommendations = async query => pageOf(query, [{ assetId: 42, assetCode: 'AST-42' }]);
    const root = await reportsPage(services); dom.body.append(root);
    const link = descendants(root).find(n => n.tagName === 'BUTTON' && n.textContent === 'AST-42');
    assert.ok(link); root.remove(); link.fire('click'); assert.equal(window.location.hash, '#/reports?tab=replacement');
    services.auth.session.permissions = ['reports.replacement.read'];
    const restricted = await reportsPage(services);
    assert.equal(descendants(restricted).some(n => n.tagName === 'BUTTON' && n.textContent === 'AST-42'), false);
  } finally { dom.restore(); }
});

test('real prepared API adapter sends only GET report routes for the new UI controls', async () => {
  const dom = installDom('/reports?tab=replacement&plannedYear=2029&priority=HIGH');
  const calls = [];
  try {
    const services = createApiServices({ reportingEnabled: true, fetchImpl: async (url, options) => {
      calls.push({ url, options });
      if (url.endsWith('/auth/login')) return Response.json({ accessToken: 'synthetic-reports-token', tokenType: 'Bearer', expiresAt: new Date(Date.now() + 60000).toISOString(), user: { id: 7, roles: [], permissions: ['reports.replacement.read'] } });
      return Response.json(pageOf({ page: 1 }));
    } });
    await services.auth.login({ email: 'fixture@example.test', password: 'synthetic-password' });
    await reportsPage(services);
    assert.equal(calls[1].url, '/api/v1/reports/replacement-recommendations?page=1&pageSize=20&priority=HIGH&plannedYear=2029');
    assert.equal(calls[1].options.method, 'GET'); assert.equal(calls[1].options.headers.Authorization, 'Bearer synthetic-reports-token');
    assert.equal(calls[1].options.cache, 'no-store'); assert.equal(calls[1].options.body, undefined);
    await services.auth.logout();
  } finally { dom.restore(); }
});
