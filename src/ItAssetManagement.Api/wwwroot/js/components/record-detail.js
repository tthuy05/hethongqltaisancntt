// Detail dialogs opened from report rows: one asset or one maintenance ticket.
// Read-only; cost fields follow the same grants as the full pages.
import { h } from '../utils/dom.js';
import { badge, button, dataTable, loadingState } from '../components.js';
import { can, date, money, formAlert, openDialog, route, modulePermissions } from '../pages/shared.js';
import { assetAssignmentHistory, assetMaintenanceHistory, canReadAssignments, canReadMaintenance, currentHolder } from './asset-activity.js';

const time = value => value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)) : '—';
const maintenanceStates = { PENDING: 'Chờ xử lý', IN_PROGRESS: 'Đang xử lý', RESOLVED: 'Đã sửa xong', FAILED: 'Không sửa được', CANCELLED: 'Đã hủy' };
const priorities = { LOW: 'Thấp', MEDIUM: 'Trung bình', HIGH: 'Cao', CRITICAL: 'Khẩn cấp' };
const item = (label, value, wide = false) => h('div', { className: wide ? 'record-wide' : '' }, h('dt', {}, label), h('dd', {}, value ?? '—'));

function shell(title, content, footer = []) {
  const body = h('div', { className: 'dialog-body' }, loadingState());
  const modal = openDialog({ title, className: 'dialog--wide detail-dialog', body, footer });
  void content(body).catch(error => { if (body.isConnected) body.replaceChildren(formAlert(error?.message || 'Không tải được chi tiết.')); });
  return modal;
}

/**
 * Everything about one asset in one place: master data, who holds it now,
 * assignment history and maintenance history. Sections the viewer may not read are omitted.
 * `fallback` (label/value pairs from a report row) is shown when the viewer cannot read assets.
 */
export function openAssetDetail(services, assetId, fallback = []) {
  if (!can(services, 'assets.read')) {
    return shell('Chi tiết dòng báo cáo', async body => body.replaceChildren(h('dl', { className: 'record-grid' }, fallback.map(([label, value]) => item(label, value))),
      h('p', { className: 'meta' }, 'Bạn chưa có quyền xem chi tiết tài sản.')));
  }
  const histories = [];
  return shell('Chi tiết tài sản', async body => {
    const [asset, holder] = await Promise.all([services.assets.get(assetId), currentHolder(services, assetId)]);
    if (!body.isConnected) return;
    const sections = [
      h('div', { className: 'record-head' }, h('div', {}, h('h2', {}, `${asset.assetCode} — ${asset.name}`), h('p', { className: 'meta' }, [asset.assetType?.name, asset.owningDepartment?.name].filter(Boolean).join(' · '))), badge(asset.status)),
      h('dl', { className: 'record-grid' },
        item('Người đang sử dụng', holder || asset.currentAssignment?.user?.displayName || 'Chưa có dữ liệu cấp phát'),
        item('Serial', asset.serialNumber), item('Thương hiệu / Model', [asset.brand, asset.model].filter(Boolean).join(' / ') || '—'),
        item('Vị trí', asset.location), item('Ngày mua', date(asset.purchaseDate)),
        can(services, 'assets.cost.read') ? item('Giá mua', money(asset.purchasePrice)) : null,
        item('Hết bảo hành', date(asset.warrantyExpirationDate)), item('Cấu hình', asset.specification), item('Hệ điều hành', asset.operatingSystem),
        asset.note ? item('Ghi chú', asset.note, true) : null)
    ];
    if (canReadAssignments(services)) { const list = assetAssignmentHistory(services, assetId, { pageSize: 5 }); histories.push(list); sections.push(h('h3', {}, 'Lịch sử cấp phát (ai đã mượn / đang dùng)'), list.element); }
    if (canReadMaintenance(services)) { const list = assetMaintenanceHistory(services, assetId, { pageSize: 5 }); histories.push(list); sections.push(h('h3', {}, 'Lịch sử bảo trì'), list.element); }
    body.replaceChildren(...sections);
    for (const list of histories) void list.load();
  }, [button('Mở trang tài sản', { kind: 'secondary', iconName: 'arrow-right', onClick: () => route('/assets/' + assetId) })]);
}

/** One maintenance ticket with its processing history. */
export function openMaintenanceDetail(services, ticketId, assetId) {
  if (!canReadMaintenance(services)) return openAssetDetail(services, assetId);
  const cost = modulePermissions(services.auth?.session).maintenance?.readCost === true;
  return shell('Chi tiết phiếu bảo trì', async body => {
    const [ticket, history] = await Promise.all([services.maintenance.get(ticketId), services.maintenance.history(ticketId, { page: 1, pageSize: 20 })]);
    if (!body.isConnected) return;
    body.replaceChildren(
      h('div', { className: 'record-head' }, h('div', {}, h('h2', {}, `${ticket.ticketCode} — ${ticket.title}`), h('p', { className: 'meta' }, `Tài sản: ${ticket.assetCode || ''} — ${ticket.assetName || ''}`)),
        h('span', { className: 'badge neutral' }, maintenanceStates[ticket.status] || ticket.status)),
      h('dl', { className: 'record-grid' },
        item('Mức ưu tiên', priorities[ticket.priority] || ticket.priority), item('Kỹ thuật viên', ticket.assignedToName || 'Chưa giao'),
        item('Mở phiếu', time(ticket.openedAtUtc)), item('Hạn xử lý', time(ticket.dueAtUtc)), item('Bắt đầu', time(ticket.startedAtUtc)), item('Kết thúc', time(ticket.completedAtUtc)),
        ...(cost ? [item('Chi phí dự kiến', money(ticket.estimatedCost)), item('Chi phí thực tế', money(ticket.actualCost))] : []),
        item('Mô tả sự cố', ticket.description, true), ticket.resolution ? item('Kết quả xử lý', ticket.resolution, true) : null),
      h('h3', {}, 'Quá trình xử lý'),
      dataTable({ label: 'Quá trình xử lý', rows: history.items, emptyMessage: 'Chưa có sự kiện.', columns: [
        { label: 'Thời điểm', className: 'nowrap', render: row => time(row.performedAtUtc) }, { key: 'eventType', label: 'Sự kiện' },
        { label: 'Trạng thái', render: row => maintenanceStates[row.toStatus] || '—' }, { label: 'Nội dung', className: 'asset-history-reason', render: row => row.comment || '—' }] }));
  }, [button('Mở trang phiếu', { kind: 'secondary', iconName: 'arrow-right', onClick: () => route('/maintenance/' + ticketId) }),
    ...(can(services, 'assets.read') && assetId ? [button('Xem tài sản', { kind: 'secondary', onClick: () => openAssetDetail(services, assetId) })] : [])]);
}
