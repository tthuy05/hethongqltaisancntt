import test from 'node:test';
import assert from 'node:assert/strict';
import { createImportExportService } from '../src/ItAssetManagement.Api/wwwroot/js/services/import-export-service.js';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';

const importResult = { totalRows: 1, validRows: 1, errorCount: 0, committed: false, dryRun: true, errors: [] };
const mime = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
test('import template uses authenticated safe workbook transport and separate import grant', async () => {
  let path;
  const services = await authenticated(async url => { path = url; return new Response(new Blob(['xlsx'], { type: mime }), { headers: { 'Content-Type': mime } }); });
  const file = await services.importExport.template(); assert.equal(path, '/api/v1/import/assets/template'); assert.equal(file.filename, 'export.xlsx');
  assert.equal(services.importExportEnabled, false);
});

test('import transport rejects wrong dry-run mode and malformed row errors', async () => {
  for (const result of [{ ...importResult, dryRun: false }, { ...importResult, errorCount: 1, errors: [{ field: 'name' }] }]) {
    const services = await authenticated(async () => Response.json(result));
    await assert.rejects(services.importExport.importAssets(new File(['fixture'], 'fixture.xlsx'), { dryRun: true }), e => e.code === 'INVALID_API_RESPONSE');
  }
});
async function authenticated(fetchBody, grants = ['import.assets.execute', 'export.assets.execute']) {
  const services = createApiServices({ fetchImpl: async (url, options) => url.endsWith('/auth/login')
    ? Response.json({ accessToken: 'synthetic-bearer', tokenType: 'Bearer', expiresAt: new Date(Date.now() + 3600000).toISOString(), user: { id: 1, displayName: 'Fixture', email: 'fixture@example.test', roles: ['ADMIN_IT'], permissions: grants } })
    : fetchBody(url, options) });
  await services.auth.login({ email: 'fixture@example.test', password: 'Fixture-password-2026!' });
  return services;
}

test('real adapter uploads with closure JWT even though CurrentUser has no token field', async () => {
  let sent;
  const services = await authenticated(async (url, options) => { sent = { url, options }; return Response.json(importResult); });
  assert.equal(services.auth.session.token, undefined);
  await services.importExport.importAssets(new File(['synthetic'], 'fixture.xlsx', { type: mime }), { dryRun: true });
  assert.equal(sent.options.headers.Authorization, 'Bearer synthetic-bearer');
  assert.ok(sent.options.body instanceof FormData);
  assert.equal(sent.options.headers['Content-Type'], undefined);
  assert.ok(!sent.url.includes('synthetic-bearer'));
});

test('download cannot complete into a logged-out session', async () => {
  let finish;
  const services = await authenticated(() => new Promise(resolve => { finish = resolve; }));
  const pending = services.importExport.exportAssets();
  await services.auth.logout();
  finish(new Response(new Blob(['synthetic'], { type: mime }), { headers: { 'Content-Type': mime } }));
  await assert.rejects(pending, error => error.code === 'SESSION_CHANGED');
});

test('private import 401 clears the matching API session and unknown query cannot leak a token', async () => {
  let calls = 0;
  const services = await authenticated(async () => { calls++; return Response.json({ code: 'UNAUTHORIZED' }, { status: 401 }); });
  await assert.rejects(async () => services.importExport.exportAssets({ accessToken: 'must-not-send' }), e => e.status === 400);
  assert.equal(calls, 0);
  await assert.rejects(services.importExport.importAssets(new File(['fixture'], 'fixture.xlsx', { type: mime })), e => e.status === 401);
  assert.equal(services.auth.session, null); assert.equal(calls, 1);
});

test('session change during body parsing rejects response and malformed workbook download is denied', async () => {
  let release;
  const services = await authenticated(async () => ({ ok: true, status: 200, headers: new Headers({ 'Content-Type': mime }), blob: () => new Promise(resolve => { release = resolve; }) }));
  const pending = services.importExport.exportAssets();
  await new Promise(resolve => setImmediate(resolve));
  await services.auth.logout(); release(new Blob(['fixture'], { type: mime }));
  await assert.rejects(pending, e => e.code === 'SESSION_CHANGED');
  const other = await authenticated(async () => Response.json({ not: 'xlsx' }));
  await assert.rejects(other.importExport.exportAssets(), e => e.code === 'INVALID_API_RESPONSE');
});

test('importAssets requires authentication', async () => {
  const service = createImportExportService({
    fetchImpl: async () => assert.fail('should not call fetch'),
    getSession: () => null,
  });

  await assert.rejects(
    async () => service.importAssets({ name: 'test.xlsx', size: 100 }),
    (err) => err.status === 401 && err.code === 'UNAUTHORIZED'
  );
});

test('importAssets requires import.assets.execute permission', async () => {
  const service = createImportExportService({
    fetchImpl: async () => assert.fail('should not call fetch'),
    getSession: () => ({ id: 1, permissions: ['reports.assets.read'] }),
  });

  await assert.rejects(
    async () => service.importAssets({ name: 'test.xlsx', size: 100 }),
    (err) => err.status === 403 && err.code === 'FORBIDDEN'
  );
});

test('importAssets rejects missing, empty, oversize, or non-xlsx files', async () => {
  const service = createImportExportService({
    fetchImpl: async () => assert.fail('should not call fetch'),
    getSession: () => ({ id: 1, permissions: ['import.assets.execute'] }),
  });

  // Null file
  await assert.rejects(
    async () => service.importAssets(null),
    (err) => err.status === 400 && err.code === 'EMPTY_FILE'
  );

  // 0-byte file
  await assert.rejects(
    async () => service.importAssets({ name: 'empty.xlsx', size: 0 }),
    (err) => err.status === 400 && err.code === 'EMPTY_FILE'
  );

  // File > 5MB
  await assert.rejects(
    async () => service.importAssets({ name: 'large.xlsx', size: 6 * 1024 * 1024 }),
    (err) => err.status === 413 && err.code === 'FILE_TOO_LARGE'
  );

  // Non-xlsx extension
  await assert.rejects(
    async () => service.importAssets({ name: 'test.csv', size: 500 }),
    (err) => err.status === 415 && err.code === 'UNSUPPORTED_MEDIA_TYPE'
  );
});

test('importAssets uploads file with multipart/form-data and query parameters', async () => {
  let calledUrl = '';
  let calledMethod = '';
  let calledHeaders = {};

  const fakeFetch = async (url, opts) => {
    calledUrl = url;
    calledMethod = opts.method;
    calledHeaders = opts.headers;
    return {
      ok: true,
      status: 200,
      json: async () => ({ totalRows: 10, validRows: 10, errorCount: 0, committed: true, dryRun: false, errors: [] }),
    };
  };

  const service = createImportExportService({
    fetchImpl: fakeFetch,
    baseUrl: '/api/v1',
    getSession: () => ({ id: 1, token: 'fake-jwt-token', permissions: ['import.assets.execute'] }),
    getToken: () => 'fake-jwt-token',
  });

  const fakeFile = new File(['synthetic'], 'valid.xlsx', { type: mime });
  const result = await service.importAssets(fakeFile, { dryRun: false });

  assert.equal(calledUrl, '/api/v1/import/assets?dryRun=false');
  assert.equal(calledMethod, 'POST');
  assert.equal(calledHeaders.Authorization, 'Bearer fake-jwt-token');
  assert.equal(result.totalRows, 10);
  assert.equal(result.committed, true);
});

test('exportAssets requires export.assets.execute and streams file without token in URL', async () => {
  let calledUrl = '';
  let calledHeaders = {};

  const fakeFetch = async (url, opts) => {
    calledUrl = url;
    calledHeaders = opts.headers;
    return {
      ok: true,
      status: 200,
      headers: new Headers({
        'Content-Disposition': 'attachment; filename="asset-inventory-20261102.xlsx"',
        'Content-Type': mime,
      }),
      blob: async () => ({ size: 5000, type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }),
    };
  };

  const service = createImportExportService({
    fetchImpl: fakeFetch,
    baseUrl: '/api/v1',
    getSession: () => ({ id: 1, token: 'bearer-secret', permissions: ['export.assets.execute'] }),
    getToken: () => 'bearer-secret',
  });

  const { blob, filename } = await service.exportAssets({ departmentId: 10, status: 'InStock' });

  assert.equal(calledUrl, '/api/v1/export/assets?departmentId=10&status=InStock');
  assert.equal(calledHeaders.Authorization, 'Bearer bearer-secret');
  assert.ok(!calledUrl.includes('bearer-secret')); // Token NEVER leaked into query string!
  assert.equal(filename, 'asset-inventory-20261102.xlsx');
  assert.equal(blob.size, 5000);
});

test('export reports handle 413 row limit exceeded error gracefully', async () => {
  const fakeFetch = async () => ({
    ok: false,
    status: 413,
  });

  const service = createImportExportService({
    fetchImpl: fakeFetch,
    getSession: () => ({ id: 1, permissions: ['export.maintenance.execute'] }),
    getToken: () => 'synthetic-token',
  });

  await assert.rejects(
    async () => service.exportMaintenance(),
    (err) => err.status === 413 && err.code === 'FILE_TOO_LARGE'
  );
});
