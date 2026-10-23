import test from 'node:test';
import assert from 'node:assert/strict';
import { userPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/users.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const version = 'AAAAAAAAAAAAAAAAAAAAAA==';
const user = { id: 21, displayName: 'Synthetic staff', username: 'fixture.staff', email: 'fixture@example.test', isActive: true, isEmailConfirmed: true, rowVersion: version };
const state = { id: 21, isActive: true, isAdminLocked: false, roleIds: [7], rowVersion: version, effectivePermissions: ['assets.read'] };
const page = items => ({ items, totalPages: 1, page: 1, pageSize: 100, totalItems: items.length });
const grants = ['users.read', 'users.create', 'users.update', 'users.status.manage', 'roles.assign', 'roles.read', 'departments.read'];
function source() {
  const calls = { get: 0, account: 0, roles: 0, writes: 0 }; return { mode: 'api', calls, auth: { session: { id: 7, permissions: [...grants] } },
    departments: { list: async () => page([]) }, roles: { list: async () => { calls.roles++; return page([{ id: 7, name: 'Fixture role', isActive: true }]); } },
    users: { list: async () => page([user]), get: async () => { calls.get++; return user; }, account: async () => { calls.account++; return state; },
      update: async () => { calls.writes++; return user; }, create: async () => { calls.writes++; return user; },
      setStatus: async () => { calls.writes++; return { user, warnings: [] }; }, replaceRoles: async () => { calls.writes++; return { user, warnings: [] }; } } };
}
const button = (root, name) => descendants(root).find(n => n.tagName === 'BUTTON' && n.textContent === name);
const dialogs = dom => descendants(dom.body).filter(n => n.tagName === 'DIALOG');

test('late user detail cannot open or render private detail after navigation, detach, actor or read-grant change', async () => {
  const dom = installDom('/users'); try {
    for (const change of ['hash', 'detach', 'actor', 'revoke']) {
      location.hash = '#/users'; let finish; const services = source(); services.users.get = () => new Promise(resolve => { finish = resolve; });
      const root = await userPage(services); dom.body.append(root); button(root, 'Xem').fire('click');
      if (change === 'hash') location.hash = '#/assets'; else if (change === 'detach') root.remove();
      else if (change === 'actor') services.auth.session.id = 8; else services.auth.session.permissions = [];
      finish({ ...user, phone: 'private-detail-fixture' }); await new Promise(resolve => setImmediate(resolve));
      assert.equal(dialogs(dom).length, 0); assert.doesNotMatch(dom.body.textContent, /private-detail-fixture/); root.remove();
    }
  } finally { dom.restore(); }
});

test('stale action controls cannot fetch privileged confirmation data after the required grant is revoked', async () => {
  const dom = installDom('/users'); try {
    for (const [action, permission] of [['Xem', 'users.read'], ['Sửa', 'users.update'], ['Trạng thái', 'users.status.manage'], ['Vai trò', 'roles.assign']]) {
      const services = source(); const root = await userPage(services); dom.body.append(root); const rolesBefore = services.calls.roles;
      services.auth.session.permissions = grants.filter(p => p !== permission); button(root, action).fire('click');
      await new Promise(resolve => setImmediate(resolve)); assert.equal(services.calls.get, 0); assert.equal(services.calls.account, 0);
      assert.equal(services.calls.roles, rolesBefore); assert.equal(dialogs(dom).length, 0); root.remove();
    }
  } finally { dom.restore(); }
});

test('user list initial result is rejected if its actor changes before mount', async () => {
  const dom = installDom('/users'); try {
    let finish; const services = source(); services.users.list = () => new Promise(resolve => { finish = resolve; });
    const pending = userPage(services); const rejected = assert.rejects(pending, { code: 'SESSION_CHANGED' });
    services.auth.session.id = 8; finish(page([user])); await rejected;
  } finally { dom.restore(); }
});

test('open user edit/status/membership dialogs recheck grants before write, not merely when displaying the button', async () => {
  const dom = installDom('/users'); try {
    for (const [action, permission, confirm] of [['Sửa', 'users.update', 'Lưu hồ sơ'], ['Trạng thái', 'users.status.manage', 'Xác nhận trạng thái'], ['Vai trò', 'roles.assign', 'Lưu vai trò']]) {
      const services = source(); const root = await userPage(services); dom.body.append(root); button(root, action).fire('click'); await until(() => dialogs(dom).length === 1);
      const reason = descendants(dom.body).find(n => n.getAttribute('name') === 'reason'); if (reason) reason.value = 'Fixture reason';
      services.auth.session.permissions = grants.filter(p => p !== permission);
      if (action === 'Sửa') descendants(dialogs(dom)[0]).find(n => n.tagName === 'FORM').fire('submit'); else button(dom.body, confirm).fire('click');
      await new Promise(resolve => setImmediate(resolve)); assert.equal(services.calls.writes, 0); assert.match(dom.body.textContent, /Phiên hoặc quyền/);
      dialogs(dom).forEach(n => n.remove()); root.remove();
    }
  } finally { dom.restore(); }
});

test('invitation resend checks the current grant and single-flights without late navigation feedback or real email', async () => {
  const dom = installDom('/users'); try {
    let finish, refreshes = 0; const services = source(); window.dispatchEvent = () => { refreshes++; };
    services.users.list = async () => page([{ ...user, isEmailConfirmed: false }]);
    services.users.resendInvitation = async () => { services.calls.writes++; return new Promise(resolve => { finish = resolve; }); };
    const root = await userPage(services); dom.body.append(root); const resend = button(root, 'Gửi lại lời mời');
    services.auth.session.permissions = ['users.read']; resend.fire('click'); assert.equal(services.calls.writes, 0);
    services.auth.session.permissions = [...grants]; resend.fire('click'); resend.fire('click'); assert.equal(services.calls.writes, 1);
    location.hash = '#/assets'; finish({ emailStatus: 'Sent' }); await until(() => resend.getAttribute('aria-busy') === null);
    assert.equal(refreshes, 0); assert.doesNotMatch(dom.body.textContent, /Email đã được dịch vụ tiếp nhận/);
  } finally { dom.restore(); }
});

const scenarios = [['Sửa', 'Lưu hồ sơ', 'update'], ['Trạng thái', 'Xác nhận trạng thái', 'setStatus'], ['Vai trò', 'Lưu vai trò', 'replaceRoles']];
async function mountCommand(dom, services, action) {
  const root = await userPage(services); dom.body.append(root); button(root, action).fire('click'); await until(() => dialogs(dom).length === 1);
  const reason = descendants(dom.body).find(n => n.getAttribute('name') === 'reason'); if (reason) reason.value = 'Fixture reason'; return root;
}
function sendCommand(dom, action, confirm) {
  if (action === 'Sửa') descendants(dialogs(dom)[0]).find(n => n.tagName === 'FORM').fire('submit'); else button(dom.body, confirm).fire('click');
}

test('user commands lock captured inputs, single-flight, and release detached controls after pending completion', async () => {
  const dom = installDom('/users'); try {
    for (const [action, label, method] of scenarios) {
      let finish; const calls = []; const services = source(); services.users[method] = async (...args) => { calls.push(structuredClone(args)); return new Promise(resolve => { finish = resolve; }); };
      const root = await mountCommand(dom, services, action); const confirm = button(dom.body, label); const modal = dialogs(dom)[0];
      const inputs = descendants(modal).filter(n => ['INPUT', 'SELECT', 'TEXTAREA'].includes(n.tagName)); sendCommand(dom, action, label);
      try {
        for (const input of inputs) assert.equal(input.tagName === 'SELECT' || input.getAttribute('type') === 'checkbox' ? input.disabled : input.readOnly, true);
        sendCommand(dom, action, label);
        assert.equal(calls.length, 1); assert.equal(calls[0][0], 21); assert.equal(calls[0][1].rowVersion, version);
      } finally { modal.remove(); finish(method === 'update' ? user : { user, warnings: [] }); await until(() => confirm.getAttribute('aria-busy') === null); }
      for (const input of inputs) assert.equal(input.tagName === 'SELECT' || input.getAttribute('type') === 'checkbox' ? input.disabled : input.readOnly, false);
      assert.doesNotMatch(dom.body.textContent, /Đã cập nhật/); root.remove();
    }
  } finally { dom.restore(); }
});

test('user unknown outcomes and version conflicts keep dialog draft but block blind write replay', async () => {
  const dom = installDom('/users'); try {
    for (const [action, label, method] of scenarios) for (const [status, code] of [[0, 'NETWORK_ERROR'], [409, 'CONCURRENCY_CONFLICT'], [401, 'SESSION_CHANGED']]) {
      const services = source(); services.users[method] = async () => { services.calls.writes++; throw Object.assign(Error('synthetic-private-detail'), { status, code }); };
      const root = await mountCommand(dom, services, action); const confirm = button(dom.body, label); sendCommand(dom, action, label);
      await until(() => confirm.getAttribute('aria-busy') === null); assert.equal(confirm.disabled, true);
      assert.doesNotMatch(dom.body.textContent, /synthetic-private-detail/); sendCommand(dom, action, label); assert.equal(services.calls.writes, 1);
      assert.ok(button(dom.body, 'Đóng và tải lại danh sách người dùng')); dialogs(dom)[0].remove(); root.remove();
    }
  } finally { dom.restore(); }
});

test('malformed successful user-command responses cannot show success or allow replay', async () => {
  const dom = installDom('/users'); try {
    for (const [action, label, method] of scenarios) {
      const services = source(); services.users[method] = async () => { services.calls.writes++; return method === 'update' ? { ...user, id: 99 } : { user: { ...user, id: 99 }, warnings: [] }; };
      const root = await mountCommand(dom, services, action); const confirm = button(dom.body, label); sendCommand(dom, action, label);
      await until(() => confirm.getAttribute('aria-busy') === null); assert.equal(confirm.disabled, true);
      assert.doesNotMatch(dom.body.textContent, /Đã cập nhật/); assert.equal(services.calls.writes, 1); dialogs(dom)[0].remove(); root.remove();
    }
  } finally { dom.restore(); }
});

test('known user validation remains editable, while self-update still returns to Login after adapter clears session', async () => {
  const dom = installDom('/users'); try {
    const services = source(); services.users.update = async () => { throw Object.assign(Error('synthetic-private-detail'), { status: 400, code: 'VALIDATION_ERROR', errors: { displayName: ['Fixture validation'] } }); };
    const root = await mountCommand(dom, services, 'Sửa'); const confirm = button(dom.body, 'Lưu hồ sơ'); sendCommand(dom, 'Sửa', 'Lưu hồ sơ');
    await until(() => confirm.getAttribute('aria-busy') === null); assert.equal(confirm.disabled, false); assert.match(dom.body.textContent, /Fixture validation/);
    assert.doesNotMatch(dom.body.textContent, /synthetic-private-detail/); dialogs(dom)[0].remove(); root.remove();
    const self = source(); self.users.list = async () => page([{ ...user, id: 7 }]); self.users.get = async () => ({ ...user, id: 7 });
    self.users.update = async () => { self.auth.session = null; return { ...user, id: 7 }; };
    await mountCommand(dom, self, 'Sửa'); sendCommand(dom, 'Sửa', 'Lưu hồ sơ'); await until(() => /\/?login$/.test(location.hash));
    assert.equal(self.auth.session, null);
  } finally { dom.restore(); }
});

test('user creation captures selected roles without a password, and handles pending invitation outcomes without sending email', async () => {
  const dom = installDom('/users'); try {
    for (const outcome of ['known', 'unknown', 'invalid']) {
      const calls = []; let finish, fail; const services = source(); services.users.create = async dto => {
        calls.push(structuredClone(dto)); return new Promise((resolve, reject) => { finish = resolve; fail = reject; });
      };
      const root = await userPage(services); dom.body.append(root); button(root, 'Thêm người dùng').fire('click'); await until(() => dialogs(dom).length === 1);
      const modal = dialogs(dom)[0]; const controls = descendants(modal); const input = name => controls.find(n => n.getAttribute('name') === name);
      input('displayName').value = 'Fixture new staff'; input('username').value = 'fixture.new'; input('email').value = 'new@example.test'; input('roleIds').checked = true;
      const form = controls.find(n => n.tagName === 'FORM'); const confirm = button(modal, 'Lưu hồ sơ'); form.fire('submit'); form.fire('submit');
      assert.equal(calls.length, 1); assert.equal(input('roleIds').disabled, true); assert.equal(input('displayName').readOnly, true);
      assert.deepEqual(calls[0], { displayName: 'Fixture new staff', username: 'fixture.new', email: 'new@example.test', employeeCode: null, departmentId: null, phone: null, roleIds: [7] });
      assert.ok(!controls.some(n => n.getAttribute('type') === 'password'));
      if (outcome === 'unknown') fail(Object.assign(Error('synthetic-private-detail'), { status: 0 }));
      else finish(outcome === 'invalid' ? null : { ...user, isEmailConfirmed: false, invitationEmailStatus: 'NotConfigured' });
      await until(() => confirm.getAttribute('aria-busy') === null);
      if (outcome === 'known') { assert.match(dom.body.textContent, /Chưa cấu hình dịch vụ email/); assert.equal(dialogs(dom).length, 0); }
      else { assert.equal(confirm.disabled, true); assert.equal(input('displayName').value, 'Fixture new staff'); form.fire('submit'); assert.equal(calls.length, 1); modal.remove(); }
      assert.doesNotMatch(dom.body.textContent, /synthetic-private-detail/); descendants(dom.body).filter(n => n.classList.contains('toast-container')).forEach(n => n.remove()); root.remove();
    }
  } finally { dom.restore(); }
});
