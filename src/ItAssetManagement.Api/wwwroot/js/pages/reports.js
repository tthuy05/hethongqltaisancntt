import { h } from '../utils/dom.js';
import { button, pageHeader, dataTable, pagination, statCard, badge } from '../components.js';
import { can, date, money, moneyExact, moneyCompact, moneyCell, route, setQuery } from './shared.js';
import { ServiceError } from '../services/service-error.js';
import { readReportQuery, validateReportQuery, reportNumber, reportUtc } from './reports-query.js';
import { reportExcelActions } from '../components/report-excel.js';
import { openAssetDetail, openMaintenanceDetail } from '../components/record-detail.js';

export async function reportsPage(services) {
  if (services.reportingEnabled !== true) throw new ServiceError(404, 'MODULE_UNAVAILABLE', 'Báo cáo chưa được mở.');
  const session = services.auth?.session;
  const actorId = session?.id;
  const pageHash = window.location.hash;

  if (!Number.isSafeInteger(actorId) || actorId <= 0) {
    throw new ServiceError(401, 'UNAUTHENTICATED', 'Vui lòng đăng nhập để xem báo cáo.');
  }

  const canAssets = can(services, 'reports.assets.read');
  const canAssignments = can(services, 'reports.assignments.read');
  const canMaintenance = can(services, 'reports.maintenance.read');
  const canLicenses = can(services, 'reports.licenses.read');
  const canCost = can(services, 'reports.cost.read');
  const canReplacement = can(services, 'reports.replacement.read');

  if (!canAssets && !canAssignments && !canMaintenance && !canLicenses && !canCost && !canReplacement) {
    throw new ServiceError(403, 'FORBIDDEN', 'Bạn không có quyền truy cập module báo cáo.');
  }

  const query = readReportQuery();
  const allowedTabs = [['inventory', canAssets], ['groups', canAssets], ['assignments', canAssignments], ['maintenance', canMaintenance], ['expiry', canAssets || canLicenses], ['cost', canCost], ['replacement', canReplacement]]
    .filter(([, allowed]) => allowed).map(([tab]) => tab);
  const currentTab = allowedTabs.includes(query.tab) ? query.tab : allowedTabs[0];
  let viewRoot;
  const required = { inventory: ['reports.assets.read'], groups: ['reports.assets.read'], assignments: ['reports.assignments.read'], maintenance: ['reports.maintenance.read'],
    expiry: [...(canAssets ? ['reports.assets.read'] : []), ...(canLicenses ? ['reports.licenses.read'] : [])], cost: ['reports.cost.read'], replacement: ['reports.replacement.read'] }[currentTab];
  const validView = () => services.reportingEnabled === true && services.auth?.session?.id === actorId && window.location.hash === pageHash &&
    required.every(p => can(services, p)) && (!canCost || can(services, 'reports.cost.read'));
  const navigate = values => { if (viewRoot?.isConnected && validView()) setQuery('/reports', validateReportQuery(values)); };
  const assetCell = (code, id) => can(services, 'assets.read') && Number.isSafeInteger(id) && id > 0
    ? button(code, { kind: 'ghost', onClick: () => { if (viewRoot?.isConnected && validView() && can(services, 'assets.read')) route('/assets/' + id); } }) : code;
  // Pick departments / asset types by name when the catalog is readable; otherwise keep the ID box.
  const catalog = async (service, permission) => {
    if (!service?.list || !can(services, permission)) return null;
    try { return (await service.list({ page: 1, pageSize: 100 })).items.map(item => [String(item.id), item.name]); } catch { return null; }
  };
  // Loaded alongside the report data, never before it; an ID box is upgraded to a picker once names arrive.
  const catalogsPending = Promise.all([catalog(services.departments, 'departments.read'), catalog(services.assetTypes, 'asset-types.read')]);
  let filterCount = 0;
  function filters(label, specs, state, reset = {}) {
    const prefix = 'report-filter-' + ++filterCount;
    const controls = specs.map(spec => {
      const id = prefix + '-' + spec.name;
      const options = spec.options && state[spec.name] && !spec.options.some(([value]) => value === String(state[spec.name])) ? [...spec.options, [String(state[spec.name]), 'ID ' + state[spec.name]]] : spec.options;
      const input = spec.options ? h('select', { id, name: spec.name, className: 'select', value: state[spec.name] || '' },
        options.map(([value, text]) => h('option', { value }, text)))
        : h('input', { id, name: spec.name, className: 'input', type: spec.type || 'number', value: state[spec.name] || '',
          maxLength: spec.type === 'search' ? (spec.maxLength ?? 200) : undefined, placeholder: spec.placeholder,
          min: spec.type ? undefined : (spec.name === 'year' || spec.name === 'plannedYear' ? 2000 : 1),
          max: spec.name === 'year' || spec.name === 'plannedYear' ? 2100 : spec.name === 'pageSize' ? 100 : undefined,
          step: spec.type === 'datetime-local' ? '0.001' : 1, required: spec.required || false });
      const caption = h('label', { htmlFor: id }, spec.label);
      const control = { spec, input, element: h('div', { className: 'filter-field' }, caption, input) };
      if (spec.catalog !== undefined) void catalogsPending.then(lists => {
        const names = lists[spec.catalog], value = String(control.input.value || '');
        if (!names || typeof control.input.replaceWith !== 'function') return;
        const picker = h('select', { id, name: spec.name, className: 'select' }, h('option', { value: '' }, spec.all),
          names.map(([key, text]) => h('option', { value: key }, text)), value && !names.some(([key]) => key === value) ? h('option', { value }, 'ID ' + value) : null);
        picker.value = value; control.input.replaceWith(picker); control.input = picker; caption.textContent = spec.friendly;
      });
      return control;
    });
    const error = h('p', { className: 'field-error report-filter-error', role: 'alert' });
    return h('form', { className: 'filters report-filters', 'aria-label': label, onSubmit: event => {
      event.preventDefault(); if (!viewRoot?.isConnected || !validView()) return;
      try {
        const values = { ...state, ...reset };
        for (const { spec, input } of controls) {
          if (spec.required && input.value === '') throw new ServiceError(400, 'VALIDATION_ERROR', 'Vui lòng nhập năm ngân sách.');
          values[spec.name] = spec.type === 'search' ? input.value.trim() : input.value;
        }
        navigate(values);
      } catch (failure) { error.textContent = failure.message || 'Bộ lọc không hợp lệ.'; }
    } }, controls.map(c => c.element), h('div', { className: 'toolbar report-filter-actions' },
      button('Lọc', { type: 'submit', iconName: 'search' }),
      button('Đặt lại', { kind: 'secondary', onClick: () => navigate({ tab: currentTab }) })), error);
  }
  const priorities = [['', 'Tất cả mức ưu tiên'], ['LOW', 'Thấp'], ['MEDIUM', 'Trung bình'], ['HIGH', 'Cao'], ['CRITICAL', 'Khẩn cấp']];
  const pageSizeFilter = { name: 'pageSize', label: 'Số dòng mỗi trang' };
  const departmentFilter = { name: 'departmentId', label: 'ID phòng ban (tùy chọn)', catalog: 0, friendly: 'Phòng ban', all: 'Tất cả phòng ban' };
  const typeFilter = { name: 'assetTypeId', label: 'ID loại tài sản (tùy chọn)', catalog: 1, friendly: 'Loại tài sản', all: 'Tất cả loại' };
  const assetFilter = { name: 'assetCode', label: 'Mã tài sản (tùy chọn)', type: 'search', maxLength: 50, placeholder: 'Ví dụ: AST-000012' };
  const assetStatuses = [['', 'Tất cả trạng thái'], ['IN_STOCK', 'Trong kho'], ['IN_USE', 'Đang sử dụng'],
    ['MAINTENANCE', 'Đang bảo trì'], ['BROKEN', 'Hỏng'], ['RETIRED', 'Ngừng sử dụng']];

  // Mentor feedback: 7 tabs were too many. Four groups; groups with two reports get a sub-switch.
  // URL `tab` values stay the same so bookmarks, Excel export and filters keep working.
  const groups = [
    { label: 'Tài sản', iconName: 'laptop', tabs: [['inventory', 'Kiểm kê tài sản', canAssets], ['groups', 'Tổng hợp tài sản', canAssets]] },
    { label: 'Cấp phát & Bảo trì', iconName: 'assignment', tabs: [['assignments', 'Lịch sử phân bổ', canAssignments], ['maintenance', 'Lịch sử bảo trì', canMaintenance]] },
    { label: 'Hạn bảo hành & License', iconName: 'license', tabs: [['expiry', 'Hạn bảo hành & License', canAssets || canLicenses]] },
    { label: canCost ? 'Chi phí & Thay thế' : 'Đề xuất thay thế', iconName: 'chart', tabs: [['cost', 'Chi phí & Ngân sách', canCost], ['replacement', 'Đề xuất thay thế', canReplacement]] }
  ].map(group => ({ ...group, tabs: group.tabs.filter(([, , allowed]) => allowed) })).filter(group => group.tabs.length);
  const activeGroup = groups.find(group => group.tabs.some(([tab]) => tab === currentTab));
  const firstPage = tab => ['inventory', 'assignments', 'maintenance', 'replacement'].includes(tab) ? { tab, page: 1 } : { tab };
  const tabBar = h('div', { className: 'tab-bar', role: 'tablist', 'aria-label': 'Nhóm báo cáo' }, ...groups.map(group => button(group.label, {
    kind: group === activeGroup ? 'primary' : 'secondary', iconName: group.iconName, role: 'tab', 'aria-selected': String(group === activeGroup),
    onClick: () => navigate(firstPage(group.tabs[0][0])) })));
  const subTabs = activeGroup && activeGroup.tabs.length > 1 ? h('div', { className: 'report-subtabs', role: 'tablist', 'aria-label': activeGroup.label },
    ...activeGroup.tabs.map(([tab, label]) => button(label, { kind: tab === currentTab ? 'primary' : 'secondary', role: 'tab', 'aria-selected': String(tab === currentTab), onClick: () => navigate(firstPage(tab)) }))) : null;
  const rowHint = h('p', { className: 'meta report-note' }, 'Bấm vào một dòng để xem chi tiết: người đang dùng, lịch sử cấp phát và bảo trì.');
  const assetRow = (id, pairs) => row => { if (viewRoot?.isConnected && validView()) openAssetDetail(services, id(row), pairs(row)); };

  let tabContent = null;
  const page = reportNumber(query.page, 1);
  const pageSize = reportNumber(query.pageSize, 20);

  // Tab 1: Kiểm kê tài sản (EP-076)
  if (currentTab === 'inventory') {
    const keyword = query.keyword || '';
    const status = query.status || '';
    const sortBy = query.sortBy || 'createdAt', sortDirection = query.sortDirection || 'desc';
    if (sortBy === 'purchaseCost' && !canCost)
      throw new ServiceError(403, 'FORBIDDEN', 'Yêu cầu quyền xem chi phí để sắp xếp theo giá mua.');
    const res = await services.reports.assetInventory({
      page, pageSize,
      keyword: keyword || undefined,
      status: status || undefined,
      departmentId: reportNumber(query.departmentId), assetTypeId: reportNumber(query.assetTypeId),
      purchasedFrom: query.purchasedFrom, purchasedTo: query.purchasedTo, sortBy, sortDirection
    });
    const state = { ...query, tab: 'inventory', keyword, status, pageSize, sortBy, sortDirection };
    const filterForm = filters('Lọc kiểm kê tài sản', [
      { name: 'keyword', label: 'Mã hoặc tên tài sản', type: 'search', placeholder: 'Tìm mã hoặc tên tài sản…' },
      { name: 'status', label: 'Trạng thái tài sản', options: assetStatuses }, departmentFilter, typeFilter,
      { name: 'purchasedFrom', label: 'Ngày mua từ', type: 'date' }, { name: 'purchasedTo', label: 'Ngày mua đến', type: 'date' },
      { name: 'sortBy', label: 'Sắp xếp theo', options: [['createdAt', 'Ngày tạo'], ['assetCode', 'Mã tài sản'], ['name', 'Tên tài sản'],
        ['purchaseDate', 'Ngày mua'], ...(canCost ? [['purchaseCost', 'Giá mua']] : [])] },
      { name: 'sortDirection', label: 'Chiều sắp xếp', options: [['desc', 'Giảm dần'], ['asc', 'Tăng dần']] }, pageSizeFilter
    ], state, { page: 1 });

    const columns = [
      { key: 'assetCode', label: 'Mã tài sản', render: row => assetCell(row.assetCode, row.id) },
      { key: 'name', label: 'Tên tài sản', render: row => h('div', { className: 'asset-name' }, h('strong', {}, row.name), h('span', { className: 'meta' }, row.serialNumber || 'Chưa có serial')) },
      { key: 'assetTypeName', label: 'Loại' },
      { key: 'departmentName', label: 'Phòng ban' },
      { key: 'currentStatus', label: 'Trạng thái', render: row => badge(row.currentStatus) },
      { key: 'purchaseDate', label: 'Ngày mua', render: row => date(row.purchaseDate) },
      ...(canCost ? [{ key: 'purchaseCost', label: 'Giá mua', render: row => moneyCell(row.purchaseCost) }] : []),
      { key: 'warrantyEndDate', label: 'Hết bảo hành', render: row => date(row.warrantyEndDate) }
    ];

    tabContent = h('div', {},
      h('section', { className: 'card filters-card' }, filterForm),
      h('section', { className: 'card' },
        rowHint,
        dataTable({ columns, rows: res.items, label: 'Kiểm kê tài sản', onRowClick: assetRow(row => row.id, row => [['Mã tài sản', row.assetCode], ['Tên', row.name], ['Loại', row.assetTypeName], ['Phòng ban', row.departmentName], ['Ngày mua', date(row.purchaseDate)], ['Hết bảo hành', date(row.warrantyEndDate)]]) }),
        pagination(res, p => navigate({ ...state, page: p }))
      )
    );
  }

  // Existing EP-077/078/079 aggregates: counts only, not a financial valuation.
  else if (currentTab === 'groups') {
    const groupQuery = { departmentId: reportNumber(query.departmentId), assetTypeId: reportNumber(query.assetTypeId), status: query.status };
    const [departments, types, statuses] = await Promise.all([
      services.reports.assetsByDepartment(groupQuery), services.reports.assetsByType(groupQuery), services.reports.assetStatus(groupQuery)
    ]);
    const groupTable = (title, rows) => h('section', { className: 'card' },
      h('div', { className: 'card-header' }, h('h2', {}, title)),
      dataTable({ columns: [{ key: 'label', label: 'Nhóm' }, { key: 'count', label: 'Số tài sản' }], rows, label: title }));
    tabContent = h('div', { className: 'report-sections' },
      h('section', { className: 'card filters-card' }, filters('Lọc tổng hợp tài sản', [departmentFilter, typeFilter,
        { name: 'status', label: 'Trạng thái tài sản', options: assetStatuses }], { ...query, tab: 'groups' })),
      h('p', { className: 'meta' }, 'Đếm tài sản chưa lưu trữ theo phòng ban, loại và trạng thái. Không phải định giá; không áp dụng bộ lọc từ khóa/ngày mua của tab Kiểm kê. Các bảng là những truy vấn đọc độc lập.'),
      groupTable('Tài sản theo phòng ban', departments), groupTable('Tài sản theo loại', types), groupTable('Tài sản theo trạng thái', statuses));
  }

  // Tab 2: Lịch sử phân bổ (EP-080)
  else if (currentTab === 'assignments') {
    const state = { ...query, tab: 'assignments', pageSize };
    const res = await services.reports.assignmentHistory({ page, pageSize, assetId: reportNumber(query.assetId), assetCode: query.assetCode,
      userId: reportNumber(query.userId), departmentId: reportNumber(query.departmentId),
      fromUtc: reportUtc(query.assignmentFrom), toUtc: reportUtc(query.assignmentTo) });
    const columns = [
      { key: 'assetCode', label: 'Mã tài sản', render: row => assetCell(row.assetCode, row.assetId) },
      { key: 'assetName', label: 'Tên thiết bị' },
      { key: 'assignedUserName', label: 'Người nhận', render: row => row.assignedUserName || '—' },
      { key: 'assignedDepartmentName', label: 'Phòng ban nhận', render: row => row.assignedDepartmentName || '—' },
      { key: 'assignedAtUtc', label: 'Ngày giao', render: row => date(row.assignedAtUtc) },
      { key: 'returnedAtUtc', label: 'Ngày trả', render: row => date(row.returnedAtUtc) },
      { key: 'status', label: 'Trạng thái', render: row => badge(row.status === 'RETURNED' ? 'Resolved' : 'InUse') }
    ];

    tabContent = h('section', { className: 'card' },
      filters('Lọc lịch sử cấp phát', [assetFilter, { name: 'userId', label: 'ID người nhận (tùy chọn)' }, departmentFilter,
        { name: 'assignmentFrom', label: 'Cấp phát từ (UTC)', type: 'datetime-local' },
        { name: 'assignmentTo', label: 'Cấp phát đến (UTC)', type: 'datetime-local' }, pageSizeFilter], state, { page: 1, assetId: undefined }),
      h('p', { className: 'meta report-note' }, 'Khoảng thời gian lọc theo lúc cấp phát (UTC), không phải ngày thu hồi. Phòng ban là bên nhận; không phải phòng ban sở hữu tài sản. Chỉ đọc lịch sử, không thực hiện cấp phát/thu hồi.'),
      rowHint,
      dataTable({ columns, rows: res.items, label: 'Báo cáo phân bổ', onRowClick: assetRow(row => row.assetId, row => [['Mã tài sản', row.assetCode], ['Thiết bị', row.assetName], ['Người nhận', row.assignedUserName || '—'], ['Phòng ban nhận', row.assignedDepartmentName || '—'], ['Ngày giao', date(row.assignedAtUtc)], ['Ngày trả', date(row.returnedAtUtc)]]) }),
      pagination(res, p => navigate({ ...state, page: p }))
    );
  }

  // Tab 3: Lịch sử bảo trì (EP-081)
  else if (currentTab === 'maintenance') {
    const state = { ...query, tab: 'maintenance', pageSize };
    const res = await services.reports.maintenanceHistory({ page, pageSize, assetId: reportNumber(query.assetId), assetCode: query.assetCode,
      technicianId: reportNumber(query.technicianId), status: query.maintenanceStatus,
      fromUtc: reportUtc(query.maintenanceFrom), toUtc: reportUtc(query.maintenanceTo) });
    const columns = [
      { key: 'ticketCode', label: 'Mã phiếu' },
      { key: 'assetCode', label: 'Mã tài sản', render: row => assetCell(row.assetCode, row.assetId) },
      { key: 'title', label: 'Tiêu đề' },
      { key: 'priority', label: 'Ưu tiên', render: row => badge(row.priority) },
      { key: 'status', label: 'Trạng thái', render: row => badge(row.status) },
      { key: 'technicianName', label: 'Kỹ thuật viên', render: row => row.technicianName || '—' },
      { key: 'openedAtUtc', label: 'Ngày tạo', render: row => date(row.openedAtUtc) },
      { key: 'resolvedAtUtc', label: 'Hoàn tất', render: row => date(row.resolvedAtUtc) },
      ...(canCost ? [{ key: 'cost', label: 'Chi phí', render: row => moneyCell(row.cost) }] : [])
    ];

    tabContent = h('section', { className: 'card' },
      filters('Lọc báo cáo bảo trì', [assetFilter, { name: 'technicianId', label: 'ID kỹ thuật viên (tùy chọn)' },
        { name: 'maintenanceStatus', label: 'Trạng thái phiếu', options: [['', 'Tất cả trạng thái'], ['PENDING', 'Chờ xử lý'],
          ['IN_PROGRESS', 'Đang xử lý'], ['RESOLVED', 'Đã xử lý'], ['FAILED', 'Không khắc phục được'], ['CANCELLED', 'Đã hủy']] },
        { name: 'maintenanceFrom', label: 'Mở phiếu từ (UTC)', type: 'datetime-local' },
        { name: 'maintenanceTo', label: 'Mở phiếu đến (UTC)', type: 'datetime-local' }, pageSizeFilter], state, { page: 1, assetId: undefined }),
      h('p', { className: 'meta report-note' }, 'Bộ lọc thời gian dùng lúc mở phiếu (UTC). Mỗi dòng là trạng thái hiện tại của một phiếu, không phải mọi sự kiện trong nhật ký bảo trì.'),
      h('p', { className: 'meta report-note' }, 'Bấm vào một dòng để xem chi tiết phiếu và quá trình xử lý.'),
      dataTable({ columns, rows: res.items, label: 'Lịch sử bảo trì', onRowClick: row => { if (viewRoot?.isConnected && validView()) openMaintenanceDetail(services, row.id, row.assetId); } }),
      pagination(res, p => navigate({ ...state, page: p }))
    );
  }

  // Tab 4: Hạn bảo hành & License (EP-083 & EP-084)
  else if (currentTab === 'expiry') {
    const warrantyPage = reportNumber(query.warrantyPage, 1), licensePage = reportNumber(query.licensePage, 1);
    const expiryState = { ...query, tab: 'expiry', pageSize };
    const [warranties, licenses] = await Promise.all([
      canAssets ? services.reports.warrantyExpiration({ page: warrantyPage, pageSize, fromDate: query.warrantyFrom, toDate: query.warrantyTo,
        departmentId: reportNumber(query.departmentId), assetTypeId: reportNumber(query.assetTypeId) }) : null,
      canLicenses ? services.reports.licenseExpiration({ page: licensePage, pageSize, fromUtc: reportUtc(query.licenseFrom), toUtc: reportUtc(query.licenseTo),
        softwareId: reportNumber(query.softwareId), status: query.licenseStatus }) : null
    ]);

    const warrantyColumns = [
      { key: 'assetCode', label: 'Mã tài sản', render: row => assetCell(row.assetCode, row.id) },
      { key: 'name', label: 'Thiết bị' },
      { key: 'departmentName', label: 'Phòng ban' },
      { key: 'warrantyEndDate', label: 'Ngày hết hạn', render: row => date(row.warrantyEndDate) },
      { key: 'daysRemaining', label: 'Còn lại', render: row => `${row.daysRemaining} ngày` },
      { key: 'isExpired', label: 'Trạng thái', render: row => badge(row.isExpired ? 'Đã hết hạn' : 'Còn hạn') }
    ];

    const licenseColumns = [
      { key: 'licenseCode', label: 'Mã license', render: row => services.licenseEnabled === true && can(services, 'licenses.read') ? h('a', { href: '#/licenses/' + row.id }, row.licenseCode) : row.licenseCode },
      { label: 'Seat đang dùng', className: 'nowrap', render: row => `${row.activeSeats}/${row.totalQuantity}` },
      { key: 'softwareName', label: 'Phần mềm' },
      { key: 'licenseType', label: 'Loại' },
      { key: 'licenseKeyLast4', label: 'Key', render: row => row.licenseKeyLast4 ? `••••-${row.licenseKeyLast4}` : '—' },
      { key: 'expiresAtUtc', label: 'Ngày hết hạn', render: row => date(row.expiresAtUtc) },
      { key: 'daysRemaining', label: 'Còn lại', render: row => row.expiresAtUtc ? `${row.daysRemaining} ngày` : 'Không áp dụng' },
      { key: 'isExpired', label: 'Trạng thái', render: row => badge(!row.expiresAtUtc ? 'Không có thời hạn' : row.isExpired ? 'Đã hết hạn' : 'Còn hạn') }
    ];

    tabContent = h('div', { className: 'report-sections' },
      canAssets ? h('section', { className: 'card', 'aria-label': 'Báo cáo hạn bảo hành' },
        h('div', { className: 'card-header' }, h('h2', {}, 'Hạn bảo hành')),
        filters('Lọc hạn bảo hành', [{ name: 'warrantyFrom', label: 'Hết hạn từ ngày', type: 'date' },
          { name: 'warrantyTo', label: 'Hết hạn đến ngày', type: 'date' }, departmentFilter, typeFilter, pageSizeFilter], expiryState, { warrantyPage: 1, licensePage: 1 }),
        h('p', { className: 'meta report-note' }, 'Không chọn ngày: hiển thị tất cả tài sản có hạn bảo hành, không chỉ thiết bị sắp hết hạn.'),
        dataTable({ columns: warrantyColumns, rows: warranties.items, label: 'Hạn bảo hành', onRowClick: assetRow(row => row.id, row => [['Mã tài sản', row.assetCode], ['Thiết bị', row.name], ['Phòng ban', row.departmentName], ['Hết bảo hành', date(row.warrantyEndDate)]]) }),
        pagination(warranties, p => navigate({ ...expiryState, warrantyPage: p, licensePage }))
      ) : null,
      canLicenses ? h('section', { className: 'card', 'aria-label': 'Báo cáo thời hạn License' },
        h('div', { className: 'card-header' }, h('h2', {}, 'Thời hạn License')),
        filters('Lọc thời hạn License', [{ name: 'licenseFrom', label: 'Hết hạn từ thời điểm (UTC)', type: 'datetime-local' },
          { name: 'licenseTo', label: 'Hết hạn đến thời điểm (UTC)', type: 'datetime-local' },
          { name: 'licenseStatus', label: 'Thời hạn', options: [['', 'Tất cả'], ['Expired', 'Đã hết hạn'], ['Expiring', 'Sắp hết hạn trong 30 ngày'], ['Valid', 'Còn hạn trên 30 ngày / không thời hạn']] },
          { name: 'softwareId', label: 'ID phần mềm (tùy chọn)' }, pageSizeFilter], expiryState, { warrantyPage: 1, licensePage: 1 }),
        h('p', { className: 'meta report-note' }, 'Mốc giờ nhập theo UTC. Không chọn bộ lọc: hiển thị tất cả License active; khóa chỉ hiện 4 ký tự cuối.'),
        dataTable({ columns: licenseColumns, rows: licenses.items, label: 'Thời hạn License' }),
        pagination(licenses, p => navigate({ ...expiryState, warrantyPage, licensePage: p }))
      ) : null
    );
  }

  // Tab 5: Chi phí & Ngân sách (EP-082 & EP-086)
  else if (currentTab === 'cost') {
    const currentYear = new Date().getUTCFullYear();
    const year = reportNumber(query.year, Math.min(2100, Math.max(2000, currentYear + 1)));
    const groupBy = query.groupBy || 'month';
    const departmentId = reportNumber(query.departmentId), assetTypeId = reportNumber(query.assetTypeId);
    const [maintenanceCost, budget] = await Promise.all([
      services.reports.maintenanceCost({ groupBy, departmentId, assetTypeId,
        ...(query.costFrom ? { fromUtc: reportUtc(query.costFrom) } : {}), ...(query.costTo ? { toUtc: reportUtc(query.costTo) } : {}) }),
      services.reports.replacementBudget({ year, departmentId, priority: query.priority })
    ]);

    const costColumns = [
      { key: 'groupLabel', label: 'Khoảng thời gian / Nhóm' },
      { key: 'ticketCount', label: 'Số lượt sửa chữa' },
      { key: 'missingCostCount', label: 'Chưa có chi phí', render: row => row.missingCostCount ?? '—' },
      { key: 'totalCost', label: 'Tổng chi phí', render: row => moneyCell(row.totalCost) }
    ];

    tabContent = h('div', { className: 'report-sections' },
      h('section', { className: 'card filters-card' }, filters('Lọc chi phí và ngân sách', [
        { name: 'year', label: 'Năm dự toán thay thế', required: true }, departmentFilter,
        { name: 'priority', label: 'Ưu tiên dự toán', options: priorities },
        { name: 'groupBy', label: 'Nhóm chi phí bảo trì', options: [['month', 'Theo tháng'], ['department', 'Theo phòng ban'], ['type', 'Theo loại tài sản']] }, typeFilter,
        { name: 'costFrom', label: 'Hoàn tất bảo trì từ (UTC)', type: 'datetime-local' },
        { name: 'costTo', label: 'Hoàn tất bảo trì đến (UTC)', type: 'datetime-local' }
      ], { ...query, tab: 'cost', year, groupBy })),
      h('p', { className: 'meta' }, 'Năm và mức ưu tiên chỉ lọc dự toán thay thế. Khoảng hoàn tất (UTC) và loại tài sản chỉ lọc chi phí bảo trì; bỏ trống thời gian sẽ tổng hợp tất cả. Phòng ban áp dụng cho cả hai. Nhóm “Chưa có ngày hoàn thành” chỉ có khi không lọc thời gian.'),
      h('div', { className: 'grid-kpis' },
        statCard({ label: 'Tổng chi phí bảo trì', value: moneyCompact(maintenanceCost?.totalCost), title: moneyExact(maintenanceCost?.totalCost), detail: `${maintenanceCost?.missingCostCount ?? '—'}/${maintenanceCost?.ticketCount ?? '—'} phiếu chưa có chi phí; tổng phần đã biết`, iconName: 'wrench', tone: 'amber' }),
        statCard({ label: `Dự toán thay thế năm ${year}`, value: moneyCompact(budget?.totalBudget), title: moneyExact(budget?.totalBudget), detail: `${budget?.totalRecommendations ?? '—'} khuyến nghị; ${budget?.missingEstimateCount ?? '—'} chưa có dự toán`, iconName: 'chart', tone: 'green' })
      ),
      h('section', { className: 'card' },
        h('div', { className: 'card-header' }, h('h2', {}, `Tổng hợp chi phí bảo trì ${groupBy === 'month' ? 'theo tháng' : groupBy === 'type' ? 'theo loại tài sản' : 'theo phòng ban'}`)),
        dataTable({ columns: costColumns, rows: maintenanceCost?.groups || [], label: 'Chi phí bảo trì' })
      )
    );
  }

  else if (currentTab === 'replacement') {
    const res = await services.reports.replacementRecommendations({ page, pageSize, departmentId: reportNumber(query.departmentId),
      priority: query.priority, disposition: query.disposition, plannedYear: reportNumber(query.plannedYear),
      generatedFromUtc: reportUtc(query.generatedFrom), generatedToUtc: reportUtc(query.generatedTo) });
    const state = { ...query, tab: 'replacement', pageSize };
    const columns = [
      { key: 'assetCode', label: 'Mã tài sản', render: row => assetCell(row.assetCode, row.assetId) },
      { key: 'assetName', label: 'Tài sản' }, { key: 'departmentName', label: 'Phòng ban' }, { key: 'ruleCode', label: 'Quy tắc' },
      { key: 'priority', label: 'Ưu tiên', render: row => badge(priorities.find(([value]) => value === row.priority)?.[1] || row.priority) },
      { key: 'disposition', label: 'Xử lý', render: row => badge(({ ACTIVE: 'Đang xem xét', PLANNED: 'Đã lên kế hoạch', DISMISSED: 'Đã bỏ qua', SUPERSEDED: 'Đã thay thế' })[row.disposition] || row.disposition) },
      { key: 'plannedYear', label: 'Năm dự kiến' }, { key: 'isCurrent', label: 'Hiện hành', render: row => row.isCurrent ? 'Có' : 'Không' },
      { key: 'score', label: 'Điểm', render: row => row.score ?? 'Chưa có điểm' },
      { key: 'recommendedAtUtc', label: 'Đề xuất lúc (UTC)', render: row => {
        const value = row.recommendedAtUtc ? new Date(row.recommendedAtUtc) : null;
        return value && Number.isFinite(value.getTime()) ? value.toISOString().replace('T', ' ').replace('Z', '') : '—';
      } },
      ...(canCost ? [{ key: 'estimatedCost', label: 'Chi phí dự kiến', render: row => moneyCell(row.estimatedCost) }] : [])
    ];
    tabContent = h('section', { className: 'card' },
      filters('Lọc báo cáo đề xuất thay thế', [departmentFilter, { name: 'priority', label: 'Ưu tiên', options: priorities },
        { name: 'disposition', label: 'Trạng thái xử lý', options: [['', 'Tất cả'], ['ACTIVE', 'Đang xem xét'], ['PLANNED', 'Đã lên kế hoạch'], ['DISMISSED', 'Đã bỏ qua'], ['SUPERSEDED', 'Đã thay thế']] },
        { name: 'plannedYear', label: 'Năm dự kiến (tùy chọn)' }, { name: 'generatedFrom', label: 'Đề xuất từ (UTC)', type: 'datetime-local' },
        { name: 'generatedTo', label: 'Đề xuất đến (UTC)', type: 'datetime-local' }, pageSizeFilter], state, { page: 1 }),
      h('p', { className: 'meta report-note' }, 'Báo cáo chỉ đọc, gồm các bản hiện hành/lịch sử khớp bộ lọc; không chạy đánh giá hoặc sửa đề xuất.'),
      rowHint,
      dataTable({ columns, rows: res.items, label: 'Báo cáo đề xuất thay thế', onRowClick: assetRow(row => row.assetId, row => [['Mã tài sản', row.assetCode], ['Tài sản', row.assetName], ['Phòng ban', row.departmentName], ['Quy tắc', row.ruleCode], ['Năm dự kiến', row.plannedYear ?? '—']]) }),
      pagination(res, p => navigate({ ...state, page: p })));
  }

  if (!validView())
    throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên, quyền hoặc trang đã thay đổi.');
  viewRoot = h('div', { className: 'reports-page' },
    pageHeader('Báo cáo hệ thống', 'Tra cứu số liệu kiểm kê, phân bổ, bảo trì, hạn bản quyền và ngân sách.', { eyebrow: 'Báo cáo & Thống kê' }),
    tabBar,
    subTabs,
    reportExcelActions(services, currentTab, query, () => viewRoot?.isConnected && validView()),
    tabContent
  );
  return viewRoot;
}
