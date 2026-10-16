import test from 'node:test';
import assert from 'node:assert/strict';
import { permissionPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/permissions.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const version = 'AAAAAAAAAAAAAAAAAAAAAA==';
const catalog = [{ id: 1, code: 'assets.read', module: 'assets', action: 'Xem', available: true },
  { id: 2, code: 'assets.update', module: 'assets', action: 'Sửa', available: true, requires: 'assets.read' },
  { id: 3, code: 'assignments.read', module: 'assignments', action: 'Xem', available: false }];
const group = () => ({ id: 12, name: 'Synthetic group', code: 'CUSTOM_FIXTURE', isSystem: false, isActive: true, memberCount: 2, permissions: [], rowVersion: version });
function source(handler) {
  const writes = []; let current = group(); return { mode: 'api', writes,
    auth: { session: { id: 7, roles: ['ADMIN_IT'], permissions: ['roles.assign', 'roles.permissions.read', 'roles.read'] } },
    roles: { list: async () => ({ items: [group()], totalPages: 1 }), matrix: async () => catalog,
      get: async () => structuredClone(current), replacePermissions: async (id, dto) => { writes.push(structuredClone({ id, dto }));
        const result = await handler(id, dto); current = result; return structuredClone(result); } } };
}
const findButton = (root, text) => descendants(root).find(n => n.tagName === 'BUTTON' && n.textContent === text);
const checkbox = (root, label) => descendants(root).find(n => n.tagName === 'INPUT' && n.getAttribute('aria-label') === label);
async function mount(dom, services) { const root = await permissionPage(services); dom.body.append(root); await until(() => Boolean(checkbox(root, 'Quản lý tài sản Sửa'))); return root; }
function dirty(root) { const input = checkbox(root, 'Quản lý tài sản Sửa'); input.checked = true; input.fire('change'); }
function confirm(dom, root) { findButton(root, 'Lưu phân quyền').fire('click'); findButton(dom.body, 'Xác nhận').fire('click'); }
const result = dto => ({ ...group(), permissions: dto.permissionIds.map(id => ({ id })), rowVersion: 'AQEBAQEBAQEBAQEBAQEBAQ==' });

test('matrix save snapshots one reviewed command and locks toggles until result without unlocking unavailable permissions', async () => {
  const dom = installDom('/permissions'); try {
    let finish; const services = source((_id, dto) => new Promise(resolve => { finish = () => resolve(result(dto)); }));
    const root = await mount(dom, services); dirty(root); const save = findButton(root, 'Lưu phân quyền'); confirm(dom, root);
    try {
      assert.equal(checkbox(root, 'Quản lý tài sản Sửa').disabled, true);
      save.fire('click'); assert.equal(services.writes.length, 1); assert.deepEqual(services.writes[0].dto.permissionIds, [2, 1]);
      const read = checkbox(root, 'Quản lý tài sản Xem'); read.checked = false; read.fire('change'); assert.equal(read.checked, true);
    } finally { finish(); await until(() => save.getAttribute('aria-busy') === null); }
    assert.equal(checkbox(root, 'Quản lý tài sản Sửa').disabled, false); assert.equal(checkbox(root, 'Cấp phát tài sản Xem').disabled, true);
  } finally { dom.restore(); }
});

test('late matrix success cannot toast after page detach, hash change or another actor', async () => {
  const dom = installDom('/permissions'); try {
    for (const change of ['detach', 'hash', 'actor']) {
      location.hash = '#/permissions'; let finish; const services = source((_id, dto) => new Promise(resolve => { finish = () => resolve(result(dto)); }));
      const root = await mount(dom, services); dirty(root); const save = findButton(root, 'Lưu phân quyền'); confirm(dom, root);
      if (change === 'detach') root.remove(); else if (change === 'hash') location.hash = '#/assets'; else services.auth.session.id = 8;
      finish(); await until(() => save.getAttribute('aria-busy') === null);
      assert.doesNotMatch(dom.body.textContent, /Đã lưu phân quyền/); assert.equal(services.writes.length, 1); root.remove();
    }
  } finally { dom.restore(); }
});

test('matrix conflict or uncertain result preserves draft and blocks replay until explicit reviewed read', async () => {
  const dom = installDom('/permissions'); try {
    for (const [status, code] of [[409, 'CONCURRENCY_CONFLICT'], [0, 'NETWORK_ERROR'], [401, 'SESSION_CHANGED']]) {
      const services = source(async () => { throw Object.assign(Error('synthetic-private-detail'), { status, code }); });
      const root = await mount(dom, services); dirty(root); const save = findButton(root, 'Lưu phân quyền'); confirm(dom, root);
      await until(() => save.getAttribute('aria-busy') === null); assert.equal(save.disabled, true);
      assert.equal(checkbox(root, 'Quản lý tài sản Sửa').checked, true); assert.doesNotMatch(root.textContent, /synthetic-private-detail/);
      save.fire('click'); assert.equal(services.writes.length, 1);
      findButton(root, 'Tải lại').fire('click'); findButton(dom.body, 'Xác nhận').fire('click');
      await until(() => !root.textContent.includes('Bạn có thay đổi chưa lưu.'));
      assert.equal(checkbox(root, 'Quản lý tài sản Sửa').checked, false); assert.equal(services.writes.length, 1); root.remove();
    }
  } finally { dom.restore(); }
});

test('revoked matrix permission before final confirmation prevents a write', async () => {
  const dom = installDom('/permissions'); try {
    const services = source(async (_id, dto) => result(dto)); const root = await mount(dom, services); dirty(root);
    findButton(root, 'Lưu phân quyền').fire('click'); services.auth.session.permissions = ['roles.read']; findButton(dom.body, 'Xác nhận').fire('click');
    await new Promise(resolve => setImmediate(resolve)); assert.equal(services.writes.length, 0); assert.match(root.textContent, /Phiên hoặc quyền/);
  } finally { dom.restore(); }
});

test('malformed successful save result cannot overwrite reviewed group or unlock replay', async () => {
  const dom = installDom('/permissions'); try {
    for (const invalid of [null, { ...group(), id: 99 }, { ...group(), permissions: [null] }, { ...group(), rowVersion: null }]) {
      const services = source(async () => invalid); const root = await mount(dom, services); dirty(root); const save = findButton(root, 'Lưu phân quyền'); confirm(dom, root);
      await until(() => save.getAttribute('aria-busy') === null); assert.equal(save.disabled, true);
      assert.equal(checkbox(root, 'Quản lý tài sản Sửa').checked, true); assert.match(root.textContent, /Chưa xác nhận được kết quả/);
      assert.doesNotMatch(dom.body.textContent, /Đã lưu phân quyền/); assert.equal(services.writes.length, 1); root.remove();
    }
  } finally { dom.restore(); }
});

test('failed initial group read can be explicitly retried without dereferencing a missing selection', async () => {
  const dom = installDom('/permissions'); try {
    const services = source(async (_id, dto) => result(dto)); let reads = 0;
    services.roles.get = async () => { if (++reads === 1) throw Error('synthetic-private-detail'); return group(); };
    const root = await permissionPage(services); dom.body.append(root); await until(() => root.textContent.includes('Không tải được nhóm quyền'));
    assert.doesNotMatch(root.textContent, /synthetic-private-detail/); findButton(root, 'Tải lại').fire('click');
    await until(() => Boolean(checkbox(root, 'Quản lý tài sản Sửa'))); assert.equal(reads, 2); assert.equal(services.writes.length, 0);
    assert.equal(checkbox(root, 'Quản lý tài sản Sửa').disabled, false);
  } finally { dom.restore(); }
});
