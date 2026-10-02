import test from 'node:test';
import assert from 'node:assert/strict';
import { createServices, services as defaultServices, ServiceError } from '../src/ItAssetManagement.Api/wwwroot/js/services/index.js';
import { ASSET_METADATA_FIELDS, ASSET_STATUSES } from '../src/ItAssetManagement.Api/wwwroot/js/models/contracts.js';
import { validateAsset, validateMaster } from '../src/ItAssetManagement.Api/wwwroot/js/utils/validation.js';

const fixture = { assetCode: 'TEST-01', name: 'Tài sản demo kiểm thử', assetTypeId: 1, owningDepartmentId: 1 };
const dtoFrom = (asset) => Object.fromEntries([...ASSET_METADATA_FIELDS, 'rowVersion'].map((field) => [field, asset[field]]));
const errorWith = (status, code) => (error) => error instanceof ServiceError && error.status === status && (!code || error.code === code);
async function mock(role = 'ADMIN_IT') {
  const services = createServices({ mode: 'mock', hostname: 'localhost', latency: 0 });
  await services.auth.login({ email: 'demo@example.invalid', password: 'not-a-real-password', role });
  return services;
}

test('mock gate rejects non-local hosts; isolated default has no automatic mock', () => {
  assert.throws(() => createServices({ mode: 'mock', hostname: 'example.com' }), errorWith(403, 'MOCK_DISABLED'));
  assert.equal(defaultServices.mode, 'api');
  assert.throws(() => createServices({ mode: 'unexpected' }), TypeError);
});
test('login validation, safe session and logout work without credential storage', async () => {
  const services = createServices({ mode: 'mock', hostname: '127.0.0.1', latency: 0 });
  await assert.rejects(services.assets.list(), errorWith(401));
  await assert.rejects(services.auth.login({ email: 'invalid', password: '' }), errorWith(400));
  await assert.rejects(services.auth.login({ email: 'demo@example.invalid', password: 'demo', role: 'SUPERUSER' }), errorWith(400));
  const response = await services.auth.login({ email: 'demo@example.invalid', password: 'demo' });
  assert.equal(response.accessToken, 'development-mock-not-a-jwt');
  assert.equal(response.tokenType, 'Bearer');
  assert.equal(services.auth.session.email, undefined);
  assert.equal(services.auth.session.password, undefined);
  assert.equal(services.auth.session.accessToken, undefined);
  const session = services.auth.session;
  session.permissions.length = 0;
  assert.ok((await services.auth.me()).permissions.includes('assets.read'));
  await services.auth.logout();
  assert.equal(services.auth.session, null);
  await assert.rejects(services.assets.list(), errorWith(401));
});
test('logout also invalidates in-flight mock calls', async () => {
  const services = createServices({ mode: 'mock', hostname: 'localhost', latency: 15 });
  await services.auth.login({ email: 'demo@example.invalid', password: 'demo' });
  const pending = services.assets.list();
  await services.auth.logout();
  await assert.rejects(pending, errorWith(401));
});
test('24 synthetic assets use exact paged response and summary contract', async () => {
  const services = await mock();
  const page = await services.assets.list();
  assert.deepEqual(Object.keys(page), ['items', 'page', 'pageSize', 'totalItems', 'totalPages']);
  assert.equal(page.totalItems, 24);
  assert.equal(page.items.length, 20);
  assert.equal(page.totalPages, 2);
  assert.equal(page.items[0].brand, undefined);
  assert.equal(page.items[0].rowVersion, undefined);
  const outside = await services.assets.list({ page: 100 });
  assert.deepEqual(outside.items, []);
  assert.equal(outside.totalItems, 24);
});
test('paging, sorts, status and unsupported advanced filters reject invalid values', async () => {
  const services = await mock();
  for (const query of [{ page: 0 }, { page: 1.5 }, { pageSize: 101 }, { pageSize: 0 }, { sortBy: 'purchasePrice' }, { sortDirection: 'DOWN' }, { status: 'RETIRED' }, { departmentId: -1 }, { keyword: 'x'.repeat(201) }, { purchaseYear: 2026 }]) {
    await assert.rejects(services.assets.list(query), errorWith(400, 'VALIDATION_ERROR'));
  }
});
test('asset literal search, filters and explicit latest sorting work', async () => {
  const services = await mock();
  const result = await services.assets.list({ keyword: ' demo-sn-00001 ', departmentId: 1, assetTypeId: 1, status: 'InStock' });
  assert.equal(result.totalItems, 1);
  assert.equal(result.items[0].assetCode, 'TS-0001');
  assert.equal((await services.assets.list({ keyword: '%' })).totalItems, 0);
  const latest = await services.assets.list({ sortBy: 'createdAt', sortDirection: 'desc', pageSize: 5 });
  assert.equal(latest.items[0].id, 24);
});
test('technical support response omits purchasePrice and rejects direct mutations', async () => {
  const services = await mock('TECHNICAL_SUPPORT');
  const list = await services.assets.list();
  assert.equal(Object.hasOwn(list.items[0], 'purchasePrice'), false);
  assert.equal(Object.hasOwn(await services.assets.get(1), 'purchasePrice'), false);
  await assert.rejects(services.assets.create(fixture), errorWith(403));
  await assert.rejects(services.assets.update(1, {}), errorWith(403));
  await assert.rejects(services.assets.archive(1), errorWith(403));
  await assert.rejects(services.departments.create({ code: 'NEW', name: 'Demo' }), errorWith(403));
});
test('manager can mutate assets but not master data', async () => {
  const services = await mock('SYSTEM_MANAGER');
  assert.ok(Object.hasOwn(await services.assets.get(1), 'purchasePrice'));
  assert.equal((await services.assets.create(fixture)).status, 'InStock');
  await assert.rejects(services.assetTypes.create({ code: 'NEW', name: 'Demo' }), errorWith(403));
  assert.equal((await services.departments.list()).totalItems, 4);
});
test('asset required, length, money, date and immutable state validation', async () => {
  const services = await mock();
  const invalid = [ {}, { ...fixture, name: 'x'.repeat(201) }, { ...fixture, purchasePrice: -1 }, { ...fixture, purchasePrice: 1.234 }, { ...fixture, purchaseDate: '2026-02-30' }, { ...fixture, purchaseDate: '2026-04-01', warrantyExpirationDate: '2026-03-31' }, { ...fixture, status: 'InUse' }, { ...fixture, currentAssignment: {} } ];
  for (const dto of invalid) await assert.rejects(services.assets.create(dto), errorWith(400));
  assert.equal(Object.keys(validateAsset({ ...fixture, purchasePrice: null })).length, 0);
});
test('new asset forces InStock, preserves null money and has valid opaque version', async () => {
  const services = await mock();
  const created = await services.assets.create({ ...fixture, assetCode: ' test-01 ', purchasePrice: null });
  assert.equal(created.assetCode, 'TEST-01');
  assert.equal(created.status, 'InStock');
  assert.equal(created.purchasePrice, null);
  assert.equal(created.currentAssignment, null);
  assert.equal(Buffer.from(created.rowVersion, 'base64').length, 16);
  assert.equal((await services.assets.get(created.id)).id, created.id);
});
test('case-insensitive asset code and serial duplicates yield specific 409', async () => {
  const services = await mock();
  await assert.rejects(services.assets.create({ ...fixture, assetCode: 'ts-0001' }), errorWith(409, 'ASSET_CODE_CONFLICT'));
  await assert.rejects(services.assets.create({ ...fixture, serialNumber: 'demo-sn-00001' }), errorWith(409, 'ASSET_SERIAL_CONFLICT'));
});
test('create rejects unknown/inactive references with 400 field errors', async () => {
  const services = await mock();
  for (const dto of [{ ...fixture, assetTypeId: 999 }, { ...fixture, owningDepartmentId: 999 }, { ...fixture, assetTypeId: 5 }, { ...fixture, owningDepartmentId: 4 }]) await assert.rejects(services.assets.create(dto), errorWith(400));
});
test('PUT keeps existing inactive references but rejects newly selected inactive targets', async () => {
  const services = await mock();
  const legacy = await services.assets.get(20);
  assert.equal(legacy.assetType.isActive, false);
  assert.equal(legacy.owningDepartment.isActive, false);
  const edited = await services.assets.update(20, { ...dtoFrom(legacy), name: 'Sửa metadata giữ danh mục inactive' });
  assert.equal(edited.owningDepartmentId, 4);
  const ordinary = await services.assets.get(1);
  await assert.rejects(services.assets.update(1, { ...dtoFrom(ordinary), owningDepartmentId: 4 }), errorWith(400));
});
test('PUT clears omitted optional metadata and changes version; stale update rejected', async () => {
  const services = await mock();
  const original = await services.assets.get(1);
  const edited = await services.assets.update(1, { ...fixture, rowVersion: original.rowVersion });
  assert.equal(edited.serialNumber, null);
  assert.equal(edited.purchasePrice, null);
  assert.notEqual(edited.rowVersion, original.rowVersion);
  assert.ok(edited.updatedAt.endsWith('Z'));
  await assert.rejects(services.assets.update(1, { ...fixture, rowVersion: original.rowVersion }), errorWith(409, 'CONCURRENCY_CONFLICT'));
});
test('archive requires version, rejects active fixture workflows, hides archived assets and retains uniqueness', async () => {
  const services = await mock();
  const asset = await services.assets.get(1);
  await assert.rejects(services.assets.archive(1), errorWith(428));
  await assert.rejects(services.assets.archive(1, '*'), errorWith(400));
  const inUse = await services.assets.get(2);
  await assert.rejects(services.assets.archive(2, inUse.rowVersion), errorWith(409, 'ASSET_ACTIVE_WORKFLOW'));
  await services.assets.archive(1, asset.rowVersion);
  assert.equal((await services.assets.list()).totalItems, 23);
  await assert.rejects(services.assets.get(1), errorWith(404, 'ASSET_NOT_FOUND'));
  await assert.rejects(services.assets.create({ ...fixture, assetCode: asset.assetCode }), errorWith(409, 'ASSET_CODE_CONFLICT'));
});
test('isolated factory resets synthetic state; response mutations do not mutate service records', async () => {
  const services = await mock();
  await services.assets.create(fixture);
  const asset = await services.assets.get(1);
  asset.assetType.name = 'Changed externally';
  assert.notEqual((await services.assets.get(1)).assetType.name, asset.assetType.name);
  assert.equal((await (await mock()).assets.list()).totalItems, 24);
});
test('master list supports active/inactive, keyword, sorting and pagination', async () => {
  const services = await mock();
  assert.equal((await services.departments.list({ status: 'Active' })).totalItems, 3);
  assert.equal((await services.assetTypes.list({ status: 'Inactive' })).items[0].code, 'PRINTER');
  assert.equal((await services.departments.list({ keyword: 'nhân' })).items[0].code, 'HR');
  assert.equal((await services.departments.list({ pageSize: 1 })).totalPages, 4);
  await assert.rejects(services.departments.list({ status: 'Enabled' }), errorWith(400));
});
test('master create/update/deactivate/reactivate preserves references and validates version', async () => {
  const services = await mock();
  const department = await services.departments.create({ code: 'NEW', name: 'Phòng demo', parentDepartmentId: 1, description: 'Demo' });
  assert.equal(department.isActive, true);
  const updated = await services.departments.update(department.id, { code: department.code, name: 'Đổi tên', parentDepartmentId: 1, rowVersion: department.rowVersion });
  assert.notEqual(updated.rowVersion, department.rowVersion);
  const disabled = await services.departments.setStatus(updated.id, { isActive: false, reason: 'Demo deactivate', rowVersion: updated.rowVersion });
  assert.equal(disabled.isActive, false);
  const enabled = await services.departments.setStatus(disabled.id, { isActive: true, reason: 'Demo reactivate', rowVersion: disabled.rowVersion });
  assert.equal(enabled.isActive, true);
  await assert.rejects(services.departments.setStatus(enabled.id, { isActive: false, reason: '', rowVersion: enabled.rowVersion }), errorWith(400));
  await assert.rejects(services.departments.update(enabled.id, { code: 'CHANGED', name: 'Tên', rowVersion: enabled.rowVersion }), errorWith(400));
  await assert.rejects(services.departments.update(enabled.id, { code: enabled.code, name: 'Tên', rowVersion: updated.rowVersion }), errorWith(409));
});
test('department cycle and duplicate checks; asset type lifetime constraint', async () => {
  const services = await mock();
  const department = await services.departments.get(1);
  await assert.rejects(services.departments.update(1, { code: department.code, name: department.name, parentDepartmentId: 1, rowVersion: department.rowVersion }), errorWith(409, 'DEPARTMENT_CYCLE'));
  const child = await services.departments.create({ code: 'CHILD', name: 'Con', parentDepartmentId: 1 });
  await assert.rejects(services.departments.update(1, { code: department.code, name: department.name, parentDepartmentId: child.id, rowVersion: department.rowVersion }), errorWith(409, 'DEPARTMENT_CYCLE'));
  await assert.rejects(services.departments.create({ code: 'it', name: 'Trùng' }), errorWith(409));
  assert.ok(validateMaster({ code: 'TYPE', name: 'Loại', defaultUsefulLifeMonths: 0 }, { kind: 'assetType' }).defaultUsefulLifeMonths);
  const type = await services.assetTypes.create({ code: 'NEW-TYPE', name: 'Loại demo', defaultUsefulLifeMonths: 36 });
  assert.equal(type.defaultUsefulLifeMonths, 36);
  await assert.rejects(services.assetTypes.update(type.id, { code: type.code, name: type.name, defaultUsefulLifeMonths: -2, rowVersion: type.rowVersion }), errorWith(400));
});
test('dashboard returns computed synthetic counts and planned/mock provenance', async () => {
  const services = await mock();
  const summary = await services.dashboard.summary();
  assert.equal(summary.totalAssets, 24);
  assert.equal(summary.byStatus.reduce((count, item) => count + item.count, 0), 24);
  assert.equal(summary.byStatus.length, ASSET_STATUSES.length);
  assert.equal(summary.recentAssets[0].id, 24);
  assert.equal(summary.replacementStatus, 'MOCK');
  assert.equal(summary.dataMode, 'mock');
  assert.equal(summary.replacementNeeded, 6);
});

function apiHarness() {
  const calls = [];
  let nextResponse = null;
  const services = createServices({ mode: 'api', fetchImpl: async (url, options) => {
    calls.push({ url, ...options });
    if (nextResponse) { const response = nextResponse; nextResponse = null; return response; }
    if (url.endsWith('/auth/login')) return Response.json({ accessToken: 'test-token-not-production', tokenType: 'Bearer', expiresAt: new Date(Date.now() + 3600000).toISOString(), user: { id: 1, displayName: 'Test', roles: ['ADMIN_IT'], permissions: ['assets.read'] } });
    return Response.json({ id: 1 });
  } });
  return { services, calls, next(response) { nextResponse = response; } };
}
test('API adapter uses exact login body, bearer header, PUT metadata and strong If-Match archive', async () => {
  const harness = apiHarness();
  await harness.services.auth.login({ email: ' test@example.invalid ', password: ' keep whitespace ', role: 'ADMIN_IT' });
  assert.deepEqual(JSON.parse(harness.calls[0].body), { email: 'test@example.invalid', password: ' keep whitespace ' });
  assert.equal(harness.calls[0].credentials, 'omit');
  assert.equal(harness.calls[0].headers.Authorization, undefined);
  await harness.services.assets.update(1, { ...fixture, rowVersion: 'AAAAAAAAAAAAAAAAAAAAAA==' });
  assert.equal(harness.calls[1].url, '/api/v1/assets/1');
  assert.equal(harness.calls[1].method, 'PUT');
  assert.equal(harness.calls[1].headers.Authorization, 'Bearer test-token-not-production');
  harness.next(new Response(null, { status: 204 }));
  await harness.services.assets.archive(1, 'AAAAAAAAAAAAAAAAAAAAAA==');
  assert.equal(harness.calls[2].method, 'DELETE');
  assert.equal(harness.calls[2].headers['If-Match'], '"AAAAAAAAAAAAAAAAAAAAAA=="');
});
test('API adapter encodes query and surfaces structured problem details; 401 clears memory session', async () => {
  const harness = apiHarness();
  await harness.services.auth.login({ email: 'test@example.invalid', password: 'demo' });
  await harness.services.assets.list({ keyword: 'Dell & HP', status: '', page: 1 });
  assert.ok(harness.calls[1].url.includes('keyword=Dell+%26+HP'));
  assert.equal(harness.calls[1].url.includes('status='), false);
  harness.next(Response.json({ status: 409, code: 'CONCURRENCY_CONFLICT', detail: 'Stale', traceId: 'test-trace' }, { status: 409 }));
  await assert.rejects(harness.services.assets.get(1), (error) => errorWith(409, 'CONCURRENCY_CONFLICT')(error) && error.traceId === 'test-trace');
  harness.next(Response.json({ code: 'UNAUTHENTICATED' }, { status: 401 }));
  await assert.rejects(harness.services.assets.get(1), errorWith(401));
  assert.equal(harness.services.auth.session, null);
});
test('API mode fails without backend and never falls back to mock data', async () => {
  const services = createServices({ mode: 'api', fetchImpl: async () => { throw new Error('Offline'); } });
  await assert.rejects(services.auth.login({ email: 'test@example.invalid', password: 'demo' }), errorWith(0, 'NETWORK_ERROR'));
  assert.equal(services.auth.session, null);
  assert.equal(services.mode, 'api');
});

test('decimal text cannot be silently rounded during purchase price validation', () => {
  assert.ok(validateAsset({ ...fixture, purchasePrice: '9007199254740991.01' }).purchasePrice);
  assert.ok(validateAsset({ ...fixture, purchasePrice: '9999999999999999.99' }).purchasePrice);
  assert.deepEqual(validateAsset({ ...fixture, purchasePrice: '12345678.50' }), {});
  assert.deepEqual(validateAsset({ ...fixture, purchasePrice: '0.01' }), {});
});

test('pending mock login cannot recreate a logged-out session', async () => {
  const services = createServices({ mode: 'mock', hostname: 'localhost', latency: 15 });
  const pending = services.auth.login({ email: 'test@example.test', password: 'test-only' });
  await services.auth.logout();
  await assert.rejects(pending, errorWith(401, 'SESSION_CHANGED'));
  assert.equal(services.auth.session, null);
});

test('a new mock login invalidates commands started in the old session', async () => {
  const services = createServices({ mode: 'mock', hostname: 'localhost', latency: 15 });
  await services.auth.login({ email: 'old@example.test', password: 'test-only' });
  const pending = services.assets.create(fixture);
  const rejected = assert.rejects(pending, errorWith(401, 'SESSION_CHANGED'));
  await services.auth.login({ email: 'new@example.test', password: 'test-only' });
  await rejected;
  assert.equal((await services.assets.list({ keyword: 'TEST-01' })).totalItems, 0);
});

test('API adapter cannot restore a logged-out session from pending me response', async () => {
  let resolveMe;
  const user = { id: 1, displayName: 'Demo', roles: ['ADMIN_IT'], permissions: ['assets.read'] };
  const services = createServices({ mode: 'api', fetchImpl: async url => {
    if (url.endsWith('/auth/login')) return Response.json({ accessToken: 'test-only', tokenType: 'Bearer', expiresAt: new Date(Date.now() + 3600000).toISOString(), user });
    return new Promise(resolve => { resolveMe = resolve; });
  } });
  await services.auth.login({ email: 'test@example.test', password: 'test-only' });
  const pending = services.auth.me();
  await services.auth.logout();
  resolveMe(Response.json(user));
  await assert.rejects(pending, errorWith(401, 'SESSION_CHANGED'));
  assert.equal(services.auth.session, null);
});

test('stale 401 cannot invalidate a newer API login', async () => {
  let resolveOld;
  let loginId = 0;
  const services = createServices({ mode: 'api', fetchImpl: async url => {
    if (url.endsWith('/auth/login')) return Response.json({ accessToken: 'test-only', tokenType: 'Bearer', expiresAt: new Date(Date.now() + 3600000).toISOString(), user: { id: ++loginId, displayName: 'Demo', roles: ['ADMIN_IT'], permissions: ['assets.read'] } });
    return new Promise(resolve => { resolveOld = resolve; });
  } });
  await services.auth.login({ email: 'test@example.test', password: 'test-only' });
  const pending = services.assets.get(1);
  await services.auth.login({ email: 'new@example.test', password: 'test-only' });
  resolveOld(Response.json({ title: 'Old unauthorized' }, { status: 401 }));
  await assert.rejects(pending, errorWith(401, 'SESSION_CHANGED'));
  assert.equal(services.auth.session.id, 2);
});
