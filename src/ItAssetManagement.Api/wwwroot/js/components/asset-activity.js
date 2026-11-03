// Read-only activity of one asset (assignments, maintenance, current holder).
// Shared by the asset detail tabs and the report detail dialog; each part is
// shown only when its module flag is on and the viewer has the read grant.
import { h } from '../utils/dom.js';
import { badge, dataTable, pagination, loadingState } from '../components.js';
import { can, formAlert, modulePermissions } from '../pages/shared.js';

const time = value => value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)) : '—';
const money = value => value == null ? 'Chưa xác định' : new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
const maintenanceStates = { PENDING: 'Chờ xử lý', IN_PROGRESS: 'Đang xử lý', RESOLVED: 'Đã sửa xong', FAILED: 'Không sửa được', CANCELLED: 'Đã hủy' };
const priorities = { LOW: 'Thấp', MEDIUM: 'Trung bình', HIGH: 'Cao', CRITICAL: 'Khẩn cấp' };
const recipient = row => row.assignedUserName || row.assignedDepartmentName || '—';

export const canReadAssignments = services => services.assignmentEnabled === true && can(services, 'assignments.read');
export const canReadMaintenance = services => services.maintenanceEnabled === true && can(services, 'maintenance.read');

// One paged history table that loads on demand and ignores answers that arrive after it was replaced.
function pagedHistory({ load, columns, empty, label }) {
  const element = h('div', {}, loadingState());
  let revision = 0;
  async function show(page = 1) {
    const own = ++revision;
    try {
      const data = await load(page);
      if (own !== revision) return;
      element.replaceChildren(dataTable({ columns, rows: data.items, emptyMessage: empty, label }),
        (data.totalPages ?? 1) > 1 ? pagination(data, next => { void show(next); }) : null);
    } catch (error) {
      if (own === revision && error?.code !== 'SESSION_CHANGED') element.replaceChildren(formAlert(error?.message || 'Không tải được lịch sử.'));
    }
  }
  return { element, load: show, dispose: () => { revision++; } };
}

export function assetAssignmentHistory(services, assetId, { pageSize = 10 } = {}) {
  return pagedHistory({ label: 'Lịch sử cấp phát', empty: 'Tài sản chưa từng được cấp phát.',
    load: page => services.assignments.history(assetId, { page, pageSize }),
    columns: [
      { label: 'Người/phòng ban nhận', render: recipient },
      { label: 'Cấp lúc', className: 'nowrap', render: row => time(row.assignedAt) },
      { label: 'Hạn trả', className: 'nowrap', render: row => time(row.expectedReturnAt) },
      { label: 'Thu hồi lúc', className: 'nowrap', render: row => time(row.returnedAt) },
      { label: 'Trạng thái', render: row => badge(row.returnedAt ? 'Đã đóng' : 'Đang cấp phát') },
      { label: 'Ghi chú', className: 'asset-history-reason', render: row => [row.assignmentNote, row.returnNote].filter(Boolean).join('\n') || '—' },
      { label: '', render: row => h('a', { href: '#/assignments/' + row.id }, 'Chi tiết') }
    ] });
}

export function assetMaintenanceHistory(services, assetId, { pageSize = 10 } = {}) {
  const cost = modulePermissions(services.auth?.session).maintenance?.readCost === true;
  return pagedHistory({ label: 'Lịch sử bảo trì', empty: 'Tài sản chưa có phiếu bảo trì nào.',
    load: page => services.maintenance.list({ assetId, page, pageSize, sortBy: 'openedAt', sortDirection: 'desc' }),
    columns: [
      { label: 'Mã phiếu', className: 'nowrap', render: row => h('a', { href: '#/maintenance/' + row.id }, row.ticketCode) },
      { key: 'title', label: 'Tiêu đề' },
      { label: 'Trạng thái', render: row => maintenanceStates[row.status] || row.status },
      { label: 'Ưu tiên', render: row => priorities[row.priority] || row.priority },
      { label: 'Kỹ thuật viên', render: row => row.assignedToName || 'Chưa giao' },
      { label: 'Mở lúc', className: 'nowrap', render: row => time(row.openedAtUtc) },
      { label: 'Kết thúc', className: 'nowrap', render: row => time(row.completedAtUtc) },
      ...(cost ? [{ label: 'Chi phí thực tế', className: 'nowrap', render: row => money(row.actualCost) }] : [])
    ] });
}

/** Who holds the asset right now, from the assignment module (null = unknown). */
export async function currentHolder(services, assetId) {
  if (!canReadAssignments(services)) return null;
  try {
    const result = await services.assignments.list({ assetId, status: 'Active', page: 1, pageSize: 1 });
    const row = result.items?.[0];
    return row ? `${recipient(row)} (từ ${time(row.assignedAt)})` : 'Chưa cấp phát — đang trong kho';
  } catch { return null; }
}
