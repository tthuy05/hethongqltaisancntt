import { h } from '../utils/dom.js';
import { button, pageHeader, dataTable, pagination, statCard, badge } from '../components.js';
import { can, date, money, route, queryFrom, setQuery } from './shared.js';
import { ServiceError } from '../services/service-error.js';

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

  if (!canAssets && !canAssignments && !canMaintenance && !canLicenses && !canCost) {
    throw new ServiceError(403, 'FORBIDDEN', 'Bạn không có quyền truy cập module báo cáo.');
  }

  const query = queryFrom();
  const allowedTabs = [['inventory', canAssets], ['assignments', canAssignments], ['maintenance', canMaintenance], ['expiry', canAssets || canLicenses], ['cost', canCost]]
    .filter(([, allowed]) => allowed).map(([tab]) => tab);
  const currentTab = allowedTabs.includes(query.tab) ? query.tab : allowedTabs[0];

  const tabButtons = [];
  if (canAssets) {
    tabButtons.push(button('Kiểm kê tài sản', {
      kind: currentTab === 'inventory' ? 'primary' : 'secondary',
      iconName: 'laptop',
      onClick: () => setQuery('/reports', { tab: 'inventory', page: 1 })
    }));
  }
  if (canAssignments) {
    tabButtons.push(button('Lịch sử phân bổ', {
      kind: currentTab === 'assignments' ? 'primary' : 'secondary',
      iconName: 'assignment',
      onClick: () => setQuery('/reports', { tab: 'assignments', page: 1 })
    }));
  }
  if (canMaintenance) {
    tabButtons.push(button('Lịch sử bảo trì', {
      kind: currentTab === 'maintenance' ? 'primary' : 'secondary',
      iconName: 'maintenance',
      onClick: () => setQuery('/reports', { tab: 'maintenance', page: 1 })
    }));
  }
  if (canAssets || canLicenses) {
    tabButtons.push(button('Hạn bảo hành & License', {
      kind: currentTab === 'expiry' ? 'primary' : 'secondary',
      iconName: 'license',
      onClick: () => setQuery('/reports', { tab: 'expiry', page: 1 })
    }));
  }
  if (canCost) {
    tabButtons.push(button('Chi phí & Ngân sách', {
      kind: currentTab === 'cost' ? 'primary' : 'secondary',
      iconName: 'chart',
      onClick: () => setQuery('/reports', { tab: 'cost' })
    }));
  }

  const tabBar = h('div', { className: 'tab-bar', style: 'display: flex; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 1.5rem;' }, ...tabButtons);

  let tabContent = null;
  const page = Math.max(1, Number(query.page) || 1);
  const pageSize = Math.min(100, Math.max(1, Number(query.pageSize) || 20));

  // Tab 1: Kiểm kê tài sản (EP-076)
  if (currentTab === 'inventory') {
    const keyword = query.keyword || '';
    const status = query.status || '';
    const res = await services.reports.assetInventory({
      page, pageSize,
      keyword: keyword || undefined,
      status: status || undefined,
      sortBy: query.sortBy || 'createdAt',
      sortDirection: query.sortDirection || 'desc'
    });

    const searchInput = h('input', { className: 'input', type: 'search', value: keyword, placeholder: 'Tìm mã hoặc tên tài sản…', 'aria-label': 'Tìm tài sản' });
    const statusSelect = h('select', { className: 'select', 'aria-label': 'Lọc trạng thái' },
      h('option', { value: '' }, 'Tất cả trạng thái'),
      h('option', { value: 'IN_STOCK', selected: status === 'IN_STOCK' }, 'Trong kho'),
      h('option', { value: 'IN_USE', selected: status === 'IN_USE' }, 'Đang sử dụng'),
      h('option', { value: 'MAINTENANCE', selected: status === 'MAINTENANCE' }, 'Đang bảo trì'),
      h('option', { value: 'BROKEN', selected: status === 'BROKEN' }, 'Hỏng'),
      h('option', { value: 'RETIRED', selected: status === 'RETIRED' }, 'Ngừng sử dụng')
    );

    const filterForm = h('form', {
      className: 'filters',
      onSubmit: e => {
        e.preventDefault();
        setQuery('/reports', { tab: 'inventory', page: 1, keyword: searchInput.value.trim() || undefined, status: statusSelect.value || undefined });
      }
    },
    searchInput,
    statusSelect,
    button('Lọc', { type: 'submit', iconName: 'search' }),
    button('Đặt lại', { kind: 'secondary', onClick: () => setQuery('/reports', { tab: 'inventory', page: 1 }) })
    );

    const columns = [
      { key: 'assetCode', label: 'Mã tài sản', render: row => button(row.assetCode, { kind: 'ghost', onClick: () => route('/assets/' + row.id) }) },
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
        pagination(res, p => setQuery('/reports', { ...query, tab: 'inventory', page: p }))
      )
    );
  }

  // Tab 2: Lịch sử phân bổ (EP-080)
  else if (currentTab === 'assignments') {
    const res = await services.reports.assignmentHistory({ page, pageSize });
    const columns = [
      { key: 'assetCode', label: 'Mã tài sản', render: row => button(row.assetCode, { kind: 'ghost', onClick: () => route('/assets/' + row.assetId) }) },
      { key: 'assetName', label: 'Tên thiết bị' },
      { key: 'assignedUserName', label: 'Người nhận', render: row => row.assignedUserName || '—' },
      { key: 'assignedDepartmentName', label: 'Phòng ban nhận', render: row => row.assignedDepartmentName || '—' },
      { key: 'assignedAtUtc', label: 'Ngày giao', render: row => date(row.assignedAtUtc) },
      { key: 'returnedAtUtc', label: 'Ngày trả', render: row => date(row.returnedAtUtc) },
      { key: 'status', label: 'Trạng thái', render: row => badge(row.status === 'RETURNED' ? 'Resolved' : 'InUse') }
    ];

    tabContent = h('section', { className: 'card' },
      dataTable({ columns, rows: res.items, label: 'Báo cáo phân bổ' }),
      pagination(res, p => setQuery('/reports', { ...query, tab: 'assignments', page: p }))
    );
  }

  // Tab 3: Lịch sử bảo trì (EP-081)
  else if (currentTab === 'maintenance') {
    const res = await services.reports.maintenanceHistory({ page, pageSize });
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
      dataTable({ columns, rows: res.items, label: 'Lịch sử bảo trì' }),
      pagination(res, p => setQuery('/reports', { ...query, tab: 'maintenance', page: p }))
    );
  }

  // Tab 4: Hạn bảo hành & License (EP-083 & EP-084)
  else if (currentTab === 'expiry') {
    const [warranties, licenses] = await Promise.all([
      canAssets ? services.reports.warrantyExpiration({ page: 1, pageSize: 10 }) : { items: [] },
      canLicenses ? services.reports.licenseExpiration({ page: 1, pageSize: 10 }) : { items: [] }
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
      { key: 'daysRemaining', label: 'Còn lại', render: row => `${row.daysRemaining} ngày` },
      { key: 'isExpired', label: 'Trạng thái', render: row => badge(row.isExpired ? 'Đã hết hạn' : 'Còn hạn') }
    ];

    tabContent = h('div', { style: 'display: flex; flex-direction: column; gap: 1.5rem;' },
      h('section', { className: 'card' },
        h('div', { className: 'card-header' }, h('h2', {}, 'Hạn bảo hành — 10 dòng đầu, không lọc thời hạn')),
        dataTable({ columns: warrantyColumns, rows: warranties.items, label: 'Hạn bảo hành' })
      ),
      h('section', { className: 'card' },
        h('div', { className: 'card-header' }, h('h2', {}, 'License — 10 dòng đầu, không lọc thời hạn')),
        dataTable({ columns: licenseColumns, rows: licenses.items, label: 'Thời hạn License' })
      )
    );
  }

  // Tab 5: Chi phí & Ngân sách (EP-082 & EP-086)
  else if (currentTab === 'cost') {
    const currentYear = new Date().getUTCFullYear();
    const [maintenanceCost, budget] = await Promise.all([
      services.reports.maintenanceCost({ groupBy: 'month' }),
      services.reports.replacementBudget({ year: currentYear + 1 })
    ]);

    const costColumns = [
      { key: 'groupLabel', label: 'Khoảng thời gian / Nhóm' },
      { key: 'ticketCount', label: 'Số lượt sửa chữa' },
      { key: 'missingCostCount', label: 'Chưa có chi phí', render: row => row.missingCostCount ?? '—' },
      { key: 'totalCost', label: 'Tổng chi phí', render: row => money(row.totalCost) }
    ];

    tabContent = h('div', { style: 'display: flex; flex-direction: column; gap: 1.5rem;' },
      h('div', { className: 'grid-kpis' },
        statCard({ label: 'Tổng chi phí bảo trì', value: money(maintenanceCost?.totalCost), detail: `${maintenanceCost?.missingCostCount ?? '—'}/${maintenanceCost?.ticketCount ?? '—'} phiếu chưa có chi phí; tổng phần đã biết`, iconName: 'wrench', tone: 'amber' }),
        statCard({ label: `Dự toán thay thế năm ${currentYear + 1}`, value: money(budget?.totalBudget), detail: `${budget?.totalRecommendations ?? '—'} khuyến nghị; ${budget?.missingEstimateCount ?? '—'} chưa có dự toán`, iconName: 'chart', tone: 'green' })
      ),
      h('section', { className: 'card' },
        h('div', { className: 'card-header' }, h('h2', {}, 'Tổng hợp chi phí bảo trì theo tháng')),
        dataTable({ columns: costColumns, rows: maintenanceCost?.groups || [], label: 'Chi phí bảo trì' })
      )
    );
  }

  const required = { inventory: ['reports.assets.read'], assignments: ['reports.assignments.read'], maintenance: ['reports.maintenance.read'],
    expiry: [...(canAssets ? ['reports.assets.read'] : []), ...(canLicenses ? ['reports.licenses.read'] : [])], cost: ['reports.cost.read'] }[currentTab];
  if (services.auth?.session?.id !== actorId || window.location.hash !== pageHash || required.some(p => !can(services, p)) ||
      (canCost && !can(services, 'reports.cost.read')))
    throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên, quyền hoặc trang đã thay đổi.');
  return h('div', { className: 'reports-page' },
    pageHeader('Báo cáo hệ thống', 'Tra cứu số liệu kiểm kê, phân bổ, bảo trì, hạn bản quyền và ngân sách.', { eyebrow: 'Báo cáo & Thống kê' }),
    tabBar,
    tabContent
  );
}
