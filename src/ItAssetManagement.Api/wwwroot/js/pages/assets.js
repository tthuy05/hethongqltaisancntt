import { h } from '../utils/dom.js';
import { button, pageHeader, badge, dataTable, pagination, showToast } from '../components.js';
import { allCatalog, can, hasPermission, route, queryFrom, setQuery, option, date, money, statusLabels, archiveDialog } from './shared.js';
import { createAssetStatusHistory } from '../components/asset-status-history.js';
import { assetRetireDialog, canRetireAsset } from '../components/asset-retire-dialog.js';

export async function assetListPage(services) {
  const actorId = services.auth?.session?.id;
  const pageHash = window.location.hash;
  const requireReader = () => {
    const session = services.auth?.session;
    if (!session || session.id !== actorId) throw Object.assign(new Error('Phiên đăng nhập đã thay đổi.'), { status: 401, code: 'SESSION_CHANGED' });
    if (!Number.isSafeInteger(actorId) || actorId <= 0 || !hasPermission(session, 'assets.read'))
      throw Object.assign(new Error('Bạn không có quyền xem tài sản.'), { status: 403, code: 'FORBIDDEN' });
  };
  requireReader();
  const query = { page: 1, pageSize: 10, sortBy: 'assetCode', sortDirection: 'asc', ...queryFrom() };
  // Catalog read grants are independent from Asset read. Never hide an otherwise
  // readable list because an optional lookup is forbidden or temporarily down.
  const catalog = async (service, permission) => {
    if (!can(services, permission)) return { items: [], state: 'forbidden' };
    try { return { items: await allCatalog(service), state: 'ready' }; }
    catch (error) {
      if (error?.status === 401 || error?.code === 'SESSION_CHANGED') throw error;
      return { items: [], state: error?.status === 403 ? 'forbidden' : 'unavailable' };
    }
  };
  const [result, departments, types] = await Promise.all([services.assets.list(query),
    catalog(services.departments, 'departments.read'), catalog(services.assetTypes, 'asset-types.read')]);
  requireReader();
  // Summary DTO deliberately has no brand/model/warranty. Use documented detail endpoints for the visible page only.
  const details = await Promise.allSettled(result.items.map(item => services.assets.get(item.id)));
  requireReader();
  if (!can(services, 'departments.read')) { departments.items = []; departments.state = 'forbidden'; }
  if (!can(services, 'asset-types.read')) { types.items = []; types.state = 'forbidden'; }
  const rows = result.items.map((item, index) => ({ ...item, detail: details[index].status === 'fulfilled' ? details[index].value : null }));
  const input = h('input', { className: 'input', name: 'keyword', value: query.keyword || '', maxLength: 200, placeholder: 'Mã tài sản, tên hoặc serial…', 'aria-label': 'Tìm kiếm tài sản' });
  const lookup = (name, label, all, result, fallback) => {
    const selected = query[name] || '';
    const missing = selected && !result.items.some(item => String(item.id) === selected);
    const select = h('select', { className: 'select', name, disabled: result.state !== 'ready', 'aria-label': label,
      ...(result.state !== 'ready' ? { 'aria-describedby': name + '-hint' } : {}) },
      option('', all), result.items.map(item => option(item.id, item.name, String(item.id) === selected)),
      missing ? option(selected, fallback + selected, true) : null);
    select.value = selected;
    return select;
  };
  const type = lookup('assetTypeId', 'Lọc loại tài sản', 'Tất cả loại', types, 'ID loại tài sản: ');
  const department = lookup('departmentId', 'Lọc phòng ban', 'Tất cả phòng ban', departments, 'ID phòng ban: ');
  const catalogHint = (name, result) => result.state === 'ready' ? null : h('span', { id: name + '-hint', className: 'field-hint' },
    (result.state === 'forbidden' ? 'Không có quyền tải danh mục.' : 'Không tải được danh mục.') +
      ' Bộ lọc URL hiện tại vẫn được giữ; dùng Đặt lại để bỏ bộ lọc.');
  const status = h('select', { className: 'select', name: 'status', 'aria-label': 'Lọc trạng thái' }, option('', 'Tất cả trạng thái'), Object.entries(statusLabels).map(([value, label]) => option(value, label, value === query.status)));
  const sort = h('select', { className: 'select', name: 'sortBy', 'aria-label': 'Sắp xếp tài sản' }, ['assetCode', 'name', 'status', 'createdAt', 'updatedAt'].map((value, index) => option(value, ['Mã tài sản', 'Tên', 'Trạng thái', 'Ngày tạo', 'Cập nhật'][index], value === query.sortBy)));
  const direction = h('select', { className: 'select', name: 'sortDirection', 'aria-label': 'Chiều sắp xếp' }, option('asc', 'Tăng dần', query.sortDirection === 'asc'), option('desc', 'Giảm dần', query.sortDirection === 'desc'));
  const filters = h('form', { className: 'filters', onSubmit: event => { event.preventDefault(); setQuery('/assets', { keyword: input.value.trim(), assetTypeId: type.value, departmentId: department.value, status: status.value, sortBy: sort.value, sortDirection: direction.value, page: 1, pageSize: 10 }); } },
    h('div', { className: 'filter-field search-field' }, h('label', {}, 'Tìm kiếm'), input),
    h('div', { className: 'filter-field' }, h('label', {}, 'Loại tài sản'), type, catalogHint('assetTypeId', types)), h('div', { className: 'filter-field' }, h('label', {}, 'Phòng ban'), department, catalogHint('departmentId', departments)), h('div', { className: 'filter-field' }, h('label', {}, 'Trạng thái'), status),
    h('div', { className: 'filter-field' }, h('label', {}, 'Sắp xếp'), sort), h('div', { className: 'filter-field' }, h('label', {}, 'Chiều'), direction), h('div', { className: 'actions' }, button('Tìm kiếm', { type: 'submit', iconName: 'search' }), button('Đặt lại', { kind: 'secondary', iconName: 'rotate-ccw', onClick: () => route('/assets') })));
  const archiveAction = row => {
    let pending = false;
    const active = () => control.isConnected && window.location.hash === pageHash;
    const allowed = () => { const session = services.auth?.session; return Number.isSafeInteger(actorId) && actorId > 0 && session?.id === actorId &&
      hasPermission(session, 'assets.read') && hasPermission(session, 'assets.archive'); };
    const control = button('Archive', { kind: 'ghost', iconName: 'archive', 'aria-label': 'Archive ' + row.assetCode, onClick: async () => {
      if (pending || !active() || !allowed()) return;
      pending = true; control.disabled = true; control.setAttribute('aria-busy', 'true');
      try {
        const asset = await services.assets.get(row.id);
        if (active() && allowed()) archiveDialog(services, asset, () => window.dispatchEvent(new Event('frontend-refresh')));
      } catch {
        if (active() && allowed()) showToast('Không tải được tài sản để xác nhận archive. Chưa gửi thao tác archive.', 'error');
      } finally { pending = false; control.disabled = false; control.removeAttribute('aria-busy'); }
    } });
    return control;
  };
  const columns = [
    { key: 'assetCode', label: 'Mã tài sản', render: row => button(row.assetCode, { kind: 'ghost', onClick: () => route('/assets/' + row.id) }) },
    { key: 'name', label: 'Tên tài sản', render: row => h('div', { className: 'asset-name' }, h('strong', {}, row.name), h('span', { className: 'meta' }, row.serialNumber || 'Chưa có serial')) },
    { key: 'assetType', label: 'Loại', render: row => row.assetType?.name || '—' },
    { key: 'brand', label: 'Brand / Model', render: row => row.detail ? [row.detail.brand, row.detail.model].filter(Boolean).join(' / ') || '—' : 'Không tải được' },
    { key: 'owningDepartment', label: 'Phòng ban', render: row => row.owningDepartment?.name || '—' },
    { key: 'user', label: 'Người sử dụng', render: row => row.detail?.currentAssignment?.user?.displayName || (services.mode === 'mock' ? 'Chưa phân bổ' : 'Chưa có dữ liệu cấp phát') },
    { key: 'status', label: 'Trạng thái', render: row => badge(row.status) },
    { key: 'warranty', label: 'Bảo hành', render: row => row.detail ? date(row.detail.warrantyExpirationDate) : 'Không tải được' },
    { key: 'actions', label: 'Thao tác', render: row => h('div', { className: 'actions' }, button('Xem', { kind: 'ghost', iconName: 'eye', onClick: () => route('/assets/' + row.id), 'aria-label': 'Xem ' + row.assetCode }), can(services, 'assets.update') ? button('Sửa', { kind: 'ghost', iconName: 'pencil', onClick: () => route('/assets/' + row.id + '/edit'), 'aria-label': 'Sửa ' + row.assetCode }) : null, can(services, 'assets.read') && can(services, 'assets.archive') ? archiveAction(row) : null) }
  ];
  return h('div', {}, pageHeader('Danh sách tài sản', 'Tra cứu, theo dõi và quản lý thiết bị trong doanh nghiệp.', { eyebrow: 'Tài sản', actions: can(services, 'assets.create') ? [button('Thêm tài sản', { iconName: 'plus', onClick: () => route('/assets/new') })] : [] }), h('section', { className: 'card filters-card' }, filters), h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('h2', {}, 'Danh mục thiết bị'), h('span', { className: 'meta' }, result.totalItems + ' tài sản')), dataTable({ columns, rows, emptyMessage: 'Không tìm thấy tài sản. Thử thay đổi hoặc đặt lại bộ lọc.' }), pagination(result, page => setQuery('/assets', { ...query, page }))));
}

export async function assetDetailPage(services, id) {
  const actorId = services.auth?.session?.id;
  const pageHash = window.location.hash;
  const requireReader = () => {
    const session = services.auth?.session;
    if (!Number.isSafeInteger(actorId) || actorId <= 0 || session?.id !== actorId || window.location.hash !== pageHash)
      throw Object.assign(new Error('Phiên hoặc trang đã thay đổi.'), { status: 401, code: 'SESSION_CHANGED' });
    if (!hasPermission(session, 'assets.read'))
      throw Object.assign(new Error('Bạn không có quyền xem tài sản.'), { status: 403, code: 'FORBIDDEN' });
  };
  requireReader();
  const asset = await services.assets.get(id);
  requireReader();
  if (!Number.isSafeInteger(asset?.id) || asset.id !== id)
    throw Object.assign(new Error('Chi tiết tài sản không khớp ID đã yêu cầu.'), { status: 502, code: 'INVALID_API_RESPONSE' });
  const tabs = h('div', { className: 'tab-list', role: 'tablist', 'aria-label': 'Lịch sử tài sản' });
  const panel = h('div', { className: 'card-body', role: 'tabpanel', id: 'history-panel', tabindex: 0, 'aria-live': 'polite' });
  const history = [{ key: 'assignment', label: 'Phân bổ', note: 'Assignment History — PLANNED. Chưa kết nối API lịch sử phân bổ.' }, { key: 'maintenance', label: 'Bảo trì', note: 'Maintenance History — PLANNED. Chưa kết nối API bảo trì.' }, { key: 'status', label: 'Trạng thái' }];
  let statusHistory = null;
  const choose = key => {
    statusHistory?.dispose(); statusHistory = null;
    for (const child of tabs.children) { const selected = child.dataset.key === key; child.classList.toggle('active', selected); child.setAttribute('aria-selected', String(selected)); child.tabIndex = selected ? 0 : -1; }
    panel.setAttribute('aria-labelledby', 'history-tab-' + key);
    if (key !== 'status') { panel.textContent = history.find(item => item.key === key).note; return; }
    statusHistory = createAssetStatusHistory(services, id);
    panel.replaceChildren(statusHistory.element); void statusHistory.load();
  };
  history.forEach((item, index) => tabs.append(button(item.label, { kind: 'ghost', className: 'tab-button', role: 'tab', id: 'history-tab-' + item.key, 'aria-controls': 'history-panel', dataset: { key: item.key }, onClick: () => choose(item.key), onKeyDown: event => {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? history.length - 1 : (index + (event.key === 'ArrowRight' ? 1 : -1) + history.length) % history.length;
    choose(history[next].key);
    tabs.children[next].focus();
  } })));
  choose('assignment');
  const info = (label, value) => h('div', { className: 'detail-item' }, h('dt', {}, label), h('dd', {}, value || 'Chưa có thông tin'));
  return h('div', {}, pageHeader(asset.name, asset.assetCode + ' · Thông tin thiết bị', { eyebrow: 'Chi tiết tài sản', actions: [button('Danh sách', { kind: 'secondary', iconName: 'arrow-left', onClick: () => route('/assets') }), ...(can(services, 'assets.update') ? [button('Chỉnh sửa', { iconName: 'pencil', onClick: () => route('/assets/' + asset.id + '/edit') })] : []), ...(canRetireAsset(services, asset) ? [button('Ngừng sử dụng', { kind: 'danger', onClick: () => assetRetireDialog(services, asset, () => window.dispatchEvent(new Event('frontend-refresh'))) })] : [])] }),
    h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('h2', {}, 'Thông tin chung'), badge(asset.status)), h('dl', { className: 'card-body detail-grid' }, info('Mã tài sản', asset.assetCode), info('Tên tài sản', asset.name), info('Loại tài sản', asset.assetType?.name), info('Serial Number', asset.serialNumber), info('Thương hiệu', asset.brand), info('Model', asset.model), info('Phòng ban sở hữu', asset.owningDepartment?.name), info('Vị trí', asset.location), info('Người sử dụng', asset.currentAssignment?.user?.displayName || (services.mode === 'mock' ? 'Chưa phân bổ' : 'Chưa có dữ liệu cấp phát')))),
    h('div', { className: 'dashboard-grid' }, h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('h2', {}, 'Thông tin kỹ thuật')), h('dl', { className: 'card-body detail-grid' }, info('Cấu hình', asset.specification), info('Hệ điều hành', asset.operatingSystem))), h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('h2', {}, 'Mua sắm & bảo hành')), h('dl', { className: 'card-body detail-grid' }, info('Ngày mua', date(asset.purchaseDate)), can(services, 'assets.cost.read') ? info('Giá mua', money(asset.purchasePrice)) : null, info('Hết bảo hành', date(asset.warrantyExpirationDate)), info('Ngày cập nhật', date(asset.updatedAt))))),
    h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('h2', {}, 'Ghi chú')), h('p', { className: 'card-body whitespace-pre-wrap' }, asset.note || 'Chưa có ghi chú.')),
    h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('h2', {}, 'Lịch sử tài sản')), tabs, panel));
}
