import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';

function source(grants = ['reports.assets.read']) {
  const calls = []; let next = () => Response.json({ fixture: true });
  const services = createApiServices({ fetchImpl: async (url, options) => {
    calls.push({ url, ...options });
    if (url.endsWith('/auth/login')) return Response.json({ accessToken: 'synthetic-reports-token', tokenType: 'Bearer',
      expiresAt: new Date(Date.now() + 60_000).toISOString(), user: { id: 7, roles: ['ADMIN_IT'], permissions: grants } });
    return next(url, options);
  } });
  return { services, calls, respond(handler) { next = handler; }, login: () => services.auth.login({ email: 'fixture@example.test', password: 'synthetic-password' }) };
}

test('prepared reports reader uses only authenticated no-store GETs with exact Week 6 routes and filters', async () => {
  const allGrants = [
    'reports.assets.read',
    'reports.cost.read',
    'reports.assignments.read',
    'reports.maintenance.read',
    'reports.licenses.read',
    'reports.replacement.read'
  ];
  const fixture = source(allGrants); await fixture.login();
  const { services, calls } = fixture;

  await services.reports.assetInventory({ page: 2, pageSize: 15, keyword: 'Dell', assetTypeId: 10, departmentId: 1, status: 'IN_STOCK', purchasedFrom: '2026-01-01', purchasedTo: '2026-06-30', sortBy: 'name', sortDirection: 'asc', includeArchived: true });
  await services.reports.assetsByDepartment({ status: 'IN_USE', assetTypeId: 10 });
  await services.reports.assetsByType({ status: 'IN_STOCK', departmentId: 2 });
  await services.reports.assetStatus({ departmentId: 1, assetTypeId: 10 });
  await services.reports.assignmentHistory({ page: 1, pageSize: 20, assetId: 101, userId: 5, departmentId: 1, fromUtc: '2026-01-01T00:00:00Z', toUtc: '2026-06-01T00:00:00Z', includeArchived: false });
  await services.reports.maintenanceHistory({ page: 1, pageSize: 20, assetId: 101, technicianId: 3, status: 'RESOLVED', fromUtc: '2026-01-01T00:00:00Z', toUtc: '2026-06-01T00:00:00Z', includeArchived: true });
  await services.reports.maintenanceCost({ groupBy: 'month', departmentId: 1, assetTypeId: 10, fromUtc: '2026-01-01T00:00:00Z', toUtc: '2026-12-31T00:00:00Z' });
  await services.reports.warrantyExpiration({ page: 1, pageSize: 25, fromDate: '2026-10-01', toDate: '2026-12-31', departmentId: 1, assetTypeId: 10 });
  await services.reports.licenseExpiration({ page: 1, pageSize: 20, softwareId: 50, fromUtc: '2026-10-01T00:00:00Z', toUtc: '2026-12-31T00:00:00Z', status: 'Expiring' });
  await services.reports.replacementRecommendations({ page: 1, pageSize: 20, departmentId: 1, priority: 'HIGH', disposition: 'ACTIVE', plannedYear: 2027, generatedFromUtc: '2026-01-01T00:00:00Z', generatedToUtc: '2026-10-17T00:00:00Z' });
  await services.reports.replacementBudget({ year: 2027, departmentId: 1, priority: 'HIGH' });

  assert.deepEqual(calls.slice(1).map(call => call.url), [
    '/api/v1/reports/asset-inventory?page=2&pageSize=15&keyword=Dell&assetTypeId=10&departmentId=1&status=IN_STOCK&purchasedFrom=2026-01-01&purchasedTo=2026-06-30&sortBy=name&sortDirection=asc&includeArchived=true',
    '/api/v1/reports/assets-by-department?status=IN_USE&assetTypeId=10',
    '/api/v1/reports/assets-by-type?status=IN_STOCK&departmentId=2',
    '/api/v1/reports/asset-status?departmentId=1&assetTypeId=10',
    '/api/v1/reports/assignment-history?page=1&pageSize=20&fromUtc=2026-01-01T00%3A00%3A00Z&toUtc=2026-06-01T00%3A00%3A00Z&assetId=101&userId=5&departmentId=1',
    '/api/v1/reports/maintenance-history?page=1&pageSize=20&fromUtc=2026-01-01T00%3A00%3A00Z&toUtc=2026-06-01T00%3A00%3A00Z&assetId=101&technicianId=3&status=RESOLVED&includeArchived=true',
    '/api/v1/reports/maintenance-cost?fromUtc=2026-01-01T00%3A00%3A00Z&toUtc=2026-12-31T00%3A00%3A00Z&departmentId=1&assetTypeId=10&groupBy=month',
    '/api/v1/reports/warranty-expiration?page=1&pageSize=25&fromDate=2026-10-01&toDate=2026-12-31&departmentId=1&assetTypeId=10',
    '/api/v1/reports/license-expiration?page=1&pageSize=20&fromUtc=2026-10-01T00%3A00%3A00Z&toUtc=2026-12-31T00%3A00%3A00Z&softwareId=50&status=Expiring',
    '/api/v1/reports/replacement-recommendations?page=1&pageSize=20&generatedFromUtc=2026-01-01T00%3A00%3A00Z&generatedToUtc=2026-10-17T00%3A00%3A00Z&departmentId=1&priority=HIGH&disposition=ACTIVE&plannedYear=2027',
    '/api/v1/reports/replacement-budget?year=2027&departmentId=1&priority=HIGH'
  ]);

  for (const call of calls.slice(1)) {
    assert.equal(call.method, 'GET');
    assert.equal(call.cache, 'no-store');
    assert.equal(call.credentials, 'omit');
    assert.equal(call.headers.Authorization, 'Bearer synthetic-reports-token');
    assert.equal(call.body, undefined);
  }
});

test('prepared reports reader denies unauthenticated and ungranted calls before fetch', async () => {
  const fixture = source(); const { services, calls } = fixture;
  assert.throws(() => services.reports.assetInventory(), { status: 401 });
  assert.equal(calls.length, 0);

  await fixture.login();
  // Caller with reports.assets.read cannot read maintenanceCost or replacementBudget
  assert.throws(() => services.reports.maintenanceCost(), { status: 403 });
  assert.throws(() => services.reports.replacementBudget({ year: 2027 }), { status: 403 });
  // Sorting inventory by purchaseCost requires reports.cost.read
  assert.throws(() => services.reports.assetInventory({ sortBy: 'purchaseCost' }), { status: 403 });
  assert.equal(calls.length, 1); // Only login call

  const noGrants = source([]); await noGrants.login();
  assert.throws(() => noGrants.services.reports.assetInventory(), { status: 403 });
  assert.throws(() => noGrants.services.reports.assignmentHistory(), { status: 403 });
  assert.throws(() => noGrants.services.reports.maintenanceHistory(), { status: 403 });
  assert.throws(() => noGrants.services.reports.licenseExpiration(), { status: 403 });
  assert.throws(() => noGrants.services.reports.replacementRecommendations(), { status: 403 });
  assert.equal(noGrants.calls.length, 1);
});

test('invalid report query filters are rejected before HTTP fetch', async () => {
  const allGrants = [
    'reports.assets.read',
    'reports.cost.read',
    'reports.assignments.read',
    'reports.maintenance.read',
    'reports.licenses.read',
    'reports.replacement.read'
  ];
  const fixture = source(allGrants); await fixture.login(); const { services, calls } = fixture;

  for (const invoke of [
    () => services.reports.assetInventory({ page: 0 }),
    () => services.reports.assetInventory({ pageSize: 101 }),
    () => services.reports.assetInventory({ departmentId: -1 }),
    () => services.reports.assetInventory({ purchasedFrom: '2026-10-20', purchasedTo: '2026-10-10' }),
    () => services.reports.assetInventory({ purchasedFrom: 'invalid-date' }),
    () => services.reports.maintenanceCost({ groupBy: 'invalid' }),
    () => services.reports.maintenanceCost({ fromUtc: '2026-10-20T00:00:00Z', toUtc: '2026-10-10T00:00:00Z' }),
    () => services.reports.warrantyExpiration({ fromDate: '2026-12-31', toDate: '2026-01-01' }),
    () => services.reports.replacementBudget({ year: 1999 }),
    () => services.reports.replacementBudget({ year: 2101 }),
    () => services.reports.assignmentHistory({ fromUtc: 'not-a-utc-date' }),
    () => services.reports.maintenanceHistory({ technicianId: 0 })
  ]) {
    assert.throws(invoke, { status: 400 });
  }
  assert.equal(calls.length, 1); // Only login call
});

test('reports read failure never retries or falls back to mock data, and stale response cannot cross session switch', async () => {
  const fixture = source(['reports.assets.read']); await fixture.login(); const { services, calls } = fixture;
  fixture.respond(() => Response.json({ code: 'FORBIDDEN' }, { status: 403 }));
  await assert.rejects(services.reports.assetInventory(), { status: 403 });
  assert.equal(calls.length, 2);

  let release; fixture.respond((url) => url.endsWith('/auth/login') ? Response.json({}) : new Promise(resolve => { release = resolve; }));
  const pending = services.reports.assetInventory();
  await services.auth.logout(); await fixture.login();
  release(Response.json({ fixture: 'old-stale-report' }));
  await assert.rejects(pending, { status: 401, code: 'SESSION_CHANGED' });
  assert.equal(services.auth.session.id, 7);
});
