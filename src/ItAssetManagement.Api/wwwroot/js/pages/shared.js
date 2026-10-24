import { h } from '../utils/dom.js';
import { button, showToast } from '../components.js';
export { modulePermissions } from '../models/module-permissions.js';

export const statusLabels = { InStock: 'Trong kho', InUse: 'Đang sử dụng', Maintenance: 'Đang bảo trì', Broken: 'Hỏng', Retired: 'Ngừng sử dụng' };
export const date = value => value ? new Intl.DateTimeFormat('vi-VN', { timeZone: 'UTC' }).format(new Date(value)) : 'Chưa có thông tin';
export const money = value => value == null ? 'Chưa có thông tin' : new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 2 }).format(value);
export const hasPermission = (session, permission) => {
  const grants = session?.permissions;
  return Array.isArray(grants) && grants.every(grant => typeof grant === 'string') && grants.includes(permission);
};
export const can = (services, permission) => hasPermission(services.auth?.session, permission);
export const route = path => {
  if (window.location.hash.slice(1) === path) window.dispatchEvent(new Event('frontend-refresh'));
  else window.location.hash = path;
};
export const option = (value, label, selected = false) => h('option', { value, selected }, label);
export const queryFrom = () => Object.fromEntries(new URLSearchParams(window.location.hash.split('?')[1] || ''));
export function setQuery(path, values) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) if (value !== '' && value != null) query.set(key, String(value));
  route(path + (query.size ? '?' + query.toString() : ''));
}
export function field({ name, label, type = 'text', value = '', required = false, maxLength, options, readOnly = false, hint = '', rows = 3 }) {
  const id = 'field-' + name;
  const props = { id, name, value: value ?? '', required, readOnly, maxLength, className: type === 'textarea' ? 'textarea' : options ? 'select' : 'input', 'aria-describedby': id + '-error' };
  const input = options ? h('select', props, options.map(item => option(item.value, item.label, String(item.value) === String(value)))) : type === 'textarea' ? h('textarea', { ...props, rows }, value ?? '') : h('input', { ...props, type });
  if (options) input.value = String(value ?? '');
  const error = h('span', { id: id + '-error', className: 'field-error', 'aria-live': 'polite' });
  return { element: h('div', { className: 'field' }, h('label', { htmlFor: id }, label, required ? h('span', { 'aria-hidden': 'true', className: 'required' }, ' *') : null), input, hint ? h('span', { className: 'field-hint' }, hint) : null, error), input, error };
}
export function displayErrors(fields, errors = {}) {
  for (const [key, item] of Object.entries(fields)) {
    const value = errors[key];
    item.error.textContent = Array.isArray(value) ? value.join(' ') : value || '';
    item.input.setAttribute('aria-invalid', value ? 'true' : 'false');
  }
  const first = Object.keys(errors).map(key => fields[key]).find(Boolean);
  first?.input.focus();
}
export function formAlert(message) { return h('div', { className: 'error-state', role: 'alert' }, message); }
export async function busy(control, work) {
  const before = [...control.childNodes];
  control.disabled = true;
  control.setAttribute('aria-busy', 'true');
  control.textContent = 'Đang xử lý…';
  try { return await work(); } finally { control.disabled = false; control.removeAttribute('aria-busy'); control.replaceChildren(...before); }
}
export function openDialog({ title, body, footer, onClose, className = '' }) {
  const dialog = h('dialog', { className: ('dialog ' + className).trim(), 'aria-label': title });
  const close = () => { dialog.close(); dialog.remove(); onClose?.(); };
  dialog.append(h('div', { className: 'dialog-header' }, h('h2', {}, title), button('Đóng', { kind: 'ghost', iconName: 'x', onClick: close, 'aria-label': 'Đóng hộp thoại' })), body, h('div', { className: 'dialog-footer' }, footer));
  dialog.addEventListener('cancel', event => { event.preventDefault(); close(); });
  dialog.addEventListener('click', event => { if (event.target === dialog) close(); });
  document.body.append(dialog);
  dialog.showModal();
  return { dialog, close };
}
export function archiveDialog(services, asset, onDone) {
  const session = services.auth?.session;
  const allowed = session => Number.isSafeInteger(session?.id) && session.id > 0 &&
    hasPermission(session, 'assets.read') && hasPermission(session, 'assets.archive');
  if (!allowed(session) || !['api', 'mock'].includes(services.mode) || typeof services.assets?.archive !== 'function' ||
      !Number.isSafeInteger(asset?.id) || asset.id <= 0 || asset.isArchived === true) return null;
  // Capture the exact record/version the user reviewed; do not follow a mutable DTO.
  const { id, rowVersion } = asset; const actorId = session.id; const openedHash = window.location.hash;
  let modal, pending = false, closed = false, blocked = false;
  const feedback = h('div', { 'aria-live': 'polite' });
  const active = () => !closed && feedback.isConnected && window.location.hash === openedHash;
  const currentActor = () => { const current = services.auth?.session; return current?.id === actorId && allowed(current); };
  const stop = message => {
    blocked = true; confirm.disabled = true;
    feedback.replaceChildren(formAlert(message), button('Đóng và tải lại danh sách', { kind: 'secondary', onClick: () => {
      if (!active()) return; modal.close(); window.dispatchEvent(new Event('frontend-refresh'));
    } }));
  };
  const confirm = button('Archive tài sản', { kind: 'danger', onClick: async () => {
    if (pending || blocked || !active()) return;
    if (!currentActor()) { stop('Phiên hoặc quyền đã thay đổi. Hãy tải lại để kiểm tra tài sản.'); return; }
    pending = true; confirm.disabled = true; confirm.setAttribute('aria-busy', 'true');
    feedback.replaceChildren();
    try {
      await services.assets.archive(id, rowVersion);
      if (!active()) return;
      if (!currentActor()) { stop('Phiên hoặc quyền đã thay đổi. Hãy tải lại để kiểm tra kết quả thực tế.'); return; }
      modal.close(); showToast(services.mode === 'mock' ? 'Đã archive trong dữ liệu minh họa. Lịch sử được giữ lại.' : 'Đã archive tài sản. Lịch sử được giữ lại.'); onDone?.();
    } catch (error) {
      if (!active()) return;
      if (!currentActor()) { stop('Phiên hoặc quyền đã thay đổi. Hãy tải lại để kiểm tra kết quả thực tế.'); return; }
      const message = error?.code === 'CONCURRENCY_CONFLICT' ? 'Tài sản đã được cập nhật bởi người khác.' :
        error?.code === 'ASSET_ACTIVE_WORKFLOW' ? 'Tài sản còn quy trình cấp phát/bảo trì đang hoạt động.' :
        error?.code === 'SESSION_CHANGED' ? 'Phiên đăng nhập đã thay đổi. Hãy kiểm tra kết quả thực tế.' :
        error?.status === 401 ? 'Phiên đăng nhập đã hết hạn.' : error?.status === 403 ? 'Bạn không có quyền archive tài sản.' :
        error?.status === 404 ? 'Tài sản không còn khả dụng; chưa thể kết luận thao tác này đã thành công.' :
        'Chưa xác nhận được kết quả archive. Không tự gửi lại; hãy tải lại danh sách để đối chiếu.';
      // A network error may follow a committed DELETE. Require a fresh read,
      // including after a same-account SESSION_CHANGED; never replay blindly.
      stop(message);
    } finally {
      pending = false; confirm.removeAttribute('aria-busy'); if (active()) confirm.disabled = blocked;
    }
  } });
  modal = openDialog({ title: 'Archive tài sản', body: h('div', { className: 'dialog-body' }, h('p', {}, 'Archive ', h('strong', {}, asset.assetCode), ' — ', asset.name, '? Thao tác không xóa vĩnh viễn hoặc mất lịch sử.'), h('p', { className: 'meta' }, services.mode === 'mock' ? 'Chỉ thay đổi dữ liệu demo trong bộ nhớ trình duyệt.' : 'Backend sẽ kiểm tra quyền, version và quy trình đang hoạt động.'), feedback), footer: [button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm], onClose: () => { closed = true; } });
  return modal;
}
export async function allCatalog(service, filters = {}) {
  const first = await service.list({ ...filters, page: 1, pageSize: 100 });
  const items = [...first.items];
  for (let page = 2; page <= first.totalPages; page++) items.push(...(await service.list({ ...filters, page, pageSize: 100 })).items);
  return items;
}
export const allActive = service => allCatalog(service, { status: 'Active' });
