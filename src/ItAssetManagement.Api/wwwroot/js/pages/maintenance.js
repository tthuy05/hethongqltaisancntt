import { h } from '../utils/dom.js';
import { button, pageHeader, dataTable, pagination, showToast } from '../components.js';
import { ServiceError } from '../services/service-error.js';
import { can, route, queryFrom, setQuery, field, formAlert, openDialog, modulePermissions } from './shared.js';

const states = { PENDING: 'Chờ xử lý', IN_PROGRESS: 'Đang xử lý', RESOLVED: 'Đã sửa xong', FAILED: 'Không sửa được', CANCELLED: 'Đã hủy' };
const priorities = { LOW: 'Thấp', MEDIUM: 'Trung bình', HIGH: 'Cao', CRITICAL: 'Khẩn cấp' };
const names = { create: 'Mở phiếu bảo trì', update: 'Cập nhật phiếu', assign: 'Giao kỹ thuật viên', start: 'Bắt đầu xử lý', resolve: 'Hoàn tất bảo trì', fail: 'Ghi nhận không sửa được', cancel: 'Hủy phiếu', cost: 'Hiệu chỉnh chi phí' };
const grants = { create: 'maintenance.create', update: 'maintenance.update', assign: 'maintenance.assign', start: 'maintenance.work', resolve: 'maintenance.resolve', fail: 'maintenance.resolve', cancel: 'maintenance.cancel', cost: 'maintenance.update' };
const terminal = item => ['RESOLVED', 'FAILED', 'CANCELLED'].includes(item.status);
const time = value => value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)) : '—';
const money = value => value == null ? 'Chưa xác định' : new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
const options = object => Object.entries(object).map(([value, label]) => ({ value, label }));
const localTime = value => { const date = new Date(value); return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16); };

export async function maintenancePage(services, id = null) {
  if (services.maintenanceEnabled !== true) throw new ServiceError(404, 'MODULE_UNAVAILABLE', 'Bảo trì chưa được mở.');
  const actor = services.auth?.session?.id, hash = window.location.hash;
  const current = () => Number.isSafeInteger(actor) && actor > 0 && services.auth?.session?.id === actor && window.location.hash === hash && services.maintenanceEnabled === true && can(services, 'maintenance.read');
  if (!current()) throw new ServiceError(403, 'FORBIDDEN', 'Bạn không có quyền xem phiếu bảo trì.');
  const raw = queryFrom(), query = {};
  const allowed = id == null ? ['page', 'pageSize', 'assetId', 'reporterId', 'technicianId', 'status', 'priority', 'from', 'to', 'keyword', 'sortBy', 'sortDirection'] : ['historyPage'];
  for (const [key, value] of Object.entries(raw)) {
    if (!allowed.includes(key)) throw new ServiceError(400, 'VALIDATION_ERROR', 'Bộ lọc bảo trì không hợp lệ.');
    query[key] = ['page', 'pageSize', 'assetId', 'reporterId', 'technicianId', 'historyPage'].includes(key) ? Number(value) : value;
  }
  const selected = id == null ? null : await services.maintenance.get(id);
  if (!current()) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên hoặc trang đã thay đổi.');
  const data = selected ? await services.maintenance.history(id, { page: query.historyPage ?? 1, pageSize: 20 }) : await services.maintenance.list(query);
  if (!current()) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên hoặc trang đã thay đổi.');
  const root = h('div'), feedback = h('div', { 'aria-live': 'polite' });
  const alive = () => root.isConnected && current();
  const policy = () => modulePermissions(services.auth.session).maintenance;
  const navigate = values => { if (alive()) setQuery(id == null ? '/maintenance' : '/maintenance/' + id, values); };
  async function edit(mode, row = null) {
    if (!alive() || !can(services, grants[mode])) return;
    try {
      const fresh = row ? await services.maintenance.get(row.id) : null;
      if (alive() && can(services, grants[mode])) maintenanceDialog(services, mode, fresh, alive);
    } catch (error) { if (alive() && error.code !== 'SESSION_CHANGED') feedback.replaceChildren(formAlert(error.message)); }
  }
  function actions(item) {
    const list = [button('Chi tiết', { kind: 'ghost', onClick: () => { if (alive()) route('/maintenance/' + item.id); } })];
    for (const mode of ['update', 'assign', 'start', 'resolve', 'fail', 'cancel', 'cost']) {
      const eligible = mode === 'cost' ? terminal(item) && policy().writeCost : !terminal(item) &&
        (mode !== 'assign' || policy().globalScope) && (mode !== 'cancel' || policy().globalScope) &&
        (mode !== 'start' || item.status === 'PENDING' && item.assignedToUserId > 0) &&
        (!['resolve', 'fail'].includes(mode) || item.status === 'IN_PROGRESS');
      if (eligible && can(services, grants[mode])) list.push(button(names[mode], { kind: 'secondary', onClick: () => edit(mode, item) }));
    }
    return h('div', { className: 'actions' }, list);
  }
  root.append(pageHeader(selected ? 'Chi tiết bảo trì' : 'Bảo trì tài sản', policy().globalScope ? 'Theo dõi sự cố, kỹ thuật viên, quá trình xử lý và kết quả.' : 'Chỉ hiển thị các phiếu được giao cho bạn xử lý.', {
    actions: [selected ? button('Về danh sách', { kind: 'secondary', onClick: () => { if (alive()) route('/maintenance'); } }) : null,
      can(services, 'maintenance.create') && can(services, 'assets.read') ? button(names.create, { onClick: () => edit('create') }) : null].filter(Boolean)
  }), feedback);
  if (selected) {
    root.append(h('section', { className: 'card' }, h('div', { className: 'card-body' },
      h('h2', {}, selected.ticketCode + ' — ' + selected.title), h('p', {}, (selected.assetCode || '') + ' — ' + (selected.assetName || 'Tài sản')),
      h('p', {}, states[selected.status], ' · Ưu tiên: ', priorities[selected.priority]),
      h('p', {}, 'Kỹ thuật viên: ', selected.assignedToName || (selected.assignedToUserId ? 'Chưa có tên kỹ thuật viên' : 'Chưa được giao')),
      h('p', {}, 'Mở: ', time(selected.openedAtUtc), ' · Hạn xử lý: ', time(selected.dueAtUtc)),
      h('p', {}, 'Bắt đầu: ', time(selected.startedAtUtc), ' · Kết thúc: ', time(selected.completedAtUtc)),
      h('p', {}, selected.description), selected.resolution ? h('p', {}, 'Kết quả: ', selected.resolution) : null,
      policy().readCost ? h('p', {}, 'Dự kiến: ', money(selected.estimatedCost), ' · Thực tế: ', money(selected.actualCost)) : null,
      actions(selected))));
    root.append(h('h2', {}, 'Lịch sử xử lý'), dataTable({ label: 'Lịch sử bảo trì', rows: data.items, columns: [
      { label: 'Thời điểm', render: row => time(row.performedAtUtc) }, { key: 'eventType', label: 'Sự kiện' },
      { label: 'Trạng thái', render: row => states[row.toStatus] || '—' }, { key: 'comment', label: 'Nội dung' },
      ...(policy().readCost ? [{ label: 'Chi phí', render: row => money(row.cost) }] : [])
    ] }), pagination(data, page => navigate({ historyPage: page })));
  } else {
    const keyword = field({ name: 'maintenanceKeyword', label: 'Tìm phiếu', type: 'search', maxLength: 200, value: query.keyword || '' });
    const status = field({ name: 'maintenanceStatus', label: 'Trạng thái', value: query.status || '', options: [{ value: '', label: 'Tất cả' }, ...options(states)] });
    const priority = field({ name: 'maintenancePriority', label: 'Ưu tiên', value: query.priority || '', options: [{ value: '', label: 'Tất cả' }, ...options(priorities)] });
    root.append(h('form', { className: 'filters', onSubmit: event => { event.preventDefault(); navigate({ ...query, page: 1, keyword: keyword.input.value.trim() || undefined, status: status.input.value || undefined, priority: priority.input.value || undefined }); } },
      keyword.element, status.element, priority.element, button('Lọc', { type: 'submit' }), button('Xóa bộ lọc', { kind: 'secondary', onClick: () => navigate({}) })),
    dataTable({ label: 'Phiếu bảo trì', rows: data.items, columns: [
      { key: 'ticketCode', label: 'Mã phiếu' }, { key: 'title', label: 'Tiêu đề' }, { label: 'Tài sản', render: row => `${row.assetCode || ''} — ${row.assetName || 'Tài sản'}` },
      { label: 'Trạng thái', render: row => states[row.status] }, { label: 'Ưu tiên', render: row => priorities[row.priority] },
      { label: 'Kỹ thuật viên', render: row => row.assignedToName || (row.assignedToUserId ? 'Chưa có tên' : 'Chưa giao') },
      { label: 'Thao tác', render: actions }
    ] }), pagination(data, page => navigate({ ...query, page })));
  }
  return root;
}

function maintenanceDialog(services, mode, row, ownerAlive) {
  let modal, closed = false, pending = false, blocked = false, lookupRevision = 0;
  const actor = services.auth.session.id, hash = window.location.hash, fields = {};
  const current = () => !closed && ownerAlive() && services.auth?.session?.id === actor && window.location.hash === hash &&
    services.maintenanceEnabled === true && can(services, grants[mode]);
  const finance = () => modulePermissions(services.auth.session).maintenance.writeCost;
  const body = h('div', { className: 'dialog-body' }), feedback = h('div', { 'aria-live': 'polite' });
  const add = (key, label, props = {}) => { const control = field({ name: 'maint-' + key, label, ...props }); fields[key] = control; body.append(control.element); return control; };
  const resetChoices = (control, rows, label) => { control.input.replaceChildren(h('option', { value: '' }, label), ...rows.map(item => h('option', { value: item.id }, item.label))); control.input.value = ''; };
  if (row) body.append(h('p', {}, row.ticketCode + ' — ' + row.title));
  if (mode === 'create' || mode === 'assign') {
    add('search', mode === 'create' ? 'Tìm tài sản' : 'Tìm kỹ thuật viên', { type: 'search', maxLength: 200 });
    if (mode === 'create') add('assetStatus', 'Trạng thái tài sản', { value: 'InStock', options: [{ value: 'InStock', label: 'Trong kho' }, { value: 'InUse', label: 'Đang sử dụng' }, { value: 'Broken', label: 'Hỏng' }] });
    add('target', mode === 'create' ? 'Tài sản' : 'Kỹ thuật viên', { required: true, options: [{ value: '', label: 'Chọn kết quả' }] });
    const lookup = async () => {
      if (!current() || mode === 'create' && !can(services, 'assets.read')) return;
      const revision = ++lookupRevision;
      resetChoices(fields.target, [], 'Đang tìm…');
      try {
        const result = mode === 'create' ? await services.assets.list({ keyword: fields.search.input.value.trim(), status: fields.assetStatus.input.value, page: 1, pageSize: 20 }) :
          await services.maintenance.technicians({ keyword: fields.search.input.value.trim(), page: 1, pageSize: 20 });
        if (!current() || revision !== lookupRevision || mode === 'create' && !can(services, 'assets.read')) return;
        resetChoices(fields.target, result.items.map(item => ({ id: item.id, label: mode === 'create' ? `${item.assetCode} — ${item.name}` : item.displayName })), 'Chọn kết quả');
      } catch (error) { if (current() && revision === lookupRevision && error.code !== 'SESSION_CHANGED') feedback.replaceChildren(formAlert(error.message)); }
    };
    body.append(button('Tìm', { kind: 'secondary', onClick: lookup }));
    fields.assetStatus?.input.addEventListener('change', () => { void lookup(); });
    // Start only after the dialog is connected; no eager lookup on hidden/disabled pages.
    fields.target.load = lookup;
  }
  if (mode === 'create' || mode === 'update') {
    add('title', 'Tiêu đề', { required: true, maxLength: 200, value: row?.title || '' });
    add('description', 'Mô tả sự cố', { type: 'textarea', required: true, maxLength: 2000, value: row?.description || '' });
    add('priority', 'Mức ưu tiên', { value: row?.priority || 'MEDIUM', options: options(priorities) });
    add('dueAt', 'Hạn xử lý', { type: 'datetime-local', value: row?.dueAtUtc ? localTime(row.dueAtUtc) : '' });
    if (finance()) add('estimatedCost', 'Chi phí dự kiến', { type: 'number', value: '', hint: row ? 'Để trống để giữ nguyên.' : 'Để trống nếu chưa xác định.' });
  }
  if (['resolve', 'fail', 'cost', 'update'].includes(mode) && finance()) {
    add('actualCost', 'Chi phí thực tế', { type: 'number', value: '', hint: 'Để trống để giữ nguyên; số 0 có nghĩa đã xác nhận không phát sinh.' });
    const clear = h('input', { type: 'checkbox', id: 'maint-clear-cost' }); fields.clearCost = { input: clear };
    body.append(h('label', { htmlFor: 'maint-clear-cost' }, clear, ' Đặt lại chi phí thực tế về chưa xác định'));
  }
  if (mode !== 'create') add('note', ['resolve', 'fail'].includes(mode) ? 'Kết quả xử lý' : 'Lý do / ghi chú', { type: 'textarea', required: true, maxLength: ['resolve', 'fail'].includes(mode) ? 2000 : 1000 });
  body.append(feedback);
  const confirm = button('Xác nhận', { onClick: () => submit() });
  async function submit() {
    if (!current() || pending || blocked) return;
    const dto = mode === 'create' ? {} : { rowVersion: row.rowVersion };
    try {
      if (mode === 'create' || mode === 'assign') {
        const target = Number(fields.target.input.value);
        if (!Number.isSafeInteger(target) || target <= 0) throw new Error(mode === 'create' ? 'Chọn tài sản hợp lệ.' : 'Chọn kỹ thuật viên hợp lệ.');
        dto[mode === 'create' ? 'assetId' : 'technicianId'] = target;
      }
      if (mode === 'create' || mode === 'update') {
        dto.title = fields.title.input.value.trim(); dto.description = fields.description.input.value.trim(); dto.priority = fields.priority.input.value;
        if (!dto.title || !dto.description) throw new Error('Nhập tiêu đề và mô tả sự cố.');
        if (fields.dueAt.input.value) dto.dueAt = new Date(fields.dueAt.input.value).toISOString();
        else if (mode === 'update' && row.dueAtUtc) dto.dueAt = null;
      }
      if (mode !== 'create') {
        const note = fields.note.input.value.trim(); if (!note) throw new Error('Nhập lý do hoặc kết quả xử lý.');
        dto[mode === 'resolve' ? 'resolution' : ['fail', 'cancel'].includes(mode) ? 'reason' : 'note'] = note;
      }
      for (const key of ['estimatedCost', 'actualCost']) if (fields[key]) {
        if (!finance()) throw new Error('Quyền chi phí đã thay đổi. Vui lòng tải lại.');
        const value = fields[key].input.value.trim();
        if (value !== '') { const cost = Number(value); if (!Number.isFinite(cost) || cost < 0 || !Number.isSafeInteger(Math.round(cost * 100))) throw new Error('Chi phí không hợp lệ hoặc vượt giới hạn nhập chính xác.'); dto[key === 'actualCost' && mode === 'fail' ? 'cost' : key] = cost; }
      }
      if (fields.clearCost?.input.checked) { if (!finance()) throw new Error('Quyền chi phí đã thay đổi.'); dto[mode === 'fail' ? 'cost' : 'actualCost'] = null; }
      if (mode === 'cost' && !Object.hasOwn(dto, 'actualCost')) throw new Error('Nhập chi phí hoặc chọn đặt lại về chưa xác định.');
      if (mode === 'create' && !can(services, 'assets.read')) throw new Error('Quyền xem tài sản đã thay đổi.');
    } catch (error) { feedback.replaceChildren(formAlert(error.message)); return; }
    pending = true; confirm.disabled = true; confirm.setAttribute('aria-busy', 'true'); feedback.replaceChildren();
    try {
      if (mode === 'create') await services.maintenance.create(dto);
      else await services.maintenance[mode === 'cost' ? 'update' : mode](row.id, dto);
      if (!current() || !modal.dialog.isConnected) return;
      modal.close(); showToast('Đã cập nhật phiếu bảo trì.'); window.dispatchEvent(new Event('frontend-refresh'));
    } catch (error) {
      if (!current() || !modal.dialog.isConnected || error.code === 'SESSION_CHANGED') return;
      if (error.status === 400) feedback.replaceChildren(formAlert(error.message));
      else { blocked = true; feedback.replaceChildren(formAlert('Chưa xác nhận được kết quả hoặc dữ liệu đã thay đổi. Hãy đóng và tải lại trước khi thao tác tiếp.'),
        button('Đóng và tải lại', { kind: 'secondary', onClick: () => { if (current()) { modal.close(); window.dispatchEvent(new Event('frontend-refresh')); } } })); }
    } finally { pending = false; confirm.removeAttribute('aria-busy'); if (!closed) confirm.disabled = blocked || !current(); }
  }
  modal = openDialog({ title: names[mode], body, footer: [button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm],
    onClose: () => { closed = true; lookupRevision++; } });
  if (fields.target) void fields.target.load();
}
