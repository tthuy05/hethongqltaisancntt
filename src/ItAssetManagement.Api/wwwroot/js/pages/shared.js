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
// Money is typed as "20.000.000" (vi-VN) but `input.value` still reads/writes the
// plain machine value ("20000000" / "1500.5"), so callers keep parsing with Number().
const moneyParts = text => {
  const [whole = '', ...rest] = String(text).replace(/[^\d,]/g, '').split(',');
  return { whole: whole.replace(/^0+(?=\d)/, ''), fraction: rest.join('').slice(0, 2), comma: String(text).includes(',') };
};
const formatMoney = text => {
  const { whole, fraction, comma } = moneyParts(text);
  const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  return comma ? `${grouped || '0'},${fraction}` : grouped;
};
function moneyInput(input) {
  let owner = input, descriptor;
  while (owner && !(descriptor = Object.getOwnPropertyDescriptor(owner, 'value'))) owner = Object.getPrototypeOf(owner);
  let store = descriptor && 'value' in descriptor ? descriptor.value : '';
  const read = descriptor?.get ? () => descriptor.get.call(input) : () => store;
  const write = descriptor?.set ? text => descriptor.set.call(input, text) : text => { store = text; };
  Object.defineProperty(input, 'value', { configurable: true,
    get: () => { const { whole, fraction } = moneyParts(read()); return whole === '' ? '' : fraction ? `${whole}.${fraction}` : whole; },
    set: raw => write(raw == null || raw === '' ? '' : formatMoney(String(raw).replace(/\.0+$/, '').replace(/(\.\d*?)0+$/, '$1').replace('.', ','))) });
  input.addEventListener('input', () => {
    const before = read(), caret = input.selectionStart ?? before.length;
    const kept = before.slice(0, caret).replace(/[^\d,]/g, '').length, next = formatMoney(before);
    write(next);
    let position = 0;
    for (let seen = 0; position < next.length && seen < kept; position++) if (/[\d,]/.test(next[position])) seen++;
    try { input.setSelectionRange(position, position); } catch { /* not focusable yet */ }
  });
  return input;
}
// Non-negative numeric inputs: block "-", "+" and exponent keys instead of rejecting later.
function guardNumber(input, min) {
  if (!(min >= 0)) return;
  input.addEventListener('keydown', event => { if (['-', '+', 'e', 'E'].includes(event.key)) event.preventDefault(); });
}
export function field({ name, label, type = 'text', value = '', required = false, maxLength, options, readOnly = false, hint = '', rows = 3, placeholder, min, max, step }) {
  const id = 'field-' + name;
  const props = { id, name, value: value ?? '', required, readOnly, maxLength, placeholder, className: type === 'textarea' ? 'textarea' : options ? 'select' : 'input', 'aria-describedby': id + '-error' };
  const money = type === 'money';
  const input = options ? h('select', props, options.map(item => option(item.value, item.label, String(item.value) === String(value)))) : type === 'textarea' ? h('textarea', { ...props, rows }, value ?? '') :
    h('input', { ...props, value: money ? '' : props.value, type: money ? 'text' : type, inputMode: money ? 'decimal' : undefined, autocomplete: money ? 'off' : undefined,
      placeholder: placeholder ?? (money ? 'VD: 20.000.000' : undefined), min: type === 'number' ? min : undefined, max: type === 'number' ? max : undefined, step: type === 'number' ? step : undefined });
  if (money) { moneyInput(input); input.value = value ?? ''; }
  if (type === 'number') guardNumber(input, min);
  if (options) input.value = String(value ?? '');
  const error = h('span', { id: id + '-error', className: 'field-error', 'aria-live': 'polite' });
  const suffix = money ? h('span', { className: 'input-suffix', 'aria-hidden': 'true' }, '₫') : null;
  const hintNode = h('span', { className: 'field-hint', 'aria-live': 'polite' }, hint || '');
  return { element: h('div', { className: 'field' }, h('label', { htmlFor: id }, label, required ? h('span', { 'aria-hidden': 'true', className: 'required' }, ' *') : null),
    suffix ? h('div', { className: 'input-affix' }, input, suffix) : input, hintNode, error), input, error, hint: hintNode };
}
export function displayErrors(fields, errors = {}) {
  for (const [key, item] of Object.entries(fields)) {
    if (!item?.error || !item.input) continue;
    const value = errors[key];
    item.error.textContent = Array.isArray(value) ? value.join(' ') : value || '';
    item.input.setAttribute('aria-invalid', value ? 'true' : 'false');
  }
  const first = Object.keys(errors).map(key => fields[key]).find(item => item?.input);
  first?.input.focus();
}
/** A validation error that belongs to one form field; shown in red under that field. */
export class FieldError extends Error {
  constructor(field, message) { super(message); this.field = field; }
}
/** Show `error` under its field when it has one; otherwise in the form-level feedback area. */
export function showFormError(fields, feedback, error) {
  if (error?.field && fields[error.field]?.error) { feedback?.replaceChildren(); displayErrors(fields, { [error.field]: error.message }); return; }
  displayErrors(fields, {});
  feedback?.replaceChildren(formAlert(error?.message || 'Dữ liệu chưa hợp lệ.'));
}
/** Run `search` while typing (debounced) and immediately on Enter. */
export function liveSearch(input, search, delay = 300) {
  let timer = 0;
  input.addEventListener('input', () => { window.clearTimeout?.(timer); timer = window.setTimeout(() => { if (input.isConnected) void search(); }, delay); });
  input.addEventListener('keydown', event => { if (event.key !== 'Enter') return; event.preventDefault(); window.clearTimeout?.(timer); void search(); });
  return input;
}
/** Show lookup results as an open list (no need to expand the dropdown); auto-pick a single match. */
export function showChoices(select, count) {
  select.size = count > 0 ? Math.min(count + 1, 7) : 1;
  select.classList.toggle('select-list', count > 0);
  if (count === 1 && select.options?.length === 2) select.value = select.options[1].value;
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
  // Close on a real backdrop click only. Selecting text inside an input and releasing the
  // mouse outside the box also yields click.target === dialog; that must not close it.
  let pressed = null;
  dialog.addEventListener('pointerdown', event => { pressed = event.target; });
  dialog.addEventListener('click', event => { if (event.target === dialog && (pressed === null || pressed === dialog)) close(); pressed = null; });
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
