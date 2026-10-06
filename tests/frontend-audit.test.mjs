import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';
import { auditQuery, localDateInput, localDateToUtc, auditDateErrors } from '../src/ItAssetManagement.Api/wwwroot/js/utils/audit-filters.js';

async function client(handler = () => ({})) {
  const calls = [];
  const services = createApiServices({ fetchImpl: async (url, options) => {
    calls.push({ url, ...options });
    const payload = url.endsWith('/auth/login') ? { accessToken: 'synthetic-audit-token', tokenType: 'Bearer', expiresAt: new Date(Date.now() + 60000).toISOString(), user: { id: 7, roles: ['ADMIN_IT'], permissions: ['audit-logs.read'] } } : await handler(url, options);
    return payload instanceof Response ? payload : new Response(JSON.stringify(payload));
  } });
  await services.auth.login({ email: 'audit@example.test', password: 'synthetic-test-password' });
  return { services, calls };
}
test('audit default seven-day window is frozen and preserved when paging', () => {
  const now = new Date('2026-10-05T08:00:00Z');
  const query = auditQuery('', now);
  assert.equal(query.from, '2026-09-28T08:00:00.000Z'); assert.equal(query.to, now.toISOString());
  const page2 = auditQuery(new URLSearchParams({ ...query, page: 2 }).toString(), new Date('2026-10-05T09:00:00Z'));
  assert.equal(page2.from, query.from); assert.equal(page2.to, query.to); assert.equal(page2.page, '2');
});
test('audit URLs reject unknown duplicate blank and invalid to filters without silently overwriting', () => {
  for (const input of ['page=1&page=2', 'password=secret', 'From=2026-01-01', 'action=', 'to=bad', 'to=2026-10-05'])
    assert.throws(() => auditQuery(input), error => error.status === 400);
  assert.equal(auditQuery('action=assets.create&userId=71').action, 'assets.create');
  assert.equal(auditQuery('to=2026-10-05T08%3A00Z').to, '2026-10-05T08:00Z');
});
test('local date controls roundtrip to UTC and reject invalid calendar rollover', () => {
  const date = new Date('2026-10-05T08:12:37Z');
  assert.equal(localDateToUtc(localDateInput(date.toISOString())), date.toISOString());
  assert.equal(localDateToUtc('2026-10-05T08:12'), new Date(2026, 9, 5, 8, 12).toISOString());
  for (const input of ['2026-02-30T08:12', '2026-13-05T08:12', '2026-10-05T25:12', '2026-10-05', '', '2026-10-05T08:12Z']) assert.equal(localDateToUtc(input), null);
  assert.equal(localDateInput('bad'), '');
});
test('date validation enforces positive half-open interval and maximum 31 days', () => {
  assert.deepEqual(auditDateErrors('2026-10-01T00:00:00Z', '2026-11-01T00:00:00Z'), {});
  assert.ok(auditDateErrors('2026-10-01T00:00:00Z', '2026-11-01T00:00:01Z').to);
  assert.ok(auditDateErrors('2026-10-01T00:00:00Z', '2026-10-01T00:00:00Z').to);
  assert.ok(auditDateErrors(null, null).from); assert.ok(auditDateErrors('bad', 'bad').to);
});
test('audit adapter exposes only reads with exact encoded filters and no-store auth', async () => {
  const { services, calls } = await client();
  const query = { userId: 71, action: 'assets.create', entityType: 'Asset', entityId: '1 / 2', outcome: 'FAILURE', correlationId: '00000000-0000-0000-0000-000000000071', from: '2026-10-04T08:00:00+07:00', to: '2026-10-05T08:00:00+07:00', page: 2, pageSize: 20, sortBy: 'id', sortDirection: 'desc' };
  await services.auditLogs.list(query); await services.auditLogs.get(71);
  assert.deepEqual(Object.keys(services.auditLogs).sort(), ['get', 'list']);
  const parsed = new URL(calls[1].url, 'http://localhost');
  assert.equal(parsed.pathname, '/api/v1/audit-logs');
  for (const [key, value] of Object.entries(query)) assert.equal(parsed.searchParams.get(key), String(value));
  assert.equal(calls[2].url, '/api/v1/audit-logs/71');
  for (const call of calls.slice(1)) { assert.equal(call.method, 'GET'); assert.equal(call.cache, 'no-store'); assert.equal(call.credentials, 'omit'); assert.equal(call.headers.Authorization, 'Bearer synthetic-audit-token'); assert.equal(call.body, undefined); }
  assert.throws(() => services.auditLogs.get('../users'), error => error.status === 400);
});
test('audit errors are not retried or replaced with mock and unauthorized session is cleared', async () => {
  for (const status of [401, 403, 500]) {
    const { services, calls } = await client(() => new Response(JSON.stringify({ code: 'SAFE_ERROR', detail: 'safe message' }), { status }));
    await assert.rejects(services.auditLogs.list(), error => error.status === status);
    assert.equal(calls.length, 2); assert.equal(services.mode, 'api');
    assert.equal(services.auth.session === null, status === 401);
  }
});
test('logout prevents late sensitive audit responses from being consumed', async () => {
  let release;
  const { services } = await client(() => new Promise(resolve => { release = resolve; }));
  const pending = services.auditLogs.get(71);
  await services.auth.logout(); release({ id: 71, oldValues: {}, newValues: {} });
  await assert.rejects(pending, error => error.status === 401 && error.code === 'SESSION_CHANGED');
});
