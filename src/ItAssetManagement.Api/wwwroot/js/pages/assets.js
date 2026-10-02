import { h } from '../utils/dom.js';
import { button, pageHeader, badge, dataTable, pagination, showToast } from '../components.js';
import { allCatalog, can, route, queryFrom, setQuery, option, date, money, statusLabels, archiveDialog } from './shared.js';

export async function assetListPage(services) {
  const query = { page: 1, pageSize: 10, sortBy: 'assetCode', sortDirection: 'asc', ...queryFrom() };
  const [result, departments, types] = await Promise.all([services.assets.list(query), allCatalog(services.departments), allCatalog(services.assetTypes)]);
  // Summary DTO deliberately has no brand/model/warranty. Use documented detail endpoints for the visible page only.
  const details = await Promise.allSettled(result.items.map(item => services.assets.get(item.id)));
  const rows = result.items.map((item, index) => ({ ...item, detail: details[index].status === 'fulfilled' ? details[index].value : null }));
  const input = h('input', { className: 'input', name: 'keyword', value: query.keyword || '', maxLength: 200, placeholder: 'Mã tài sản, tên hoặc serial…', 'aria-label': 'Tìm kiếm tài sản' });
  const type = h('select', { className: 'select', name: 'assetTypeId', 'aria-label': 'Lọc loại tài sản' }, option('', 'Tất cả loại'), types.map(item => option(item.id, item.name, String(item.id) === query.assetTypeId)));
  const department = h('select', { className: 'select', name: 'departmentId', 'aria-label': 'Lọc phòng ban' }, option('', 'Tất cả phòng ban'), departments.map(item => option(item.id, item.name, String(item.id) === query.departmentId)));
  const status = h('select', { className: 'select', name: 'status', 'aria-label': 'Lọc trạng thái' }, option('', 'Tất cả trạng thái'), Object.entries(statusLabels).map(([value, label]) => option(value, label, value === query.status)));
  const sort = h('select', { className: 'select', name: 'sortBy', 'aria-label': 'Sắp xếp tài sản' }, ['assetCode', 'name', 'status', 'createdAt', 'updatedAt'].map((value, index) => option(value, ['Mã tài sản', 'Tên', 'Trạng thái', 'Ngày tạo', 'Cập nhật'][index], value === query.sortBy)));
  const direction = h('select', { className: 'select', name: 'sortDirection', 'aria-label': 'Chiều sắp xếp' }, option('asc', 'Tăng dần', query.sortDirection === 'asc'), option('desc', 'Giảm dần', query.sortDirection === 'desc'));
  const filters = h('form', { className: 'filters', onSubmit: event => { event.preventDefault(); setQuery('/assets', { keyword: input.value.trim(), assetTypeId: type.value, departmentId: department.value, status: status.value, sortBy: sort.value, sortDirection: direction.value, page: 1, pageSize: 10 }); } },
    h('div', { className: 'filter-field search-field' }, h('label', {}, 'Tìm kiếm'), input),
    h('div', { className: 'filter-field' }, h('label', {}, 'Loại tài sản'), type), h('div', { className: 'filter-field' }, h('label', {}, 'Phòng ban'), department), h('div', { className: 'filter-field' }, h('label', {}, 'Trạng thái'), status),
    h('div', { className: 'filter-field' }, h('label', {}, 'Sắp xếp'), sort), h('div', { className: 'filter-field' }, h('label', {}, 'Chiều'), direction), h('div', { className: 'actions' }, button('Tìm kiếm', { type: 'submit', iconName: 'search' }), button('Đặt lại', { kind: 'secondary', iconName: 'rotate-ccw', onClick: () => route('/assets') })));
  const columns = [
    { key: 'assetCode', label: 'Mã tài sản', render: row => button(row.assetCode, { kind: 'ghost', onClick: () => route('/assets/' + row.id) }) },
    { key: 'name', label: 'Tên tài sản', render: row => h('div', { className: 'asset-name' }, h('strong', {}, row.name), h('span', { className: 'meta' }, row.serialNumber || 'Chưa có serial')) },
    { key: 'assetType', label: 'Loại', render: row => row.assetType?.name || '—' },
    { key: 'brand', label: 'Brand / Model', render: row => row.detail ? [row.detail.brand, row.detail.model].filter(Boolean).join(' / ') || '—' : 'Không tải được' },
    { key: 'owningDepartment', label: 'Phòng ban', render: row => row.owningDepartment?.name || '—' },
    { key: 'user', label: 'Người sử dụng', render: row => row.detail?.currentAssignment?.user?.displayName || 'Chưa phân bổ' },
    { key: 'status', label: 'Trạng thái', render: row => badge(row.status) },
    { key: 'warranty', label: 'Bảo hành', render: row => row.detail ? date(row.detail.warrantyExpirationDate) : 'Không tải được' },
    { key: 'actions', label: 'Thao tác', render: row => h('div', { className: 'actions' }, button('Xem', { kind: 'ghost', iconName: 'eye', onClick: () => route('/assets/' + row.id), 'aria-label': 'Xem ' + row.assetCode }), can(services, 'assets.update') ? button('Sửa', { kind: 'ghost', iconName: 'pencil', onClick: () => route('/assets/' + row.id + '/edit'), 'aria-label': 'Sửa ' + row.assetCode }) : null, can(services, 'assets.archive') ? button('Archive', { kind: 'ghost', iconName: 'archive', onClick: async () => { try { archiveDialog(services, await services.assets.get(row.id), () => { window.dispatchEvent(new Event('frontend-refresh')); }); } catch (error) { showToast(error.message, 'error'); } }, 'aria-label': 'Archive ' + row.assetCode }) : null) }
  ];
  return h('div', {}, pageHeader('Danh sách tài sản', 'Tra cứu, theo dõi và quản lý thiết bị trong doanh nghiệp.', { eyebrow: 'Tài sản', actions: can(services, 'assets.create') ? [button('Thêm tài sản', { iconName: 'plus', onClick: () => route('/assets/new') })] : [] }), h('section', { className: 'card filters-card' }, filters), h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('h2', {}, 'Danh mục thiết bị'), h('span', { className: 'meta' }, result.totalItems + ' tài sản')), dataTable({ columns, rows, emptyMessage: 'Không tìm thấy tài sản. Thử thay đổi hoặc đặt lại bộ lọc.' }), pagination(result, page => setQuery('/assets', { ...query, page }))));
}

export async function assetDetailPage(services, id) {
  const asset = await services.assets.get(id);
  const tabs = h('div', { className: 'tab-list', role: 'tablist', 'aria-label': 'Lịch sử tài sản' });
  const panel = h('div', { className: 'card-body', role: 'tabpanel', id: 'history-panel', tabindex: 0, 'aria-live': 'polite' });
  const history = [{ key: 'assignment', label: 'Phân bổ', note: 'Assignment History — PLANNED. Chưa kết nối API lịch sử phân bổ.' }, { key: 'maintenance', label: 'Bảo trì', note: 'Maintenance History — PLANNED. Chưa kết nối API bảo trì.' }, { key: 'status', label: 'Trạng thái', note: 'Status History — PLANNED. Chưa kết nối API lịch sử trạng thái.' }];
  const choose = key => { for (const child of tabs.children) { const selected = child.dataset.key === key; child.classList.toggle('active', selected); child.setAttribute('aria-selected', String(selected)); child.tabIndex = selected ? 0 : -1; } panel.setAttribute('aria-labelledby', 'history-tab-' + key); panel.textContent = history.find(item => item.key === key).note; };
  history.forEach((item, index) => tabs.append(button(item.label, { kind: 'ghost', className: 'tab-button', role: 'tab', id: 'history-tab-' + item.key, 'aria-controls': 'history-panel', dataset: { key: item.key }, onClick: () => choose(item.key), onKeyDown: event => {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? history.length - 1 : (index + (event.key === 'ArrowRight' ? 1 : -1) + history.length) % history.length;
    choose(history[next].key);
    tabs.children[next].focus();
  } })));
  choose('assignment');
  const info = (label, value) => h('div', { className: 'detail-item' }, h('dt', {}, label), h('dd', {}, value || 'Chưa có thông tin'));
  return h('div', {}, pageHeader(asset.name, asset.assetCode + ' · Thông tin thiết bị', { eyebrow: 'Chi tiết tài sản', actions: [button('Danh sách', { kind: 'secondary', iconName: 'arrow-left', onClick: () => route('/assets') }), ...(can(services, 'assets.update') ? [button('Chỉnh sửa', { iconName: 'pencil', onClick: () => route('/assets/' + asset.id + '/edit') })] : [])] }),
    h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('h2', {}, 'Thông tin chung'), badge(asset.status)), h('dl', { className: 'card-body detail-grid' }, info('Mã tài sản', asset.assetCode), info('Tên tài sản', asset.name), info('Loại tài sản', asset.assetType?.name), info('Serial Number', asset.serialNumber), info('Thương hiệu', asset.brand), info('Model', asset.model), info('Phòng ban sở hữu', asset.owningDepartment?.name), info('Vị trí', asset.location), info('Người sử dụng', asset.currentAssignment?.user?.displayName || 'Chưa phân bổ'))),
    h('div', { className: 'dashboard-grid' }, h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('h2', {}, 'Thông tin kỹ thuật')), h('dl', { className: 'card-body detail-grid' }, info('Cấu hình', asset.specification), info('Hệ điều hành', asset.operatingSystem))), h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('h2', {}, 'Mua sắm & bảo hành')), h('dl', { className: 'card-body detail-grid' }, info('Ngày mua', date(asset.purchaseDate)), can(services, 'assets.cost.read') ? info('Giá mua', money(asset.purchasePrice)) : null, info('Hết bảo hành', date(asset.warrantyExpirationDate)), info('Ngày cập nhật', date(asset.updatedAt))))),
    h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('h2', {}, 'Ghi chú')), h('p', { className: 'card-body whitespace-pre-wrap' }, asset.note || 'Chưa có ghi chú.')),
    h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('h2', {}, 'Lịch sử tài sản')), tabs, panel));
}
