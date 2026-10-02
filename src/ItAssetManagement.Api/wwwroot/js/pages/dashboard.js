import { h } from '../utils/dom.js';
import { button, pageHeader, statCard, badge, dataTable } from '../components.js';
import { can, route, statusLabels } from './shared.js';

export async function dashboardPage(services) {
  const summary = await services.dashboard.summary();
  const counts = summary.byStatus || [];
  return h('div', {}, pageHeader('Tổng quan hệ thống', 'Thông tin tài sản rõ ràng, tập trung và dễ theo dõi.', { eyebrow: 'Tổng quan', actions: [button('Xem tài sản', { kind: 'secondary', onClick: () => route('/assets'), iconName: 'laptop' }), ...(can(services, 'assets.create') ? [button('Thêm tài sản', { onClick: () => route('/assets/new'), iconName: 'plus' })] : [])] }),
    h('div', { className: 'grid-kpis' },
      statCard({ label: 'Tổng tài sản', value: summary.totalAssets, detail: 'Trong danh mục hiện tại', iconName: 'devices', tone: 'blue' }),
      statCard({ label: 'Đang sử dụng', value: summary.inUse, detail: 'Thiết bị đang được sử dụng', iconName: 'laptop', tone: 'green' }),
      statCard({ label: 'Đang bảo trì', value: summary.maintenance, detail: 'Cần theo dõi kỹ thuật', iconName: 'wrench', tone: 'amber' }),
      statCard({ label: 'Cần thay thế', value: summary.replacementNeeded ?? '—', detail: services.mode === 'mock' ? 'Chỉ số minh họa, không phải khuyến nghị thật' : 'PLANNED · API khuyến nghị chưa tích hợp', iconName: 'alert-triangle', tone: 'red' })),
    h('div', { className: 'dashboard-grid' }, h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('div', {}, h('h2', {}, 'Tài sản theo trạng thái'), h('p', { className: 'meta' }, services.mode === 'mock' ? 'Tổng hợp từ bộ dữ liệu minh họa' : 'Tổng hợp từ kết quả API'))), h('div', { className: 'card-body status-distribution' }, counts.map(item => h('div', { className: 'distribution-row' }, h('div', { className: 'toolbar' }, badge(item.status), h('strong', {}, String(item.count))), h('progress', { max: Math.max(summary.totalAssets, 1), value: item.count, 'aria-label': statusLabels[item.status] || item.status }))))),
      h('section', { className: 'card' }, h('div', { className: 'card-header' }, h('div', {}, h('h2', {}, 'Tiếp theo'), h('p', { className: 'meta' }, 'Lối tắt cho công việc hàng ngày'))), h('div', { className: 'card-body quick-links' }, button('Tra cứu tài sản', { kind: 'secondary', iconName: 'search', onClick: () => route('/assets') }), button('Danh mục phòng ban', { kind: 'secondary', iconName: 'building', onClick: () => route('/departments') }), button('Danh mục loại tài sản', { kind: 'secondary', iconName: 'tag', onClick: () => route('/asset-types') }), h('p', { className: 'info-note' }, 'Bảo trì, phân bổ, license và khuyến nghị nâng cao vẫn PLANNED. Giao diện này không chứng minh backend đã hoạt động.')))),
    h('section', { className: 'card recent-assets' }, h('div', { className: 'card-header' }, h('div', {}, h('h2', {}, 'Tài sản mới nhất'), h('p', { className: 'meta' }, '5 tài sản theo ngày tạo, mới nhất trước')), button('Xem tất cả', { kind: 'ghost', iconName: 'arrow-right', onClick: () => route('/assets') })), dataTable({ rows: summary.recentAssets || [], columns: [
      { key: 'assetCode', label: 'Mã tài sản', render: row => button(row.assetCode, { kind: 'ghost', onClick: () => route('/assets/' + row.id) }) },
      { key: 'name', label: 'Tên tài sản' }, { key: 'assetType', label: 'Loại', render: row => row.assetType?.name || '—' }, { key: 'owningDepartment', label: 'Phòng ban', render: row => row.owningDepartment?.name || '—' }, { key: 'status', label: 'Trạng thái', render: row => badge(row.status) }
    ] })));
}
