import { h } from '../utils/dom.js';
import { button, pageHeader, dataTable, pagination, badge, showToast } from '../components.js';
import { ServiceError } from '../services/service-error.js';
import { can, route, queryFrom, setQuery, field, formAlert, openDialog, displayErrors, FieldError, showFormError, liveSearch, showChoices } from './shared.js';

const required = { assign: 'assignments.assign', return: 'assignments.return', transfer: 'assignments.transfer' };
const time = value => value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)) : '—';
const target = item => item.assignedUserName || item.assignedDepartmentName || 'Không có tên người/phòng ban nhận';
const positive = value => Number.isSafeInteger(value) && value > 0;
const utcInput = value => value ? new Date(value).toISOString() : undefined;
// Backend stores a return/transfer note as "Tình trạng: …\nLý do: …"; show both parts on their own rows.
function closingNote(note) {
  const match = /^Tình trạng: ([\s\S]*?)\nLý do: ([\s\S]*)$/u.exec(note || '');
  return match ? { condition: match[1], reason: match[2] } : { condition: null, reason: note || null };
}
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
  function actions(row, withDetail = true) {
    return h('div', { className: 'actions' },
      withDetail && row.id !== selected?.id ? button('Chi tiết', { kind: 'ghost', onClick: () => { if (alive()) route('/assignments/' + row.id); } }) : null,
      !row.returnedAt && can(services, required.return) ? button('Thu hồi', { kind: 'secondary', onClick: () => edit('return', row) }) : null,
      !row.returnedAt && can(services, required.transfer) && (can(services, 'users.lookup') || can(services, 'departments.read')) ? button('Điều chuyển', { kind: 'secondary', onClick: () => edit('transfer', row) }) : null);
  }
  const assetLabel = row => `${row.assetCode || ''} — ${row.assetName || 'Tài sản'}`;
  const assetLink = row => can(services, 'assets.read') && positive(row.assetId)
    ? h('a', { href: '#/assets/' + row.assetId }, assetLabel(row)) : assetLabel(row);
  root.append(pageHeader(selected ? 'Chi tiết cấp phát' : 'Cấp phát tài sản', selected ? 'Thông tin một lần bàn giao và toàn bộ lịch sử cấp phát của tài sản này. Giờ Việt Nam.' : 'Theo dõi người nhận, thời điểm bàn giao, hạn trả và lịch sử. Thời gian hiển thị theo giờ Việt Nam.', {
    actions: [selected ? button('Về danh sách', { kind: 'secondary', iconName: 'arrow-left', onClick: () => { if (alive()) route('/assignments'); } }) : null,
      canCreate() ? button('Cấp phát tài sản', { iconName: 'plus', onClick: () => edit('assign') }) : null].filter(Boolean)
  }), feedback);
  if (selected) {
    const closing = closingNote(selected.returnNote);
    const item = (label, value, wide = false) => h('div', { className: wide ? 'record-wide' : '' }, h('dt', {}, label), h('dd', {}, value || '—'));
    root.append(h('section', { className: 'card record-card' },
      h('div', { className: 'record-head' }, h('div', {}, h('h2', {}, assetLink(selected)), h('p', { className: 'meta' }, 'Người/phòng ban nhận: ', target(selected))),
        badge(selected.returnedAt ? 'Đã đóng' : 'Đang cấp phát')),
      h('dl', { className: 'record-grid' },
        item('Cấp cho', selected.assignedUserName ? 'Nhân viên' : 'Phòng ban'), item('Người/phòng ban nhận', target(selected)),
        item('Cấp lúc', time(selected.assignedAt)), item('Hạn trả dự kiến', time(selected.expectedReturnAt)),
        item('Thu hồi / điều chuyển lúc', selected.returnedAt ? time(selected.returnedAt) : 'Chưa thu hồi'),
        item('Ghi chú bàn giao', selected.assignmentNote, true),
        selected.returnedAt ? item('Tình trạng khi thu hồi', closing.condition, true) : null,
        selected.returnedAt ? item('Lý do thu hồi / điều chuyển', closing.reason, true) : null),
      !selected.returnedAt ? h('div', { className: 'record-actions' }, actions(selected, false)) : null),
      h('div', { className: 'section-heading' }, h('h2', {}, 'Lịch sử cấp phát của tài sản này'), h('span', { className: 'meta' }, `${data.totalItems ?? data.items.length} lần cấp phát`)));
  } else {
    const state = field({ name: 'assignmentStatus', label: 'Trạng thái', value: filters.status || '', options: [
      { value: '', label: 'Tất cả' }, { value: 'Active', label: 'Đang cấp phát' }, { value: 'Returned', label: 'Đã thu hồi/điều chuyển' }] });
    const from = field({ name: 'assignmentFrom', label: 'Cấp từ', type: 'datetime-local' });
    const to = field({ name: 'assignmentTo', label: 'Cấp đến', type: 'datetime-local' });
    const sort = field({ name: 'assignmentSort', label: 'Sắp xếp', value: filters.sortDirection || 'desc', options: [
      { value: 'desc', label: 'Cấp mới nhất' }, { value: 'asc', label: 'Cấp cũ nhất' }] });
    const localTime = value => { const date = new Date(value); return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16); };
    if (filters.from) from.input.value = localTime(filters.from);
    if (filters.to) to.input.value = localTime(filters.to);
    root.append(h('section', { className: 'card filters-card' }, h('form', { className: 'filters', onSubmit: event => {
      event.preventDefault(); if (!alive()) return;
      try {
        const start = utcInput(from.input.value), end = utcInput(to.input.value);
        if (start && end && Date.parse(start) > Date.parse(end)) throw new FieldError('to', 'Thời điểm kết thúc phải sau thời điểm bắt đầu.');
        displayErrors({ to }, {});
        navigate({ ...filters, page: 1, status: state.input.value || undefined, from: start, to: end, sortBy: 'assignedAt', sortDirection: sort.input.value });
      } catch (error) { showFormError({ to }, feedback, error); }
    } }, state.element, from.element, to.element, sort.element, h('div', { className: 'actions' }, button('Lọc', { type: 'submit', iconName: 'search' }),
    button('Xóa bộ lọc', { kind: 'secondary', onClick: () => navigate({}) })))));
  }
  root.append(h('section', { className: 'card' }, dataTable({ columns: [
    { label: 'Tài sản', render: assetLink },
    { label: 'Người/phòng ban nhận', render: target }, { label: 'Cấp lúc', className: 'nowrap', render: row => time(row.assignedAt) },
    { label: 'Hạn trả dự kiến', className: 'nowrap', render: row => time(row.expectedReturnAt) },
    { label: 'Thu hồi lúc', className: 'nowrap', render: row => time(row.returnedAt) },
    { label: 'Trạng thái', render: row => badge(row.returnedAt ? 'Đã đóng' : 'Đang cấp phát') },
    { label: 'Thao tác', render: row => actions(row) }
  ], rows: data.items, emptyMessage: 'Chưa có cấp phát phù hợp với bộ lọc.', label: 'Lịch sử cấp phát' }),
  pagination(data, page => navigate({ ...filters, page }))));
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
  const body = h('div', { className: 'dialog-body' }, row ? h('p', { className: 'info-note' }, `${row.assetCode || ''} — ${row.assetName || ''} · đang cấp cho ${target(row)}`) : null);
  const empty = label => [{ value: '', label }];
  let asset, kind, recipient, search, expected;
  const found = (control, count, keyword, none) => { control.error.textContent = ''; control.input.setAttribute('aria-invalid', 'false');
    control.hint.textContent = count ? `Tìm thấy ${count} kết quả${keyword ? ` cho «${keyword}»` : ''}${count === 1 ? ' — đã tự chọn.' : '. Bấm để chọn.'}` : none(keyword); };
  const replaceOptions = (input, values, label) => { input.replaceChildren(...[...empty(label), ...values].map(item => h('option', { value: item.value }, item.label))); input.value = ''; showChoices(input, values.length); };
  async function lookupAssets() {
    if (!current() || !can(services, 'assets.read')) return;
    const sequence = ++assetSequence;
    try {
      const keyword = fields.assetSearch.input.value.trim();
      const result = await services.assets.list({ keyword, status: 'InStock', page: 1, pageSize: 20 });
      if (!current() || !can(services, 'assets.read') || sequence !== assetSequence) return;
      const items = result.items.filter(item => !item.isArchived);
      replaceOptions(asset.input, items.map(item => ({ value: item.id, label: `${item.assetCode} — ${item.name}` })), 'Chọn tài sản trong kho');
      found(asset, items.length, keyword, text => text ? `Không có tài sản Trong kho khớp «${text}». Tài sản có thể chưa được tạo, đang được cấp phát, đang bảo trì hoặc đã ngừng sử dụng.` : 'Kho hiện không có tài sản nào ở trạng thái Trong kho.');
    } catch (error) { if (current() && sequence === assetSequence && error.code !== 'SESSION_CHANGED') feedback.replaceChildren(formAlert(error.message)); }
  }
  async function lookupRecipients() {
    if (!current()) return;
    const sequence = ++searchSequence, type = kind.input.value;
    const grant = type === 'user' ? 'users.lookup' : 'departments.read';
    if (!can(services, grant)) return;
    try {
      const keyword = search.input.value.trim();
      const result = type === 'user' ? await services.users.lookup({ keyword, page: 1, pageSize: 20 }) :
        await services.departments.list({ keyword, status: 'Active', page: 1, pageSize: 20 });
      if (!current() || sequence !== searchSequence || kind.input.value !== type || !can(services, grant)) return;
      replaceOptions(recipient.input, result.items.map(item => ({ value: item.id, label: [item.displayName || item.name, item.departmentName || item.code].filter(Boolean).join(' — ') })), type === 'user' ? 'Chọn nhân viên nhận' : 'Chọn phòng ban nhận');
      found(recipient, result.items.length, keyword, text => type === 'user' ? `Không có nhân viên đang hoạt động${text ? ` khớp «${text}»` : ''}. Tài khoản mới chỉ hiện sau khi đã kích hoạt.` : `Không có phòng ban đang hoạt động${text ? ` khớp «${text}»` : ''}.`);
    } catch (error) { if (current() && sequence === searchSequence && error.code !== 'SESSION_CHANGED') feedback.replaceChildren(formAlert(error.message)); }
  }
  if (mode === 'assign') {
    fields.assetSearch = field({ name: 'assetSearch', label: 'Tìm tài sản trong kho', type: 'search', maxLength: 200, placeholder: 'Gõ mã, tên hoặc serial… (kết quả hiện ngay khi gõ)' });
    asset = (field({ name: 'assignmentAsset', label: 'Tài sản', required: true, options: empty('Chọn tài sản trong kho') }));
    fields.asset = asset;
    liveSearch(fields.assetSearch.input, lookupAssets);
    body.append(h('div', { className: 'lookup-row' }, fields.assetSearch.element, button('Tìm', { kind: 'secondary', iconName: 'search', onClick: lookupAssets })), asset.element);
  }
  if (mode !== 'return') {
    const choices = [can(services, 'users.lookup') ? { value: 'user', label: 'Nhân viên' } : null,
      can(services, 'departments.read') ? { value: 'department', label: 'Phòng ban' } : null].filter(Boolean);
    kind = field({ name: 'assignmentTargetType', label: 'Cấp cho', value: choices[0]?.value || '', options: choices });
    recipient = (field({ name: 'assignmentRecipient', label: 'Người/phòng ban nhận', required: true, options: empty('Chọn người/phòng ban nhận') }));
    search = field({ name: 'recipientSearch', label: 'Tìm người/phòng ban nhận', type: 'search', maxLength: 200, placeholder: 'Gõ một phần tên… (để trống để xem tất cả)' });
    fields.recipient = recipient;
    kind.input.addEventListener('change', () => { search.input.value = ''; void lookupRecipients(); });
    liveSearch(search.input, lookupRecipients);
    expected = field({ name: 'expectedReturnAt', label: 'Hạn trả dự kiến', type: 'datetime-local', hint: 'Không bắt buộc. Dùng khi cho mượn có thời hạn.' });
    fields.expected = expected;
    body.append(kind.element, h('div', { className: 'lookup-row' }, search.element, button('Tìm', { kind: 'secondary', iconName: 'search', onClick: lookupRecipients })), recipient.element, expected.element);
  }
  if (mode !== 'assign') {
    fields.condition = field({ name: 'returnCondition', label: 'Tình trạng khi bàn giao / thu hồi', maxLength: 200, placeholder: 'VD: Đủ sạc, hoạt động tốt' });
    body.append(fields.condition.element);
  }
  const form = h('form', { onSubmit: event => { event.preventDefault(); void submit(); } }, fields.note.element, feedback);
  body.append(form);
  const confirm = button(mode === 'assign' ? 'Xác nhận cấp phát' : mode === 'return' ? 'Xác nhận thu hồi' : 'Xác nhận điều chuyển', { onClick: () => submit() });
  // Server validation keys → the control that shows the message.
  const serverKeys = { assetId: 'asset', userId: 'recipient', departmentId: 'recipient', expectedReturnAt: 'expected', condition: 'condition', returnCondition: 'condition' };
  async function submit() {
    if (!current() || pending || blocked) return;
    const type = kind?.input.value;
    if (mode === 'assign' && !can(services, 'assets.read') || mode !== 'return' && !can(services, type === 'user' ? 'users.lookup' : 'departments.read')) {
      blocked = true; confirm.disabled = true; feedback.replaceChildren(formAlert('Quyền đã thay đổi. Vui lòng tải lại.')); return;
    }
    const dto = { note: fields.note.input.value.trim() };
    try {
      if (mode === 'assign') { dto.assetId = Number(asset.input.value); if (!positive(dto.assetId)) throw new FieldError('asset', 'Chọn tài sản trong kho.'); }
      if (mode !== 'return') {
        const recipientId = Number(recipient.input.value);
        if (!positive(recipientId)) throw new FieldError('recipient', 'Chọn người/phòng ban nhận.');
        dto[type === 'user' ? 'userId' : 'departmentId'] = recipientId;
        if (expected.input.value) dto.expectedReturnAt = utcInput(expected.input.value);
      }
      if (mode !== 'assign') { dto.rowVersion = row.rowVersion; dto[mode === 'return' ? 'condition' : 'returnCondition'] = fields.condition.input.value.trim() || undefined; }
      if (mode !== 'assign' && !dto.note) throw new FieldError('note', 'Nhập lý do bàn giao/thu hồi.');
    } catch (error) { showFormError(fields, feedback, error); return; }
    displayErrors(fields, {});
    pending = true; confirm.disabled = true; confirm.setAttribute('aria-busy', 'true'); feedback.replaceChildren();
    try {
      if (mode === 'assign') await services.assignments.assign(dto);
      else if (mode === 'return') await services.assignments.returnAsset(row.id, dto);
      else await services.assignments.transfer(row.id, dto);
      if (!current() || !modal.dialog.isConnected) return;
      modal.close(); showToast('Đã cập nhật cấp phát tài sản.'); window.dispatchEvent(new Event('frontend-refresh'));
    } catch (error) {
      if (!current() || !modal.dialog.isConnected || error.code === 'SESSION_CHANGED') return;
      if (error.status === 400) {
        const mapped = Object.fromEntries(Object.entries(error.errors || {}).map(([key, value]) => [serverKeys[key] || key, value]));
        displayErrors(fields, mapped); feedback.replaceChildren(formAlert(error.message));
      }
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
