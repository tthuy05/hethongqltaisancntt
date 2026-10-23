import { h } from '../utils/dom.js';
import { button, pageHeader, dataTable, pagination, statCard, badge } from '../components.js';
import { can, date, money, route, setQuery } from './shared.js';
import { ServiceError } from '../services/service-error.js';
import { readReportQuery, validateReportQuery, reportNumber, reportUtc } from './reports-query.js';
import { reportExcelActions } from '../components/report-excel.js';

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
  let filterCount = 0;
  function filters(label, specs, state, reset = {}) {
    const prefix = 'report-filter-' + ++filterCount;
    const controls = specs.map(spec => {
      const id = prefix + '-' + spec.name;
      const input = spec.options ? h('select', { id, name: spec.name, className: 'select', value: state[spec.name] || '' },
        spec.options.map(([value, text]) => h('option', { value }, text)))
        : h('input', { id, name: spec.name, className: 'input', type: spec.type || 'number', value: state[spec.name] || '',
          maxLength: spec.type === 'search' ? 200 : undefined, placeholder: spec.placeholder,
          min: spec.type ? undefined : (spec.name === 'year' || spec.name === 'plannedYear' ? 2000 : 1),
          max: spec.name === 'year' || spec.name === 'plannedYear' ? 2100 : spec.name === 'pageSize' ? 100 : undefined,
          step: spec.type === 'datetime-local' ? '0.001' : 1, required: spec.required || false });
      return { spec, input, element: h('div', { className: 'filter-field' }, h('label', { htmlFor: id }, spec.label), input) };
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
  const departmentFilter = { name: 'departmentId', label: 'ID phòng ban (tùy chọn)' };
  const typeFilter = { name: 'assetTypeId', label: 'ID loại tài sản (tùy chọn)' };
  const assetFilter = { name: 'assetId', label: 'ID tài sản (tùy chọn)' };
  const assetStatuses = [['', 'Tất cả trạng thái'], ['IN_STOCK', 'Trong kho'], ['IN_USE', 'Đang sử dụng'],
    ['MAINTENANCE', 'Đang bảo trì'], ['BROKEN', 'Hỏng'], ['RETIRED', 'Ngừng sử dụng']];

  const tabButtons = [];
  if (canAssets) {
    tabButtons.push(button('Kiểm kê tài sản', {
      kind: currentTab === 'inventory' ? 'primary' : 'secondary',
      iconName: 'laptop',
      onClick: () => navigate({ tab: 'inventory', page: 1 })
    }));
    tabButtons.push(button('Tổng hợp tài sản', { kind: currentTab === 'groups' ? 'primary' : 'secondary',
      iconName: 'chart', onClick: () => navigate({ tab: 'groups' }) }));
  }
  if (canAssignments) {
    tabButtons.push(button('Lịch sử phân bổ', {
      kind: currentTab === 'assignments' ? 'primary' : 'secondary',
      iconName: 'assignment',
      onClick: () => navigate({ tab: 'assignments', page: 1 })
    }));
  }
  if (canMaintenance) {
    tabButtons.push(button('Lịch sử bảo trì', {
      kind: currentTab === 'maintenance' ? 'primary' : 'secondary',
      iconName: 'maintenance',
      onClick: () => navigate({ tab: 'maintenance', page: 1 })
    }));
  }
  if (canAssets || canLicenses) {
    tabButtons.push(button('Hạn bảo hành & License', {
      kind: currentTab === 'expiry' ? 'primary' : 'secondary',
      iconName: 'license',
      onClick: () => navigate({ tab: 'expiry' })
    }));
  }
  if (canCost) {
    tabButtons.push(button('Chi phí & Ngân sách', {
      kind: currentTab === 'cost' ? 'primary' : 'secondary',
      iconName: 'chart',
      onClick: () => navigate({ tab: 'cost' })
    }));
  }
  if (canReplacement) tabButtons.push(button('Đề xuất thay thế', { kind: currentTab === 'replacement' ? 'primary' : 'secondary',
    iconName: 'laptop', onClick: () => navigate({ tab: 'replacement', page: 1 }) }));

  const tabBar = h('div', { className: 'tab-bar' }, ...tabButtons);

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
      { key: 'name', label: 'Tên tài sản' },
      { key: 'assetTypeName', label: 'Loại' },
      { key: 'departmentName', label: 'Phòng ban' },
      { key: 'currentStatus', label: 'Trạng thái', render: row => badge(row.currentStatus) },
      { key: 'purchaseDate', label: 'Ngày mua', render: row => date(row.purchaseDate) },
      ...(canCost ? [{ key: 'purchaseCost', label: 'Giá mua', render: row => money(row.purchaseCost) }] : []),
      { key: 'warrantyEndDate', label: 'Hết bảo hành', render: row => date(row.warrantyEndDate) }
    ];

    tabContent = h('div', {},
      h('section', { className: 'card filters-card' }, filterForm),
      h('section', { className: 'card' },
        dataTable({ columns, rows: res.items, label: 'Kiểm kê tài sản' }),
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
    const res = await services.reports.assignmentHistory({ page, pageSize, assetId: reportNumber(query.assetId),
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
        { name: 'assignmentTo', label: 'Cấp phát đến (UTC)', type: 'datetime-local' }, pageSizeFilter], state, { page: 1 }),
      h('p', { className: 'meta report-note' }, 'Khoảng thời gian lọc theo lúc cấp phát (UTC), không phải ngày thu hồi. Phòng ban là bên nhận; không phải phòng ban sở hữu tài sản. Chỉ đọc lịch sử, không thực hiện cấp phát/thu hồi.'),
      dataTable({ columns, rows: res.items, label: 'Báo cáo phân bổ' }),
      pagination(res, p => navigate({ ...state, page: p }))
    );
  }

  // Tab 3: Lịch sử bảo trì (EP-081)
  else if (currentTab === 'maintenance') {
    const state = { ...query, tab: 'maintenance', pageSize };
    const res = await services.reports.maintenanceHistory({ page, pageSize, assetId: reportNumber(query.assetId),
      technicianId: reportNumber(query.technicianId), status: query.maintenanceStatus,
      fromUtc: reportUtc(query.maintenanceFrom), toUtc: reportUtc(query.maintenanceTo) });
    const columns = [
      { key: 'ticketCode', label: 'Mã phiếu' },
      { key: 'assetCode', label: 'Mã tài sản' },
      { key: 'title', label: 'Tiêu đề' },
      { key: 'priority', label: 'Ưu tiên', render: row => badge(row.priority) },
      { key: 'status', label: 'Trạng thái', render: row => badge(row.status) },
      { key: 'technicianName', label: 'Kỹ thuật viên', render: row => row.technicianName || '—' },
      { key: 'openedAtUtc', label: 'Ngày tạo', render: row => date(row.openedAtUtc) },
      { key: 'resolvedAtUtc', label: 'Hoàn tất', render: row => date(row.resolvedAtUtc) },
      ...(canCost ? [{ key: 'cost', label: 'Chi phí', render: row => money(row.cost) }] : [])
    ];

    tabContent = h('section', { className: 'card' },
      filters('Lọc báo cáo bảo trì', [assetFilter, { name: 'technicianId', label: 'ID kỹ thuật viên (tùy chọn)' },
        { name: 'maintenanceStatus', label: 'Trạng thái phiếu', options: [['', 'Tất cả trạng thái'], ['PENDING', 'Chờ xử lý'],
          ['IN_PROGRESS', 'Đang xử lý'], ['RESOLVED', 'Đã xử lý'], ['FAILED', 'Không khắc phục được'], ['CANCELLED', 'Đã hủy']] },
        { name: 'maintenanceFrom', label: 'Mở phiếu từ (UTC)', type: 'datetime-local' },
        { name: 'maintenanceTo', label: 'Mở phiếu đến (UTC)', type: 'datetime-local' }, pageSizeFilter], state, { page: 1 }),
      h('p', { className: 'meta report-note' }, 'Bộ lọc thời gian dùng lúc mở phiếu (UTC). Mỗi dòng là trạng thái hiện tại của một phiếu, không phải mọi sự kiện trong nhật ký bảo trì.'),
      dataTable({ columns, rows: res.items, label: 'Lịch sử bảo trì' }),
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
      { key: 'assetCode', label: 'Mã tài sản' },
      { key: 'name', label: 'Thiết bị' },
      { key: 'departmentName', label: 'Phòng ban' },
      { key: 'warrantyEndDate', label: 'Ngày hết hạn', render: row => date(row.warrantyEndDate) },
      { key: 'daysRemaining', label: 'Còn lại', render: row => `${row.daysRemaining} ngày` },
      { key: 'isExpired', label: 'Trạng thái', render: row => badge(row.isExpired ? 'Đã hết hạn' : 'Còn hạn') }
    ];

    const licenseColumns = [
      { key: 'licenseCode', label: 'Mã license' },
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
        dataTable({ columns: warrantyColumns, rows: warranties.items, label: 'Hạn bảo hành' }),
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
      { key: 'totalCost', label: 'Tổng chi phí', render: row => money(row.totalCost) }
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
        statCard({ label: 'Tổng chi phí bảo trì', value: money(maintenanceCost?.totalCost), detail: `${maintenanceCost?.missingCostCount ?? '—'}/${maintenanceCost?.ticketCount ?? '—'} phiếu chưa có chi phí; tổng phần đã biết`, iconName: 'wrench', tone: 'amber' }),
        statCard({ label: `Dự toán thay thế năm ${year}`, value: money(budget?.totalBudget), detail: `${budget?.totalRecommendations ?? '—'} khuyến nghị; ${budget?.missingEstimateCount ?? '—'} chưa có dự toán`, iconName: 'chart', tone: 'green' })
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
      ...(canCost ? [{ key: 'estimatedCost', label: 'Chi phí dự kiến', render: row => money(row.estimatedCost) }] : [])
    ];
    tabContent = h('section', { className: 'card' },
      filters('Lọc báo cáo đề xuất thay thế', [departmentFilter, { name: 'priority', label: 'Ưu tiên', options: priorities },
        { name: 'disposition', label: 'Trạng thái xử lý', options: [['', 'Tất cả'], ['ACTIVE', 'Đang xem xét'], ['PLANNED', 'Đã lên kế hoạch'], ['DISMISSED', 'Đã bỏ qua'], ['SUPERSEDED', 'Đã thay thế']] },
        { name: 'plannedYear', label: 'Năm dự kiến (tùy chọn)' }, { name: 'generatedFrom', label: 'Đề xuất từ (UTC)', type: 'datetime-local' },
        { name: 'generatedTo', label: 'Đề xuất đến (UTC)', type: 'datetime-local' }, pageSizeFilter], state, { page: 1 }),
      h('p', { className: 'meta report-note' }, 'Báo cáo chỉ đọc, gồm các bản hiện hành/lịch sử khớp bộ lọc; không chạy đánh giá hoặc sửa đề xuất.'),
      dataTable({ columns, rows: res.items, label: 'Báo cáo đề xuất thay thế' }),
      pagination(res, p => navigate({ ...state, page: p })));
  }

  if (!validView())
    throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên, quyền hoặc trang đã thay đổi.');
  viewRoot = h('div', { className: 'reports-page' },
    pageHeader('Báo cáo hệ thống', 'Tra cứu số liệu kiểm kê, phân bổ, bảo trì, hạn bản quyền và ngân sách.', { eyebrow: 'Báo cáo & Thống kê' }),
    tabBar,
    reportExcelActions(services, currentTab, query, () => viewRoot?.isConnected && validView()),
    tabContent
  );
  return viewRoot;
}
