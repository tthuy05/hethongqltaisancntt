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
    for (const invalid of [null, { ...group(), id: 99 }, { ...group(), permissions: [null] }, { ...group(), rowVersion: null }, group(),
      { ...group(), permissions: [{ id: 2 }, { id: 2 }] }]) {
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

test('group status snapshots target/version and locks competing commands until a valid reviewed result', async () => {
  const dom = installDom('/permissions'); try {
    let finish; const calls = []; const services = source(async (_id, dto) => result(dto));
    services.roles.setStatus = async (id, dto) => { calls.push(structuredClone({ id, dto })); return new Promise(resolve => { finish = () => resolve({ ...group(), isActive: false }); }); };
    const root = await mount(dom, services); dirty(root); const status = findButton(root, 'Ngừng sử dụng nhóm'); status.fire('click');
    assert.match(dom.body.textContent, /Bỏ thay đổi ma trận/); findButton(dom.body, 'Xác nhận').fire('click');
    try {
      assert.equal(status.disabled, true); assert.equal(checkbox(root, 'Quản lý tài sản Sửa').disabled, true);
      status.fire('click'); assert.equal(calls.length, 1);
      assert.deepEqual(calls[0], { id: 12, dto: { status: 'Inactive', reason: 'Admin thay đổi nhóm quyền', rowVersion: version } });
    } finally { finish(); await until(() => status.getAttribute('aria-busy') === null); }
    assert.equal(status.textContent, 'Kích hoạt nhóm'); assert.equal(status.disabled, false);
    assert.equal(checkbox(root, 'Quản lý tài sản Sửa').checked, false); assert.equal(checkbox(root, 'Quản lý tài sản Sửa').disabled, true);
  } finally { dom.restore(); }
});

test('group status conflict, unknown outcome or malformed result preserves draft and requires a fresh read', async () => {
  const dom = installDom('/permissions'); try {
    for (const outcome of ['ROLE_IN_USE', 'CONCURRENCY_CONFLICT', 'NETWORK_ERROR', 'invalid', 'mismatched']) {
      let calls = 0; const services = source(async (_id, dto) => result(dto));
      services.roles.setStatus = async () => { calls++; if (outcome === 'invalid') return null; if (outcome === 'mismatched') return group();
        throw Object.assign(Error('synthetic-private-detail'), { status: outcome === 'NETWORK_ERROR' ? 0 : 409, code: outcome }); };
      const root = await mount(dom, services); dirty(root); const status = findButton(root, 'Ngừng sử dụng nhóm'); status.fire('click'); findButton(dom.body, 'Xác nhận').fire('click');
      await until(() => status.getAttribute('aria-busy') === null);
      assert.equal(status.disabled, true); assert.equal(checkbox(root, 'Quản lý tài sản Sửa').checked, true);
      assert.doesNotMatch(root.textContent, /synthetic-private-detail/); status.fire('click'); assert.equal(calls, 1);
      if (outcome === 'ROLE_IN_USE') assert.match(root.textContent, /còn người dùng/);
      findButton(root, 'Tải lại').fire('click'); findButton(dom.body, 'Xác nhận').fire('click');
      await until(() => !status.disabled); assert.equal(calls, 1); root.remove();
    }
  } finally { dom.restore(); }
});

test('group status checks permission at confirmation and suppresses late success after navigation', async () => {
  const dom = installDom('/permissions'); try {
    for (const change of ['revoke', 'detach', 'hash']) {
      location.hash = '#/permissions'; let finish, calls = 0; const services = source(async (_id, dto) => result(dto));
      services.roles.setStatus = async () => { calls++; return new Promise(resolve => { finish = () => resolve({ ...group(), isActive: false }); }); };
      const root = await mount(dom, services); const status = findButton(root, 'Ngừng sử dụng nhóm'); status.fire('click');
      if (change === 'revoke') services.auth.session.permissions = ['roles.read']; findButton(dom.body, 'Xác nhận').fire('click');
      if (change === 'revoke') { assert.equal(calls, 0); assert.match(root.textContent, /Phiên hoặc quyền/); }
      else {
        if (change === 'detach') root.remove(); else location.hash = '#/assets'; finish(); await until(() => status.getAttribute('aria-busy') === null);
        assert.doesNotMatch(dom.body.textContent, /Đã cập nhật trạng thái nhóm/); assert.equal(calls, 1);
      }
      root.remove();
    }
  } finally { dom.restore(); }
});

const editorInput = (dom, name) => descendants(dom.body).find(n => n.getAttribute('name') === name);
const editorForm = dom => descendants(dom.body).find(n => n.tagName === 'FORM');
const closeEditor = dom => descendants(dom.body).find(n => n.getAttribute('aria-label') === 'Đóng hộp thoại').fire('click');

test('group editor uses one keyboard-submit form, validates before sending, and captures readonly single-flight metadata', async () => {
  const dom = installDom('/permissions'); try {
    for (const editing of [false, true]) {
      let finish; const calls = []; const services = source(async (_id, dto) => result(dto));
      const handler = async (...args) => { calls.push(structuredClone(args)); return new Promise(resolve => { finish = () => resolve({ ...group(), id: editing ? 12 : 20, name: 'Reviewed name' }); }); };
      services.roles.create = handler; services.roles.update = handler;
      const root = await mount(dom, services); findButton(root, editing ? 'Sửa nhóm' : 'Tạo nhóm quyền').fire('click');
      assert.ok(editorForm(dom)); editorInput(dom, 'groupName').value = ' '; editorForm(dom).fire('submit'); assert.equal(calls.length, 0);
      editorInput(dom, 'groupName').value = ' Reviewed name '; editorInput(dom, 'groupDescription').value = ' Fixture description ';
      editorForm(dom).fire('submit'); const submit = findButton(dom.body, 'Lưu nhóm'); editorForm(dom).fire('submit');
      try {
        assert.equal(calls.length, 1); assert.equal(editorInput(dom, 'groupName').readOnly, true);
        assert.deepEqual(calls[0], editing ? [12, { name: 'Reviewed name', description: 'Fixture description', rowVersion: version }] : [{ name: 'Reviewed name', description: 'Fixture description' }]);
      } finally { finish(); await until(() => submit.getAttribute('aria-busy') === null); }
      assert.match(dom.body.textContent, /Đã lưu nhóm quyền/); assert.ok(!editorForm(dom));
      descendants(dom.body).filter(n => n.classList.contains('toast-container')).forEach(n => n.remove()); root.remove();
    }
  } finally { dom.restore(); }
});

test('group editor unknown outcome or wrong identity preserves text and cannot replay a mutation', async () => {
  const dom = installDom('/permissions'); try {
    for (const outcome of ['NETWORK_ERROR', 'CONCURRENCY_CONFLICT', 'SESSION_CHANGED', 'invalid']) {
      let calls = 0; const services = source(async (_id, dto) => result(dto)); services.roles.update = async () => {
        calls++; if (outcome === 'invalid') return { ...group(), id: 99 }; throw Object.assign(Error('synthetic-private-detail'), { status: outcome === 'NETWORK_ERROR' ? 0 : 409, code: outcome });
      };
      const root = await mount(dom, services); findButton(root, 'Sửa nhóm').fire('click'); const form = editorForm(dom);
      editorInput(dom, 'groupName').value = 'Keep this draft'; form.fire('submit'); const submit = findButton(dom.body, 'Lưu nhóm');
      await until(() => submit.getAttribute('aria-busy') === null); assert.equal(submit.disabled, true); form.fire('submit'); assert.equal(calls, 1);
      assert.equal(editorInput(dom, 'groupName').value, 'Keep this draft'); assert.doesNotMatch(dom.body.textContent, /synthetic-private-detail/);
      assert.ok(findButton(dom.body, 'Bỏ bản đang nhập và tải lại danh sách nhóm'));
      closeEditor(dom); root.remove();
    }
  } finally { dom.restore(); }
});

test('group editor cannot write after grant revocation and cannot toast after closing or navigation', async () => {
  const dom = installDom('/permissions'); try {
    for (const change of ['revoke', 'close', 'hash']) {
      location.hash = '#/permissions'; let finish, calls = 0; const services = source(async (_id, dto) => result(dto));
      services.roles.create = async () => { calls++; return new Promise(resolve => { finish = () => resolve({ ...group(), id: 20 }); }); };
      const root = await mount(dom, services); findButton(root, 'Tạo nhóm quyền').fire('click'); const form = editorForm(dom); editorInput(dom, 'groupName').value = 'Fixture group';
      if (change === 'revoke') services.auth.session.permissions = ['roles.read']; form.fire('submit');
      if (change === 'revoke') { assert.equal(calls, 0); closeEditor(dom); }
      else {
        const submit = findButton(dom.body, 'Lưu nhóm'); if (change === 'close') closeEditor(dom); else location.hash = '#/assets';
        finish(); await until(() => submit.getAttribute('aria-busy') === null); assert.equal(calls, 1); assert.doesNotMatch(dom.body.textContent, /Đã lưu nhóm quyền/);
        if (change === 'hash') closeEditor(dom);
      }
      root.remove();
    }
  } finally { dom.restore(); }
});

test('known group validation rejection allows correction without throwing away draft or altering permissions', async () => {
  const dom = installDom('/permissions'); try {
    let calls = 0; const services = source(async (_id, dto) => result(dto)); services.roles.create = async () => { calls++; throw Object.assign(Error('synthetic-private-detail'), { status: 400, code: 'VALIDATION_ERROR' }); };
    const root = await mount(dom, services); findButton(root, 'Tạo nhóm quyền').fire('click'); editorInput(dom, 'groupName').value = 'Fixture group';
    editorForm(dom).fire('submit'); const submit = findButton(dom.body, 'Lưu nhóm'); await until(() => submit.getAttribute('aria-busy') === null);
    assert.equal(submit.disabled, false); assert.equal(editorInput(dom, 'groupName').readOnly, false); assert.equal(editorInput(dom, 'groupName').value, 'Fixture group');
    assert.equal(calls, 1); assert.doesNotMatch(dom.body.textContent, /synthetic-private-detail/);
    closeEditor(dom); root.remove();
  } finally { dom.restore(); }
});

test('unknown create followed by closing editor reloads the whole catalog, including an initially empty group list', async () => {
  const dom = installDom('/permissions'); try {
    for (const empty of [false, true]) {
      let calls = 0, refreshes = 0; window.dispatchEvent = () => { refreshes++; };
      const services = source(async (_id, dto) => result(dto)); services.roles.create = async () => { calls++; throw Object.assign(Error('synthetic failure'), { status: 0 }); };
      if (empty) services.roles.list = async () => ({ items: [], totalPages: 0 });
      const root = await permissionPage(services); dom.body.append(root); if (!empty) await until(() => Boolean(checkbox(root, 'Quản lý tài sản Sửa')));
      findButton(root, 'Tạo nhóm quyền').fire('click'); editorInput(dom, 'groupName').value = 'Fixture group'; editorForm(dom).fire('submit');
      const submit = findButton(dom.body, 'Lưu nhóm'); await until(() => submit.getAttribute('aria-busy') === null); closeEditor(dom);
      buttonAfterUnknown(root, empty).fire('click'); assert.equal(refreshes, 1); assert.equal(calls, 1); root.remove();
    }
  } finally { dom.restore(); }
});

function buttonAfterUnknown(root, empty) {
  return empty ? findButton(root, 'Tải lại') : descendants(root).find(n => n.tagName === 'BUTTON' && n.textContent === 'Synthetic group · Tùy chỉnh');
}
