import { h } from '../utils/dom.js';
import { button, pageHeader, showToast } from '../components.js';
import { allCatalog, busy, can, hasPermission, field, formAlert, openDialog, route } from './shared.js';

const modules = { assets: 'Quản lý tài sản', departments: 'Phòng ban', 'asset-types': 'Loại tài sản', users: 'Người dùng', roles: 'Nhóm quyền', dashboard: 'Tổng quan', 'audit-logs': 'Nhật ký thao tác', assignments: 'Cấp phát tài sản', maintenance: 'Bảo trì tài sản', software: 'Danh mục phần mềm', licenses: 'Bản quyền phần mềm', 'license-assignments': 'Phân bổ bản quyền', 'replacement-rules': 'Quy tắc thay thế', 'replacement-recommendations': 'Đề xuất thay thế' };

export async function permissionPage(services) {
  if (services.mode !== 'api' || !services.auth.session?.roles?.includes('ADMIN_IT') || !can(services, 'roles.assign') || !can(services, 'roles.permissions.read')) throw Object.assign(new Error('Chỉ Admin IT được quản lý phân quyền.'), { status: 403 });
  const actorId = services.auth.session.id, pageHash = window.location.hash;
  const authorized = () => { const session = services.auth?.session; return session?.id === actorId &&
    Array.isArray(session?.roles) && session.roles.includes('ADMIN_IT') && hasPermission(session, 'roles.assign') && hasPermission(session, 'roles.permissions.read'); };
  const [groups, catalog] = await Promise.all([allCatalog(services.roles), services.roles.matrix()]);
  if (!authorized()) throw Object.assign(new Error('Phiên hoặc quyền đã thay đổi.'), { status: 403 });
  const root = h('div', { className: 'permission-page' });
  const active = () => root.isConnected && window.location.hash === pageHash;
  let selected = null, dirty = false, draft = new Set(), controls = new Map(), modal, selectionRequest = 0, loading = false, saving = false, blocked = false, groupEditorOpen = false, needsCatalogRefresh = false;
  const forbiddenSupport = permission => selected?.code === 'TECHNICAL_SUPPORT' &&
    (permission.code.startsWith('licenses.') || permission.code.startsWith('license-assignments.') || permission.code.startsWith('software.') || permission.code.startsWith('assignments.') || ['maintenance.assign', 'maintenance.cancel', 'maintenance.cost.write'].includes(permission.code)) ||
    selected?.code !== 'ADMIN_IT' && ['licenses.key.manage','licenses.key.reveal'].includes(permission.code);
  const feedback = h('div', { 'aria-live': 'polite' });
  const notice = h('p', { className: 'meta', role: 'status' });
  const matrix = h('div', { className: 'permission-matrix table-wrap' });
  const title = h('h2', {}, 'Chọn nhóm để thiết lập quyền');
  const description = h('p', { className: 'meta' });
  const groupList = h('div', { className: 'permission-groups', 'aria-label': 'Các nhóm quyền' });
  function reviewedGroup(value, id) {
    if (!value || !Number.isSafeInteger(value.id) || value.id <= 0 || value.id !== id || typeof value.name !== 'string' || !value.name.trim() || typeof value.code !== 'string' ||
        typeof value.isSystem !== 'boolean' || typeof value.isActive !== 'boolean' || !Number.isSafeInteger(value.memberCount) || value.memberCount < 0 ||
        typeof value.rowVersion !== 'string' || !value.rowVersion || !Array.isArray(value.permissions) ||
        !value.permissions.every(item => Number.isSafeInteger(item?.id) && item.id > 0) ||
        (value.description != null && typeof value.description !== 'string')) throw new Error('INVALID_API_RESPONSE');
    return value;
  }
  function syncControls() {
    const locked = loading || saving || blocked || !authorized();
    for (const [id, input] of controls) {
      const permission = catalog.find(item => item.id === id);
      input.disabled = Boolean(locked || !permission.available || !selected.isActive || selected.code === 'ADMIN_IT' || permission.adminOnly || forbiddenSupport(permission) && !draft.has(id));
    }
    save.disabled = locked || !dirty; edit.disabled = status.disabled = locked || !selected || selected.isSystem;
    create.disabled = locked;
  }
  function markChanged() { dirty = true; notice.textContent = 'Bạn có thay đổi chưa lưu.'; save.disabled = false; }
  function confirmAction(message, action) {
    const proceed = button('Xác nhận', { onClick: () => { modal.close(); action(); } });
    modal = openDialog({ title: 'Xác nhận thay đổi', body: h('div', { className: 'dialog-body' }, h('p', {}, message)), footer: [button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), proceed] });
  }
  function toggle(permission, value) {
    if (value && forbiddenSupport(permission)) { controls.get(permission.id).checked = draft.has(permission.id); return; }
    if (loading || saving || blocked || !active() || !authorized()) { controls.get(permission.id).checked = draft.has(permission.id); return; }
    if (value) {
      draft.add(permission.id);
      const parent = catalog.find(item => item.code === permission.requires);
      if (parent && parent.available) { draft.add(parent.id); controls.get(parent.id).checked = true; }
    } else {
      draft.delete(permission.id);
      for (const dependent of catalog.filter(item => item.requires === permission.code)) {
        if (!dependent.available && draft.has(dependent.id)) { controls.get(permission.id).checked = true; draft.add(permission.id); return; }
        draft.delete(dependent.id); controls.get(dependent.id).checked = false;
      }
    }
    markChanged();
  }
  function draw() {
    title.textContent = selected.name;
    description.textContent = (selected.isSystem ? 'Nhóm mặc định' : 'Nhóm tùy chỉnh') + ' · ' + selected.memberCount + ' người dùng. ' + (selected.description || '');
    controls = new Map(); draft = new Set(selected.permissions.map(item => item.id));
    const actions = [...new Set(catalog.map(item => item.action))];
    const rows = [...new Set(catalog.map(item => item.module))];
    const table = h('table', { className: 'data-table', 'aria-label': 'Ma trận quyền theo chức năng' },
      h('thead', {}, h('tr', {}, h('th', { scope: 'col' }, 'Chức năng'), actions.map(action => h('th', { scope: 'col' }, action)))),
      h('tbody', {}, rows.map(module => h('tr', {}, h('th', { scope: 'row' }, modules[module] || module), actions.map(action => {
        const permission = catalog.find(item => item.module === module && item.action === action);
        if (!permission) return h('td', {}, '—');
        const protectedAdmin = selected.code === 'ADMIN_IT';
        const input = h('input', { type: 'checkbox', checked: draft.has(permission.id), disabled: !permission.available || !selected.isActive || protectedAdmin || (permission.adminOnly && !protectedAdmin) || forbiddenSupport(permission) && !draft.has(permission.id),
          'aria-label': `${modules[module] || module} ${action}`, title: permission.adminOnly ? 'Chỉ Admin IT' : permission.available ? permission.code : 'Chưa khả dụng · API chưa được mở', onChange: event => toggle(permission, event.target.checked) });
        controls.set(permission.id, input);
        return h('td', {}, input, !permission.available ? h('span', { className: 'field-hint' }, 'Chưa khả dụng') : null);
      })))));
    matrix.replaceChildren(table); dirty = false; save.disabled = true; notice.textContent = selected.code === 'ADMIN_IT' ? 'Quyền nền tảng của Admin IT được bảo vệ để luôn có thể quản trị hệ thống.' : 'Quyền thao tác sẽ tự chọn quyền xem tương ứng. Quyền module chưa mở được giữ nguyên.';
    syncControls();
    status.textContent = selected.isActive ? 'Ngừng sử dụng nhóm' : 'Kích hoạt nhóm';
    groupList.querySelectorAll('button').forEach(item => item.setAttribute('aria-pressed', String(item.dataset.group === String(selected.id))));
  }
  async function load(id) {
    if (!Number.isSafeInteger(id) || id <= 0 || !active() || !authorized()) return;
    const own = ++selectionRequest; loading = true; syncControls();
    try { const result = await services.roles.get(id); if (!active() || own !== selectionRequest) return;
      if (!authorized()) { blocked = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang.')); return; }
      selected = reviewedGroup(result, id); blocked = false; feedback.replaceChildren(); draw(); }
    catch { if (active() && own === selectionRequest) { blocked = true; feedback.replaceChildren(formAlert('Không tải được nhóm quyền. Bản đang nhập vẫn giữ; chưa được gửi lại thay đổi.')); } }
    finally { if (own === selectionRequest) { loading = false; syncControls(); } }
  }
  function choose(id) {
    if (saving || loading || !active() || !authorized()) return;
    if (needsCatalogRefresh) { reloadGroups(); return; }
    if (dirty) confirmAction('Thay đổi quyền chưa được lưu. Bỏ thay đổi và chuyển nhóm?', () => load(id)); else load(id);
  }
  function reloadGroups() {
    if (saving || loading || !active() || !authorized()) return;
    const id = selected?.id ?? groups[0]?.id;
    if (!needsCatalogRefresh && id) { choose(id); return; }
    const refresh = () => { if (active() && authorized()) window.dispatchEvent(new Event('frontend-refresh')); };
    if (dirty) confirmAction('Bỏ thay đổi ma trận chưa lưu và tải lại toàn bộ danh sách nhóm?', refresh); else refresh();
  }
  function groupDialog(existing = null) {
    if (groupEditorOpen || saving || loading || blocked || !active() || !authorized() || existing?.isSystem) return;
    const target = existing ? { id: existing.id, rowVersion: existing.rowVersion } : null;
    const name = field({ name: 'groupName', label: 'Tên nhóm', required: true, maxLength: 200, value: existing?.name });
    const details = field({ name: 'groupDescription', label: 'Mô tả', type: 'textarea', maxLength: 1000, value: existing?.description });
    const error = h('div', { 'aria-live': 'polite' }); let editor, pending = false, closed = false, uncertain = false;
    const liveEditor = () => !closed && active() && editor.dialog.isConnected;
    const stop = message => {
      uncertain = blocked = needsCatalogRefresh = true; submit.disabled = true;
      error.replaceChildren(formAlert(message), button('Bỏ bản đang nhập và tải lại danh sách nhóm', { kind: 'secondary', onClick: () => {
        if (!liveEditor()) return; editor.close(); window.dispatchEvent(new Event('frontend-refresh'));
      } }));
    };
    const formId = 'group-editor-form';
    const submit = button('Lưu nhóm', { type: 'submit', form: formId });
    const form = h('form', { id: formId, className: 'dialog-body', noValidate: true, onSubmit: async event => {
      event.preventDefault(); if (pending || saving || loading || blocked || uncertain || !liveEditor()) return;
      if (!authorized()) { stop('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang.'); syncControls(); return; }
      const text = name.input.value.trim(), description = details.input.value.trim();
      if (!text || text.length > 200 || description.length > 1000) { error.replaceChildren(formAlert('Tên nhóm là bắt buộc, tối đa 200 ký tự; mô tả tối đa 1000 ký tự.')); return; }
      const dto = { name: text, description: description || null, ...(target ? { rowVersion: target.rowVersion } : {}) };
      pending = saving = true; syncControls(); submit.disabled = true; submit.setAttribute('aria-busy', 'true');
      name.input.readOnly = details.input.readOnly = true; error.replaceChildren();
      try {
        const result = target ? await services.roles.update(target.id, dto) : await services.roles.create(dto);
        if (!liveEditor()) return;
        if (!authorized()) { stop('Phiên hoặc quyền đã thay đổi. Hãy tải lại để đối chiếu kết quả thực tế.'); return; }
        const reviewed = reviewedGroup(result, target?.id ?? result?.id);
        if (reviewed.isSystem || reviewed.name !== text) throw new Error('INVALID_API_RESPONSE');
        const index = groups.findIndex(group => group.id === reviewed.id); if (index < 0) groups.push(reviewed); else groups[index] = reviewed;
        selected = reviewed; dirty = false; pending = false; editor.close(); draw(); populate(); feedback.replaceChildren(); showToast('Đã lưu nhóm quyền.');
      } catch (failure) {
        if (!liveEditor()) return;
        if (failure?.status === 400 && failure?.code === 'VALIDATION_ERROR') error.replaceChildren(formAlert('Thông tin nhóm không hợp lệ. Kiểm tra tên và mô tả rồi sửa lại.'));
        else stop(failure?.code === 'CONCURRENCY_CONFLICT' ? 'Nhóm vừa được cập nhật. Giữ bản đang nhập và tải lại trước khi lưu tiếp.' :
          'Chưa xác nhận được kết quả lưu nhóm. Giữ bản đang nhập, không tự gửi lại; hãy tải lại danh sách nhóm để đối chiếu.');
      } finally {
        pending = saving = false; submit.removeAttribute('aria-busy'); submit.disabled = uncertain || blocked || !authorized();
        name.input.readOnly = details.input.readOnly = false; syncControls();
      }
    } }, name.element, details.element, error);
    groupEditorOpen = true;
    editor = openDialog({ title: existing ? 'Chỉnh sửa nhóm quyền' : 'Tạo nhóm quyền', body: form, footer: submit, onClose: () => {
      closed = true; groupEditorOpen = false;
      if (pending) {
        blocked = needsCatalogRefresh = true;
        if (active()) feedback.replaceChildren(formAlert('Đã đóng hộp thoại trong lúc đang gửi. Hãy tải lại danh sách nhóm để đối chiếu kết quả trước khi thao tác tiếp.'));
        syncControls();
      }
    } });
  }
  const edit = button('Sửa nhóm', { kind: 'secondary', disabled: true, onClick: () => dirty ? confirmAction('Bỏ thay đổi ma trận chưa lưu để sửa nhóm?', () => groupDialog(selected)) : groupDialog(selected) });
  const status = button('Ngừng sử dụng nhóm', { kind: 'secondary', disabled: true, onClick: () => {
    if (!active() || saving || loading || blocked || !selected || selected.isSystem) return;
    const command = { id: selected.id, dto: { status: selected.isActive ? 'Inactive' : 'Active', reason: 'Admin thay đổi nhóm quyền', rowVersion: selected.rowVersion } };
    confirmAction((dirty ? 'Bỏ thay đổi ma trận chưa lưu và đổi trạng thái nhóm? ' : '') +
      'Chỉ có thể ngừng nhóm tùy chỉnh khi đã bỏ gán khỏi mọi người dùng. Tiếp tục?', async () => {
      if (!active() || saving || loading || blocked) return;
      if (!authorized()) { blocked = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang.')); syncControls(); return; }
      saving = true; syncControls(); status.setAttribute('aria-busy', 'true'); status.textContent = 'Đang xử lý…';
      try {
        const result = await services.roles.setStatus(command.id, command.dto);
        if (!active()) return;
        if (!authorized()) { blocked = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Hãy tải lại để đối chiếu kết quả thực tế.')); return; }
        const reviewed = reviewedGroup(result, command.id);
        if (reviewed.isActive !== (command.dto.status === 'Active')) throw new Error('INVALID_API_RESPONSE');
        selected = reviewed;
        const index = groups.findIndex(group => group.id === selected.id); if (index >= 0) groups[index] = selected;
        dirty = false; draw(); populate(); feedback.replaceChildren(); showToast('Đã cập nhật trạng thái nhóm.');
      } catch (error) {
        if (!active()) return;
        blocked = true;
        feedback.replaceChildren(formAlert(error?.code === 'ROLE_IN_USE' ? 'Nhóm còn người dùng được gán. Hãy bỏ gán và tải lại trước khi ngừng sử dụng.' :
          error?.code === 'CONCURRENCY_CONFLICT' ? 'Nhóm vừa được cập nhật. Hãy tải lại trước khi đổi trạng thái.' :
          'Chưa xác nhận được kết quả đổi trạng thái nhóm. Bản đang nhập vẫn giữ; không tự gửi lại. Dùng Tải lại để đối chiếu.'));
      } finally {
        saving = false; status.removeAttribute('aria-busy'); status.textContent = selected?.isActive ? 'Ngừng sử dụng nhóm' : 'Kích hoạt nhóm'; syncControls();
      }
    });
  } });
  const save = button('Lưu phân quyền', { iconName: 'check', disabled: true, onClick: () => {
    if (loading || saving || blocked || !dirty || !active()) return;
    const command = { id: selected.id, dto: { permissionIds: [...draft], rowVersion: selected.rowVersion } };
    confirmAction(`Thay đổi này sẽ ảnh hưởng ${selected.memberCount} người dùng thuộc nhóm ${selected.name} từ các request tiếp theo. Lưu phân quyền?`, () => {
      if (!active() || loading || saving || blocked) return;
      if (!authorized()) { blocked = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang.')); syncControls(); return; }
      saving = true; syncControls();
      return busy(save, async () => {
        try {
          const result = await services.roles.replacePermissions(command.id, command.dto);
          if (!active()) return;
          if (!authorized()) { blocked = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Hãy kiểm tra kết quả thực tế trước khi tiếp tục.')); return; }
          const reviewed = reviewedGroup(result, command.id);
          if (reviewed.permissions.length !== command.dto.permissionIds.length ||
              !reviewed.permissions.every(item => command.dto.permissionIds.includes(item.id)) ||
              new Set(reviewed.permissions.map(item => item.id)).size !== reviewed.permissions.length) throw new Error('INVALID_API_RESPONSE');
          selected = reviewed; draw(); showToast('Đã lưu phân quyền. Các request mới dùng quyền mới.');
        } catch (error) {
          if (!active()) return;
          blocked = true;
          feedback.replaceChildren(formAlert(error?.code === 'CONCURRENCY_CONFLICT' ? 'Nhóm vừa được cập nhật. Tải lại trước khi lưu; hệ thống không ghi đè.' :
            'Chưa xác nhận được kết quả lưu phân quyền. Bản đang nhập vẫn giữ; không tự gửi lại. Dùng Tải lại để đối chiếu trước khi quyết định tiếp.'));
        }
      }).finally(() => { saving = false; syncControls(); });
    });
  } });
  const create = button('Tạo nhóm quyền', { iconName: 'plus', onClick: () => dirty ? confirmAction('Bỏ thay đổi ma trận chưa lưu để tạo nhóm?', () => groupDialog()) : groupDialog() });
  function populate() { groupList.replaceChildren(...groups.map(group => button(group.name + (group.isSystem ? ' · Mặc định' : ' · Tùy chỉnh'), { kind: 'ghost', dataset: { group: group.id }, 'aria-pressed': String(selected?.id === group.id), onClick: () => choose(group.id) }))); }
  populate();
  root.append(pageHeader('Quản lý phân quyền', 'Quyền phụ thuộc nhóm được gán, không phụ thuộc phòng ban hay chức vụ. Chỉ Admin IT được quản lý ma trận và xem Nhật ký thao tác.'),
    h('div', { className: 'permission-layout' }, h('section', { className: 'card' }, h('div', { className: 'card-body' }, h('h2', {}, 'Thiết lập nhóm người dùng'), groupList, create, h('p', { className: 'meta' }, 'Gán nhóm tại trang Người dùng.'), button('Quản lý người dùng', { kind: 'secondary', onClick: () => dirty ? confirmAction('Bỏ thay đổi chưa lưu và chuyển trang?', () => route('/users')) : route('/users') }))),
      h('section', { className: 'card' }, h('div', { className: 'card-body' }, title, description, matrix, notice, feedback, h('div', { className: 'actions' }, edit, status, button('Tải lại', { kind: 'secondary', onClick: reloadGroups }), save)))));
  // Bind once per live page; listeners from removed pages are inert and disposed.
  const controller = new AbortController();
  const guard = event => { if (!root.isConnected) { controller.abort(); return; } if (dirty) { event.preventDefault(); event.returnValue = ''; } };
  window.addEventListener('beforeunload', guard, { signal: controller.signal });
  document.addEventListener('click', event => {
    if (!root.isConnected) { controller.abort(); return; }
    const link = event.target.closest?.('a[href^="#"]');
    if (dirty && link) { event.preventDefault(); event.stopImmediatePropagation(); confirmAction('Bỏ thay đổi phân quyền chưa lưu và chuyển trang?', () => { dirty = false; window.location.hash = link.getAttribute('href'); }); }
  }, { capture: true, signal: controller.signal });
  // Initial selection happens after the caller mounts the page.
  setTimeout(() => { if (groups.length && root.isConnected) load(groups[0].id); }, 0);
  return root;
}
