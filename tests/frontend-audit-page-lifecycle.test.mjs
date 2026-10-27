import test from 'node:test';
import assert from 'node:assert/strict';
import { auditLogPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/audit-logs.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const entry = { id: 71, occurredAt: '2026-10-16T09:00:00Z', actorUserId: 7, action: 'assets.update', outcome: 'SUCCESS',
  correlationId: '00000000-0000-0000-0000-000000000071', oldValues: { departmentId: 9 }, newValues: { departmentId: 9 } };
const page = items => ({ items, totalPages: 1, page: 1, pageSize: 20, totalItems: items.length });
function source() {
  const calls = { list: 0, get: 0, departments: 0 };
  return { calls, mode: 'api', auth: { session: { id: 7, roles: ['ADMIN_IT'], permissions: ['audit-logs.read', 'departments.read'] } },
    auditLogs: { list: async () => { calls.list++; return page([entry]); }, get: async () => { calls.get++; return entry; } },
    departments: { list: async () => { calls.departments++; return page([{ id: 9, name: 'Fixture department' }]); } } };
}
const view = root => descendants(root).find(node => node.tagName === 'BUTTON' && node.textContent === 'Xem');
const dialogs = dom => descendants(dom.body).filter(node => node.tagName === 'DIALOG');

test('audit list rejects a changed actor or revoked Admin/read role after the response', async () => {
  const dom = installDom('/audit-logs'); try {
    for (const change of ['actor', 'role', 'grant']) {
      let finish; const services = source(); services.auditLogs.list = () => new Promise(resolve => { finish = resolve; });
      const loading = auditLogPage(services);
      if (change === 'actor') services.auth.session.id = 8;
      if (change === 'role') services.auth.session.roles = [];
      if (change === 'grant') services.auth.session.permissions = [];
      const rejection = assert.rejects(loading, error => [401, 403].includes(error.status));
      finish(page([entry])); await rejection;
      assert.equal(services.calls.departments, 0);
    }
  } finally { dom.restore(); }
});

test('optional department lookup failure does not hide the authorized audit list', async () => {
  const dom = installDom('/audit-logs'); try {
    const services = source(); services.departments.list = async () => { throw Object.assign(Error('private-detail'), { status: 403 }); };
    const root = await auditLogPage(services); dom.body.append(root);
    assert.match(root.textContent, /assets.update/);
    view(root).fire('click'); await until(() => dialogs(dom).length === 1);
    assert.match(dialogs(dom)[0].textContent, /Phòng ban không còn khả dụng/);
    assert.doesNotMatch(dom.body.textContent, /private-detail/);
  } finally { dom.restore(); }
});

test('audit detail refuses stale controls and suppresses late response after navigation, detach, actor or grant change', async () => {
  const dom = installDom('/audit-logs'); try {
    for (const change of ['hash', 'detach', 'actor', 'role', 'grant']) {
      location.hash = '#/audit-logs'; let finish; const services = source(); services.auditLogs.get = () => { services.calls.get++; return new Promise(resolve => { finish = resolve; }); };
      const root = await auditLogPage(services); dom.body.append(root); const control = view(root); control.fire('click');
      assert.equal(services.calls.get, 1);
      if (change === 'hash') location.hash = '#/assets';
      if (change === 'detach') root.remove();
      if (change === 'actor') services.auth.session.id = 8;
      if (change === 'role') services.auth.session.roles = [];
      if (change === 'grant') services.auth.session.permissions = [];
      finish({ ...entry, action: 'fixture-private-late-action' }); await until(() => control.getAttribute('aria-busy') === null);
      assert.equal(dialogs(dom).length, 0); assert.doesNotMatch(dom.body.textContent, /fixture-private-late-action/); root.remove();
    }
    location.hash = '#/audit-logs';
    const services = source(); const root = await auditLogPage(services); dom.body.append(root);
    services.auth.session.roles = []; view(root).fire('click'); assert.equal(services.calls.get, 0);
  } finally { dom.restore(); }
});

test('audit detail is single-flight and rejects a mismatched response identity', async () => {
  const dom = installDom('/audit-logs'); try {
    let finish; const services = source(); services.auditLogs.get = () => { services.calls.get++; return new Promise(resolve => { finish = resolve; }); };
    const root = await auditLogPage(services); dom.body.append(root); const control = view(root);
    control.fire('click'); control.fire('click'); assert.equal(services.calls.get, 1);
    finish({ ...entry, id: 72 }); await until(() => control.getAttribute('aria-busy') === null);
    assert.equal(dialogs(dom).length, 0);
  } finally { dom.restore(); }
});

test('stale 401 does not replace a newer login; current failures show only fixed feedback', async () => {
  const dom = installDom('/audit-logs'); try {
    for (const kind of ['stale401', 'server500']) {
      let finish, navigations = 0; const services = source(); window.dispatchEvent = () => { navigations++; };
      services.auditLogs.get = () => new Promise((_resolve, reject) => { finish = reject; });
      const root = await auditLogPage(services); dom.body.append(root); const control = view(root); control.fire('click');
      if (kind === 'stale401') services.auth.session = { id: 8, roles: ['ADMIN_IT'], permissions: ['audit-logs.read'] };
      finish(Object.assign(Error('fixture-sensitive-error'), { status: kind === 'stale401' ? 401 : 500, code: kind === 'stale401' ? 'SESSION_CHANGED' : 'API_ERROR' }));
      await until(() => control.getAttribute('aria-busy') === null);
      assert.equal(location.hash, '#/audit-logs'); assert.equal(dialogs(dom).length, 0);
      assert.doesNotMatch(dom.body.textContent, /fixture-sensitive-error/);
      if (kind === 'server500') {
        assert.equal(navigations, 0);
        assert.match(dom.body.textContent, /Không thể tải chi tiết nhật ký/);
      }
      root.remove();
    }
  } finally { dom.restore(); }
});

test('an expired current audit session routes to login after its detail request returns 401', async () => {
  const dom = installDom('/audit-logs'); try {
    let fail; const services = source(); services.auditLogs.get = () => new Promise((_resolve, reject) => { fail = reject; });
    const root = await auditLogPage(services); dom.body.append(root); const control = view(root); control.fire('click');
    services.auth.session = null;
    fail(Object.assign(Error('expired-fixture'), { status: 401 }));
    await until(() => control.getAttribute('aria-busy') === null);
    assert.equal(location.hash, '/login'); assert.equal(dialogs(dom).length, 0);
    assert.doesNotMatch(dom.body.textContent, /expired-fixture/);
  } finally { dom.restore(); }
});


test('audit displays friendly actor/action as text and keeps action code and category', async () => {
  const dom = installDom('/audit-logs'); try {
    const services = source(); let query;
    const item = { ...entry, actorFullName: '<b>Admin</b>', actionDescription: 'Cập nhật tài sản' };
    services.auditLogs.list = async value => { query = value; return page([item]); };
    services.auditLogs.get = async () => item;
    const root = await auditLogPage(services); dom.body.append(root);
    assert.equal(query.category, 'business');
    assert.match(root.textContent, /<b>Admin<\/b>/);
    assert.match(root.textContent, /Cập nhật tài sản/);
    assert.match(root.textContent, /assets.update/);
    assert.equal(descendants(root).some(node => node.tagName === 'B'), false);
    view(root).fire('click'); await until(() => dialogs(dom).length === 1);
    assert.match(dialogs(dom)[0].textContent, /Cập nhật tài sản/);
    assert.match(dialogs(dom)[0].textContent, /assets.update/);
  } finally { dom.restore(); }
});
