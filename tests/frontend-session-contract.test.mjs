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
