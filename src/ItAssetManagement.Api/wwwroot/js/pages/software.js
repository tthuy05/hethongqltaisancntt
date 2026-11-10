import { h } from '../utils/dom.js';
import { button, pageHeader, badge, dataTable, pagination, showToast, loadingState } from '../components.js';
import { can, route, queryFrom, setQuery, option, field, displayErrors, formAlert, openDialog, date } from './shared.js';
import { validateSoftware } from '../services/software-service.js';

export async function softwarePage(services) {
  if (services.softwareEnabled !== true) throw Object.assign(new Error('Danh mục phần mềm chưa được mở.'), { status: 404 });
  if (!can(services, 'software.read')) throw Object.assign(new Error('Bạn không có quyền xem phần mềm.'), { status: 403 });
  const actor = services.auth.session.id, hash = window.location.hash;
  const current = () => services.auth.session?.id === actor && services.softwareEnabled === true && can(services, 'software.read');
  const query = { page: 1, pageSize: 20, sortBy: 'name', sortDirection: 'asc', ...queryFrom() };
  const result = await services.software.list(query);
  if (!current()) throw Object.assign(new Error('Phiên đã thay đổi.'), { status: 401 });
  const root = h('div', {}), feedback = h('div', { 'aria-live': 'polite' });
  const alive = () => root.isConnected && window.location.hash === hash && current();
  async function edit(row = null) {
    const grant = row ? 'software.update' : 'software.create';
    if (!alive() || !can(services, grant)) return;
    try {
      const fresh = row ? await services.software.get(row.id) : null;
      if (alive() && can(services, grant)) dialog(fresh, false);
    } catch (error) { if (alive()) feedback.replaceChildren(formAlert(error.message)); }
  }
  function dialog(item, archive) {
    const grant = archive ? 'software.archive' : item ? 'software.update' : 'software.create';
    let modal, pending = false, blocked = false, closed = false;
    const fields = {}, message = h('div', { 'aria-live': 'polite' });
    const add = (name, label, maxLength, type = 'text') => {
      const f = field({ name: 'software-' + name, label, maxLength, type, value: item?.[name] || '', required: name === 'name' }); fields[name] = f; return f.element;
    };
    const active = () => !closed && alive() && message.isConnected;
    const allowed = () => current() && can(services, grant);
    const stop = text => {
      blocked = true; confirm.disabled = true;
      message.replaceChildren(formAlert(text), button('Đóng và tải lại', { kind: 'secondary', onClick: () => { if (!active()) return; modal.close(); window.dispatchEvent(new Event('frontend-refresh')); } }));
    };
    const confirm = button(archive ? 'Xác nhận ngừng sử dụng' : 'Lưu', { type: 'submit' });
    const form = h('form', { noValidate: true, onSubmit: async event => {
      event.preventDefault(); if (pending || blocked || !active()) return;
      if (!allowed()) { stop('Phiên hoặc quyền đã thay đổi. Hãy tải lại.'); return; }
      const dto = Object.fromEntries(Object.entries(fields).map(([key, f]) => [key, f.input.value.trim() || null]));
      if (item) dto.rowVersion = item.rowVersion;
      const errors = archive ? {} : validateSoftware(dto, Boolean(item)); displayErrors(fields, errors); message.replaceChildren();
      if (Object.keys(errors).length) return;
      pending = true; confirm.disabled = true; confirm.setAttribute('aria-busy', 'true');
      try {
        if (archive) await services.software.archive(item.id, item.rowVersion);
        else if (item) await services.software.update(item.id, dto);
        else await services.software.create(dto);
        if (!active()) return;
        if (!allowed()) { stop('Phiên đã thay đổi. Hãy kiểm tra kết quả thực tế.'); return; }
        modal.close(); showToast(archive ? 'Đã ngừng sử dụng phần mềm.' : 'Đã lưu phần mềm.'); window.dispatchEvent(new Event('frontend-refresh'));
      } catch (error) {
        if (!active()) return;
        if (error.status === 400 && allowed()) { message.replaceChildren(formAlert(error.message)); displayErrors(fields, error.errors || {}); }
        else stop(error.code === 'SOFTWARE_ACTIVE_LICENSES' ? 'Phần mềm còn bản quyền hoạt động. Hãy xử lý bản quyền trước.' : error.status === 409 ? 'Dữ liệu hoặc trạng thái đã thay đổi. Hãy tải lại.' : 'Chưa xác nhận được kết quả. Hãy tải lại trước khi thao tác tiếp.');
      } finally { pending = false; confirm.removeAttribute('aria-busy'); if (active()) confirm.disabled = blocked; }
    } }, h('div', { className: 'dialog-body form-grid' },
      h('p', {}, item?.code || 'Mã PMxxxxxx sẽ được backend tự sinh khi lưu.'),
      ...(archive ? [h('p', {}, 'Ngừng sử dụng ', item.name, '? Thông tin và lịch sử vẫn được giữ lại.')] :
        [add('name', 'Tên phần mềm', 250), add('publisher', 'Nhà phát hành', 250), add('version', 'Phiên bản', 100), add('description', 'Mô tả', 2000, 'textarea')]), message),
      h('div', { className: 'dialog-footer' }, button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm));
    modal = openDialog({ title: archive ? 'Ngừng sử dụng phần mềm' : item ? 'Sửa phần mềm' : 'Thêm phần mềm', body: form, onClose: () => { closed = true; } });
  }
  async function archive(row) {
    if (!alive() || !can(services, 'software.archive')) return;
    try { const fresh = await services.software.get(row.id); if (alive() && fresh.isActive && can(services, 'software.archive')) dialog(fresh, true); }
    catch (error) { if (alive()) feedback.replaceChildren(formAlert(error.message)); }
  }
  async function detail(row) {
    if (!alive()) return;
    try {
      const fresh = await services.software.get(row.id); if (!alive()) return;
      const item = (label, value, wide = false) => h('div', { className: wide ? 'record-wide' : '' }, h('dt', {}, label), h('dd', {}, value || '—'));
      const licenses = h('div', {}, loadingState());
      openDialog({ title: 'Chi tiết phần mềm', className: 'dialog--wide detail-dialog', body: h('div', { className: 'dialog-body' },
        h('div', { className: 'record-head' }, h('div', {}, h('h2', {}, fresh.code + ' — ' + fresh.name), h('p', { className: 'meta' }, fresh.publisher || 'Chưa có nhà phát hành')), badge(fresh.isActive)),
        h('dl', { className: 'record-grid' }, item('Phiên bản', fresh.version), item('Ngày tạo', date(fresh.createdAt)), item('Cập nhật lần cuối', date(fresh.updatedAt)),
          item('Mô tả', fresh.description || 'Chưa có mô tả', true)),
        h('h3', {}, 'Bản quyền của phần mềm này'), licenses) });
      if (services.licenseEnabled !== true || !can(services, 'licenses.read') || !services.licenses) { licenses.replaceChildren(h('p', { className: 'meta' }, 'Bạn chưa có quyền xem bản quyền phần mềm.')); return; }
      try {
        const result = await services.licenses.list({ softwareId: fresh.id, page: 1, pageSize: 50 });
        if (!licenses.isConnected) return;
        const seats = result.items.reduce((sum, x) => ({ total: sum.total + (x.totalQuantity || 0), used: sum.used + (x.usedQuantity || 0) }), { total: 0, used: 0 });
        licenses.replaceChildren(h('p', { className: 'meta tab-panel-note' }, `${result.totalItems ?? result.items.length} bản quyền · tổng ${seats.total} seat, đang dùng ${seats.used}.`),
          dataTable({ label: 'Bản quyền của phần mềm', emptyMessage: 'Phần mềm này chưa có bản quyền nào.', rows: result.items, columns: [
            { label: 'Mã', className: 'nowrap', render: x => h('a', { href: '#/licenses/' + x.id }, x.licenseCode) },
            { label: 'Loại', key: 'licenseType' }, { label: 'Seat', className: 'nowrap', render: x => `${x.usedQuantity ?? '—'}/${x.totalQuantity}` },
            { label: 'Nhà cung cấp', render: x => x.vendor || '—' }, { label: 'Hết hạn', className: 'nowrap', render: x => date(x.expiresAt) },
            { label: 'Trạng thái', render: x => badge(x.isActive) }] }));
      } catch (error) { if (licenses.isConnected) licenses.replaceChildren(formAlert(error.message || 'Không tải được bản quyền.')); }
    } catch (error) { if (alive()) feedback.replaceChildren(formAlert(error.message)); }
  }
  const keyword = h('input', { className: 'input', value: query.keyword || '', maxLength: 200, 'aria-label': 'Tìm phần mềm', placeholder: 'Mã hoặc tên phần mềm…' });
  const vendor = h('input', { className: 'input', value: query.vendor || '', maxLength: 250, 'aria-label': 'Nhà phát hành', placeholder: 'Nhà phát hành (VD: Microsoft)…' });
  const labelled = (text, control) => h('div', { className: 'filter-field' }, h('label', {}, text), control);
  const status = h('select', { className: 'select', 'aria-label': 'Trạng thái phần mềm' }, option('', 'Tất cả trạng thái'), option('Active', 'Đang hoạt động', query.status === 'Active'), option('Inactive', 'Ngừng hoạt động', query.status === 'Inactive'));
  const sort = h('select', { className: 'select', 'aria-label': 'Sắp xếp phần mềm' }, ...[['name', 'Tên'], ['code', 'Mã'], ['publisher', 'Nhà phát hành'], ['createdAt', 'Ngày tạo']].map(([key, label]) => option(key, label, query.sortBy === key)));
  const direction = h('select', { className: 'select', 'aria-label': 'Chiều sắp xếp' }, option('asc', 'Tăng dần', query.sortDirection === 'asc'), option('desc', 'Giảm dần', query.sortDirection === 'desc'));
  root.append(pageHeader('Phần mềm', 'Danh mục phần mềm, nhà phát hành và phiên bản.', { actions: can(services, 'software.create') ? [button('Thêm phần mềm', { iconName: 'plus', onClick: () => edit() })] : [] }), feedback,
    h('section', { className: 'card filters-card' }, h('form', { className: 'filters', onSubmit: event => { event.preventDefault(); if (alive()) setQuery('/software', { ...query, page: 1, keyword: keyword.value.trim(), vendor: vendor.value.trim(), status: status.value, sortBy: sort.value, sortDirection: direction.value }); } }, labelled('Tìm phần mềm', keyword), labelled('Nhà phát hành', vendor), labelled('Trạng thái', status), labelled('Sắp xếp theo', sort), labelled('Chiều', direction),
      h('div', { className: 'actions' }, button('Tìm kiếm', { type: 'submit', iconName: 'search' }), button('Đặt lại', { kind: 'secondary', onClick: () => { if (alive()) route('/software'); } })))),
    h('section', { className: 'card' }, dataTable({ rows: result.items, columns: [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên phần mềm' }, { key: 'publisher', label: 'Nhà phát hành' }, { key: 'version', label: 'Phiên bản' }, { label: 'Trạng thái', render: row => badge(row.isActive) }, { label: 'Thao tác', render: row => h('div', { className: 'actions' }, button('Chi tiết', { kind: 'ghost', onClick: () => detail(row) }), row.isActive && can(services, 'software.update') ? button('Sửa', { kind: 'ghost', onClick: () => edit(row) }) : null, row.isActive && can(services, 'software.archive') ? button('Ngừng sử dụng', { kind: 'ghost', onClick: () => archive(row) }) : null) }] }), pagination(result, page => { if (alive()) setQuery('/software', { ...query, page }); })));
  return root;
}
