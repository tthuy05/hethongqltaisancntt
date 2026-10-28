import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';
import { licenseView, licensePageView } from '../src/ItAssetManagement.Api/wwwroot/js/models/license-view.js';

const license = () => ({ id: 4, licenseCode: 'FIXTURE-LICENSE', software: { id: 2, name: 'Synthetic software' },
  licenseType: 'VOLUME', totalQuantity: 5, usedQuantity: 0, isActive: true, maskedKey: '****-ABCD', expiresAt: null });
const page = item => ({ items: [item], page: 1, pageSize: 20, totalItems: 1, totalPages: 1 });
const credentials = { email: 'fixture@example.test', password: 'synthetic-password' };
const user = { id: 7, roles: ['SYSTEM_MANAGER'], permissions: ['licenses.read'] };
async function client(handler) {
  const calls = [];
  const services = createApiServices({ fetchImpl: async (url, options) => {
    calls.push({ url, options });
    if (url.endsWith('/auth/login')) return Response.json({ accessToken: 'synthetic-token', tokenType: 'Bearer',
      expiresAt: new Date(Date.now() + 60000).toISOString(), user });
    return handler(url, options);
  } });
  await services.auth.login(credentials); return { services, calls };
}
test('planned read adapter shares bearer/no-store transport and exact paths without automatic requests', async () => {
  const { services, calls } = await client(url => Response.json(url.includes('?') ? page(license()) : license()));
  assert.equal(calls.length, 1);
  const list = await services.licenses.list({ page: 1, pageSize: 20, keyword: 'a & b' });
  assert.equal(calls[1].url, '/api/v1/software-licenses?page=1&pageSize=20&keyword=a+%26+b');
  assert.equal(calls[1].options.headers.Authorization, 'Bearer synthetic-token');
  assert.equal(calls[1].options.cache, 'no-store'); assert.equal(list.items[0].availableQuantity, 5);
  assert.equal((await services.licenses.get(4)).maskedKey, '****-ABCD');
  assert.equal(calls[2].url, '/api/v1/software-licenses/4');
  assert.equal(services.licenses.reveal, undefined); assert.equal(services.licenses.assign, undefined);
  await services.auth.logout();
});
test('invalid filters and IDs fail before network; unprepared sort/expiry filters are not silently accepted', async () => {
  const { services, calls } = await client(() => Response.json(page(license())));
  for (const query of [{ page: 0 }, { pageSize: 101 }, { softwareId: '2' }, { keyword: '' }, { sortBy: 'licenseCode' },
    { expirationFrom: '2026-10-14' }, { unknown: 1 }]) {
    await assert.rejects(services.licenses.list(query), error => error.status === 400);
  }
  await assert.rejects(services.licenses.get('4'), error => error.status === 400);
  assert.equal(calls.length, 1); await services.auth.logout();
});
test('metadata rejects full key/ciphertext leak anywhere without echoing payload in the error', async () => {
  for (const extra of [{ licenseKey: 'synthetic-secret' }, { metadata: { LicenseKeyCiphertext: 'synthetic-secret' } }, { key: null }]) {
    const { services } = await client(() => Response.json({ ...license(), ...extra }));
    await assert.rejects(services.licenses.get(4), error => error.status === 502 && !JSON.stringify(error).includes('synthetic-secret'));
    assert.equal(services.auth.session.id, 7); await services.auth.logout();
  }
});
test('view uses unknown rather than fake zero capacity and rejects inconsistent/malformed data', () => {
  const missing = license(); delete missing.usedQuantity;
  assert.equal(licenseView(missing).usedQuantity, null); assert.equal(licenseView(missing).availableQuantity, null);
  for (const change of [{ usedQuantity: 6 }, { availableQuantity: 2 }, { maskedKey: 'FULL-KEY' }, { id: '4' },
    { expiresAt: 'not-date' }, { expiresAt: '2026-02-30T00:00:00Z' }, { expiresAt: '2026-10-14' },
    { expiresAt: '2026-10-14T00:00:00+07:00' }, { totalQuantity: 0 }]) assert.throws(() => licenseView({ ...license(), ...change }), error => error.status === 502);
  assert.throws(() => licensePageView({ ...page(license()), totalPages: 0 }), error => error.status === 502);
  const safe = licenseView({ ...license(), unexpected: 'not-retained', purchaseCost: 100 });
  assert.ok(Object.isFrozen(safe)); assert.equal(safe.purchaseCost, 100); assert.equal(safe.unexpected, undefined);
});
test('wrong resource/page responses fail instead of rendering another record', async () => {
  const { services } = await client(url => Response.json(url.includes('?') ? { ...page(license()), page: 2 } : { ...license(), id: 5 }));
  await assert.rejects(services.licenses.get(4), error => error.status === 502);
  await assert.rejects(services.licenses.list(), error => error.status === 502);
  await services.auth.logout();
});
test('disabled backend/network errors have no fallback and 401 clears session with no retries', async () => {
  for (const status of [401, 403, 404, 503]) {
    const { services, calls } = await client(() => Response.json({ code: 'FIXTURE_UNAVAILABLE', message: 'Fixture unavailable' }, { status }));
    await assert.rejects(services.licenses.get(4), error => error.status === status);
    assert.equal(calls.length, 2); assert.equal(services.auth.session === null, status === 401);
    await services.auth.logout();
  }
});


test('license view preserves edit metadata and validates optional cost without inventing redacted values', () => {
  const dto = { ...license(), notes: '<b>Keep this note</b>', purchasedAt: '2026-01-02T00:00:00Z', purchaseCost: 1234.50 };
  const projected = licenseView(licenseView(dto));
  assert.equal(projected.notes, dto.notes);
  assert.equal(projected.purchasedAt, dto.purchasedAt);
  assert.equal(projected.purchaseCost, dto.purchaseCost);
  assert.equal(Object.hasOwn(licenseView(license()), 'purchaseCost'), false);
  for (const extra of [{ notes: 'a'.repeat(2001) }, { notes: {} }, { purchasedAt: 'invalid' }, { purchaseCost: -1 }, { purchaseCost: '123' }, { purchaseCost: Infinity }])
    assert.throws(() => licenseView({ ...license(), ...extra }), error => error.status === 502);
});
