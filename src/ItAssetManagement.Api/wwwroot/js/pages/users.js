import { h } from '../utils/dom.js';
import { button, pageHeader, badge, dataTable, pagination, showToast } from '../components.js';
import { validateUser, validateUserStatus, validateUserRoles } from '../utils/validation.js';
import { ServiceError } from '../services/service-error.js';
import { can, hasPermission, route, queryFrom, setQuery, option, field, displayErrors, formAlert, busy, openDialog, allCatalog, allActive, date } from './shared.js';

const moduleLabels = {
  assets: 'Quản lý tài sản',
  departments: 'Phòng ban',
  'asset-types': 'Loại tài sản',
  users: 'Người dùng',
  roles: 'Nhóm quyền & Phân quyền',
  dashboard: 'Tổng quan',
  'audit-logs': 'Nhật ký kiểm toán',
  assignments: 'Cấp phát tài sản',
  maintenance: 'Bảo trì tài sản',
  licenses: 'Bản quyền phần mềm',
  'license-assignments': 'Phân bổ bản quyền',
  software: 'Danh mục phần mềm',
  'replacement-rules': 'Quy tắc thay thế',
  'replacement-recommendations': 'Đề xuất thay thế',
  reports: 'Báo cáo',
  import: 'Nhập Excel',
  export: 'Xuất Excel',
};

const permissionLabels = {
  'assets.read': 'Xem tài sản',
  'assets.create': 'Thêm tài sản',
  'assets.update': 'Sửa thông tin tài sản',
  'assets.archive': 'Lưu trữ tài sản',
  'assets.status.manage': 'Cập nhật trạng thái tài sản',
  'assets.history.read': 'Xem lịch sử tài sản',
  'assets.cost.read': 'Xem giá mua & thông tin tài chính',
  'import.assets.execute': 'Nhập dữ liệu Excel tài sản',
  'export.assets.execute': 'Xuất dữ liệu Excel tài sản',
  'departments.read': 'Xem danh sách phòng ban',
  'departments.create': 'Thêm phòng ban',
  'departments.update': 'Sửa phòng ban',
  'departments.archive': 'Lưu trữ phòng ban',
  'asset-types.read': 'Xem danh sách loại tài sản',
  'asset-types.create': 'Thêm loại tài sản',
  'asset-types.update': 'Sửa loại tài sản',
  'asset-types.archive': 'Lưu trữ loại tài sản',
  'dashboard.read': 'Xem dashboard tổng quan',
  'dashboard.cost.read': 'Xem giá trị tài chính trên dashboard',
  'users.lookup': 'Tra cứu nhân viên',
  'users.read': 'Xem danh sách người dùng',
  'users.create': 'Tạo tài khoản & gửi email kích hoạt',
  'users.update': 'Sửa thông tin người dùng',
  'users.status.manage': 'Khóa / Mở khóa người dùng',
  'roles.read': 'Xem danh sách vai trò',
  'roles.assign': 'Phân quyền & gán vai trò',
  'roles.permissions.read': 'Xem ma trận quyền hệ thống',
  'audit-logs.read': 'Xem nhật ký kiểm toán (Audit Logs)',
  'assignments.read': 'Xem danh sách cấp phát',
  'assignments.assign': 'Cấp phát tài sản cho nhân viên',
  'assignments.return': 'Thu hồi tài sản từ nhân viên',
  'assignments.transfer': 'Điều chuyển tài sản',
  'maintenance.read': 'Xem phiếu bảo trì',
  'maintenance.create': 'Mở phiếu bảo trì',
  'maintenance.update': 'Cập nhật phiếu bảo trì',
  'maintenance.assign': 'Giao kỹ thuật viên',
  'maintenance.work': 'Bắt đầu xử lý bảo trì',
  'maintenance.resolve': 'Ghi kết quả bảo trì',
  'maintenance.cancel': 'Hủy phiếu bảo trì',
  'maintenance.cost.write': 'Xem và cập nhật chi phí bảo trì',
  'licenses.read': 'Xem bản quyền',
  'licenses.create': 'Thêm bản quyền',
  'licenses.update': 'Sửa bản quyền',
  'licenses.archive': 'Ngừng sử dụng bản quyền',
  'licenses.key.manage': 'Quản lý khóa bản quyền',
  'licenses.key.reveal': 'Xem khóa bản quyền',
  'license-assignments.read': 'Xem phân bổ bản quyền',
  'license-assignments.assign': 'Cấp seat bản quyền',
  'license-assignments.revoke': 'Thu hồi seat bản quyền',
  'license-assignments.transfer': 'Điều chuyển seat bản quyền',
  'software.read': 'Xem phần mềm',
  'replacement-rules.read': 'Xem quy tắc thay thế',
  'replacement-rules.create': 'Tạo quy tắc thay thế',
  'replacement-rules.update': 'Phát hành quy tắc mới',
  'replacement-recommendations.read': 'Xem đề xuất thay thế',
  'replacement-recommendations.evaluate': 'Đánh giá thay thế',
  'replacement-recommendations.update': 'Lập kế hoạch / bỏ qua',
  'software.create': 'Thêm phần mềm',
  'software.update': 'Sửa phần mềm',
  'software.archive': 'Ngừng sử dụng phần mềm',
  'reports.assets.read': 'Báo cáo tài sản',
  'reports.assignments.read': 'Báo cáo cấp phát',
  'reports.maintenance.read': 'Báo cáo bảo trì',
  'reports.licenses.read': 'Báo cáo bản quyền',
  'reports.replacement.read': 'Báo cáo thay thế',
  'reports.cost.read': 'Báo cáo tài chính & chi phí',
};

const permissionDependencies = {
  'assets.create': 'assets.read',
  'assets.update': 'assets.read',
  'assets.archive': 'assets.read',
  'assets.status.manage': 'assets.read',
  'assets.history.read': 'assets.read',
  'assets.cost.read': 'assets.read',
  'import.assets.execute': 'assets.read',
  'export.assets.execute': 'assets.read',
  'departments.create': 'departments.read',
  'departments.update': 'departments.read',
  'departments.archive': 'departments.read',
  'asset-types.create': 'asset-types.read',
  'asset-types.update': 'asset-types.read',
  'asset-types.archive': 'asset-types.read',
  'users.create': 'users.read',
  'users.update': 'users.read',
  'users.status.manage': 'users.read',
  'roles.assign': 'roles.read',
  'roles.permissions.read': 'roles.read',
  'assignments.assign': 'assignments.read',
  'assignments.return': 'assignments.read',
  'assignments.transfer': 'assignments.read',
  'maintenance.create': 'maintenance.read',
  'maintenance.update': 'maintenance.read',
  'maintenance.assign': 'maintenance.read',
  'maintenance.work': 'maintenance.read',
  'maintenance.resolve': 'maintenance.read',
  'maintenance.cancel': 'maintenance.read',
  'maintenance.cost.write': 'maintenance.read',
  'licenses.create': 'licenses.read',
  'licenses.update': 'licenses.read',
  'licenses.archive': 'licenses.read',
  'licenses.key.manage': 'licenses.read',
  'licenses.key.reveal': 'licenses.read',
  'license-assignments.read': 'licenses.read',
  'license-assignments.assign': 'license-assignments.read',
  'license-assignments.revoke': 'license-assignments.read',
  'license-assignments.transfer': 'license-assignments.read',
  'software.create': 'software.read',
  'software.update': 'software.read',
  'software.archive': 'software.read',
  'replacement-rules.create': 'replacement-rules.read',
  'replacement-rules.update': 'replacement-rules.read',
  'replacement-recommendations.read': 'assets.read',
  'replacement-recommendations.evaluate': 'replacement-recommendations.read',
  'replacement-recommendations.update': 'replacement-recommendations.read',
  'reports.cost.read': 'reports.assets.read',
  'dashboard.cost.read': 'dashboard.read',
};

function createUserPermissionMatrix({ roleDetail, initialSelectedCodes = null, hasCustom = false, onChange }) {
  const rolePermissions = roleDetail?.permissions || [];
  const maxCodes = new Set(rolePermissions.map(p => p.code));

  const selected = new Set();
  let isCustom = hasCustom;
  if (initialSelectedCodes != null) {
    for (const code of initialSelectedCodes) {
      if (maxCodes.has(code)) selected.add(code);
    }
  } else {
    for (const code of maxCodes) selected.add(code);
  }

  const container = h('div', { className: 'user-permissions-panel' });
  const countBadge = h('span', { className: 'permission-counter' });
  const previewBox = h('div', { className: 'permission-preview-box', hidden: true });
  const groupsContainer = h('div', { className: 'permission-matrix-body' });
  const inputs = new Map();

  function update() {
    countBadge.textContent = `Đã chọn: ${selected.size}/${maxCodes.size} quyền`;
    if (isCustom) {
      countBadge.className = 'permission-counter custom-badge';
      countBadge.title = 'Tài khoản đang được tùy chỉnh quyền riêng';
    } else {
      countBadge.className = 'permission-counter default-badge';
      countBadge.title = 'Tài khoản đang dùng quyền mặc định theo vai trò';
    }
    for (const updater of groupCountUpdaters) updater();
    renderPreview();
    onChange?.({ selectedCodes: Array.from(selected), isCustom });
  }

  function renderPreview() {
    previewBox.replaceChildren(
      h('div', { className: 'preview-content' },
        h('strong', {}, 'Quyền hiệu lực thực tế (' + selected.size + '): '),
        h('div', { className: 'preview-tags' },
          selected.size === 0
            ? h('span', { className: 'meta' }, 'Không có quyền nào')
            : Array.from(selected).sort().map(c => h('span', { className: 'preview-tag' }, permissionLabels[c] || c))
        )
      )
    );
  }

  function toggle(code, checked) {
    if (checked) {
      selected.add(code);
      const req = permissionDependencies[code];
      if (req && maxCodes.has(req) && !selected.has(req)) {
        selected.add(req);
        const reqInput = inputs.get(req);
        if (reqInput) reqInput.checked = true;
      }
    } else {
      selected.delete(code);
      for (const [depCode, reqCode] of Object.entries(permissionDependencies)) {
        if (reqCode === code && selected.has(depCode)) {
          selected.delete(depCode);
          const depInput = inputs.get(depCode);
          if (depInput) depInput.checked = false;
        }
      }
    }
    isCustom = (selected.size !== maxCodes.size || Array.from(maxCodes).some(c => !selected.has(c)));
    update();
  }

  function resetToDefaults() {
    selected.clear();
    for (const code of maxCodes) selected.add(code);
    for (const [code, input] of inputs) {
      input.checked = selected.has(code);
    }
    isCustom = false;
    update();
  }

  const resetBtn = button('Khôi phục mặc định theo vai trò', {
    kind: 'secondary',
    type: 'button',
    onClick: () => resetToDefaults()
  });

  const previewBtn = button('Xem trước quyền hiệu lực', {
    kind: 'ghost',
    type: 'button',
    onClick: () => {
      previewBox.hidden = !previewBox.hidden;
      previewBtn.textContent = previewBox.hidden ? 'Xem trước quyền hiệu lực' : 'Ẩn xem trước';
    }
  });

  const header = h('div', { className: 'permission-panel-header' },
    h('div', { className: 'permission-summary' },
      h('span', { className: 'permission-panel-title' }, 'Ma trận quyền chi tiết của người dùng:'),
      countBadge
    ),
    h('div', { className: 'permission-actions' }, resetBtn, previewBtn),
    previewBox
  );

  const moduleMap = new Map();
  for (const perm of rolePermissions) {
    const mod = perm.module || 'other';
    if (!moduleMap.has(mod)) moduleMap.set(mod, []);
    moduleMap.get(mod).push(perm);
  }

  const groupCountUpdaters = [];

  inputs.clear();
  groupsContainer.replaceChildren();

  if (maxCodes.size === 0) {
    groupsContainer.append(h('p', { className: 'meta' }, 'Vai trò này hiện chưa được cấu hình quyền trong hệ thống.'));
  } else {
    for (const [mod, perms] of moduleMap) {
      const countEl = h('span', { className: 'module-perm-count' });
      function refreshCount() {
        const selCount = perms.filter(p => selected.has(p.code)).length;
        countEl.textContent = `${selCount}/${perms.length}`;
      }
      groupCountUpdaters.push(refreshCount);
      refreshCount();

      const groupEl = h('div', { className: 'permission-module-group' },
        h('div', { className: 'module-header' },
          h('h4', { className: 'module-title' }, moduleLabels[mod] || mod),
          countEl
        ),
        h('div', { className: 'permission-checkboxes-list' },
          perms.map(p => {
            const req = permissionDependencies[p.code];
            const reqName = req ? (permissionLabels[req] || req) : null;
            const input = h('input', {
              type: 'checkbox',
              checked: selected.has(p.code),
              onChange: (e) => toggle(p.code, e.target.checked)
            });
            inputs.set(p.code, input);
            return h('label', { className: 'permission-item-label' },
              input,
              h('div', { className: 'permission-info' },
                h('span', { className: 'permission-name' }, permissionLabels[p.code] || p.name || p.code),
                reqName ? h('span', { className: 'permission-req' }, `(Cần: ${reqName})`) : null
              )
            );
          })
        )
      );
      groupsContainer.append(groupEl);
    }
  }

  container.replaceChildren(header, groupsContainer);
  update();

  return {
    element: container,
    getSelectedCodes: () => Array.from(selected),
    isCustom: () => isCustom,
    resetToDefaults,
    inputs: () => Array.from(inputs.values())
  };
}

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
      if (input.tagName === 'SELECT' || ['checkbox', 'radio'].includes(input.getAttribute('type'))) {
        input.disabled = true; input.readOnly = true;
      } else input.readOnly = true;
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

      const roleDetailsCache = new Map();
      async function getRoleDetail(roleId) {
        if (roleDetailsCache.has(roleId)) return roleDetailsCache.get(roleId);
        if (typeof services.roles?.get === 'function') {
          try {
            const detail = await services.roles.get(roleId);
            roleDetailsCache.set(roleId, detail);
            return detail;
          } catch { /* ignore fallback */ }
        }
        return { id: roleId, permissions: [] };
      }

      let selectedRoleId = roles.find(r => r.isActive)?.id ?? roles[0]?.id;
      let matrixInstance = null;
      const matrixMount = h('div', { className: 'role-permission-matrix-mount' });

      async function renderMatrixForRole(roleId) {
        matrixMount.replaceChildren(h('p', { className: 'meta' }, 'Đang tải ma trận quyền...'));
        const detail = await getRoleDetail(roleId);
        matrixInstance = createUserPermissionMatrix({
          roleDetail: detail,
          initialSelectedCodes: null,
          hasCustom: false
        });
        matrixMount.replaceChildren(matrixInstance.element);
      }

      const choices = roles.map(role => {
        const input = h('input', {
          type: 'radio',
          name: 'roleIds',
          value: role.id,
          checked: role.id === selectedRoleId,
          onChange: async () => {
            if (role.id === selectedRoleId) return;
            if (matrixInstance?.isCustom()) {
              let confirmModal;
              const proceed = button('Xác nhận đổi', {
                kind: 'primary',
                onClick: async () => {
                  confirmModal.close();
                  selectedRoleId = role.id;
                  choices.forEach(c => { c.input.checked = c.role.id === selectedRoleId; });
                  await renderMatrixForRole(selectedRoleId);
                }
              });
              const cancel = button('Giữ nguyên', {
                kind: 'secondary',
                onClick: () => {
                  confirmModal.close();
                  choices.forEach(c => { c.input.checked = c.role.id === selectedRoleId; });
                }
              });
              confirmModal = openDialog({
                title: 'Xác nhận thay đổi vai trò',
                body: h('div', { className: 'dialog-body' }, h('p', {}, 'Đổi vai trò sẽ tính lại danh sách quyền theo vai trò mới. Bạn có chắc chắn muốn đổi không?')),
                footer: [cancel, proceed]
              });
              return;
            }
            selectedRoleId = role.id;
            await renderMatrixForRole(selectedRoleId);
          }
        });
        return { role, input };
      });

      if (!item && selectedRoleId) {
        await renderMatrixForRole(selectedRoleId);
      }

      const form = h('form', { noValidate: true, onSubmit: event => {
        event.preventDefault(); if (save.disabled || !form.isConnected) return;
        if (!alive(...required)) { save.disabled = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang.')); return; }
        const dto = Object.fromEntries(Object.entries(fields).map(([key, value]) => [key, value.input.value.trim() || null]));
        dto.departmentId = dto.departmentId ? Number(dto.departmentId) : null;
        if (!item) {
          dto.employeeCode = null;
          const checkedRadio = choices.find(c => c.input.checked);
          const chosenRoleId = checkedRadio ? checkedRadio.role.id : selectedRoleId;
          dto.roleIds = chosenRoleId ? [chosenRoleId] : [];
          if (matrixInstance && matrixInstance.isCustom()) {
            dto.permissionCodes = matrixInstance.getSelectedCodes();
          }
        } else {
          dto.employeeCode = item.employeeCode ?? null;
        }
        if (item) dto.rowVersion = item.rowVersion;
        const errors = validateUser(dto, { isUpdate: Boolean(item) }); displayErrors(fields, errors); feedback.replaceChildren();
        if (Object.keys(errors).length) { if (errors.roleIds) feedback.append(formAlert(errors.roleIds.join(' '))); return; }
        return runWrite(writeState, { control: save, modal, feedback, fields, required,
          inputs: [...Object.values(fields).map(field => field.input), ...choices.map(choice => choice.input), ...(matrixInstance ? matrixInstance.inputs() : [])],
          expectedId: item?.id,
          write: () => item ? services.users.update(item.id, dto) : services.users.create(dto), getUser: response => response,
          onSuccess: response => saved(modal, item ? 'Đã cập nhật hồ sơ.' : ('Đã tạo tài khoản chờ kích hoạt' + (response?.employeeCode ? ' [' + response.employeeCode + ']. ' : '. ') + emailStatus(response.invitationEmailStatus)))
        });
      } }, h('div', { className: 'dialog-body' },
        h('div', { className: 'user-profile-section' },
          h('h3', { className: 'section-title' }, 'Thông tin tài khoản'),
          h('div', { className: 'form-grid' },
            add({ name: 'displayName', label: 'Họ và tên', required: true, maxLength: 200 }),
            add({ name: 'username', label: 'Tên đăng nhập', required: true, maxLength: 100 }),
            add({ name: 'email', label: 'Email', type: 'email', required: true, maxLength: 320 }),
            add({ name: 'employeeCode', label: 'Mã nhân viên', maxLength: 50, readOnly: true, placeholder: item ? '' : 'Mã sẽ được hệ thống tự động tạo khi lưu (NVxxxxxx)', hint: item ? 'Mã nhân viên không thể thay đổi sau khi tạo.' : 'Mã sẽ được hệ thống tự động tạo khi lưu (NVxxxxxx).' }),
            add({ name: 'departmentId', label: 'Phòng ban', options: departmentOptions }),
            add({ name: 'phone', label: 'Số điện thoại', type: 'tel', maxLength: 30 })
          )
        ),
        !item ? h('div', { className: 'user-role-section' },
          h('fieldset', { className: 'role-choices-box' },
            h('legend', {}, 'Vai trò người dùng (chọn một vai trò duy nhất)'),
            h('div', { className: 'radio-group role-radio-group' },
              choices.map(({ role, input }) => h('label', { className: 'role-radio-card' }, input, h('span', { className: 'role-radio-name' }, role.name)))
            ),
            h('p', { className: 'field-hint mt-2' }, 'Người dùng tự đặt mật khẩu qua liên kết email có hiệu lực 30 phút. Mỗi người dùng chỉ sở hữu một vai trò.')
          ),
          matrixMount
        ) : null,
        feedback), h('div', { className: 'dialog-footer' }, button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), save));
      modal = openDialog({ title: item ? 'Sửa hồ sơ người dùng' : 'Thêm người dùng', body: form, className: item ? '' : 'dialog--wide' });
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
        ['Quyền riêng', state.hasCustomPermissions ? (state.customPermissions?.length + ' quyền được cấp riêng') : 'Sử dụng toàn bộ quyền mặc định của vai trò'],
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
      const [state, catalog] = await Promise.all([services.users.account(row.id), allActive(services.roles)]);
      if (!alive('roles.assign', 'roles.read')) return;
      const unmapped = state.roleIds.filter(id => !catalog.some(role => role.id === id));

      const roleDetailsCache = new Map();
      async function getRoleDetail(roleId) {
        if (roleDetailsCache.has(roleId)) return roleDetailsCache.get(roleId);
        if (typeof services.roles?.get === 'function') {
          try {
            const detail = await services.roles.get(roleId);
            roleDetailsCache.set(roleId, detail);
            return detail;
          } catch { /* ignore fallback */ }
        }
        return { id: roleId, permissions: [] };
      }

      let selectedRoleId = state.roleIds[0] || (catalog.find(r => r.isActive)?.id ?? catalog[0]?.id ?? null);
      let matrixInstance = null;
      let hasReset = false;
      const matrixMount = h('div', { className: 'role-permission-matrix-mount' });

      async function renderMatrix(roleId, initialPermissions, isCustom) {
        matrixMount.replaceChildren(h('p', { className: 'meta' }, 'Đang tải ma trận quyền...'));
        const detail = await getRoleDetail(roleId);
        matrixInstance = createUserPermissionMatrix({
          roleDetail: detail,
          initialSelectedCodes: initialPermissions,
          hasCustom: isCustom,
          onChange: ({ isCustom: custom }) => {
            if (custom) hasReset = false;
          }
        });
        matrixMount.replaceChildren(matrixInstance.element);
      }

      const choices = catalog.map(role => {
        const input = h('input', {
          type: 'radio',
          name: 'roleIds',
          value: role.id,
          checked: role.id === selectedRoleId,
          onChange: async () => {
            if (role.id === selectedRoleId) return;
            if (matrixInstance?.isCustom()) {
              let confirmModal;
              const proceed = button('Xác nhận đổi', {
                kind: 'primary',
                onClick: async () => {
                  confirmModal.close();
                  selectedRoleId = role.id;
                  hasReset = false;
                  choices.forEach(c => { c.input.checked = c.role.id === selectedRoleId; });
                  await renderMatrix(selectedRoleId, null, false);
                }
              });
              const cancel = button('Giữ nguyên', {
                kind: 'secondary',
                onClick: () => {
                  confirmModal.close();
                  choices.forEach(c => { c.input.checked = c.role.id === selectedRoleId; });
                }
              });
              confirmModal = openDialog({
                title: 'Xác nhận thay đổi vai trò',
                body: h('div', { className: 'dialog-body' }, h('p', {}, 'Đổi vai trò sẽ tính lại danh sách quyền theo vai trò mới. Bạn có chắc chắn muốn đổi không?')),
                footer: [cancel, proceed]
              });
              return;
            }
            selectedRoleId = role.id;
            hasReset = false;
            await renderMatrix(selectedRoleId, null, false);
          }
        });
        return { role, input };
      });

      if (selectedRoleId) {
        await renderMatrix(selectedRoleId, state.hasCustomPermissions ? state.customPermissions : null, state.hasCustomPermissions);
      }

      const feedback = h('div', { 'aria-live': 'polite' }); let modal;
      const writeState = { pending: false, blocked: false };
      const confirm = button('Lưu vai trò', { disabled: Boolean(unmapped.length), onClick: () => {
        if (confirm.disabled || !modal.dialog.isConnected) return;
        if (!alive('roles.assign', 'roles.read')) { confirm.disabled = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang.')); return; }
        const selectedRadio = choices.find(c => c.input.checked);
        const roleId = selectedRadio ? selectedRadio.role.id : selectedRoleId;
        const dto = {
          roleIds: roleId ? [roleId] : [],
          rowVersion: state.rowVersion
        };
        if (hasReset || (matrixInstance && !matrixInstance.isCustom())) {
          dto.resetToRoleDefaults = true;
        } else if (matrixInstance && matrixInstance.isCustom()) {
          dto.permissionCodes = matrixInstance.getSelectedCodes();
        }
        const errors = validateUserRoles(dto); feedback.replaceChildren();
        if (Object.keys(errors).length) { feedback.append(formAlert(Object.values(errors).flat().join(' '))); return; }
        return runWrite(writeState, { control: confirm, modal, feedback, required: ['roles.assign', 'roles.read'],
          inputs: [...choices.map(choice => choice.input), ...(matrixInstance ? matrixInstance.inputs() : [])],
          expectedId: row.id, accountResult: true,
          write: () => services.users.replaceRoles(row.id, dto), getUser: response => response?.user,
          onSuccess: response => saved(modal, 'Đã cập nhật vai trò và quyền. JWT cũ của tài khoản đã bị thu hồi.', response.warnings)
        });
      } });
      const summaryBar = h('div', { className: 'user-role-summary-bar' },
        h('div', { className: 'user-summary-item' },
          h('span', { className: 'summary-label' }, 'Người dùng:'),
          h('strong', {}, row.displayName + ' (' + row.username + ')')
        ),
        row.employeeCode ? h('div', { className: 'user-summary-item' },
          h('span', { className: 'summary-label' }, 'Mã NV:'),
          h('span', { className: 'font-mono' }, row.employeeCode)
        ) : null,
        h('div', { className: 'user-summary-item' },
          h('span', { className: 'summary-label' }, 'Email:'),
          h('span', {}, row.email)
        ),
        row.departmentName ? h('div', { className: 'user-summary-item' },
          h('span', { className: 'summary-label' }, 'Phòng ban:'),
          h('span', {}, row.departmentName)
        ) : null
      );

      modal = openDialog({
        title: 'Vai trò & Phân quyền — ' + row.displayName,
        className: 'dialog--wide',
        body: h('div', { className: 'dialog-body' },
          summaryBar,
          h('fieldset', { className: 'role-choices-box' },
            h('legend', {}, 'Vai trò người dùng (chọn một vai trò duy nhất)'),
            h('div', { className: 'radio-group role-radio-group' },
              choices.map(({ role, input }) => h('label', { className: 'role-radio-card' }, input, h('span', { className: 'role-radio-name' }, role.name)))
            ),
            h('p', { className: 'field-hint mt-2' }, 'Mỗi người dùng chỉ sở hữu một vai trò. Bạn có thể cấp hoặc bỏ bớt các quyền riêng trong giới hạn vai trò đã chọn. Backend bảo vệ Admin cuối cùng.')
          ),
          matrixMount,
          unmapped.length ? formAlert('Có vai trò cũ không còn trong danh mục hoạt động: ' + unmapped.join(', ') + '. Cần review; giao diện không tự loại bỏ.') : null,
          feedback
        ),
        footer: [button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm]
      });
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
