import { h } from '../utils/dom.js';
import { auditQuery, localDateInput, localDateToUtc, auditDateErrors } from '../utils/audit-filters.js';
import { button, pageHeader, dataTable, pagination, showToast } from '../components.js';
import { can, hasPermission, setQuery, route, field, displayErrors, openDialog, busy, money, statusLabels, allCatalog } from './shared.js';

const outcomes = { SUCCESS: ['Thành công', 'green'], FAILURE: ['Thất bại', 'red'], DENIED: ['Bị từ chối', 'amber'] };
const timestamp = value => new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'medium' }).format(new Date(value));
const labels = { assetTypeId: 'ID loại tài sản', owningDepartmentId: 'Phòng ban sở hữu', parentDepartmentId: 'Phòng ban cha', departmentId: 'Phòng ban', userId: 'ID người dùng', roleId: 'ID vai trò', roleCount: 'Số vai trò', defaultUsefulLifeMonths: 'Thời gian sử dụng (tháng)', currentStatus: 'Trạng thái tài sản', purchaseCost: 'Chi phí mua', isActive: 'Đang hoạt động', isArchived: 'Đã lưu trữ', isAdminLocked: 'Khóa bởi Admin', emailChanged: 'Email đã đổi', usernameChanged: 'Tên đăng nhập đã đổi', displayNameChanged: 'Tên hiển thị đã đổi', employeeCodeChanged: 'Mã nhân viên đã đổi', phoneChanged: 'Điện thoại đã đổi', adminRoleAssigned: 'Có vai trò Admin', reasonProvided: 'Có lý do', auditLogId: 'ID nhật ký được xem', page: 'Trang', pageSize: 'Số mục mỗi trang', returnedCount: 'Số mục trả về' };
const dbStatuses = { IN_STOCK: 'InStock', IN_USE: 'InUse', MAINTENANCE: 'Maintenance', BROKEN: 'Broken', RETIRED: 'Retired' };
function outcome(value) { const [label, tone] = outcomes[value] || ['Chưa xác định', 'neutral']; return h('span', { className: `badge ${tone}` }, label); }
function actor(row) { return row.actorUserId ? `Người dùng #${row.actorUserId}` : row.actorType === 'SYSTEM' ? 'Hệ thống' : 'Chưa đăng nhập'; }
function snapshotValue(key, value, departments = []) {
  if (value == null) return '—';
  if (['departmentId', 'owningDepartmentId', 'parentDepartmentId'].includes(key)) return departments.find(d => d.id === value)?.name || 'Phòng ban không còn khả dụng';
  if (typeof value === 'boolean') return value ? 'Có' : 'Không';
  if (key === 'currentStatus') return statusLabels[dbStatuses[value]] || '—';
  if (typeof value !== 'number' || !Number.isFinite(value)) return '—';
  return key === 'purchaseCost' ? money(value) : String(value);
}
export async function auditLogPage(services) {
  const actorId = services.auth?.session?.id;
  const pageHash = window.location.hash;
  const requireReader = () => {
    const session = services.auth?.session;
    if (!Number.isSafeInteger(actorId) || actorId <= 0 || session?.id !== actorId || window.location.hash !== pageHash)
      throw Object.assign(new Error('Phiên đăng nhập hoặc trang đã thay đổi.'), { status: 401, code: 'SESSION_CHANGED' });
    if (!session.roles?.includes('ADMIN_IT') || !hasPermission(session, 'audit-logs.read') || !services.auditLogs)
      throw Object.assign(new Error('Chỉ Admin IT được phép xem nhật ký thao tác.'), { status: 403 });
  };
  requireReader();
  const query = auditQuery(window.location.hash.split('?')[1] || '');
  delete query.entityType;
  delete query.entityId;
  const result = await services.auditLogs.list(query);
  requireReader();
  let departments = [];
  if (services.departments && can(services, 'departments.read')) {
    try { departments = await allCatalog(services.departments); }
    catch (error) {
      if (error?.status === 401 || error?.code === 'SESSION_CHANGED') throw error;
      // Department names are an optional lookup, not a prerequisite to audit.read.
    }
    requireReader();
    if (!can(services, 'departments.read')) departments = [];
  }
  const root = h('div', { className: 'audit-page' });
  const active = () => root.isConnected && window.location.hash === pageHash;
  const currentReader = () => {
    const session = services.auth?.session;
    return session?.id === actorId && session.roles?.includes('ADMIN_IT') && hasPermission(session, 'audit-logs.read');
  };
  const fields = {};
  const add = config => { const control = field(config); fields[config.name] = control; return control.element; };
  const filters = h('form', { className: 'filters audit-filters', noValidate: true, onSubmit: event => {
    event.preventDefault();
    const values = Object.fromEntries(Object.entries(fields).map(([key, control]) => [key, control.input.value.trim()]));
    const from = localDateToUtc(values.from), to = localDateToUtc(values.to);
    const errors = auditDateErrors(from, to);
    if (values.userId && (!Number.isSafeInteger(Number(values.userId)) || Number(values.userId) <= 0)) errors.userId = ['ID phải là số nguyên dương.'];
    displayErrors(fields, errors);
    if (Object.keys(errors).length) return;
    setQuery('/audit-logs', { ...values, from, to, page: 1, pageSize: query.pageSize });
  } },
  add({ name: 'from', label: 'Từ thời điểm', type: 'datetime-local', required: true, value: localDateInput(query.from) }),
  add({ name: 'to', label: 'Đến trước thời điểm', type: 'datetime-local', required: true, value: localDateInput(query.to) }),
  add({ name: 'outcome', label: 'Kết quả', value: query.outcome, options: [{ value: '', label: 'Tất cả kết quả' }, ...Object.entries(outcomes).map(([value, [label]]) => ({ value, label }))] }),
  add({ name: 'userId', label: 'ID người thao tác', type: 'number', value: query.userId, hint: 'Để trống để xem tất cả.' }),
  add({ name: 'action', label: 'Mã hành động', maxLength: 150, value: query.action, hint: 'Khớp chính xác, ví dụ assets.create.' }),
  add({ name: 'correlationId', label: 'Mã liên kết yêu cầu', maxLength: 36, value: query.correlationId }),
  add({ name: 'sortBy', label: 'Sắp xếp theo', value: query.sortBy, options: [{ value: 'occurredAt', label: 'Thời gian' }, { value: 'id', label: 'ID nhật ký' }] }),
  add({ name: 'sortDirection', label: 'Thứ tự', value: query.sortDirection, options: [{ value: 'desc', label: 'Mới nhất trước' }, { value: 'asc', label: 'Cũ nhất trước' }] }),
  h('div', { className: 'actions audit-filter-actions' }, button('Tra cứu', { type: 'submit', iconName: 'search' }), button('7 ngày gần nhất', { kind: 'secondary', onClick: () => route('/audit-logs') })));
  for (const key of ['from', 'to']) fields[key].input.step = '1';
  fields.userId.input.min = '1'; fields.userId.input.step = '1';
  const pendingDetails = new Set();
  async function detail(row, control) {
    if (pendingDetails.has(control) || !active() || !currentReader()) return;
    pendingDetails.add(control);
    try {
      await busy(control, async () => {
        try {
          const item = await services.auditLogs.get(row.id);
          // The actor, Admin role and route must still match the request that opened this page.
          if (!active() || !currentReader()) return;
          if (!Number.isSafeInteger(item?.id) || item.id !== row.id)
            throw new Error('Invalid audit detail identity.');
          const pairs = [['ID nhật ký', item.id], ['Thời gian', timestamp(item.occurredAt)], ['Người thao tác', actor(item)], ['Hành động', item.action], ['Kết quả', outcome(item.outcome)], ['Mã liên kết', item.correlationId]];
          const oldValues = item.oldValues || {}, newValues = item.newValues || {};
          const keys = Object.keys(labels).filter(key => (key !== 'purchaseCost' || can(services, 'assets.cost.read')) && (Object.hasOwn(oldValues, key) || Object.hasOwn(newValues, key)));
          const visibleDepartments = can(services, 'departments.read') ? departments : [];
          const body = h('div', { className: 'dialog-body' }, h('dl', { className: 'user-details' }, pairs.map(([label, value]) => h('div', {}, h('dt', {}, label), h('dd', {}, value)))),
            h('h3', {}, 'Dữ liệu thay đổi'), item.snapshotsRedacted ? h('p', { className: 'meta' }, 'Một số thông tin đã được ẩn theo quyền và chính sách dữ liệu.') : null,
            dataTable({ label: 'Dữ liệu thay đổi an toàn', emptyMessage: 'Không có dữ liệu thay đổi được phép hiển thị.', rows: keys.map(key => ({ id: key, key })), columns: [{ key: 'key', label: 'Thông tin', render: entry => labels[entry.key] }, { key: 'old', label: 'Trước', render: entry => snapshotValue(entry.key, oldValues[entry.key], visibleDepartments) }, { key: 'new', label: 'Sau', render: entry => snapshotValue(entry.key, newValues[entry.key], visibleDepartments) }] }));
          if (!active() || !currentReader()) return;
          const modal = openDialog({ title: 'Chi tiết nhật ký #' + item.id, body });
          modal.dialog.classList.add('audit-detail-dialog');
        } catch (error) {
          if (!active()) return;
          if (error?.status === 401 && !services.auth?.session) { route('/login'); return; }
          if (currentReader()) showToast('Không thể tải chi tiết nhật ký. Vui lòng kiểm tra lại phiên và thử sau.', 'error');
        }
      });
    } finally { pendingDetails.delete(control); }
  }
  root.append(pageHeader('Nhật ký thao tác', 'Tra cứu người thao tác, hành động và kết quả. Chỉ xem, không sửa hoặc xóa nhật ký.', { eyebrow: 'Quản trị' }),
    h('section', { className: 'card filters-card' }, filters, h('p', { className: 'meta audit-filter-note' }, 'Giờ theo thiết bị của bạn; tối đa 31 ngày mỗi lần tra cứu. Mỗi lần xem được ghi nhận vào nhật ký.')),
    h('section', { className: 'card' }, dataTable({ label: 'Nhật ký thao tác', rows: result.items, columns: [
      { key: 'occurredAt', label: 'Thời gian', render: row => timestamp(row.occurredAt) },
      { key: 'actor', label: 'Người thao tác', render: actor },
      { key: 'action', label: 'Hành động', className: 'audit-code' },
      { key: 'outcome', label: 'Kết quả', render: row => outcome(row.outcome) },
      { key: 'actions', label: 'Chi tiết', render: row => { const control = button('Xem', { kind: 'ghost', iconName: 'eye', onClick: () => detail(row, control) }); return control; } },
    ] }), pagination(result, page => setQuery('/audit-logs', { ...query, page }))));
  return root;
}
