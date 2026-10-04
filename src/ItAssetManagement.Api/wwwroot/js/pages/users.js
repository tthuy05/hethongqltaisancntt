import { h } from '../utils/dom.js';
import { button, pageHeader, badge, dataTable, pagination, showToast } from '../components.js';
import { validateUser, validateUserStatus, validateUserRoles } from '../utils/validation.js';
import { can, route, queryFrom, setQuery, option, field, displayErrors, formAlert, busy, openDialog, allCatalog, allActive, date } from './shared.js';

export async function userPage(services) {
  if (!can(services, 'users.read') || services.mode !== 'api') throw Object.assign(new Error('Bạn không có quyền quản lý người dùng.'), { status: 403 });
  const query = { page: 1, pageSize: 10, sortBy: 'displayName', ...queryFrom() };
  const [result, departments, roles] = await Promise.all([
    services.users.list(query), can(services, 'departments.read') ? allCatalog(services.departments) : [],
    can(services, 'roles.read') ? allActive(services.roles) : [],
  ]);
  const root = h('div', { className: 'user-admin-page' });
  const alive = () => root.isConnected && Boolean(services.auth.session);
  const refresh = () => { if (root.isConnected) window.dispatchEvent(new Event('frontend-refresh')); };
  function report(error, feedback, modal) {
    if (error.status === 401) { modal?.close(); route('/login'); return; }
    const message = error.code === 'CONCURRENCY_CONFLICT' ? 'Dữ liệu đã thay đổi. Đóng hộp thoại và mở lại để kiểm tra trước khi lưu; hệ thống không tự ghi đè.' : error.message;
    if (feedback) feedback.replaceChildren(formAlert(message)); else showToast(message, 'error');
  }
  function saved(modal, message, warnings = []) {
    modal.close();
    if (!services.auth.session) { showToast('Tài khoản của bạn đã thay đổi. Vui lòng đăng nhập lại.', 'info'); route('/login'); return; }
    if (!alive()) return;
    showToast(message);
    for (const warning of warnings) showToast(warning === 'ALLOCATION_REVIEW_REQUIRED' ? 'Cần rà soát cấp phát liên quan khi module nghiệp vụ được triển khai.' : warning, 'warning');
    refresh();
  }
  async function profileDialog(id = null) {
    try {
      const item = id ? await services.users.get(id) : null;
      if (!alive()) return;
      const fields = {}, feedback = h('div', { 'aria-live': 'polite' });
      const add = config => { const value = field({ value: item?.[config.name] ?? '', ...config }); fields[config.name] = value; return value.element; };
      const save = button('Lưu hồ sơ', { type: 'submit', iconName: 'check' }); let modal;
      const departmentOptions = [{ value: '', label: 'Không có phòng ban' }, ...departments.filter(d => d.isActive).map(d => ({ value: d.id, label: d.name }))];
      if (item?.departmentId && !departmentOptions.some(d => Number(d.value) === item.departmentId)) {
        departmentOptions.push({ value: item.departmentId, label: (departments.find(d => d.id === item.departmentId)?.name || '#' + item.departmentId) + ' (tham chiếu cũ)' });
      }
      const form = h('form', { noValidate: true, onSubmit: event => {
        event.preventDefault(); if (save.disabled) return;
        const dto = Object.fromEntries(Object.entries(fields).map(([key, value]) => [key, key === 'password' ? value.input.value : value.input.value.trim() || null]));
        dto.departmentId = dto.departmentId ? Number(dto.departmentId) : null;
        if (item) dto.rowVersion = item.rowVersion;
        const errors = validateUser(dto, { isUpdate: Boolean(item) }); displayErrors(fields, errors); feedback.replaceChildren();
        if (Object.keys(errors).length) return;
        busy(save, async () => {
          try {
            const response = item ? await services.users.update(item.id, dto) : await services.users.create(dto);
            saved(modal, item ? 'Đã cập nhật hồ sơ.' : 'Đã tạo người dùng. Chọn Vai trò để cấp quyền đăng nhập.');
            if (!item && alive()) showToast('Người dùng #' + response.id + ' chưa có vai trò mặc định.', 'info');
          } catch (error) { if (modal.dialog.isConnected) { report(error, feedback, modal); displayErrors(fields, error.errors || {}); } }
          finally { if (fields.password) fields.password.input.value = ''; }
        });
      } }, h('div', { className: 'dialog-body form-grid' },
        add({ name: 'displayName', label: 'Họ và tên', required: true, maxLength: 200 }),
        add({ name: 'username', label: 'Tên đăng nhập', required: true, maxLength: 100 }),
        add({ name: 'email', label: 'Email', type: 'email', required: true, maxLength: 320 }),
        add({ name: 'employeeCode', label: 'Mã nhân viên', maxLength: 50 }),
        add({ name: 'departmentId', label: 'Phòng ban', options: departmentOptions }),
        add({ name: 'phone', label: 'Số điện thoại', type: 'tel', maxLength: 30 }),
        !item ? add({ name: 'password', label: 'Mật khẩu ban đầu', type: 'password', required: true, maxLength: 256, hint: '12–256 ký tự. Không tự gán vai trò; không có chức năng đặt lại mật khẩu.' }) : null,
        feedback), h('div', { className: 'dialog-footer' }, button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), save));
      if (fields.password) fields.password.input.autocomplete = 'new-password';
      modal = openDialog({ title: item ? 'Sửa hồ sơ người dùng' : 'Thêm người dùng', body: form, onClose: () => { if (fields.password) fields.password.input.value = ''; } });
    } catch (error) { report(error); }
  }
  async function detailDialog(row) {
    try {
      const [item, state] = await Promise.all([services.users.get(row.id), services.users.account(row.id)]);
      if (!alive()) return;
      if (item.rowVersion !== state.rowVersion) throw new Error('Hồ sơ vừa thay đổi. Hãy mở lại chi tiết.');
      let modal;
      const pairs = [['Tên đăng nhập', item.username], ['Email', item.email], ['Mã nhân viên', item.employeeCode], ['Số điện thoại', item.phone],
        ['Phòng ban', departments.find(d => d.id === item.departmentId)?.name || (item.departmentId ? '#' + item.departmentId : 'Không có')],
        ['Vai trò', state.roleIds.map(id => roles.find(role => role.id === id)?.name || '#' + id).join(', ') || 'Chưa có vai trò'],
        ['Hoạt động', item.isActive ? 'Đang hoạt động' : 'Ngừng hoạt động'], ['Khóa quản trị', state.isAdminLocked ? 'Đang khóa' : 'Không khóa'],
        ['Ngày tạo', date(item.createdAt)], ['Cập nhật', item.updatedAt ? date(item.updatedAt) : 'Chưa có']];
      modal = openDialog({ title: item.displayName, body: h('div', { className: 'dialog-body' }, h('dl', { className: 'user-details' }, pairs.map(([name, value]) => h('div', {}, h('dt', {}, name), h('dd', {}, value || '—')))), h('p', { className: 'meta' }, 'Khóa quản trị độc lập với trạng thái hoạt động. Không hiển thị mật khẩu, token hoặc dữ liệu bảo mật.')), footer: button('Đóng', { kind: 'secondary', onClick: () => modal.close() }) });
    } catch (error) { report(error); }
  }
  async function statusDialog(row) {
    try {
      const state = await services.users.account(row.id); if (!alive()) return;
      const fields = {
        status: field({ name: 'status', label: 'Thao tác', value: state.isAdminLocked ? 'Unlocked' : state.isActive ? 'Inactive' : 'Active', options: [
          { value: 'Active', label: 'Kích hoạt' }, { value: 'Inactive', label: 'Ngừng hoạt động' }, { value: 'Locked', label: 'Khóa quản trị' }, { value: 'Unlocked', label: 'Mở khóa quản trị' }] }),
        reason: field({ name: 'reason', label: 'Lý do', required: true, maxLength: 1000, type: 'textarea' }),
      };
      const feedback = h('div', { 'aria-live': 'polite' }); let modal;
      const confirm = button('Xác nhận trạng thái', { onClick: () => busy(confirm, async () => {
        const dto = { status: fields.status.input.value, reason: fields.reason.input.value.trim(), rowVersion: state.rowVersion };
        const errors = validateUserStatus(dto); displayErrors(fields, errors); feedback.replaceChildren(); if (Object.keys(errors).length) return;
        try { const response = await services.users.setStatus(row.id, dto); saved(modal, 'Đã cập nhật trạng thái tài khoản.', response.warnings); }
        catch (error) { if (modal.dialog.isConnected) report(error, feedback, modal); }
      }) });
      modal = openDialog({ title: 'Trạng thái tài khoản — ' + row.displayName, body: h('div', { className: 'dialog-body' },
        h('p', { className: 'info-note' }, (state.isActive ? 'Đang hoạt động' : 'Ngừng hoạt động') + ' · ' + (state.isAdminLocked ? 'Đang khóa quản trị' : 'Không khóa quản trị') + '. Kích hoạt không tự mở khóa; mở khóa không tự kích hoạt. JWT cũ sẽ bị thu hồi.'),
        fields.status.element, fields.reason.element, feedback), footer: [button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm] });
    } catch (error) { report(error); }
  }
  async function rolesDialog(row) {
    try {
      const [state, catalog] = await Promise.all([services.users.account(row.id), allActive(services.roles)]); if (!alive()) return;
      const unmapped = state.roleIds.filter(id => !catalog.some(role => role.id === id));
      const choices = catalog.map(role => ({ role, input: h('input', { type: 'checkbox', name: 'roleIds', value: role.id, checked: state.roleIds.includes(role.id) }) }));
      const feedback = h('div', { 'aria-live': 'polite' }); let modal;
      const confirm = button('Lưu vai trò', { disabled: Boolean(unmapped.length), onClick: () => busy(confirm, async () => {
        const dto = { roleIds: choices.filter(choice => choice.input.checked).map(choice => choice.role.id), rowVersion: state.rowVersion };
        const errors = validateUserRoles(dto); feedback.replaceChildren();
        if (Object.keys(errors).length) { feedback.append(formAlert(Object.values(errors).flat().join(' '))); return; }
        try { const response = await services.users.replaceRoles(row.id, dto); saved(modal, 'Đã cập nhật vai trò. JWT cũ của tài khoản đã bị thu hồi.', response.warnings); }
        catch (error) { if (modal.dialog.isConnected) report(error, feedback, modal); }
      }) });
      modal = openDialog({ title: 'Vai trò — ' + row.displayName, body: h('div', { className: 'dialog-body' },
        h('fieldset', { className: 'role-choices' }, h('legend', {}, 'Chọn vai trò đăng nhập'), choices.map(({ role, input }) => h('label', {}, input, h('span', {}, role.name)))),
        h('p', { className: 'info-note' }, 'Bỏ chọn tất cả sẽ thu hồi toàn bộ vai trò đăng nhập. Không tạo/sửa định nghĩa role. Backend bảo vệ Admin cuối cùng.'),
        unmapped.length ? formAlert('Có vai trò cũ không còn trong danh mục hoạt động: ' + unmapped.join(', ') + '. Cần review; giao diện không tự loại bỏ.') : null, feedback),
        footer: [button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm] });
    } catch (error) { report(error); }
  }
  const keyword = h('input', { className: 'input', type: 'search', maxLength: 200, value: query.keyword || '', placeholder: 'Tên, email, mã nhân viên…', 'aria-label': 'Tìm kiếm người dùng' });
  const department = h('select', { className: 'select', 'aria-label': 'Lọc phòng ban' }, option('', 'Tất cả phòng ban'), departments.map(d => option(d.id, d.name, String(d.id) === String(query.departmentId))));
  const role = h('select', { className: 'select', 'aria-label': 'Lọc vai trò', disabled: !can(services, 'roles.read') }, option('', 'Tất cả vai trò'), roles.map(r => option(r.id, r.name, String(r.id) === String(query.roleId))));
  const status = h('select', { className: 'select', 'aria-label': 'Lọc trạng thái người dùng' }, option('', 'Tất cả trạng thái'), option('Active', 'Đang hoạt động', query.status === 'Active'), option('Inactive', 'Ngừng hoạt động', query.status === 'Inactive'));
  const sort = h('select', { className: 'select', 'aria-label': 'Sắp xếp người dùng' }, [['displayName', 'Họ và tên'], ['username', 'Tên đăng nhập'], ['email', 'Email'], ['createdAt', 'Ngày tạo']].map(([value, label]) => option(value, label, query.sortBy === value)));
  root.append(pageHeader('Người dùng', 'Quản lý hồ sơ, trạng thái tài khoản và vai trò đăng nhập. Không xóa tài khoản hoặc lịch sử.', { eyebrow: 'Quản trị', actions: can(services, 'users.create') ? [button('Thêm người dùng', { iconName: 'plus', onClick: () => profileDialog() })] : [] }),
    h('section', { className: 'card filters-card' }, h('form', { className: 'filters user-filters', onSubmit: event => { event.preventDefault(); setQuery('/users', { keyword: keyword.value.trim(), departmentId: department.value, roleId: role.value, status: status.value, sortBy: sort.value, sortDirection: 'asc', page: 1 }); } },
      h('div', { className: 'filter-field search-field' }, h('label', {}, 'Tìm kiếm'), keyword),
      h('div', { className: 'filter-field' }, h('label', {}, 'Phòng ban'), department), h('div', { className: 'filter-field' }, h('label', {}, 'Vai trò'), role),
      h('div', { className: 'filter-field' }, h('label', {}, 'Trạng thái'), status), h('div', { className: 'filter-field' }, h('label', {}, 'Sắp xếp'), sort),
      h('div', { className: 'actions' }, button('Tìm kiếm', { type: 'submit', iconName: 'search' }), button('Đặt lại', { kind: 'secondary', onClick: () => route('/users') })))),
    h('section', { className: 'card' }, dataTable({ label: 'Danh sách người dùng', rows: result.items, columns: [
      { key: 'displayName', label: 'Người dùng', render: row => h('div', { className: 'asset-name' }, h('strong', {}, row.displayName), h('span', { className: 'meta' }, row.username)) },
      { key: 'email', label: 'Email' }, { key: 'departmentId', label: 'Phòng ban', render: row => departments.find(d => d.id === row.departmentId)?.name || (row.departmentId ? '#' + row.departmentId : '—') },
      { key: 'isActive', label: 'Hoạt động', render: row => badge(row.isActive) },
      { key: 'actions', label: 'Thao tác', render: row => h('div', { className: 'actions' }, button('Xem', { kind: 'ghost', onClick: () => detailDialog(row) }),
        can(services, 'users.update') ? button('Sửa', { kind: 'ghost', onClick: () => profileDialog(row.id) }) : null,
        can(services, 'users.status.manage') ? button('Trạng thái', { kind: 'ghost', onClick: () => statusDialog(row) }) : null,
        can(services, 'roles.assign') && can(services, 'roles.read') ? button('Vai trò', { kind: 'ghost', onClick: () => rolesDialog(row) }) : null) }
    ] }), pagination(result, page => setQuery('/users', { ...query, page }))));
  return root;
}
