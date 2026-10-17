import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';

const row = () => ({ id: 1, fromStatus: null, toStatus: 'InStock', source: 'SYSTEM', reason: null,
  changedAtUtc: '2026-10-15T04:02:01.1234567Z', changedByUserId: 7, changedByDisplayName: 'Tên hiện tại',
  correlationId: '12345678-1234-1234-1234-123456789012' });
const page = () => ({ items: [row()], page: 1, pageSize: 20, totalItems: 1, totalPages: 1 });
async function client(handler) {
  const calls = [];
  const services = createApiServices({ fetchImpl: async (url, options) => {
    calls.push({ url, options });
    if (url.endsWith('/auth/login')) return Response.json({ accessToken: 'fixture', tokenType: 'Bearer',
      expiresAt: new Date(Date.now() + 60000).toISOString(), user: { id: 7, roles: [], permissions: ['assets.read', 'assets.history.read'] } });
    return handler(url, options);
  } });
  await services.auth.login({ email: 'fixture@example.test', password: 'fixture-password' });
  return { services, calls };
}
test('history uses the existing paging-only GET with bearer/no-store and projects nonfinancial fields', async () => {
  const { services, calls } = await client(() => Response.json({ ...page(), items: [{ ...row(), email: 'discard', purchasePrice: 999, metadata: 'discard' }] }));
  try {
    assert.equal(calls.length, 1);
    const result = await services.assets.history(5);
    assert.equal(calls[1].url, '/api/v1/assets/5/status-history?page=1&pageSize=20');
    assert.equal(calls[1].options.method, 'GET'); assert.equal(calls[1].options.cache, 'no-store');
    assert.equal(calls[1].options.headers.Authorization, 'Bearer fixture');
    assert.equal(result.items[0].changedByDisplayName, 'Tên hiện tại');
    assert.equal(result.items[0].purchasePrice, undefined); assert.equal(result.items[0].email, undefined);
    assert.ok(Object.isFrozen(result.items[0])); assert.ok(Object.isFrozen(result.items));
  } finally { await services.auth.logout(); }
});
test('history rejects invalid IDs, unsupported filters and invalid paging before fetch', async () => {
  const { services, calls } = await client(() => Response.json(page()));
  try {
    for (const id of [0, -1, '5', 1.2, null, Number.MAX_SAFE_INTEGER + 1]) await assert.rejects(services.assets.history(id), { status: 400 });
    for (const query of [null, [], { page: 0 }, { page: '1' }, { pageSize: 101 }, { page: 2147483648 },
      { from: '2026-10-15' }, { status: 'Retired' }, { sortBy: 'id' }]) await assert.rejects(services.assets.history(5, query), { status: 400 });
    assert.equal(calls.length, 1);
  } finally { await services.auth.logout(); }
});
test('old backend without display name remains readable without inventing one; empty/out-of-range pages remain honest', async () => {
  const original = row(); delete original.changedByDisplayName;
  const { services } = await client(url => Response.json(url.includes('page=2') ?
    { items: [], page: 2, pageSize: 20, totalItems: 0, totalPages: 0 } : { ...page(), items: [original] }));
  try {
    assert.equal((await services.assets.history(5)).items[0].changedByDisplayName, null);
    assert.equal((await services.assets.history(5, { page: 2 })).items.length, 0);
  } finally { await services.auth.logout(); }
});
test('malformed status/source/date/page/actor payloads fail closed without echoing input', async () => {
  for (const change of [{ toStatus: 'invalid' }, { source: 'user' }, { changedAtUtc: '2026-02-30T00:00:00Z' },
    { changedAtUtc: '2026-10-15T07:00:00+07:00' }, { changedByUserId: '7' }, { changedByDisplayName: 8 },
    { reason: 'x'.repeat(1001) }, { correlationId: 'not-guid' }]) {
    const { services } = await client(() => Response.json({ ...page(), items: [{ ...row(), ...change }] }));
    try { await assert.rejects(services.assets.history(5), { status: 502 }); } finally { await services.auth.logout(); }
  }
  for (const change of [{ page: 2 }, { pageSize: 10 }, { totalPages: 2 }, { totalItems: -1 }, { items: [row(), row()] }]) {
    const { services } = await client(() => Response.json({ ...page(), ...change }));
    try { await assert.rejects(services.assets.history(5), { status: 502 }); } finally { await services.auth.logout(); }
  }
});
test('API errors are not replaced by empty/mock history and no request is retried automatically', async () => {
  for (const status of [401, 403, 404, 503]) {
    const { services, calls } = await client(() => Response.json({ code: 'FIXTURE_ERROR' }, { status }));
    try {
      await assert.rejects(services.assets.history(5), { status }); assert.equal(calls.length, 2);
      assert.equal(services.auth.session === null, status === 401);
    } finally { await services.auth.logout(); }
  }
});
test('late history from an old login is discarded even when the same account logs in again', async () => {
  let finish;
  const { services } = await client(() => new Promise(resolve => { finish = resolve; }));
  const pending = services.assets.history(5);
  await services.auth.login({ email: 'fixture@example.test', password: 'fixture-password' });
  finish(Response.json(page()));
  await assert.rejects(pending, { code: 'SESSION_CHANGED' }); await services.auth.logout();
});
