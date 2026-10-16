import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';
import { can } from '../src/ItAssetManagement.Api/wwwroot/js/pages/shared.js';

const credentials = { email: 'fixture@example.test', password: 'synthetic-password' };
const validUser = () => ({ id: 7, displayName: 'Fixture', roles: ['CUSTOM_GROUP'], permissions: ['assets.read'], departmentId: null });
const response = user => ({ accessToken: 'synthetic-token', tokenType: 'Bearer', expiresAt: new Date(Date.now() + 60000).toISOString(), user });

test('login rejects malformed identity/grants before establishing a session', async () => {
  for (const invalid of [null, { ...validUser(), id: '7' }, { ...validUser(), id: 0 },
    { ...validUser(), roles: 'ADMIN_IT' }, { ...validUser(), permissions: 'assets.read' },
    { ...validUser(), permissions: [null] }, { ...validUser(), roles: [''] },
    { ...validUser(), departmentId: -1 }, { ...validUser(), email: {} }]) {
    const services = createApiServices({ fetchImpl: async () => Response.json(response(invalid)) });
    await assert.rejects(services.auth.login(credentials), error => error.status === 502 && error.code === 'INVALID_API_RESPONSE');
    assert.equal(services.auth.session, null);
    await assert.rejects(services.assets.get(1), error => error.status === 401);
  }
});
test('non-string access token fails contract without session', async () => {
  const services = createApiServices({ fetchImpl: async () => Response.json({ ...response(validUser()), accessToken: { token: 'invalid' } }) });
  await assert.rejects(services.auth.login(credentials), error => error.code === 'INVALID_API_RESPONSE');
  assert.equal(services.auth.session, null);
});
test('me contract failure clears prior session and notifies once without sensitive payload', async () => {
  const events = [];
  const services = createApiServices({ fetchImpl: async url => Response.json(url.endsWith('/auth/login')
    ? response(validUser()) : { ...validUser(), permissions: 'assets.read' }) });
  services.auth.onSessionInvalidated(event => events.push(event));
  await services.auth.login(credentials);
  await assert.rejects(services.auth.me(), error => error.status === 502 && error.code === 'INVALID_API_RESPONSE');
  assert.equal(services.auth.session, null); assert.deepEqual(events, [{ reason: 'invalid-response' }]);
});
test('safe session supports custom groups and refreshed grants but does not retain unknown secrets', async () => {
  const original = { ...validUser(), licenseKey: 'synthetic-secret', password: 'synthetic-password' };
  const services = createApiServices({ fetchImpl: async url => Response.json(url.endsWith('/auth/login')
    ? response(original) : { ...validUser(), permissions: [] }) });
  await services.auth.login(credentials);
  assert.deepEqual(services.auth.session.roles, ['CUSTOM_GROUP']);
  assert.doesNotMatch(JSON.stringify(services.auth.session), /synthetic-secret|synthetic-password/);
  assert.equal(can(services, 'assets.read'), true);
  await services.auth.me(); assert.equal(can(services, 'assets.read'), false);
  await services.auth.logout();
});
test('shared can never treats a string or object as a permission collection', () => {
  for (const permissions of ['assets.read', {}, [null], null]) {
    assert.equal(can({ auth: { session: { permissions } } }, 'assets.read'), false);
  }
});

test('401 from public activation never revokes an unrelated logged-in session, while private 401 still does', async () => {
  for (const publicRequest of [true, false]) for (const json of [true, false]) {
    const events = []; const services = createApiServices({ fetchImpl: async url => {
      if (url.endsWith('/auth/login')) return Response.json(response(validUser()));
      return json ? Response.json({ code: 'UNAUTHENTICATED' }, { status: 401 }) : new Response('Not JSON', { status: 401 });
    } });
    services.auth.onSessionInvalidated(event => events.push(event)); await services.auth.login(credentials);
    try {
      const action = publicRequest ? services.auth.activate({ token: 'A'.repeat(43), password: 'synthetic-password', confirmPassword: 'synthetic-password' }) : services.assets.get(5);
      await assert.rejects(action, { status: 401 });
      assert.equal(services.auth.session?.id ?? null, publicRequest ? 7 : null);
      assert.deepEqual(events, publicRequest ? [] : [{ reason: 'unauthorized' }]);
    } finally { await services.auth.logout(); }
  }
});

test('malformed JSON error envelopes preserve HTTP status and safe ServiceError contract without leaking scalar payloads', async () => {
  for (const payload of [null, [], true, 'synthetic-private-detail', {}, { code: {}, detail: {}, title: [], errors: 'synthetic-private-detail', traceId: {} }]) {
    const services = createApiServices({ fetchImpl: async url => url.endsWith('/auth/login') ? Response.json(response(validUser())) : Response.json(payload, { status: 503 }) });
    await services.auth.login(credentials);
    try {
      await assert.rejects(services.assets.get(5), error => {
        assert.equal(error.name, 'ServiceError'); assert.equal(error.status, 503); assert.equal(typeof error.code, 'string');
        assert.equal(typeof error.message, 'string'); assert.deepEqual(error.errors, {}); assert.equal(error.traceId, null);
        assert.doesNotMatch(error.message, /synthetic-private-detail/); return true;
      });
      assert.equal(services.auth.session.id, 7);
    } finally { await services.auth.logout(); }
  }
});

test('valid problem details keep field validation and Retry-After metadata', async () => {
  const services = createApiServices({ fetchImpl: async url => url.endsWith('/auth/login') ? Response.json(response(validUser())) : Response.json({
    code: 'VALIDATION_ERROR', detail: 'Known fixture validation', errors: { name: ['Fixture field error'] }, traceId: 'fixture-trace'
  }, { status: 400, headers: { 'Retry-After': '10' } }) });
  await services.auth.login(credentials);
  try {
    await assert.rejects(services.assets.get(5), error => {
      assert.equal(error.status, 400); assert.equal(error.code, 'VALIDATION_ERROR'); assert.equal(error.message, 'Known fixture validation');
      assert.deepEqual(error.errors, { name: ['Fixture field error'] }); assert.equal(error.traceId, 'fixture-trace'); assert.equal(error.retryAfter, '10'); return true;
    });
  } finally { await services.auth.logout(); }
});
