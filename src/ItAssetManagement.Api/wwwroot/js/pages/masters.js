import { h } from '../utils/dom.js';
import { button, pageHeader, badge, dataTable, pagination, showToast } from '../components.js';
import { validateMaster } from '../utils/validation.js';
import { can, route, queryFrom, setQuery, option, field, displayErrors, formAlert, busy, openDialog, allActive } from './shared.js';

export async function masterPage(services, kind) {
  const departmentMode = kind === 'departments';
  const service = services[kind];
  const permission = departmentMode ? 'departments' : 'asset-types';
  const path = departmentMode ? '/departments' : '/asset-types';
  const title = departmentMode ? 'Phòng ban' : 'Loại tài sản';
  const query = { page: 1, pageSize: 10, sortBy: 'name', ...queryFrom() };
  const result = await service.list(query);
  const keyword = h('input', { className: 'input', value: query.keyword || '', maxLength: 200, placeholder: 'Tìm mã hoặc tên…', 'aria-label': 'Tìm kiếm ' + title.toLowerCase() });
  const status = h('select', { className: 'select', 'aria-label': 'Lọc hoạt động' }, option('', 'Tất cả trạng thái'), option('Active', 'Đang hoạt động', query.status === 'Active'), option('Inactive', 'Ngừng hoạt động', query.status === 'Inactive'));
  async function editDialog(id) {
    try {
      const [item, parents] = await Promise.all([id ? service.get(id) : null, departmentMode ? allActive(service) : Promise.resolve([])]);
      const fields = {};
      const add = config => { const fieldValue = field({ value: item?.[config.name] ?? '', ...config }); fields[config.name] = fieldValue; return fieldValue.element; };
      const feedback = h('div', { 'aria-live': 'polite' });
      const save = button('Lưu', { type: 'submit', iconName: 'check' });
      let modal;
      const form = h('form', { noValidate: true, onSubmit: event => {
        event.preventDefault(); if (save.disabled) return;
        const dto = Object.fromEntries(Object.entries(fields).map(([key, value]) => [key, value.input.value.trim() || null]));
        if (departmentMode) dto.parentDepartmentId = dto.parentDepartmentId ? Number(dto.parentDepartmentId) : null;
        else dto.defaultUsefulLifeMonths = dto.defaultUsefulLifeMonths ? Number(dto.defaultUsefulLifeMonths) : null;
        if (item) dto.rowVersion = item.rowVersion;
        const errors = validateMaster(dto, kind);
        displayErrors(fields, errors); feedback.replaceChildren();
        if (Object.keys(errors).length) return;
        busy(save, async () => {
          try { if (item) await service.update(item.id, dto); else await service.create(dto); modal.close(); showToast(services.mode === 'mock' ? 'Đã lưu danh mục trong dữ liệu demo.' : 'Đã lưu danh mục.'); window.dispatchEvent(new Event('frontend-refresh')); }
          catch (error) { feedback.append(formAlert(error.message)); displayErrors(fields, error.errors || {}); }
        });
      } }, h('div', { className: 'dialog-body form-grid' }, add({ name: 'code', label: 'Mã', required: true, maxLength: 50, readOnly: Boolean(item && departmentMode), hint: item && departmentMode ? 'Mã phòng ban bất biến sau khi tạo.' : '' }), add({ name: 'name', label: 'Tên', required: true, maxLength: 200 }), departmentMode ? add({ name: 'parentDepartmentId', label: 'Phòng ban cha', options: [{ value: '', label: 'Không có' }, ...parents.filter(parent => parent.id !== item?.id).map(parent => ({ value: parent.id, label: parent.name })), ...(item?.parentDepartmentId && !parents.some(parent => parent.id === item.parentDepartmentId) ? [{ value: item.parentDepartmentId, label: 'Tham chiếu cũ #' + item.parentDepartmentId }] : [])] }) : add({ name: 'defaultUsefulLifeMonths', label: 'Thời gian sử dụng mặc định (tháng)', type: 'number', hint: 'Để trống hoặc số nguyên dương.' }), add({ name: 'description', label: 'Mô tả', type: 'textarea', maxLength: 1000 }), feedback), h('div', { className: 'dialog-footer' }, button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), save));
      modal = openDialog({ title: (id ? 'Chỉnh sửa ' : 'Thêm ') + title.toLowerCase(), body: form });
    } catch (error) { showToast(error.message, 'error'); }
  }
  async function statusDialog(row) {
    const reason = field({ name: 'reason', label: 'Lý do', required: true, maxLength: 500 });
    const feedback = h('div', { 'aria-live': 'polite' });
    let modal;
    const confirm = button(row.isActive ? 'Ngừng hoạt động' : 'Kích hoạt', { onClick: () => busy(confirm, async () => {
      if (!reason.input.value.trim()) { displayErrors({ reason }, { reason: ['Vui lòng nhập lý do.'] }); return; }
      try { await service.setStatus(row.id, { isActive: !row.isActive, reason: reason.input.value.trim(), rowVersion: row.rowVersion }); modal.close(); showToast('Đã cập nhật trạng thái danh mục' + (services.mode === 'mock' ? ' trong bản demo.' : '.')); window.dispatchEvent(new Event('frontend-refresh')); }
      catch (error) { feedback.replaceChildren(formAlert(error.message)); }
    }) });
    modal = openDialog({ title: 'Trạng thái ' + title.toLowerCase(), body: h('div', { className: 'dialog-body' }, h('p', {}, row.name, '. Không xóa danh mục hoặc các tham chiếu cũ.'), reason.element, feedback), footer: [button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm] });
  }
  return h('div', {}, pageHeader(title, departmentMode ? 'Danh mục phòng ban và đơn vị sở hữu tài sản.' : 'Nhóm thiết bị và thời gian sử dụng tham chiếu.', { eyebrow: 'Danh mục', actions: can(services, permission + '.create') ? [button('Thêm ' + title.toLowerCase(), { iconName: 'plus', onClick: () => editDialog(null) })] : [] }),
    h('section', { className: 'card filters-card' }, h('form', { className: 'filters', onSubmit: event => { event.preventDefault(); setQuery(path, { keyword: keyword.value.trim(), status: status.value, page: 1 }); } }, h('div', { className: 'filter-field search-field' }, h('label', {}, 'Tìm kiếm'), keyword), h('div', { className: 'filter-field' }, h('label', {}, 'Trạng thái'), status), h('div', { className: 'actions' }, button('Tìm kiếm', { type: 'submit', iconName: 'search' }), button('Đặt lại', { kind: 'secondary', onClick: () => route(path) })))),
    h('section', { className: 'card' }, dataTable({ rows: result.items, columns: [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, departmentMode ? { key: 'parentDepartmentId', label: 'Phòng ban cha', render: row => row.parentDepartmentId ? '#' + row.parentDepartmentId : '—' } : { key: 'defaultUsefulLifeMonths', label: 'Thời gian sử dụng', render: row => row.defaultUsefulLifeMonths ? row.defaultUsefulLifeMonths + ' tháng' : '—' }, { key: 'description', label: 'Mô tả', render: row => row.description || '—' }, { key: 'isActive', label: 'Trạng thái', render: row => badge(row.isActive) }, { key: 'actions', label: 'Thao tác', render: row => h('div', { className: 'actions' }, can(services, permission + '.update') ? button('Sửa', { kind: 'ghost', iconName: 'pencil', onClick: () => editDialog(row.id) }) : null, can(services, permission + '.archive') ? button(row.isActive ? 'Ngừng hoạt động' : 'Kích hoạt', { kind: 'ghost', onClick: () => statusDialog(row) }) : null) }] }), pagination(result, page => setQuery(path, { ...query, page }))));
}
