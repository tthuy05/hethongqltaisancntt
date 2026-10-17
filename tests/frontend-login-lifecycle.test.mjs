import test from 'node:test';
import assert from 'node:assert/strict';
import { loginPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/login.js';
import { validateLogin } from '../src/ItAssetManagement.Api/wwwroot/js/utils/validation.js';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const password = '  synthetic-password  ';
function source({ login, me } = {}) {
  const calls = { login: [], me: 0, logout: 0 }; const services = { mode: 'api', calls, auth: {
    session: { id: 7, roles: [], permissions: ['assets.read'] },
    login: async dto => { calls.login.push(structuredClone(dto)); return login?.(dto); },
    me: async () => { calls.me++; return me ? me() : services.auth.session; },
    logout: async () => { calls.logout++; services.auth.session = null; }
  } }; return services;
}
function mount(dom, services, next) {
  const root = loginPage(services, next); dom.body.append(root);
  const nodes = descendants(root); const email = nodes.find(n => n.getAttribute('name') === 'email'); email.value = ' fixture@example.test ';
  const secret = nodes.find(n => n.getAttribute('name') === 'password'); secret.value = password;
  return { root, email, secret, form: nodes.find(n => n.tagName === 'FORM'),
    submit: nodes.find(n => n.tagName === 'BUTTON' && n.getAttribute('type') === 'submit'),
    toggle: nodes.find(n => n.getAttribute('aria-label') === 'Hiện hoặc ẩn mật khẩu') };
}
test('login validation matches server maximum without imposing activation minimum on legacy logins', () => {
  assert.deepEqual(validateLogin({ email: 'fixture@example.test', password: 'short' }), {});
  assert.ok(validateLogin({ email: 'fixture@example.test', password: 'x'.repeat(257) }).password);
  assert.ok(validateLogin({ email: 'bad', password: '' }).email);
});
test('login single-flights exact untrimmed password, readonly fields, me verification and internal return path', async () => {
  const dom = installDom('/login'); try {
    let finish; const services = source({ login: () => new Promise(resolve => { finish = resolve; }) });
    const view = mount(dom, services, '/assets?keyword=a%26b'); view.form.fire('submit'); view.form.fire('submit');
    assert.deepEqual(services.calls.login, [{ email: 'fixture@example.test', password }]); assert.equal(services.calls.me, 0);
    assert.equal(view.email.readOnly, true); assert.equal(view.secret.readOnly, true); assert.equal(view.toggle.disabled, true);
    finish(); await until(() => !view.submit.disabled);
    assert.equal(services.calls.me, 1); assert.match(location.hash, /^#?\/assets\?keyword=a%26b$/);
    assert.equal(view.secret.value, ''); assert.equal(view.secret.type, 'password'); assert.equal(view.toggle.getAttribute('aria-pressed'), 'false');
    assert.equal(services.calls.logout, 0);
  } finally { dom.restore(); }
});
test('invalid local credentials send no login request', () => {
  const dom = installDom('/login'); try {
    const services = source(); const view = mount(dom, services); view.secret.value = 'x'.repeat(257); view.form.fire('submit');
    assert.equal(services.calls.login.length, 0); assert.match(view.root.textContent, /256/); assert.equal(view.submit.disabled, false);
  } finally { dom.restore(); }
});
test('detached login completion does not call me, redirect or logout another session and still clears password', async () => {
  const dom = installDom('/login'); try {
    let finish; const services = source({ login: () => new Promise(resolve => { finish = resolve; }) }); const view = mount(dom, services);
    view.form.fire('submit'); view.root.remove(); location.hash = '#/activate'; services.auth.session = { id: 8, roles: [], permissions: [] };
    finish(); await until(() => view.secret.value === '');
    assert.equal(services.calls.me, 0); assert.equal(services.calls.logout, 0); assert.equal(services.auth.session.id, 8); assert.equal(location.hash, '#/activate');
  } finally { dom.restore(); }
});
test('old me failure cannot logout a newer session or echo backend details', async () => {
  const dom = installDom('/login'); try {
    let reject; const services = source({ me: () => new Promise((_resolve, fail) => { reject = fail; }) }); const view = mount(dom, services);
    view.form.fire('submit'); await until(() => services.calls.me === 1); services.auth.session = { id: 8, roles: [], permissions: [] };
    reject(Object.assign(new Error('synthetic-secret'), { status: 503 })); await until(() => !view.submit.disabled);
    assert.equal(services.calls.logout, 0); assert.equal(services.auth.session.id, 8); assert.doesNotMatch(view.root.textContent, /synthetic-secret/);
    assert.equal(view.secret.value, ''); assert.equal(location.hash, '#/login');
  } finally { dom.restore(); }
});
test('hash change before detach suppresses late me success', async () => {
  const dom = installDom('/login'); try {
    let finish; const services = source({ me: () => new Promise(resolve => { finish = resolve; }) }); const view = mount(dom, services, '/assets');
    view.form.fire('submit'); await until(() => services.calls.me === 1); location.hash = '#/dashboard'; finish({ id: 7 });
    await until(() => view.secret.value === ''); assert.equal(view.root.isConnected, true); assert.equal(location.hash, '#/dashboard');
  } finally { dom.restore(); }
});
test('401,429 and stale-session errors stay generic and do not perform UI-owned logout or retry', async () => {
  const dom = installDom('/login'); try {
    for (const [status, code] of [[401, 'INVALID_CREDENTIALS'], [429, 'RATE_LIMITED'], [401, 'SESSION_CHANGED']]) {
      const services = source({ login: async () => { throw Object.assign(new Error('synthetic-secret'), { status, code }); } }); const view = mount(dom, services);
      view.form.fire('submit'); await until(() => !view.submit.disabled); assert.equal(services.calls.login.length, 1); assert.equal(services.calls.me, 0);
      assert.equal(services.calls.logout, 0); assert.equal(view.secret.value, ''); assert.doesNotMatch(view.root.textContent, /synthetic-secret/); view.root.remove();
    }
  } finally { dom.restore(); }
});
test('invalid external/control-character return paths fall back to internal dashboard', async () => {
  const dom = installDom('/login'); try {
    for (const target of ['https://example.test', '//example.test', '/\\example.test', '/assets#token', '/assets\n', null]) {
      location.hash = '#/login'; const services = source(); const view = mount(dom, services, target); view.form.fire('submit'); await until(() => !view.submit.disabled);
      assert.match(location.hash, /^#?\/dashboard$/); view.root.remove();
    }
  } finally { dom.restore(); }
});
test('actual adapter still clears unauthorized or malformed me sessions without UI-owned logout', async () => {
  const dom = installDom('/login'); try {
    for (const status of [401, 200]) {
      const calls = []; const events = []; const services = createApiServices({ fetchImpl: async url => {
        calls.push(url); if (url.endsWith('/auth/login')) return Response.json({ accessToken: 'fixture-only', tokenType: 'Bearer',
          expiresAt: new Date(Date.now() + 60000).toISOString(), user: { id: 7, roles: ['CUSTOM'], permissions: ['assets.read'] } });
        return status === 401 ? Response.json({ code: 'UNAUTHENTICATED' }, { status }) : Response.json({ id: 7, roles: [], permissions: 'invalid' });
      } });
      services.auth.onSessionInvalidated(event => events.push(event)); const view = mount(dom, services); view.form.fire('submit'); await until(() => !view.submit.disabled);
      assert.equal(services.auth.session, null); assert.equal(calls.length, 2); assert.equal(events.length, 1); assert.equal(view.secret.value, '');
      assert.equal(location.hash, '#/login'); view.root.remove(); await services.auth.logout();
    }
  } finally { dom.restore(); }
});
test('actual adapter preserves an authenticated login on transient me failure, but page does not report verification success', async () => {
  const dom = installDom('/login'); let services; try {
    services = createApiServices({ fetchImpl: async url => url.endsWith('/auth/login') ? Response.json({ accessToken: 'fixture-only', tokenType: 'Bearer',
      expiresAt: new Date(Date.now() + 60000).toISOString(), user: { id: 7, roles: ['CUSTOM'], permissions: ['assets.read'] } }) :
      Response.json({ code: 'UNAVAILABLE', detail: 'synthetic-secret' }, { status: 503 }) });
    const view = mount(dom, services); view.form.fire('submit'); await until(() => !view.submit.disabled);
    assert.equal(services.auth.session.id, 7); assert.equal(location.hash, '#/login'); assert.match(view.root.textContent, /Chưa hoàn tất xác minh/);
    assert.doesNotMatch(view.root.textContent, /synthetic-secret/); assert.equal(view.secret.value, '');
  } finally { await services?.auth.logout(); dom.restore(); }
});
test('demo role remains explicit and locked during pending synthetic login, never sent in API mode', async () => {
  const dom = installDom('/login'); try {
    let finish; const services = source({ login: () => new Promise(resolve => { finish = resolve; }) }); services.mode = 'mock';
    const view = mount(dom, services); const role = descendants(view.root).find(n => n.getAttribute('name') === 'role');
    view.form.fire('submit'); assert.equal(role.disabled, true); assert.equal(services.calls.login[0].role, 'ADMIN_IT');
    finish(); await until(() => !view.submit.disabled); assert.equal(role.disabled, false);
  } finally { dom.restore(); }
});
