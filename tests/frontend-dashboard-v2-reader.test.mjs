import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';

function source(grants = ['dashboard.read']) {
  const calls = []; let next = () => Response.json({ fixture: true });
  const services = createApiServices({ fetchImpl: async (url, options) => {
    calls.push({ url, ...options });
    if (url.endsWith('/auth/login')) return Response.json({ accessToken: 'synthetic-dashboard-token', tokenType: 'Bearer',
      expiresAt: new Date(Date.now() + 60_000).toISOString(), user: { id: 7, roles: ['ADMIN_IT'], permissions: grants } });
    return next(url, options);
  } });
  return { services, calls, respond(handler) { next = handler; }, login: () => services.auth.login({ email: 'fixture@example.test', password: 'synthetic-password' }) };
}

test('prepared dashboard reader uses only authenticated no-store GETs with exact Week 6 routes and filters', async () => {
  const fixture = source(['dashboard.read', 'dashboard.cost.read']); await fixture.login();
  const { services, calls } = fixture;
  await services.dashboardV2.summary({ departmentId: 9 });
  await services.dashboardV2.assetGroups({ groupBy: 'type', departmentId: 9 });
  await services.dashboardV2.alerts({ departmentId: 9, daysAhead: 14 });
  await services.dashboardV2.costs({ departmentId: 9, fromUtc: '2026-01-01T00:00:00Z', toUtc: '2026-10-17T00:00:00Z' });
  assert.deepEqual(calls.slice(1).map(call => call.url), [
    '/api/v1/dashboard/summary?departmentId=9',
    '/api/v1/dashboard/assets?groupBy=type&departmentId=9',
    '/api/v1/dashboard/alerts?departmentId=9&daysAhead=14',
    '/api/v1/dashboard/costs?departmentId=9&fromUtc=2026-01-01T00%3A00%3A00Z&toUtc=2026-10-17T00%3A00%3A00Z'
  ]);
  for (const call of calls.slice(1)) {
    assert.equal(call.method, 'GET'); assert.equal(call.cache, 'no-store'); assert.equal(call.credentials, 'omit');
    assert.equal(call.headers.Authorization, 'Bearer synthetic-dashboard-token'); assert.equal(call.body, undefined);
  }
  assert.deepEqual(Object.keys(services.dashboard).sort(), ['summary']);
});

test('prepared dashboard reader denies unauthenticated and ungranted calls before fetch', async () => {
  const fixture = source(); const { services, calls } = fixture;
  assert.throws(() => services.dashboardV2.summary(), { status: 401 }); assert.equal(calls.length, 0);
  await fixture.login(); assert.throws(() => services.dashboardV2.costs(), { status: 403 }); assert.equal(calls.length, 1);
  const noRead = source([]); await noRead.login();
  assert.throws(() => noRead.services.dashboardV2.alerts(), { status: 403 }); assert.equal(noRead.calls.length, 1);
});

test('invalid dashboard filters are rejected before HTTP, including impossible date and reversed interval', async () => {
  const fixture = source(['dashboard.read', 'dashboard.cost.read']); await fixture.login(); const { services, calls } = fixture;
  for (const invoke of [
    () => services.dashboardV2.summary({ departmentId: '9' }),
    () => services.dashboardV2.assetGroups({ groupBy: 'all' }),
    () => services.dashboardV2.alerts({ daysAhead: 0 }),
    () => services.dashboardV2.alerts({ daysAhead: 366 }),
    () => services.dashboardV2.costs({ fromUtc: '2026-02-30T00:00:00Z' }),
    () => services.dashboardV2.costs({ fromUtc: '2026-10-17T00:00:00Z', toUtc: '2026-10-16T00:00:00Z' })
  ]) assert.throws(invoke, { status: 400 });
  assert.equal(calls.length, 1);
});

test('dashboard read failure never retries or falls back to Asset summaries, and stale result cannot cross login', async () => {
  const fixture = source(); await fixture.login(); const { services, calls } = fixture;
  fixture.respond(() => Response.json({ code: 'FORBIDDEN' }, { status: 403 }));
  await assert.rejects(services.dashboardV2.summary(), { status: 403 }); assert.equal(calls.length, 2);
  let release; fixture.respond((url) => url.endsWith('/auth/login') ? Response.json({}) : new Promise(resolve => { release = resolve; }));
  const pending = services.dashboardV2.summary();
  await services.auth.logout(); await fixture.login();
  release(Response.json({ fixture: 'old-private-dashboard' }));
  await assert.rejects(pending, { status: 401, code: 'SESSION_CHANGED' });
  assert.equal(services.auth.session.id, 7);
});
