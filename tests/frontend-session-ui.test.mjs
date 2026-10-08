import test from 'node:test';
import assert from 'node:assert/strict';

// Minimal DOM for exercising the real app's navigation and page-level catches.
// This checks behavior, not source strings, and makes no browser/network/DB calls.
class TestNode {
  constructor(tag = '#text', text = '') {
    this.tagName = tag.toUpperCase(); this.data = text; this.childNodes = [];
    this.attributes = new Map(); this.handlers = new Map(); this.dataset = {};
    this.className = ''; this.value = ''; this.disabled = false;
    this.style = { setProperty() {} };
    this.classList = {
      contains: name => this.className.split(/\s+/).includes(name),
      toggle: (name, force) => {
        const names = new Set(this.className.split(/\s+/).filter(Boolean));
        const add = force ?? !names.has(name);
        if (add) names.add(name); else names.delete(name);
        this.className = [...names].join(' '); return add;
      },
    };
  }
  get children() { return this.childNodes.filter(node => node.tagName !== '#TEXT'); }
  get isConnected() { let node = this; while (node.parentNode) node = node.parentNode; return node.tagName === 'BODY'; }
  get textContent() { return this.tagName === '#TEXT' ? this.data : this.childNodes.map(node => node.textContent).join(''); }
  set textContent(value) { this.replaceChildren(new TestNode('#text', String(value))); }
  append(...nodes) { for (const node of nodes) { node.remove(); node.parentNode = this; this.childNodes.push(node); } }
  replaceChildren(...nodes) { for (const node of this.childNodes) node.parentNode = null; this.childNodes = []; this.append(...nodes); }
  remove() { if (this.parentNode) { this.parentNode.childNodes = this.parentNode.childNodes.filter(node => node !== this); this.parentNode = null; } }
  setAttribute(name, value) { this.attributes.set(name, String(value)); if (name === 'class') this.className = String(value); }
  getAttribute(name) { return this.attributes.get(name) ?? null; }
  removeAttribute(name) { this.attributes.delete(name); }
  addEventListener(name, callback) { const callbacks = this.handlers.get(name) ?? []; callbacks.push(callback); this.handlers.set(name, callbacks); }
  fire(name) { for (const callback of this.handlers.get(name) ?? []) callback({ preventDefault() {}, target: this }); }
  focus() {}
  close() {}
  showModal() {}
  querySelectorAll(selector) {
    const [tag, className] = selector.split('.');
    const matches = node => (!tag || node.tagName.toLowerCase() === tag) && (!className || node.classList.contains(className));
    return descendants(this).filter(matches);
  }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
}
function descendants(node) { return node.childNodes.flatMap(child => [child, ...descendants(child)]); }
async function until(predicate) {
  const end = Date.now() + 2000;
  while (!predicate()) {
    if (Date.now() >= end) assert.fail('App state did not settle as expected.');
    await new Promise(resolve => setTimeout(resolve, 5));
  }
}
const catalog = { items: [{ id: 1, code: 'ACTIVE', name: 'Active catalog', isActive: true }], page: 1, pageSize: 100, totalItems: 1, totalPages: 1 };
const asset = { id: 1, assetCode: 'SYNTHETIC-UI-ASSET', name: 'Synthetic asset', assetTypeId: 1, owningDepartmentId: 1, assetType: catalog.items[0], owningDepartment: catalog.items[0], status: 'InStock', rowVersion: 'AAAAAAAAAAAAAAAAAAAAAA==' };
const assetPage = { items: [asset], page: 1, pageSize: 10, totalItems: 1, totalPages: 1 };
const user = { id: 41, username: 'synthetic.staff', email: 'synthetic.staff@example.test', displayName: 'Synthetic staff', isActive: true, rowVersion: 'AAAAAAAAAAAAAAAAAAAAAA==' };
const account = { id: 41, isActive: true, isAdminLocked: false, roleIds: [], rowVersion: user.rowVersion };
const userList = { items: [user], page: 1, pageSize: 10, totalItems: 1, totalPages: 1 };

test('M1 app returns to Login on session invalidation even when pages consume errors', async t => {
  const previous = Object.fromEntries(['Node', 'document', 'window', 'location', 'fetch'].map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
  const app = new TestNode('div'); app.setAttribute('id', 'app');
  const body = new TestNode('body'); body.append(app);
  const document = {
    body, createElement: tag => new TestNode(tag), createElementNS: (_namespace, tag) => new TestNode(tag), createTextNode: text => new TestNode('#text', text),
    getElementById: id => [body, ...descendants(body)].find(node => node.getAttribute('id') === id) ?? null,
    querySelector: selector => body.querySelector(selector), querySelectorAll: selector => body.querySelectorAll(selector),
  };
  const window = new EventTarget();
  let hash = '#/login';
  const location = { hostname: 'localhost', search: '', get hash() { return hash; }, set hash(value) {
    const normalized = value && !value.startsWith('#') ? '#' + value : value;
    if (hash === normalized) return;
    hash = normalized; queueMicrotask(() => window.dispatchEvent(new Event('hashchange')));
  } };
  Object.assign(window, { location, innerWidth: 1280, matchMedia: () => ({ matches: false }), scrollTo() {}, setTimeout: (callback, delay) => { const timer = setTimeout(callback, delay); timer.unref?.(); return timer; } });
  let handler = () => Response.json(asset);
  let expiryMs = 60000;
  let loginId = 0;
  const calls = [];
  Object.assign(globalThis, { Node: TestNode, document, window, location, fetch: async (url, options) => {
    calls.push({ url, options });
    if (url.endsWith('/auth/login')) return Response.json({ accessToken: 'synthetic-ui-token', tokenType: 'Bearer', expiresAt: new Date(Date.now() + expiryMs).toISOString(), user: { id: ++loginId, displayName: 'Synthetic user ' + loginId, roles: ['ADMIN_IT'], permissions: ['assets.read', 'assets.create', 'assets.update', 'assets.cost.read', 'users.read'] } });
    if (url.startsWith('/api/v1/departments') || url.startsWith('/api/v1/asset-types')) return Response.json(catalog);
    return handler(url, options);
  } });
  const { default: services } = await import('../src/ItAssetManagement.Api/wwwroot/js/services/index.js');
  try {
    await import('../src/ItAssetManagement.Api/wwwroot/js/app.js');
    async function start(path, nextHandler, expiry = 60000) {
      await services.auth.logout();
      handler = nextHandler; expiryMs = expiry;
      location.hash = '#/login';
      await services.auth.login({ email: 'ui@example.test', password: 'synthetic-test-password' });
      location.hash = '#' + path;
    }
    const isLogin = () => app.querySelector('h1')?.textContent === 'Chào mừng bạn trở lại';

    await t.test('detail-fetch 401 is not swallowed by list allSettled into a stale authenticated page', async () => {
      await start('/assets?keyword=synthetic', url => url.startsWith('/api/v1/assets?')
        ? Response.json(assetPage) : Response.json({ code: 'UNAUTHENTICATED', detail: 'Session expired' }, { status: 401 }));
      await until(() => isLogin() && services.auth.session === null);
      assert.equal(location.hash, '#/assets?keyword=synthetic');
      assert.equal(app.textContent.includes(asset.assetCode), false);
      assert.equal(app.textContent.includes('Đăng xuất'), false);
    });

    for (const mode of ['create', 'edit']) {
      await t.test(mode + ' submit 401 returns to Login rather than an inline-only stale form', async () => {
        await start(mode === 'create' ? '/assets/new' : '/assets/1/edit', (_url, options) => options.method === 'GET'
          ? Response.json(asset) : Response.json({ code: 'UNAUTHENTICATED', detail: 'Session expired' }, { status: 401 }));
        await until(() => app.querySelector('h1')?.textContent === (mode === 'create' ? 'Thêm tài sản' : 'Chỉnh sửa tài sản'));
        for (const [name, value] of Object.entries({ assetCode: asset.assetCode, name: asset.name, assetTypeId: '1', owningDepartmentId: '1' }))
          document.getElementById('field-' + name).value = value;
        const form = descendants(app).find(node => node.tagName === 'FORM' && node.querySelectorAll('input').some(input => input.getAttribute('name') === 'assetCode'));
        assert.ok(form); form.fire('submit');
        await until(() => isLogin() && services.auth.session === null);
        assert.equal(app.textContent.includes('Đăng xuất'), false);
        assert.equal(document.getElementById('field-assetCode'), null);
        assert.ok(calls.some(call => call.options.method === (mode === 'create' ? 'POST' : 'PUT')));
      });
    }

    await t.test('idle expiry clears the authenticated shell without a further API call', async () => {
      await start('/assets/new', () => Response.json(asset), 150);
      await until(() => app.querySelector('h1')?.textContent === 'Thêm tài sản');
      const count = calls.length;
      await until(() => isLogin() && services.auth.session === null);
      assert.equal(calls.length, count);
      assert.equal(location.hash, '#/assets/new');
      assert.equal(app.textContent.includes('Đăng xuất'), false);
    });

    await t.test('stale 401 from an older page does not logout the newer valid session', async () => {
      let release;
      let reads = 0;
      await start('/assets/1', () => ++reads === 1 ? new Promise(resolve => { release = resolve; }) : Response.json(asset));
      await until(() => Boolean(release));
      await services.auth.login({ email: 'new-ui@example.test', password: 'synthetic-test-password' });
      const expectedId = services.auth.session.id;
      release(Response.json({ code: 'UNAUTHENTICATED' }, { status: 401 }));
      await until(() => app.querySelector('h1')?.textContent === asset.name);
      assert.equal(services.auth.session.id, expectedId);
      assert.equal(isLogin(), false);
      assert.ok(app.textContent.includes('Synthetic user ' + expectedId));
    });

    for (const lateFailure of ['401', 'network']) {
      await t.test('older Login form delayed me ' + lateFailure + ' does not logout a newer successful Login form', async () => {
        await services.auth.logout();
        let releaseOld;
        let rejectOld;
        let meReads = 0;
        handler = url => {
          if (url === '/api/v1/auth/me') return ++meReads === 1
            ? new Promise((resolve, reject) => { releaseOld = resolve; rejectOld = reject; })
            : Response.json(services.auth.session);
          return Response.json(assetPage);
        };
        expiryMs = 60000;
        location.hash = '#/dashboard';
        window.dispatchEvent(new Event('frontend-refresh'));
        await until(isLogin);
        const submitLogin = () => {
          document.getElementById('field-email').value = 'concurrent@example.test';
          document.getElementById('field-password').value = 'synthetic-test-password';
          const form = descendants(app).find(node => node.tagName === 'FORM' && node.classList.contains('login-form'));
          assert.ok(form); form.fire('submit'); return form;
        };
        const oldForm = submitLogin();
        const oldPassword = document.getElementById('field-password');
        await until(() => Boolean(releaseOld));
        // A new Login view is a separate submit, while the old form waits for Me.
        location.hash = '#/login';
        await until(() => isLogin() && !descendants(app).includes(oldForm));
        const newForm = submitLogin();
        assert.notEqual(newForm, oldForm);
        await until(() => app.querySelector('h1')?.textContent === 'Tổng quan hệ thống');
        const expectedId = services.auth.session.id;
        if (lateFailure === '401') releaseOld(Response.json({ code: 'UNAUTHENTICATED' }, { status: 401 }));
        else rejectOld(new Error('old connection failed'));
        await until(() => oldPassword.value === '');
        assert.equal(services.auth.session.id, expectedId);
        assert.equal(app.querySelector('h1')?.textContent, 'Tổng quan hệ thống');
        assert.ok(app.textContent.includes('Synthetic user ' + expectedId));
        assert.equal(isLogin(), false);
      });
    }

    const viewUser = () => {
      const button = descendants(app).find(node => node.tagName === 'BUTTON' && node.textContent === 'Xem');
      assert.ok(button); button.fire('click');
    };
    const clearToasts = () => { for (const container of body.querySelectorAll('.toast-container')) container.remove(); };
    for (const endpoint of ['get', 'account']) {
      for (const scenario of ['detached-403', 'detached-stale-401', 'current-stale-401']) {
        await t.test('delayed user ' + endpoint + ' callback ignores ' + scenario + ' without redirecting the current view', async () => {
          clearToasts();
          let release;
          const delayedPath = endpoint === 'get' ? '/api/v1/users/41' : '/api/v1/users/41/account';
          await start('/users', url => {
            if (url.startsWith('/api/v1/users?')) return Response.json(userList);
            if (url === delayedPath) return new Promise(resolve => { release = resolve; });
            return Response.json(url.endsWith('/account') ? account : user);
          });
          await until(() => app.querySelector('h1')?.textContent === 'Người dùng');
          viewUser();
          await until(() => Boolean(release));
          let expectedId = services.auth.session.id;
          if (scenario !== 'detached-403') {
            await services.auth.login({ email: 'new-user-ui@example.test', password: 'synthetic-test-password' });
            expectedId = services.auth.session.id;
          }
          if (scenario.startsWith('detached')) {
            location.hash = '#/assets/new';
            await until(() => app.querySelector('h1')?.textContent === 'Thêm tài sản');
          }
          release(Response.json({ code: scenario === 'detached-403' ? 'FORBIDDEN' : 'UNAUTHENTICATED', detail: 'Synthetic old user error' }, { status: scenario === 'detached-403' ? 403 : 401 }));
          await new Promise(resolve => setTimeout(resolve, 20));
          assert.equal(services.auth.session.id, expectedId);
          assert.equal(location.hash, scenario.startsWith('detached') ? '#/assets/new' : '#/users');
          assert.equal(app.querySelector('h1')?.textContent, scenario.startsWith('detached') ? 'Thêm tài sản' : 'Người dùng');
          assert.equal(body.textContent.includes('Synthetic old user error'), false);
          assert.equal(body.querySelectorAll('dialog.dialog').length, 0);
        });
      }
    }

    for (const [status, code, expectedMessage] of [[403, 'FORBIDDEN', 'Synthetic current denial'], [409, 'CONCURRENCY_CONFLICT', 'Dữ liệu đã thay đổi.']]) {
      await t.test('current user callback preserves ' + status + ' feedback without clearing the valid session', async () => {
        clearToasts();
        await start('/users', url => url.startsWith('/api/v1/users?') ? Response.json(userList)
          : url.endsWith('/account') ? Response.json(account) : Response.json({ code, detail: 'Synthetic current denial' }, { status }));
        await until(() => app.querySelector('h1')?.textContent === 'Người dùng');
        const expectedId = services.auth.session.id;
        viewUser();
        await until(() => body.textContent.includes(expectedMessage));
        assert.equal(services.auth.session.id, expectedId);
        assert.equal(location.hash, '#/users');
      });
    }

    await t.test('genuine current user GET401 still clears the session and returns to Login', async () => {
      clearToasts();
      await start('/users', url => url.startsWith('/api/v1/users?') ? Response.json(userList)
        : url.endsWith('/account') ? Response.json(account) : Response.json({ code: 'UNAUTHENTICATED', detail: 'Current session expired' }, { status: 401 }));
      await until(() => app.querySelector('h1')?.textContent === 'Người dùng');
      viewUser();
      await until(() => isLogin() && services.auth.session === null);
      assert.equal(app.textContent.includes('Đăng xuất'), false);
    });
  } finally {
    await services.auth.logout();
    for (const [key, descriptor] of Object.entries(previous)) {
      if (descriptor) Object.defineProperty(globalThis, key, descriptor); else delete globalThis[key];
    }
  }
});
