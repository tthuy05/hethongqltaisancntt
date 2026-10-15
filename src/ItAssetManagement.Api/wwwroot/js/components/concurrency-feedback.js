import { h } from '../utils/dom.js';
import { button } from '../components.js';

// Caller supplies a READ/reload callback, never the original mutation. A conflict
// preserves the current draft until the user explicitly chooses to discard it.
export function concurrencyFeedback({ onRefresh }) {
  if (typeof onRefresh !== 'function') throw new TypeError('A read refresh callback is required.');
  let pending = false;
  const status = h('p', { className: 'meta', role: 'status', 'aria-live': 'polite' });
  const refresh = button('Bỏ bản đang nhập và tải mới', { kind: 'secondary', onClick: async () => {
    if (pending || !root.isConnected) return;
    pending = true; refresh.disabled = true; refresh.setAttribute('aria-busy', 'true');
    status.textContent = 'Đang tải bản mới…';
    try { await onRefresh(); if (root.isConnected) status.textContent = 'Đã yêu cầu tải bản mới.'; }
    catch (error) {
      if (root.isConnected) status.textContent = error?.status === 401 ? 'Phiên đăng nhập đã hết hạn.' :
        error?.status === 403 ? 'Không đủ quyền tải bản mới.' : 'Không tải được bản mới. Vui lòng kiểm tra kết nối.';
    } finally { pending = false; refresh.disabled = false; refresh.removeAttribute('aria-busy'); }
  } });
  const root = h('div', { className: 'info-note', role: 'group', 'aria-label': 'Xử lý dữ liệu thay đổi' },
    h('p', {}, 'Dữ liệu trên server đã thay đổi. Bản bạn đang nhập vẫn được giữ; không tự gửi lại thao tác lưu.'),
    h('p', {}, 'Tải bản mới sẽ bỏ các thay đổi chưa lưu trong form này.'), refresh, status);
  return root;
}
