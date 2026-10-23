import { h } from '../utils/dom.js';
import { button, pageHeader, badge, dataTable, pagination, showToast } from '../components.js';
import { validateUser, validateUserStatus, validateUserRoles } from '../utils/validation.js';
import { ServiceError } from '../services/service-error.js';
import { can, hasPermission, route, queryFrom, setQuery, option, field, displayErrors, formAlert, busy, openDialog, allCatalog, allActive, date } from './shared.js';

export async function userPage(services) {
  if (!can(services, 'users.read') || services.mode !== 'api') throw Object.assign(new Error('Bạn không có quyền quản lý người dùng.'), { status: 403 });
  const actorId = services.auth.session.id, pageHash = window.location.hash;
  const permitted = (...permissions) => { const session = services.auth?.session; return session?.id === actorId &&
    hasPermission(session, 'users.read') && permissions.every(permission => hasPermission(session, permission)); };
  const query = { page: 1, pageSize: 10, sortBy: 'displayName', ...queryFrom() };
  const [result, departments, roles] = await Promise.all([
    services.users.list(query), can(services, 'departments.read') ? allCatalog(services.departments) : [],
    can(services, 'roles.read') ? allActive(services.roles) : [],
  ]);
  if (!permitted()) throw Object.assign(new Error('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang.'), { status: 401, code: 'SESSION_CHANGED' });
  const root = h('div', { className: 'user-admin-page' });
  const active = () => root.isConnected && window.location.hash === pageHash;
  const alive = (...permissions) => active() && permitted(...permissions);
  const refresh = () => { if (alive()) window.dispatchEvent(new Event('frontend-refresh')); };
  function report(error, feedback, modal) {
    // Old dialogs/requests must not change the screen or session that replaced them.
    if (!active() || error.code === 'SESSION_CHANGED') return;
    if (error.status === 401) { if (!services.auth.session) { modal?.close(); route('/login'); } return; }
    if (!alive()) return;
    const message = error.code === 'CONCURRENCY_CONFLICT' ? 'Dữ liệu đã thay đổi. Đóng hộp thoại và mở lại để kiểm tra trước khi lưu; hệ thống không tự ghi đè.' : error.message;
    if (feedback) feedback.replaceChildren(formAlert(message)); else showToast(message, 'error');
  }
  function saved(modal, message, warnings = []) {
    if (!active() || !modal.dialog.isConnected) return;
    modal.close();
    if (!services.auth.session) { showToast('Tài khoản của bạn đã thay đổi. Vui lòng đăng nhập lại.', 'info'); route('/login'); return; }
    if (!alive()) return;
    showToast(message);
    for (const warning of warnings) showToast(warning === 'ALLOCATION_REVIEW_REQUIRED' ? 'Cần rà soát cấp phát liên quan khi module nghiệp vụ được triển khai.' : 'Máy chủ có cảnh báo cần rà soát thêm.', 'warning');
    refresh();
  }
  async function runWrite(state, { control, modal, feedback, inputs, required, write, getUser, expectedId, accountResult = false, fields = {}, onSuccess }) {
    if (state.pending || state.blocked || control.disabled || !active() || !modal.dialog.isConnected) return;
    if (!alive(...required)) { state.blocked = true; control.disabled = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang.')); return; }
    state.pending = true; control.disabled = true; control.setAttribute('aria-busy', 'true');
    const before = inputs.map(input => ({ input, disabled: input.disabled, readOnly: input.readOnly }));
    for (const { input } of before) {
      if (input.tagName === 'SELECT' || input.getAttribute('type') === 'checkbox') input.disabled = true; else input.readOnly = true;
    }
    try {
      const response = await write();
      if (!active() || !modal.dialog.isConnected) return;
      const user = getUser(response);
      if (!Number.isSafeInteger(user?.id) || user.id <= 0 || expectedId != null && user.id !== expectedId ||
          accountResult && (!Array.isArray(response?.warnings) || !response.warnings.every(value => typeof value === 'string')))
        throw new ServiceError(502, 'INVALID_API_RESPONSE', 'Chưa xác nhận được kết quả thao tác.');
      // The adapter intentionally clears auth after a successful self change.
      // Preserve that required Login transition without accepting another actor.
      if (!alive(...required) && !(expectedId === actorId && !services.auth.session)) {
        state.blocked = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Hãy tải lại để đối chiếu kết quả thực tế.')); return;
      }
      onSuccess(response);
    } catch (error) {
      if (!active() || !modal.dialog.isConnected) return;
      if (!services.auth.session) { modal.close(); route('/login'); return; }
      if (error?.status === 400 && error?.code === 'VALIDATION_ERROR' && alive(...required)) {
        displayErrors(fields, error.errors || {}); feedback.replaceChildren(formAlert('Thông tin không hợp lệ. Kiểm tra các trường rồi sửa lại.')); return;
      }
      state.blocked = true;
      const message = error?.code === 'CONCURRENCY_CONFLICT' ? 'Dữ liệu đã thay đổi. Giữ bản đang nhập và tải lại trước khi quyết định tiếp.' :
        error?.code === 'LAST_ADMIN_PROTECTED' ? 'Không được làm mất Admin cuối cùng có thể đăng nhập. Hãy tải lại để rà soát tài khoản.' :
        error?.code === 'PRIVILEGE_ESCALATION' ? 'Không được gán quyền vượt phạm vi được phép. Hãy tải lại để rà soát.' :
        'Chưa xác nhận được kết quả thao tác. Giữ bản đang nhập, không tự gửi lại; hãy tải lại danh sách người dùng để đối chiếu.';
      feedback.replaceChildren(formAlert(message), button('Đóng và tải lại danh sách người dùng', { kind: 'secondary', onClick: () => {
        if (!active() || !modal.dialog.isConnected) return; modal.close(); window.dispatchEvent(new Event('frontend-refresh'));
      } }));
    } finally {
      state.pending = false; control.removeAttribute('aria-busy'); control.disabled = state.blocked;
      for (const { input, disabled, readOnly } of before) { input.disabled = disabled; input.readOnly = readOnly; }
    }
  }
  async function profileDialog(id = null) {
    const required = id ? ['users.update'] : ['users.create', 'roles.assign', 'roles.read'];
    if (!alive(...required)) return;
    try {
      const item = id ? await services.users.get(id) : null;
      if (!alive(...required)) return;
      const fields = {}, feedback = h('div', { 'aria-live': 'polite' });
      const add = config => { const value = field({ value: item?.[config.name] ?? '', ...config }); fields[config.name] = value; return value.element; };
      const save = button('Lưu hồ sơ', { type: 'submit', iconName: 'check' }); let modal;
      const writeState = { pending: false, blocked: false };
      const departmentOptions = [{ value: '', label: 'Không có phòng ban' }, ...departments.filter(d => d.isActive).map(d => ({ value: d.id, label: d.name }))];
      if (item?.departmentId && !departmentOptions.some(d => Number(d.value) === item.departmentId)) {
        departmentOptions.push({ value: item.departmentId, label: (item.departmentName || departments.find(d => d.id === item.departmentId)?.name || 'Phòng ban không còn khả dụng') + ' (tham chiếu cũ)' });
      }
      const choices = roles.map(role => ({ role, input: h('input', { type: 'checkbox', name: 'roleIds', value: role.id }) }));
      const form = h('form', { noValidate: true, onSubmit: event => {
        event.preventDefault(); if (save.disabled || !form.isConnected) return;
        if (!alive(...required)) { save.disabled = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang.')); return; }
        const dto = Object.fromEntries(Object.entries(fields).map(([key, value]) => [key, value.input.value.trim() || null]));
        dto.departmentId = dto.departmentId ? Number(dto.departmentId) : null;
        if (!item) {
          dto.employeeCode = null;
          dto.roleIds = choices.filter(choice => choice.input.checked).map(choice => choice.role.id);
        } else {
          dto.employeeCode = item.employeeCode ?? null;
        }
        if (item) dto.rowVersion = item.rowVersion;
        const errors = validateUser(dto, { isUpdate: Boolean(item) }); displayErrors(fields, errors); feedback.replaceChildren();
        if (Object.keys(errors).length) { if (errors.roleIds) feedback.append(formAlert(errors.roleIds.join(' '))); return; }
        return runWrite(writeState, { control: save, modal, feedback, fields, required,
          inputs: [...Object.values(fields).map(field => field.input), ...choices.map(choice => choice.input)], expectedId: item?.id,
          write: () => item ? services.users.update(item.id, dto) : services.users.create(dto), getUser: response => response,
          onSuccess: response => saved(modal, item ? 'Đã cập nhật hồ sơ.' : ('Đã tạo tài khoản chờ kích hoạt' + (response?.employeeCode ? ' [' + response.employeeCode + ']. ' : '. ') + emailStatus(response.invitationEmailStatus)))
        });
      } }, h('div', { className: 'dialog-body form-grid' },
        add({ name: 'displayName', label: 'Họ và tên', required: true, maxLength: 200 }),
        add({ name: 'username', label: 'Tên đăng nhập', required: true, maxLength: 100 }),
        add({ name: 'email', label: 'Email', type: 'email', required: true, maxLength: 320 }),
        add({ name: 'employeeCode', label: 'Mã nhân viên', maxLength: 50, placeholder: item ? '' : 'Mã sẽ được hệ thống tự động tạo khi lưu (NVxxxxxx)', hint: item ? 'Mã nhân viên không thể thay đổi sau khi tạo.' : 'Mã sẽ được hệ thống tự động tạo khi lưu (NVxxxxxx).' }),
        add({ name: 'departmentId', label: 'Phòng ban', options: departmentOptions }),
        add({ name: 'phone', label: 'Số điện thoại', type: 'tel', maxLength: 30 }),
        !item ? h('fieldset', { className: 'role-choices' }, h('legend', {}, 'Nhóm quyền (chọn ít nhất một)'), choices.map(({ role, input }) => h('label', {}, input, h('span', {}, role.name))), h('p', { className: 'field-hint' }, 'Người dùng tự đặt mật khẩu qua liên kết email có hiệu lực 30 phút. Không tự cấp quyền theo phòng ban.')) : null,
        feedback), h('div', { className: 'dialog-footer' }, button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), save));
      if (fields.employeeCode) {
        fields.employeeCode.input.addEventListener('keydown', e => {
          if (!['Tab', 'Escape', 'ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(e.key)) e.preventDefault();
        });
      }
      modal = openDialog({ title: item ? 'Sửa hồ sơ người dùng' : 'Thêm người dùng', body: form });
    } catch (error) { report(error); }
  }
  async function detailDialog(row) {
    if (!alive()) return;
    try {
      const [item, state] = await Promise.all([services.users.get(row.id), services.users.account(row.id)]);
      if (!alive()) return;
      if (item.rowVersion !== state.rowVersion) throw new Error('Hồ sơ vừa thay đổi. Hãy mở lại chi tiết.');
      let modal;
      const pairs = [['Tên đăng nhập', item.username], ['Email', item.email], ['Mã nhân viên', item.employeeCode], ['Số điện thoại', item.phone],
        ['Phòng ban', item.departmentName || departments.find(d => d.id === item.departmentId)?.name || (item.departmentId ? 'Phòng ban không còn khả dụng' : 'Không có')],
        ['Vai trò', state.roleIds.map(id => roles.find(role => role.id === id)?.name || '#' + id).join(', ') || 'Chưa có vai trò'],
        ['Hoạt động', item.isActive ? 'Đang hoạt động' : 'Ngừng hoạt động'], ['Khóa quản trị', state.isAdminLocked ? 'Đang khóa' : 'Không khóa'],
        ['Xác nhận email', item.isEmailConfirmed ? 'Đã kích hoạt' : 'Chờ kích hoạt'], ['Gửi lời mời', emailStatus(item.invitationEmailStatus)],
        ['Quyền hiệu lực', state.effectivePermissions?.join(', ') || 'Chưa có quyền'],
        ['Ngày tạo', date(item.createdAt)], ['Cập nhật', item.updatedAt ? date(item.updatedAt) : 'Chưa có']];
      modal = openDialog({ title: item.displayName, body: h('div', { className: 'dialog-body' }, h('dl', { className: 'user-details' }, pairs.map(([name, value]) => h('div', {}, h('dt', {}, name), h('dd', {}, value || '—')))), h('p', { className: 'meta' }, 'Khóa quản trị độc lập với trạng thái hoạt động. Không hiển thị mật khẩu, token hoặc dữ liệu bảo mật.')), footer: button('Đóng', { kind: 'secondary', onClick: () => modal.close() }) });
    } catch (error) { report(error); }
  }
  async function statusDialog(row) {
    if (!alive('users.status.manage')) return;
    try {
      const state = await services.users.account(row.id); if (!alive('users.status.manage')) return;
      const fields = {
        status: field({ name: 'status', label: 'Thao tác', value: state.isAdminLocked ? 'Unlocked' : state.isActive ? 'Inactive' : 'Active', options: [
          { value: 'Active', label: 'Cho phép hoạt động' }, { value: 'Inactive', label: 'Ngừng hoạt động' }, { value: 'Locked', label: 'Khóa quản trị' }, { value: 'Unlocked', label: 'Mở khóa quản trị' }] }),
        reason: field({ name: 'reason', label: 'Lý do', required: true, maxLength: 1000, type: 'textarea' }),
      };
      const feedback = h('div', { 'aria-live': 'polite' }); let modal;
      const writeState = { pending: false, blocked: false };
      const confirm = button('Xác nhận trạng thái', { onClick: () => {
        if (confirm.disabled || !modal.dialog.isConnected) return;
        if (!alive('users.status.manage')) { confirm.disabled = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang.')); return; }
        const dto = { status: fields.status.input.value, reason: fields.reason.input.value.trim(), rowVersion: state.rowVersion };
        const errors = validateUserStatus(dto); displayErrors(fields, errors); feedback.replaceChildren(); if (Object.keys(errors).length) return;
        return runWrite(writeState, { control: confirm, modal, feedback, fields, required: ['users.status.manage'],
          inputs: Object.values(fields).map(field => field.input), expectedId: row.id, accountResult: true,
          write: () => services.users.setStatus(row.id, dto), getUser: response => response?.user,
          onSuccess: response => saved(modal, 'Đã cập nhật trạng thái tài khoản.', response.warnings)
        });
      } });
      modal = openDialog({ title: 'Trạng thái tài khoản — ' + row.displayName, body: h('div', { className: 'dialog-body' },
        h('p', { className: 'info-note' }, (state.isActive ? 'Đang hoạt động' : 'Ngừng hoạt động') + ' · ' + (state.isAdminLocked ? 'Đang khóa quản trị' : 'Không khóa quản trị') + '. Kích hoạt không tự mở khóa; mở khóa không tự kích hoạt. JWT cũ sẽ bị thu hồi.'),
        fields.status.element, fields.reason.element, feedback), footer: [button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm] });
    } catch (error) { report(error); }
  }
  async function rolesDialog(row) {
    if (!alive('roles.assign', 'roles.read')) return;
    try {
      const [state, catalog] = await Promise.all([services.users.account(row.id), allActive(services.roles)]); if (!alive('roles.assign', 'roles.read')) return;
      const unmapped = state.roleIds.filter(id => !catalog.some(role => role.id === id));
      const choices = catalog.map(role => ({ role, input: h('input', { type: 'checkbox', name: 'roleIds', value: role.id, checked: state.roleIds.includes(role.id) }) }));
      const feedback = h('div', { 'aria-live': 'polite' }); let modal;
      const writeState = { pending: false, blocked: false };
      const confirm = button('Lưu vai trò', { disabled: Boolean(unmapped.length), onClick: () => {
        if (confirm.disabled || !modal.dialog.isConnected) return;
        if (!alive('roles.assign', 'roles.read')) { confirm.disabled = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang.')); return; }
        const dto = { roleIds: choices.filter(choice => choice.input.checked).map(choice => choice.role.id), rowVersion: state.rowVersion };
        const errors = validateUserRoles(dto); feedback.replaceChildren();
        if (Object.keys(errors).length) { feedback.append(formAlert(Object.values(errors).flat().join(' '))); return; }
        return runWrite(writeState, { control: confirm, modal, feedback, required: ['roles.assign', 'roles.read'],
          inputs: choices.map(choice => choice.input), expectedId: row.id, accountResult: true,
          write: () => services.users.replaceRoles(row.id, dto), getUser: response => response?.user,
          onSuccess: response => saved(modal, 'Đã cập nhật vai trò. JWT cũ của tài khoản đã bị thu hồi.', response.warnings)
        });
      } });
      modal = openDialog({ title: 'Vai trò — ' + row.displayName, body: h('div', { className: 'dialog-body' },
        h('fieldset', { className: 'role-choices' }, h('legend', {}, 'Chọn vai trò đăng nhập'), choices.map(({ role, input }) => h('label', {}, input, h('span', {}, role.name)))),
        h('p', { className: 'info-note' }, 'Bỏ chọn tất cả sẽ thu hồi toàn bộ vai trò đăng nhập. Quản lý định nghĩa nhóm tại trang Phân quyền. Backend bảo vệ Admin cuối cùng.'),
        unmapped.length ? formAlert('Có vai trò cũ không còn trong danh mục hoạt động: ' + unmapped.join(', ') + '. Cần review; giao diện không tự loại bỏ.') : null, feedback),
        footer: [button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm] });
    } catch (error) { report(error); }
  }
  async function resend(row, control) {
    if (control.disabled || !control.isConnected || !alive('users.create')) return;
    await busy(control, async () => {
      try { const result = await services.users.resendInvitation(row.id); if (alive('users.create')) { showToast(emailStatus(result.emailStatus)); refresh(); } }
      catch (error) { report(error); }
    });
  }
  const keyword = h('input', { className: 'input', type: 'search', maxLength: 200, value: query.keyword || '', placeholder: 'Tên, email, mã nhân viên…', 'aria-label': 'Tìm kiếm người dùng' });
  const department = h('select', { className: 'select', 'aria-label': 'Lọc phòng ban' }, option('', 'Tất cả phòng ban'), departments.map(d => option(d.id, d.name, String(d.id) === String(query.departmentId))));
  const role = h('select', { className: 'select', 'aria-label': 'Lọc vai trò', disabled: !can(services, 'roles.read') }, option('', 'Tất cả vai trò'), roles.map(r => option(r.id, r.name, String(r.id) === String(query.roleId))));
  const status = h('select', { className: 'select', 'aria-label': 'Lọc trạng thái người dùng' }, option('', 'Tất cả trạng thái'), option('Active', 'Đang hoạt động', query.status === 'Active'), option('Inactive', 'Ngừng hoạt động', query.status === 'Inactive'));
  const sort = h('select', { className: 'select', 'aria-label': 'Sắp xếp người dùng' }, [['displayName', 'Họ và tên'], ['username', 'Tên đăng nhập'], ['email', 'Email'], ['createdAt', 'Ngày tạo']].map(([value, label]) => option(value, label, query.sortBy === value)));
  root.append(pageHeader('Người dùng', 'Quản lý hồ sơ, trạng thái tài khoản và vai trò đăng nhập. Không xóa tài khoản hoặc lịch sử.', { eyebrow: 'Quản trị', actions: can(services, 'users.create') && can(services, 'roles.assign') && can(services, 'roles.read') ? [button('Thêm người dùng', { iconName: 'plus', onClick: () => profileDialog() })] : [] }),
    h('section', { className: 'card filters-card' }, h('form', { className: 'filters user-filters', onSubmit: event => { event.preventDefault(); setQuery('/users', { keyword: keyword.value.trim(), departmentId: department.value, roleId: role.value, status: status.value, sortBy: sort.value, sortDirection: 'asc', page: 1 }); } },
      h('div', { className: 'filter-field search-field' }, h('label', {}, 'Tìm kiếm'), keyword),
      h('div', { className: 'filter-field' }, h('label', {}, 'Phòng ban'), department), h('div', { className: 'filter-field' }, h('label', {}, 'Vai trò'), role),
      h('div', { className: 'filter-field' }, h('label', {}, 'Trạng thái'), status), h('div', { className: 'filter-field' }, h('label', {}, 'Sắp xếp'), sort),
      h('div', { className: 'actions' }, button('Tìm kiếm', { type: 'submit', iconName: 'search' }), button('Đặt lại', { kind: 'secondary', onClick: () => route('/users') })))),
    h('section', { className: 'card' }, dataTable({ label: 'Danh sách người dùng', rows: result.items, columns: [
      { key: 'displayName', label: 'Người dùng', render: row => h('div', { className: 'asset-name' }, h('strong', {}, row.displayName), h('span', { className: 'meta' }, row.username)) },
      { key: 'email', label: 'Email' }, { key: 'departmentId', label: 'Phòng ban', render: row => row.departmentName || departments.find(d => d.id === row.departmentId)?.name || (row.departmentId ? 'Phòng ban không còn khả dụng' : '—') },
      { key: 'isEmailConfirmed', label: 'Kích hoạt', render: row => row.isEmailConfirmed ? 'Đã kích hoạt' : h('div', {}, 'Chờ kích hoạt', h('span', { className: 'meta' }, emailStatus(row.invitationEmailStatus))) },
      { key: 'isActive', label: 'Hoạt động', render: row => badge(row.isActive) },
      { key: 'actions', label: 'Thao tác', render: row => h('div', { className: 'actions' }, button('Xem', { kind: 'ghost', onClick: () => detailDialog(row) }),
        can(services, 'users.update') ? button('Sửa', { kind: 'ghost', onClick: () => profileDialog(row.id) }) : null,
        can(services, 'users.status.manage') ? button('Trạng thái', { kind: 'ghost', onClick: () => statusDialog(row) }) : null,
        can(services, 'roles.assign') && can(services, 'roles.read') ? button('Vai trò', { kind: 'ghost', onClick: () => rolesDialog(row) }) : null,
        !row.isEmailConfirmed && can(services, 'users.create') ? button('Gửi lại lời mời', { kind: 'ghost', onClick: event => resend(row, event.currentTarget) }) : null) }
    ] }), pagination(result, page => setQuery('/users', { ...query, page }))));
  return root;
}
function emailStatus(status) { return ({ Sent: 'Email đã được dịch vụ tiếp nhận.', Sandboxed: 'Đã gửi vào sandbox kiểm thử.', Failed: 'Gửi email thất bại; có thể gửi lại.', NotConfigured: 'Chưa cấu hình dịch vụ email; cần cấu hình rồi gửi lại.', Pending: 'Đang chờ gửi email.', Activated: 'Đã kích hoạt.', NotRequired: 'Tài khoản hiện có.' })[status] || 'Chưa có trạng thái email.'; }
