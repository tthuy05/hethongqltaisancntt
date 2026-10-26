import { h } from '../utils/dom.js';
import { button, pageHeader, dataTable, pagination, badge, showToast } from '../components.js';
import { ServiceError } from '../services/service-error.js';
import { can, route, queryFrom, setQuery, field, formAlert, openDialog, displayErrors } from './shared.js';

const required = { assign: 'assignments.assign', return: 'assignments.return', transfer: 'assignments.transfer' };
const time = value => value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)) : '—';
const target = item => item.assignedUserName || item.assignedDepartmentName || 'Không có tên người/phòng ban nhận';
const positive = value => Number.isSafeInteger(value) && value > 0;
const utcInput = value => value ? new Date(value).toISOString() : undefined;
function query() {
  const raw = queryFrom(), result = {};
  const allowed = ['page', 'pageSize', 'assetId', 'userId', 'departmentId', 'status', 'from', 'to', 'sortBy', 'sortDirection'];
  for (const [key, value] of Object.entries(raw)) {
    if (!allowed.includes(key)) throw new ServiceError(400, 'VALIDATION_ERROR', 'Bộ lọc cấp phát không hợp lệ.');
    result[key] = ['page', 'pageSize', 'assetId', 'userId', 'departmentId'].includes(key) ? Number(value) : value;
  }
  return result;
}

export async function assignmentsPage(services, id = null) {
  if (services.assignmentEnabled !== true) throw new ServiceError(404, 'MODULE_UNAVAILABLE', 'Cấp phát chưa được mở.');
  const actor = services.auth?.session?.id, hash = window.location.hash;
  const current = () => services.assignmentEnabled === true && positive(actor) && services.auth?.session?.id === actor &&
    window.location.hash === hash && can(services, 'assignments.read');
  const verify = () => {
    if (!positive(actor) || services.auth?.session?.id !== actor || window.location.hash !== hash) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên hoặc trang đã thay đổi.');
    if (!current()) throw new ServiceError(403, 'FORBIDDEN', 'Bạn không có quyền xem cấp phát.');
  };
  verify();
  const filters = query();
  const selected = id == null ? null : await services.assignments.get(id);
  verify();
  const data = selected ? await services.assignments.history(selected.assetId, Object.fromEntries(Object.entries(filters).filter(([key]) => !['assetId', 'userId', 'departmentId'].includes(key)))) : await services.assignments.list(filters);
  verify();
  const root = h('div');
  const alive = () => root.isConnected && current();
  const pagePath = selected ? '/assignments/' + id : '/assignments';
  const navigate = values => { if (alive()) setQuery(pagePath, values); };
  const canCreate = () => can(services, required.assign) && can(services, 'assets.read') && (can(services, 'users.lookup') || can(services, 'departments.read'));
  const feedback = h('div', { 'aria-live': 'polite' });
  async function edit(mode, row = null) {
    if (!alive() || !can(services, required[mode]) || mode === 'assign' && !canCreate()) return;
    try {
      const fresh = row ? await services.assignments.get(row.id) : null;
      if (!alive() || !can(services, required[mode])) return;
      if (fresh?.returnedAt) { feedback.replaceChildren(formAlert('Cấp phát đã được đóng. Hãy tải lại danh sách.')); return; }
      assignmentDialog(services, mode, fresh, alive);
    } catch (error) {
      if (alive() && error.code !== 'SESSION_CHANGED') feedback.replaceChildren(formAlert(error.message || 'Không thể tải cấp phát.'));
    }
  }
  function actions(row) {
    return h('div', { className: 'actions' },
      button('Chi tiết', { kind: 'ghost', onClick: () => { if (alive()) route('/assignments/' + row.id); } }),
      !row.returnedAt && can(services, required.return) ? button('Thu hồi', { kind: 'secondary', onClick: () => edit('return', row) }) : null,
      !row.returnedAt && can(services, required.transfer) && (can(services, 'users.lookup') || can(services, 'departments.read')) ? button('Điều chuyển', { kind: 'secondary', onClick: () => edit('transfer', row) }) : null);
  }
  root.append(pageHeader(selected ? 'Chi tiết cấp phát' : 'Cấp phát tài sản', 'Theo dõi người nhận, thời điểm bàn giao, hạn trả và lịch sử. Thời gian hiển thị theo giờ Việt Nam.', {
    actions: [selected ? button('Về danh sách', { kind: 'secondary', onClick: () => { if (alive()) route('/assignments'); } }) : null,
      canCreate() ? button('Cấp phát tài sản', { onClick: () => edit('assign') }) : null].filter(Boolean)
  }), feedback);
  if (selected) root.append(h('section', { className: 'card' }, h('div', { className: 'card-body' },
    h('h2', {}, `${selected.assetCode || ''} — ${selected.assetName || 'Tài sản'}`), h('p', {}, 'Người nhận: ', target(selected)),
    h('p', {}, 'Cấp lúc: ', time(selected.assignedAt), ' · Hạn trả: ', time(selected.expectedReturnAt)),
    h('p', {}, 'Thu hồi lúc: ', time(selected.returnedAt)), h('p', {}, selected.assignmentNote || ''), h('p', {}, selected.returnNote || ''), actions(selected))));
  const state = field({ name: 'assignmentStatus', label: 'Trạng thái', value: filters.status || '', options: [
    { value: '', label: 'Tất cả' }, { value: 'Active', label: 'Đang cấp phát' }, { value: 'Returned', label: 'Đã thu hồi/điều chuyển' }] });
  const from = field({ name: 'assignmentFrom', label: 'Cấp từ', type: 'datetime-local' });
  const to = field({ name: 'assignmentTo', label: 'Cấp đến', type: 'datetime-local' });
  const sort = field({ name: 'assignmentSort', label: 'Sắp xếp', value: filters.sortDirection || 'desc', options: [
    { value: 'desc', label: 'Cấp mới nhất' }, { value: 'asc', label: 'Cấp cũ nhất' }] });
  const localTime = value => { const date = new Date(value); return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16); };
  if (filters.from) from.input.value = localTime(filters.from);
  if (filters.to) to.input.value = localTime(filters.to);
  root.append(h('form', { className: 'filters', onSubmit: event => {
    event.preventDefault(); if (!alive()) return;
    try {
      const start = utcInput(from.input.value), end = utcInput(to.input.value);
      if (start && end && Date.parse(start) > Date.parse(end)) throw new Error('Thời điểm kết thúc phải sau thời điểm bắt đầu.');
      navigate({ ...filters, page: 1, status: state.input.value || undefined, from: start, to: end, sortBy: 'assignedAt', sortDirection: sort.input.value });
    } catch (error) { feedback.replaceChildren(formAlert(error.message)); }
  } }, state.element, from.element, to.element, sort.element, button('Lọc', { type: 'submit' }),
  button('Xóa bộ lọc', { kind: 'secondary', onClick: () => navigate({}) })));
  root.append(dataTable({ columns: [
    { label: 'Tài sản', render: row => `${row.assetCode || ''} — ${row.assetName || 'Tài sản'}` },
    { label: 'Người/phòng ban nhận', render: target }, { label: 'Cấp lúc', render: row => time(row.assignedAt) },
    { label: 'Hạn trả dự kiến', render: row => time(row.expectedReturnAt) },
    { label: 'Trạng thái', render: row => badge(row.returnedAt ? 'Đã đóng' : 'Đang cấp phát') },
    { label: 'Thao tác', render: actions }
  ], rows: data.items, emptyMessage: 'Chưa có cấp phát phù hợp với bộ lọc.', label: 'Lịch sử cấp phát' }),
  pagination(data, page => navigate({ ...filters, page })));
  return root;
}

function assignmentDialog(services, mode, row, ownerAlive) {
  const actor = services.auth.session.id, openedHash = window.location.hash;
  let closed = false, pending = false, blocked = false, searchSequence = 0, assetSequence = 0;
  let modal;
  const permission = required[mode];
  const current = () => !closed && ownerAlive() && services.auth?.session?.id === actor &&
    window.location.hash === openedHash && services.assignmentEnabled === true && can(services, permission);
  const feedback = h('div', { 'aria-live': 'polite' });
  const fields = { note: field({ name: 'assignmentNote', label: mode === 'assign' ? 'Ghi chú bàn giao' : 'Lý do / ghi chú', type: 'textarea', maxLength: 1000, required: mode !== 'assign' }) };
  const body = h('div', { className: 'dialog-body' }, row ? h('p', {}, `${row.assetCode || ''} — ${row.assetName || ''} · ${target(row)}`) : null);
  const empty = label => [{ value: '', label }];
  let asset, kind, recipient, search, expected;
  const replaceOptions = (input, values, label) => { input.replaceChildren(...[...empty(label), ...values].map(item => h('option', { value: item.value }, item.label))); input.value = ''; };
  async function lookupAssets() {
    if (!current() || !can(services, 'assets.read')) return;
    const sequence = ++assetSequence;
    try {
      const result = await services.assets.list({ keyword: fields.assetSearch.input.value.trim(), status: 'InStock', page: 1, pageSize: 20 });
      if (!current() || !can(services, 'assets.read') || sequence !== assetSequence) return;
      replaceOptions(asset.input, result.items.filter(item => !item.isArchived).map(item => ({ value: item.id, label: `${item.assetCode} — ${item.name}` })), 'Chọn tài sản trong kho');
    } catch (error) { if (current() && sequence === assetSequence && error.code !== 'SESSION_CHANGED') feedback.replaceChildren(formAlert(error.message)); }
  }
  async function lookupRecipients() {
    if (!current()) return;
    const sequence = ++searchSequence, type = kind.input.value;
    const grant = type === 'user' ? 'users.lookup' : 'departments.read';
    if (!can(services, grant)) return;
    replaceOptions(recipient.input, [], 'Đang tìm…');
    try {
      const result = type === 'user' ? await services.users.lookup({ keyword: search.input.value.trim(), page: 1, pageSize: 20 }) :
        await services.departments.list({ keyword: search.input.value.trim(), status: 'Active', page: 1, pageSize: 20 });
      if (!current() || sequence !== searchSequence || kind.input.value !== type || !can(services, grant)) return;
      replaceOptions(recipient.input, result.items.map(item => ({ value: item.id, label: item.displayName || item.name })), 'Chọn người/phòng ban nhận');
    } catch (error) { if (current() && sequence === searchSequence && error.code !== 'SESSION_CHANGED') feedback.replaceChildren(formAlert(error.message)); }
  }
  if (mode === 'assign') {
    fields.assetSearch = field({ name: 'assetSearch', label: 'Tìm tài sản trong kho', type: 'search', maxLength: 200 });
    asset = field({ name: 'assignmentAsset', label: 'Tài sản', required: true, options: empty('Chọn tài sản trong kho') });
    body.append(fields.assetSearch.element, button('Tìm tài sản', { kind: 'secondary', onClick: lookupAssets }), asset.element);
  }
  if (mode !== 'return') {
    const choices = [can(services, 'users.lookup') ? { value: 'user', label: 'Nhân viên' } : null,
      can(services, 'departments.read') ? { value: 'department', label: 'Phòng ban' } : null].filter(Boolean);
    kind = field({ name: 'assignmentTargetType', label: 'Cấp cho', value: choices[0]?.value || '', options: choices });
    recipient = field({ name: 'assignmentRecipient', label: 'Người/phòng ban nhận', required: true, options: empty('Chọn người/phòng ban nhận') });
    search = field({ name: 'recipientSearch', label: 'Tìm người/phòng ban nhận', type: 'search', maxLength: 200 });
    kind.input.addEventListener('change', () => { search.input.value = ''; void lookupRecipients(); });
    expected = field({ name: 'expectedReturnAt', label: 'Hạn trả dự kiến', type: 'datetime-local' });
    body.append(kind.element, search.element, button('Tìm người nhận', { kind: 'secondary', onClick: lookupRecipients }), recipient.element, expected.element);
  }
  if (mode !== 'assign') {
    fields.condition = field({ name: 'returnCondition', label: 'Tình trạng khi bàn giao / thu hồi', maxLength: 200 });
    body.append(fields.condition.element);
  }
  const form = h('form', { onSubmit: event => { event.preventDefault(); void submit(); } }, fields.note.element, feedback);
  body.append(form);
  const confirm = button(mode === 'assign' ? 'Xác nhận cấp phát' : mode === 'return' ? 'Xác nhận thu hồi' : 'Xác nhận điều chuyển', { onClick: () => submit() });
  async function submit() {
    if (!current() || pending || blocked) return;
    const type = kind?.input.value;
    if (mode === 'assign' && !can(services, 'assets.read') || mode !== 'return' && !can(services, type === 'user' ? 'users.lookup' : 'departments.read')) {
      blocked = true; confirm.disabled = true; feedback.replaceChildren(formAlert('Quyền đã thay đổi. Vui lòng tải lại.')); return;
    }
    const dto = { note: fields.note.input.value.trim() };
    try {
      if (mode !== 'return') {
        const recipientId = Number(recipient.input.value);
        if (!positive(recipientId)) throw new Error('Chọn người/phòng ban nhận.');
        dto[type === 'user' ? 'userId' : 'departmentId'] = recipientId;
        if (expected.input.value) dto.expectedReturnAt = utcInput(expected.input.value);
      }
      if (mode === 'assign') { dto.assetId = Number(asset.input.value); if (!positive(dto.assetId)) throw new Error('Chọn tài sản trong kho.'); }
      else { dto.rowVersion = row.rowVersion; dto[mode === 'return' ? 'condition' : 'returnCondition'] = fields.condition.input.value.trim() || undefined; }
      if (mode !== 'assign' && !dto.note) throw new Error('Nhập lý do bàn giao/thu hồi.');
    } catch (error) { feedback.replaceChildren(formAlert(error.message)); return; }
    pending = true; confirm.disabled = true; confirm.setAttribute('aria-busy', 'true'); feedback.replaceChildren();
    try {
      if (mode === 'assign') await services.assignments.assign(dto);
      else if (mode === 'return') await services.assignments.returnAsset(row.id, dto);
      else await services.assignments.transfer(row.id, dto);
      if (!current() || !modal.dialog.isConnected) return;
      modal.close(); showToast('Đã cập nhật cấp phát tài sản.'); window.dispatchEvent(new Event('frontend-refresh'));
    } catch (error) {
      if (!current() || !modal.dialog.isConnected || error.code === 'SESSION_CHANGED') return;
      if (error.status === 400) { displayErrors(fields, error.errors); feedback.replaceChildren(formAlert(error.message)); }
      else {
        blocked = true;
        feedback.replaceChildren(formAlert(error.status === 409 ? 'Cấp phát hoặc tài sản đã thay đổi. Hãy đóng và tải lại.' :
          'Chưa xác nhận được kết quả. Hãy đóng và tải lại để đối chiếu; không tự gửi lại thao tác.'),
        button('Đóng và tải lại', { kind: 'secondary', onClick: () => { if (current()) { modal.close(); window.dispatchEvent(new Event('frontend-refresh')); } } }));
      }
    } finally { pending = false; confirm.removeAttribute('aria-busy'); if (!closed) confirm.disabled = blocked || !current(); }
  }
  modal = openDialog({ title: mode === 'assign' ? 'Cấp phát tài sản' : mode === 'return' ? 'Thu hồi tài sản' : 'Điều chuyển tài sản',
    body, footer: [button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm], onClose: () => { closed = true; searchSequence++; assetSequence++; } });
  if (mode === 'assign') void lookupAssets();
  if (mode !== 'return') void lookupRecipients();
}
