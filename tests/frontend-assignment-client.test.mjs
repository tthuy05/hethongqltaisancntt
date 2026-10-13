import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';
import { ServiceError } from '../src/ItAssetManagement.Api/wwwroot/js/services/service-error.js';

const credentials = { email: 'fixture@example.test', password: 'synthetic-test-password' };
const version = Buffer.alloc(16, 7).toString('base64');
const assignment = { id: 4, assetId: 2, assignedUserId: 7, assignedDepartmentId: null, rowVersion: version };
function fixture(handler = () => Response.json(assignment)) {
  const calls = [];
  const client = createApiServices({ fetchImpl: async (url, options) => {
    if (url.endsWith('/auth/login')) return Response.json({ accessToken: 'synthetic-client-token', tokenType: 'Bearer',
      expiresAt: new Date(Date.now() + 60000).toISOString(), user: { id: 7, roles: ['SYSTEM_MANAGER'], permissions: ['assignments.read'] } });
    calls.push({ url, options }); return handler(url, options);
  } });
  return { client, calls };
}

test('four current routes and lookup share the authenticated same-origin transport', async () => {
  const { client, calls } = fixture(); await client.auth.login(credentials);
  await client.assignments.list({ assetId: 2, active: false, page: 1, pageSize: 20 });
  await client.assignments.get(4);
  await client.assignments.assign({ assetId: 2, userId: 7, assignedAt: '2026-10-14T08:00:00Z', note: '  Handover  ' });
  await client.assignments.returnAsset(4, { rowVersion: version, note: '  Returned  ' });
  await client.users.lookup({ keyword: 'Thủy & Thiện', pageSize: 10 });
  assert.deepEqual(calls.map(call => [call.url.split('?')[0], call.options.method]), [
    ['/api/v1/asset-assignments', 'GET'], ['/api/v1/asset-assignments/4', 'GET'],
    ['/api/v1/asset-assignments', 'POST'], ['/api/v1/asset-assignments/4/return', 'POST'], ['/api/v1/users/lookup', 'GET'],
  ]);
  assert.equal(new URL(calls[0].url, 'https://fixture.test').searchParams.get('active'), 'false');
  assert.equal(JSON.parse(calls[2].options.body).note, 'Handover');
  assert.equal(JSON.parse(calls[3].options.body).rowVersion, version);
  assert.equal(JSON.parse(calls[3].options.body).note, 'Returned');
  assert.equal(new URL(calls[4].url, 'https://fixture.test').searchParams.get('keyword'), 'Thủy & Thiện');
  for (const { options } of calls) {
    assert.equal(options.headers.Authorization, 'Bearer synthetic-client-token');
    assert.equal(options.cache, 'no-store'); assert.equal(options.credentials, 'omit');
  }
  assert.equal(client.assignments.transfer, undefined); assert.equal(client.maintenance, undefined);
});

test('invalid XOR, DTO fields, UTC, query and stale-version inputs reject before a network request', async () => {
  const { client, calls } = fixture(); await client.auth.login(credentials);
  const invalid = [
    () => client.assignments.assign({ assetId: 2 }),
    () => client.assignments.assign({ assetId: 2, userId: 7, departmentId: 9 }),
    () => client.assignments.assign({ assetId: 2, userId: 0 }),
    () => client.assignments.assign({ assetId: 2, userId: 7, expectedReturnAt: '2026-10-15T00:00:00Z' }),
    () => client.assignments.assign({ assetId: 2, userId: 7, assignedAt: '2026-10-14T08:00:00+07:00' }),
    () => client.assignments.assign({ assetId: 2, userId: 7, assignedAt: '2026-02-31T08:00:00Z' }),
    () => client.assignments.list({ status: 'Active' }),
    () => client.assignments.list({ pageSize: 101 }),
    () => client.assignments.list({ page: 2147483647, pageSize: 100 }),
    () => client.assignments.list({ active: 'false' }),
    () => client.assignments.list({ assetId: true }),
    () => client.assignments.get('../4'),
    () => client.assignments.get(true),
    () => client.assignments.returnAsset(true, { rowVersion: version, note: 'Returned' }),
    () => client.assignments.returnAsset(4, { rowVersion: version, note: ' ' }),
    () => client.assignments.returnAsset(4, { rowVersion: version, note: 'Returned', condition: 'Good' }),
    () => client.assignments.returnAsset(4, { rowVersion: 'x'.repeat(22) + '==', note: 'Returned' }),
  ];
  for (const operation of invalid) await assert.rejects(operation, error => error instanceof ServiceError && error.status === 400);
  assert.equal(calls.length, 0);
});

test('unauthenticated callers do not send assignment or lookup requests', async () => {
  const { client, calls } = fixture();
  await assert.rejects(client.assignments.get(4), error => error.status === 401);
  await assert.rejects(client.users.lookup(), error => error.status === 401); assert.equal(calls.length, 0);
});

test('403, 409 and disabled/not-ready rollout errors preserve the session and never retry mutations', async () => {
  for (const [status, code] of [[403, 'FORBIDDEN'], [409, 'CONCURRENCY_CONFLICT'], [404, 'NOT_FOUND'], [503, 'ASSIGNMENT_NOT_READY']]) {
    const { client, calls } = fixture(() => Response.json({ code, detail: 'Fixture failure', traceId: 'fixture-trace' },
      { status, headers: { 'Retry-After': '60' } }));
    await client.auth.login(credentials);
    await assert.rejects(client.assignments.returnAsset(4, { rowVersion: version, note: 'Returned' }), error =>
      error.status === status && error.code === code && error.traceId === 'fixture-trace' && error.retryAfter === '60');
    assert.ok(client.auth.session); assert.equal(calls.length, 1);
  }
});

test('401 invalidates the session while a late assignment response cannot overwrite a newer login', async () => {
  const expired = fixture(() => Response.json({ code: 'UNAUTHORIZED' }, { status: 401 }));
  await expired.client.auth.login(credentials);
  await assert.rejects(expired.client.assignments.get(4), error => error.status === 401);
  assert.equal(expired.client.auth.session, null);
  let complete;
  const delayed = fixture(() => new Promise(resolve => { complete = resolve; }));
  await delayed.client.auth.login(credentials);
  const pending = delayed.client.assignments.get(4);
  await delayed.client.auth.logout(); await delayed.client.auth.login(credentials);
  complete(Response.json(assignment));
  await assert.rejects(pending, error => error.code === 'SESSION_CHANGED'); assert.ok(delayed.client.auth.session);
});
