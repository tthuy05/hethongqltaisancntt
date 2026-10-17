import { h } from '../utils/dom.js';
import { badge, button, dataTable, loadingState, pagination } from '../components.js';
import { hasPermission } from '../pages/shared.js';

const sources = { SYSTEM: 'Hệ thống', ADMIN: 'Quản trị', IMPORT: 'Nhập dữ liệu', ASSIGNMENT: 'Phân bổ', MAINTENANCE: 'Bảo trì' };
const allowed = session => hasPermission(session, 'assets.read') && hasPermission(session, 'assets.history.read');
const timestamp = value => new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'UTC', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false
}).format(new Date(value)) + ' (UTC)';

export function createAssetStatusHistory(services, assetId) {
  const element = h('div', { 'aria-label': 'Lịch sử trạng thái' });
  let revision = 0, disposed = false;
  const message = text => element.replaceChildren(h('p', { role: 'status' }, text));
  async function load(page = 1) {
    const mine = ++revision;
    if (disposed || !element.isConnected) return;
    const session = services.auth?.session;
    if (!allowed(session)) { message('Bạn không có quyền xem lịch sử trạng thái.'); return; }
    if (services.mode !== 'api' || typeof services.assets?.history !== 'function') {
      message('Chưa kết nối API lịch sử trạng thái trong chế độ này. Không có dữ liệu mô phỏng.'); return;
    }
    const active = () => !disposed && mine === revision && element.isConnected;
    const sameActor = () => { const current = services.auth?.session; return current?.id === session.id && allowed(current); };
    element.replaceChildren(loadingState()); element.setAttribute('aria-busy', 'true');
    try {
      const result = await services.assets.history(assetId, { page, pageSize: 20 });
      if (!active()) return;
      if (!sameActor()) { message('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang tài sản.'); return; }
      const body = result.items.length ? dataTable({ label: 'Các lần thay đổi trạng thái', rows: result.items, columns: [
        { key: 'changedAtUtc', label: 'Thời điểm (UTC)', className: 'nowrap', render: row => timestamp(row.changedAtUtc) },
        { key: 'fromStatus', label: 'Trạng thái cũ', render: row => row.fromStatus == null ? 'Khởi tạo' : badge(row.fromStatus) },
        { key: 'toStatus', label: 'Trạng thái mới', render: row => badge(row.toStatus) },
        { key: 'actor', label: 'Người thực hiện', className: 'name-cell', render: row => row.changedByDisplayName?.trim() ||
          (row.changedByUserId == null && row.source === 'SYSTEM' ? 'Hệ thống' : 'Chưa có tên người thực hiện') },
        { key: 'source', label: 'Nguồn', className: 'nowrap', render: row => sources[row.source] },
        { key: 'reason', label: 'Lý do', render: row => h('div', { className: 'asset-history-reason' }, row.reason || 'Không có lý do được ghi lại') }
      ] }) : h('p', { role: 'status' }, 'Chưa có lịch sử trạng thái ở trang này.');
      element.replaceChildren(h('p', { className: 'meta' }, 'Mới nhất trước · Tên người thực hiện là tên hiện tại, không phải bản chụp tại thời điểm thao tác.'),
        body, pagination(result, next => { void load(next); }));
    } catch (error) {
      if (!active()) return;
      if (!sameActor()) { message('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang tài sản.'); return; }
      const text = error.status === 403 ? 'Bạn không có quyền xem lịch sử trạng thái.' : error.status === 404
        ? 'Tài sản không còn khả dụng.' : error.status === 401 ? 'Phiên đăng nhập đã hết hạn.' : 'Không tải được lịch sử trạng thái. Không có dữ liệu thay thế.';
      element.replaceChildren(h('div', { role: 'alert', className: 'error-state' }, h('p', {}, text),
        ![401, 403, 404].includes(error.status) ? button('Thử lại', { kind: 'secondary', onClick: () => { void load(page); } }) : null));
    } finally { if (active()) element.removeAttribute('aria-busy'); }
  }
  return { element, load, dispose() { disposed = true; revision++; element.replaceChildren(); element.removeAttribute('aria-busy'); } };
}
