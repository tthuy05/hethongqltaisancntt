import { h } from '../utils/dom.js';
import { button, showToast } from '../components.js';
import { openDialog, hasPermission } from '../pages/shared.js';
import { concurrencyFeedback } from './concurrency-feedback.js';

const allowed = session => Number.isSafeInteger(session?.id) && session.id > 0 && hasPermission(session, 'assets.read') && hasPermission(session, 'assets.status.manage');
const eligible = (services, asset, session) => services.mode === 'api' && typeof services.assets?.retire === 'function' &&
  allowed(session) && ['InStock', 'Broken'].includes(asset.status) && asset.isArchived !== true;
export const canRetireAsset = (services, asset) => eligible(services, asset, services.auth?.session);

// Existing administrative Retired transition only. No Assignment/Maintenance commands.
export function assetRetireDialog(services, asset, onDone) {
  const session = services.auth?.session;
  if (!eligible(services, asset, session)) return null;
  const actorId = session.id;
  let modal, pending = false, closed = false, conflicted = false;
  const reason = h('textarea', { id: 'retire-reason', className: 'textarea', rows: 3, maxLength: 1000, required: true,
    'aria-describedby': 'retire-feedback', 'aria-label': 'Lý do ngừng sử dụng' });
  const feedback = h('div', { id: 'retire-feedback', 'aria-live': 'polite' });
  const body = h('div', { className: 'dialog-body' },
    h('p', {}, 'Ngừng sử dụng ', h('strong', {}, asset.assetCode), ' — ', asset.name, '?'),
    h('p', { className: 'info-note' }, 'Chuyển sang Retired, không xóa hoặc archive tài sản. Chưa có thao tác hoàn tác trạng thái này trên giao diện. Backend sẽ chặn nếu có quy trình cấp phát/bảo trì đang hoạt động.'),
    h('label', { htmlFor: 'retire-reason' }, 'Lý do ngừng sử dụng (bắt buộc, tối đa 1000 ký tự)'), reason, feedback);
  const active = () => !closed && body.isConnected;
  const currentActor = () => { const session = services.auth?.session; return session?.id === actorId && allowed(session); };
  const alert = message => feedback.replaceChildren(h('p', { role: 'alert' }, message));
  const confirm = button('Xác nhận ngừng sử dụng', { kind: 'danger', onClick: async () => {
    if (pending || conflicted || !active()) return;
    if (!currentActor()) { alert('Phiên hoặc quyền đã thay đổi. Vui lòng đóng hộp thoại và tải lại trang.'); confirm.disabled = true; return; }
    const text = reason.value.trim();
    if (!text || text.length > 1000) { alert('Vui lòng nhập lý do, tối đa 1000 ký tự.'); reason.setAttribute('aria-invalid', 'true'); reason.focus(); return; }
    reason.removeAttribute('aria-invalid'); feedback.replaceChildren();
    pending = true; confirm.disabled = true; reason.readOnly = true; confirm.setAttribute('aria-busy', 'true');
    try {
      await services.assets.retire(asset.id, { reason: text, rowVersion: asset.rowVersion });
      if (!active()) return;
      if (!currentActor()) { alert('Phiên hoặc quyền đã thay đổi. Hãy tải lại để kiểm tra trạng thái thực tế.'); conflicted = true; return; }
      modal.close(); showToast('Đã ngừng sử dụng tài sản. Lịch sử trạng thái được giữ lại.'); onDone?.();
    } catch (error) {
      if (!active()) return;
      if (!currentActor()) { alert('Phiên hoặc quyền đã thay đổi. Hãy tải lại để kiểm tra trạng thái thực tế.'); conflicted = true; return; }
      const messages = { ASSET_STATUS_CONFLICT: 'Trạng thái hiện tại không cho phép ngừng sử dụng.',
        ASSET_ACTIVE_WORKFLOW: 'Tài sản còn quy trình cấp phát/bảo trì đang hoạt động.',
        CONCURRENCY_CONFLICT: 'Tài sản đã được cập nhật bởi người khác.' };
      alert(messages[error.code] || (error.status === 401 ? 'Phiên đăng nhập đã hết hạn.' : error.status === 403 ? 'Bạn không có quyền ngừng sử dụng tài sản.' :
        error.status === 404 ? 'Tài sản không còn khả dụng.' : 'Chưa xác nhận được kết quả thao tác. Hãy tải lại trạng thái trước khi quyết định tiếp.'));
      // A lost response may follow a committed write. Never allow a blind retry,
      // even on network/5xx; retain the reason until explicit discard/read refresh.
      conflicted = true;
      const refresh = () => { modal.close(); window.dispatchEvent(new Event('frontend-refresh')); };
      feedback.append(error.code === 'CONCURRENCY_CONFLICT' ? concurrencyFeedback({ onRefresh: refresh }) :
        button('Bỏ lý do và tải lại trạng thái', { kind: 'secondary', onClick: refresh }));
    } finally {
      pending = false; confirm.removeAttribute('aria-busy');
      if (active()) { confirm.disabled = conflicted; reason.readOnly = false; }
    }
  } });
  modal = openDialog({ title: 'Ngừng sử dụng tài sản', body, footer: [button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm],
    onClose: () => { closed = true; } });
  reason.focus(); return modal;
}
