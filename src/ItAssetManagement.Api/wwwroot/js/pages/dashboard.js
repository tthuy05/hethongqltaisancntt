import { h } from '../utils/dom.js';
import { button, pageHeader, statCard, badge, dataTable } from '../components.js';
import { can, route, statusLabels, money } from './shared.js';
import { ServiceError } from '../services/service-error.js';

export async function dashboardPage(services) {
  const actorId = services.auth?.session?.id, initialHash = window.location.hash;
  const useReporting = services.reportingEnabled === true && Boolean(services.dashboardV2);
  const verify = () => {
    if (!Number.isSafeInteger(actorId) || actorId <= 0 || services.auth?.session?.id !== actorId ||
        window.location.hash !== initialHash)
      throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên hoặc trang đã thay đổi.');
    if (!can(services, useReporting ? 'dashboard.read' : 'assets.read'))
      throw new ServiceError(403, 'FORBIDDEN', 'Bạn không có quyền xem tổng quan hệ thống.');
  };
  verify();
  let summary = null;
  let alerts = null;
  let costs = null;
  let isV2 = false;

  // Keep the existing M1 asset-backed summary independent of the prepared Reporting grant.
  if (useReporting) {
      summary = await services.dashboardV2.summary();
      verify();
      isV2 = true;
      alerts = await services.dashboardV2.alerts();
      verify();
      if (can(services, 'dashboard.cost.read')) {
        costs = await services.dashboardV2.costs();
        verify();
        if (!can(services, 'dashboard.cost.read')) throw new ServiceError(403, 'FORBIDDEN', 'Quyền chi phí đã thay đổi.');
      }
  } else {
    summary = await services.dashboard.summary();
  }
  verify();

  const counts = summary.byStatus || [];
  const canReadCost = can(services, 'dashboard.cost.read') && costs != null;
  const canReadReports = services.reportingEnabled === true && (can(services, 'reports.assets.read') || can(services, 'reports.cost.read') || can(services, 'reports.assignments.read') || can(services, 'reports.maintenance.read') || can(services, 'reports.licenses.read') || can(services, 'reports.replacement.read'));

  const actions = [
    button('Xem tài sản', { kind: 'secondary', onClick: () => route('/assets'), iconName: 'laptop' }),
    ...(canReadReports ? [button('Báo cáo', { kind: 'secondary', onClick: () => route('/reports'), iconName: 'chart' })] : []),
    ...(can(services, 'assets.create') ? [button('Thêm tài sản', { onClick: () => route('/assets/new'), iconName: 'plus' })] : []),
    button('Làm mới', { kind: 'ghost', iconName: 'refresh', onClick: () => route('/dashboard') })
  ];

  const header = pageHeader('Tổng quan hệ thống', 'Thông tin tài sản, cảnh báo và số liệu rõ ràng, tập trung và dễ theo dõi.', {
    eyebrow: 'Tổng quan',
    actions
  });

  // KPI cards
  const kpiCards = [
    statCard({ label: 'Tổng tài sản', value: summary.totalAssets, detail: 'Trong danh mục hiện tại', iconName: 'devices', tone: 'blue' }),
    statCard({ label: 'Đang sử dụng', value: summary.inUse, detail: 'Thiết bị đang được sử dụng', iconName: 'laptop', tone: 'green' }),
    statCard({ label: 'Đang bảo trì', value: summary.maintenance, detail: 'Cần theo dõi kỹ thuật', iconName: 'wrench', tone: 'amber' }),
    statCard({
      label: 'Cần thay thế',
      value: summary.pendingReplacements ?? summary.replacementNeeded ?? '—',
      detail: isV2 ? (summary.pendingReplacements != null ? 'Khuyến nghị thay thế đang chờ' : 'Chưa có khuyến nghị') : (services.mode === 'mock' ? 'Chỉ số minh họa' : 'Dữ liệu tổng hợp M1'),
      iconName: 'alert-triangle',
      tone: (summary.pendingReplacements ?? summary.replacementNeeded) > 0 ? 'red' : 'neutral'
    })
  ];

  // Financial KPI row (Least privilege: only shown when dashboard.cost.read is present and cost data loaded)
  let financialSection = null;
  if (canReadCost && costs) {
    financialSection = h('div', { className: 'grid-kpis financial-kpis', style: 'margin-top: 1rem;' },
      statCard({ label: 'Tổng giá trị tài sản', value: money(costs.totalAssetValue), detail: `${costs.missingPurchaseCostCount ?? '—'}/${costs.assetCount ?? '—'} tài sản chưa có giá; tổng phần đã biết`, iconName: 'chart', tone: 'blue' }),
      statCard({ label: 'Chi phí bảo trì (YTD)', value: money(costs.maintenanceCostYtd), detail: `${costs.missingMaintenanceCostCountYtd ?? '—'}/${costs.maintenanceTicketCountYtd ?? '—'} phiếu chưa có chi phí; từ đầu năm đến hiện tại`, iconName: 'wrench', tone: 'amber' }),
      statCard({ label: 'Dự toán thay thế', value: money(costs.totalBudgetForecast), detail: `${costs.missingEstimateCount ?? 0} tài sản chưa có ước tính`, iconName: 'devices', tone: 'green' })
    );
  }

  // Distribution section
  const distributionCard = h('section', { className: 'card' },
    h('div', { className: 'card-header' },
      h('div', {},
        h('h2', {}, 'Tài sản theo trạng thái'),
        h('p', { className: 'meta' }, isV2 ? 'Truy vấn tổng hợp trực tiếp từ cơ sở dữ liệu' : (services.mode === 'mock' ? 'Tổng hợp từ bộ dữ liệu minh họa' : 'Tổng hợp từ kết quả API'))
      )
    ),
    h('div', { className: 'card-body status-distribution' },
      counts.map(item => h('div', { className: 'distribution-row' },
        h('div', { className: 'toolbar' }, badge(item.status || item.label), h('strong', {}, String(item.count))),
        h('progress', { max: Math.max(summary.totalAssets, 1), value: item.count, 'aria-label': statusLabels[item.status] || item.status || item.label })
      ))
    )
  );

  // Operational Alerts section
  let alertsCard = null;
  if (alerts) {
    const alertItems = [];
    if (alerts.expiringWarrantiesCount > 0) {
      alertItems.push(h('div', { className: 'distribution-row' },
        h('span', { className: 'badge amber' }, 'Hết hạn bảo hành'),
        h('strong', {}, `${alerts.expiringWarrantiesCount} thiết bị`),
        button('Xem chi tiết', { kind: 'ghost', iconName: 'arrow-right', onClick: () => route('/reports?tab=expiry') })
      ));
    }
    if (alerts.expiringLicensesCount > 0) {
      alertItems.push(h('div', { className: 'distribution-row' },
        h('span', { className: 'badge amber' }, 'Hết hạn license'),
        h('strong', {}, `${alerts.expiringLicensesCount} bản quyền`),
        button('Xem chi tiết', { kind: 'ghost', iconName: 'arrow-right', onClick: () => route('/reports?tab=expiry') })
      ));
    }
    if (alerts.openTicketsCount > 0) {
      alertItems.push(h('div', { className: 'distribution-row' },
        h('span', { className: 'badge blue' }, 'Phiếu bảo trì mở'),
        h('strong', {}, `${alerts.openTicketsCount} phiếu (${alerts.highPriorityTicketsCount ?? 0} ưu tiên cao)`),
        button('Xem bảo trì', { kind: 'ghost', iconName: 'arrow-right', onClick: () => route('/reports?tab=maintenance') })
      ));
    }
    if (alertItems.length === 0) {
      alertItems.push(h('p', { className: 'meta' }, 'Không có cảnh báo vận hành nào cần xử lý ngay.'));
    }
    alertsCard = h('section', { className: 'card' },
      h('div', { className: 'card-header' },
        h('div', {},
          h('h2', {}, 'Cảnh báo vận hành'),
          h('p', { className: 'meta' }, 'Bảo hành, bản quyền và phiếu bảo trì cần lưu ý')
        )
      ),
      h('div', { className: 'card-body quick-links' }, alertItems)
    );
  } else {
    // Quick links fallback
    alertsCard = h('section', { className: 'card' },
      h('div', { className: 'card-header' },
        h('div', {},
          h('h2', {}, 'Lối tắt thao tác'),
          h('p', { className: 'meta' }, 'Truy cập nhanh danh mục và báo cáo')
        )
      ),
      h('div', { className: 'card-body quick-links' },
        button('Tra cứu tài sản', { kind: 'secondary', iconName: 'search', onClick: () => route('/assets') }),
        ...(canReadReports ? [button('Báo cáo thống kê', { kind: 'secondary', iconName: 'chart', onClick: () => route('/reports') })] : []),
        button('Danh mục phòng ban', { kind: 'secondary', iconName: 'building', onClick: () => route('/departments') }),
        button('Danh mục loại tài sản', { kind: 'secondary', iconName: 'tag', onClick: () => route('/asset-types') })
      )
    );
  }

  // Recent assets table
  const recentAssetsCard = h('section', { className: 'card recent-assets' },
    h('div', { className: 'card-header' },
      h('div', {},
        h('h2', {}, 'Tài sản mới nhất'),
        h('p', { className: 'meta' }, '5 tài sản theo ngày tạo, mới nhất trước')
      ),
      button('Xem tất cả', { kind: 'ghost', iconName: 'arrow-right', onClick: () => route('/assets') })
    ),
    dataTable({
      rows: summary.recentAssets || [],
      columns: [
        { key: 'assetCode', label: 'Mã tài sản', render: row => button(row.assetCode, { kind: 'ghost', onClick: () => route('/assets/' + row.id) }) },
        { key: 'name', label: 'Tên tài sản' },
        { key: 'assetType', label: 'Loại', render: row => row.assetType?.name || row.assetTypeName || '—' },
        { key: 'owningDepartment', label: 'Phòng ban', render: row => row.owningDepartment?.name || row.departmentName || '—' },
        { key: 'status', label: 'Trạng thái', render: row => badge(row.status) }
      ]
    })
  );

  return h('div', { className: 'dashboard-page' },
    header,
    h('div', { className: 'grid-kpis' }, ...kpiCards),
    financialSection,
    h('div', { className: 'dashboard-grid', style: 'margin-top: 1.5rem;' }, distributionCard, alertsCard),
    recentAssetsCard
  );
}
