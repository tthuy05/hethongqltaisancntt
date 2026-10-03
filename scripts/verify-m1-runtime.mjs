import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { randomUUID } from 'node:crypto';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';

// Explicit manual shared-dev smoke. One uniquely named asset is created, edited,
// then archived (never deleted). Credentials/tokens are never printed or written.
const origin = 'http://localhost:5080';
const credentialsPath = process.argv[2] || path.join(process.env.LOCALAPPDATA, 'ItAssetManagement', 'development-bootstrap.json');
const credential = JSON.parse(await readFile(credentialsPath, 'utf8'));
const services = createApiServices({ baseUrl: `${origin}/api/v1` });
const statuses = {};
for (const [name, route] of Object.entries({ live: '/health/live', ready: '/health/ready', openapi: '/openapi/v1.json', frontend: '/', css: '/css/app.css', module: '/js/app.js' })) {
  const response = await fetch(origin + route);
  assert.equal(response.status, 200, `${name} must be 200`);
  statuses[name] = response.status;
  if (name === 'frontend') {
    assert.match(await response.text(), /Quản lý tài sản CNTT/u);
    assert.match(response.headers.get('content-security-policy'), /connect-src 'self'/u);
  }
}
await services.auth.login(credential);
const me = await services.auth.me();
assert(me.permissions.includes('assets.create'));
const departments = await services.departments.list({ pageSize: 100, status: 'Active' });
const types = await services.assetTypes.list({ pageSize: 100, status: 'Active' });
const department = departments.items.find(x => x.code === 'IT');
const type = types.items.find(x => x.code === 'LAPTOP');
await services.departments.get(department.id);
await services.assetTypes.get(type.id);
// Verify the earlier shared-db record survived a full API process restart.
const prior = await services.assets.get(1);
assert.equal(prior.assetCode, 'MVP-DEMO-20261002-b301b3e1');
const summary = await services.dashboard.summary();
assert(summary.totalAssets >= 1);
const dto = { assetCode: 'MVP-UI-SMOKE-' + randomUUID().slice(0, 8), name: 'Frontend API adapter smoke',
  assetTypeId: type.id, owningDepartmentId: department.id, purchasePrice: null };
const created = await services.assets.create(dto);
const detail = await services.assets.get(created.id);
assert.equal(detail.purchasePrice, null);
const updated = await services.assets.update(created.id, { ...dto, name: 'Frontend API adapter persisted update', rowVersion: detail.rowVersion });
assert.notEqual(updated.rowVersion, detail.rowVersion);
const found = await services.assets.list({ keyword: dto.assetCode, page: 1, pageSize: 20, sortBy: 'assetCode', sortDirection: 'asc', status: 'InStock' });
assert.equal(found.totalItems, 1);
await services.assets.archive(created.id, updated.rowVersion);
const absent = await services.assets.list({ keyword: dto.assetCode });
assert.equal(absent.totalItems, 0);
await services.auth.logout();
console.log(JSON.stringify({ statuses, mode: services.mode, login: 'PASS', currentUser: 'PASS', dashboard: 'PASS',
  departmentRead: 'PASS', assetTypeRead: 'PASS', assetCreateGetUpdateSearchArchive: 'PASS',
  restartPersistence: 'PASS', earlierAssetId: prior.id, earlierAssetCode: prior.assetCode,
  archivedSmokeAssetId: created.id, departmentCount: departments.totalItems, assetTypeCount: types.totalItems }));
