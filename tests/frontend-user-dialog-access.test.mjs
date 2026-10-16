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
