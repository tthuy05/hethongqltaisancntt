import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';

const login = { email: 'session@example.test', password: 'synthetic-test-password' };
function client(handler = () => Response.json({ id: 1 }), expiry = () => new Date(Date.now() + 60000).toISOString()) {
  let loginId = 0;
  const services = createApiServices({ fetchImpl: async (url, options) => url.endsWith('/auth/login')
    ? Response.json({ accessToken: 'synthetic-session-token', tokenType: 'Bearer', expiresAt: expiry(), user: { id: ++loginId, displayName: 'Synthetic', roles: ['ADMIN_IT'], permissions: ['assets.read'] } })
    : handler(url, options) });
  return services;
}

test('auth invalidation observer is explicit and receives no session or token data', async () => {
  const services = client(() => Response.json({ code: 'UNAUTHENTICATED' }, { status: 401 }));
  const events = [];
  assert.throws(() => services.auth.onSessionInvalidated(null), TypeError);
  const unsubscribe = services.auth.onSessionInvalidated(event => events.push(event));
  await services.auth.login(login);
  await assert.rejects(services.assets.get(1), error => error.status === 401);
  assert.deepEqual(events, [{ reason: 'unauthorized' }]);
  assert.equal(Object.isFrozen(events[0]), true);
  assert.equal(services.auth.session, null);
  assert.equal(unsubscribe(), true);
  await services.auth.login(login);
  await assert.rejects(services.assets.get(1), error => error.status === 401);
  assert.equal(events.length, 1);
});

test('non-JSON 401 also invalidates the session and notifies once', async () => {
  const services = client(() => new Response('Unauthorized', { status: 401 }));
  const events = [];
  services.auth.onSessionInvalidated(event => events.push(event));
  await services.auth.login(login);
  await assert.rejects(services.assets.get(1), error => error.status === 401 && error.code === 'API_UNAVAILABLE');
  await assert.rejects(services.assets.get(1), error => error.status === 401);
  assert.deepEqual(events, [{ reason: 'unauthorized' }]);
  assert.equal(services.auth.session, null);
});

test('parallel 401 responses cannot emit duplicate invalidation events', async () => {
  const pending = [];
  const services = client(() => new Promise(resolve => pending.push(resolve)));
  const events = [];
  services.auth.onSessionInvalidated(event => events.push(event));
  await services.auth.login(login);
  const results = Promise.allSettled([services.assets.get(1), services.assets.get(2)]);
  for (const resolve of pending) resolve(Response.json({ code: 'UNAUTHENTICATED' }, { status: 401 }));
  assert.ok((await results).every(result => result.status === 'rejected' && result.reason.status === 401));
  assert.deepEqual(events, [{ reason: 'unauthorized' }]);
});

test('a failing observer cannot stop other observers or prevent clearing auth state', async () => {
  const services = client(() => Response.json({}, { status: 401 }));
  const events = [];
  services.auth.onSessionInvalidated(() => { throw new Error('observer failure'); });
  services.auth.onSessionInvalidated(event => events.push(event));
  await services.auth.login(login);
  await assert.rejects(services.assets.get(1), error => error.status === 401);
  assert.equal(services.auth.session, null);
  assert.deepEqual(events, [{ reason: 'unauthorized' }]);
});

test('intentional login and logout do not report session expiry', async () => {
  const services = client();
  const events = [];
  services.auth.onSessionInvalidated(event => events.push(event));
  await services.auth.login(login);
  await services.auth.login(login);
  await services.auth.logout();
  await services.auth.logout();
  assert.equal(services.auth.session, null);
  assert.deepEqual(events, []);
});

test('403 and network failure preserve the valid session without invalidation events', async () => {
  for (const handler of [() => Response.json({}, { status: 403 }), () => { throw new Error('offline'); }]) {
    const services = client(handler);
    const events = [];
    services.auth.onSessionInvalidated(event => events.push(event));
    await services.auth.login(login);
    await assert.rejects(services.assets.get(1), error => [403, 0].includes(error.status));
    assert.equal(services.auth.session.id, 1);
    assert.deepEqual(events, []);
    await services.auth.logout();
  }
});

test('local expiry from the session getter notifies once and blocks protected requests', async () => {
  const services = client();
  const events = [];
  services.auth.onSessionInvalidated(event => events.push(event));
  await services.auth.login(login);
  const before = Date.now;
  try {
    Date.now = () => before() + 120000;
    assert.equal(services.auth.session, null);
    assert.equal(services.auth.session, null);
    await assert.rejects(services.assets.get(1), error => error.status === 401);
  } finally { Date.now = before; }
  assert.deepEqual(events, [{ reason: 'expired' }]);
});

test('idle session expiry is delivered without waiting for another API request', async () => {
  const services = client(undefined, () => new Date(Date.now() + 50).toISOString());
  const events = [];
  services.auth.onSessionInvalidated(event => events.push(event));
  await services.auth.login(login);
  await new Promise(resolve => setTimeout(resolve, 100));
  assert.deepEqual(events, [{ reason: 'expired' }]);
  assert.equal(services.auth.session, null);
});

test('new login cancels the older expiry timer; deliberate logout cancels the new timer', async () => {
  let shortExpiry = true;
  const services = client(undefined, () => new Date(Date.now() + (shortExpiry ? 40 : 60000)).toISOString());
  const events = [];
  services.auth.onSessionInvalidated(event => events.push(event));
  await services.auth.login(login);
  shortExpiry = false;
  await services.auth.login(login);
  await new Promise(resolve => setTimeout(resolve, 80));
  assert.equal(services.auth.session.id, 2);
  assert.deepEqual(events, []);
  await services.auth.logout();
  assert.equal(services.auth.session, null);
  assert.deepEqual(events, []);
});

test('stale JSON/non-JSON 401 cannot invalidate or notify after a newer login', async () => {
  for (const response of [() => Response.json({}, { status: 401 }), () => new Response('Unauthorized', { status: 401 })]) {
    let release;
    const services = client(() => new Promise(resolve => { release = resolve; }));
    const events = [];
    services.auth.onSessionInvalidated(event => events.push(event));
    await services.auth.login(login);
    const pending = services.assets.get(1);
    const rejected = assert.rejects(pending, error => error.status === 401 && error.code === 'SESSION_CHANGED');
    await services.auth.login(login);
    release(response());
    await rejected;
    assert.equal(services.auth.session.id, 2);
    assert.deepEqual(events, []);
    await services.auth.logout();
  }
});

test('stale JSON body cannot invalidate or notify after a newer login', async () => {
  let releaseBody;
  const response = { status: 401, ok: false, json: () => new Promise(resolve => { releaseBody = resolve; }) };
  const services = client(() => response);
  const events = [];
  services.auth.onSessionInvalidated(event => events.push(event));
  await services.auth.login(login);
  const pending = services.assets.get(1);
  const rejected = assert.rejects(pending, error => error.status === 401 && error.code === 'SESSION_CHANGED');
  await new Promise(resolve => setImmediate(resolve));
  await services.auth.login(login);
  releaseBody({ code: 'UNAUTHENTICATED' });
  await rejected;
  assert.equal(services.auth.session.id, 2);
  assert.deepEqual(events, []);
  await services.auth.logout();
});

test('stale network rejection is SESSION_CHANGED rather than a failure of the newer session', async () => {
  let rejectOld;
  const services = client(() => new Promise((_resolve, reject) => { rejectOld = reject; }));
  const events = [];
  services.auth.onSessionInvalidated(event => events.push(event));
  await services.auth.login(login);
  const pending = services.auth.me();
  const rejected = assert.rejects(pending, error => error.status === 401 && error.code === 'SESSION_CHANGED');
  await services.auth.login(login);
  rejectOld(new Error('old connection failed'));
  await rejected;
  assert.equal(services.auth.session.id, 2);
  assert.deepEqual(events, []);
  await services.auth.logout();
});
