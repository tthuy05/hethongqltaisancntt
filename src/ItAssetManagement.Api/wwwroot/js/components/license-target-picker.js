import { h } from '../utils/dom.js';
import { button } from '../components.js';
import { can, option, busy } from '../pages/shared.js';
import { ServiceError } from '../services/service-error.js';

let serial = 0;
const positive = value => Number.isSafeInteger(value) && value > 0;
const labels = { user: 'Người dùng', asset: 'Tài sản' };

// Preparation only: lookup/selection, never POST allocation. PER_* fixes target
// type; other license types need explicitly reviewed contract configuration.
export function licenseTargetPicker(services, { enabled = false, licenseType, allowedTargets = [], onChange = () => {} } = {}) {
  if (enabled !== true) return { element: h('p', { className: 'info-note' }, 'Allocation target picker — PLANNED.'), dispose() {} };
  if (services.mode !== 'api' || !can(services, 'licenses.read') || !can(services, 'license-assignments.assign'))
    throw new ServiceError(403, 'FORBIDDEN', 'Bạn không có quyền chuẩn bị cấp license.');
  const types = licenseType === 'PER_USER' ? ['user'] : licenseType === 'PER_DEVICE' ? ['asset'] :
    ['VOLUME', 'SUBSCRIPTION', 'OTHER'].includes(licenseType) && Array.isArray(allowedTargets)
      ? [...new Set(allowedTargets.filter(type => type === 'user' || type === 'asset'))] : [];
  if (!types.length) return { element: h('p', { className: 'info-note' }, 'PLANNED — cần chốt loại target theo hợp đồng license trước khi cấp.'), dispose() {} };
  const actorId = services.auth.session?.id;
  if (!positive(actorId)) throw new ServiceError(403, 'FORBIDDEN', 'Phiên không hợp lệ.');
  let generation = 0, disposed = false, loadedTarget = null; let choices = new Map();
  const prefix = 'license-target-' + ++serial;
  const feedback = h('p', { className: 'meta', role: 'status', 'aria-live': 'polite' });
  const selection = h('select', { id: prefix + '-selection', className: 'select', disabled: true, onChange: () => {
    const id = Number(selection.value); const selected = choices.get(id);
    if (!selected || loadedTarget !== type.value || !types.includes(type.value) || !alive() || !authorized(type.value)) { selection.value = ''; onChange(null); return; }
    onChange(Object.freeze({ userId: type.value === 'user' ? id : null, assetId: type.value === 'asset' ? id : null }));
  } }, option('', 'Chưa chọn target'));
  const clear = () => { generation++; loadedTarget = null; choices = new Map(); selection.replaceChildren(option('', 'Chưa chọn target')); selection.value = ''; selection.disabled = true; onChange(null); };
  const type = h('select', { id: prefix + '-type', className: 'select', onChange: () => { clear(); feedback.textContent = 'Tìm lại target cho loại đã chọn.'; } },
    types.map(value => option(value, labels[value])));
  type.value = types[0];
  const keyword = h('input', { id: prefix + '-keyword', className: 'input', maxLength: 200, placeholder: 'Tên, mã người dùng hoặc tài sản…',
    onKeyDown: event => { if (event.key === 'Enter') { event.preventDefault(); event.stopPropagation(); performSearch(event); } } });
  const search = button('Tìm target', { type: 'button', kind: 'secondary', onClick: performSearch });
  const authorized = target => can(services, 'licenses.read') && can(services, 'license-assignments.assign') &&
    can(services, target === 'user' ? 'users.lookup' : 'assets.read');
  const alive = () => !disposed && root.isConnected && services.auth.session?.id === actorId;
  function performSearch(event) {
    event.preventDefault(); if (search.disabled || !alive()) return;
    clear(); const revision = generation; const target = type.value;
    if (!types.includes(target) || !authorized(target)) { feedback.textContent = 'Không đủ quyền tìm target.'; return; }
    const value = keyword.value.trim();
    if (value.length > 200) { feedback.textContent = 'Từ khóa tối đa 200 ký tự.'; return; }
    feedback.textContent = 'Đang tìm target…';
    void busy(search, async () => {
      try {
        const query = { page: 1, pageSize: 20, ...(value ? { keyword: value } : {}) };
        const result = target === 'user' ? await services.users.lookup(query) : await services.assets.list(query);
        if (revision !== generation || !alive()) return;
        if (!authorized(target)) { clear(); feedback.textContent = 'Quyền đã thay đổi. Vui lòng tải lại.'; return; }
        if (!result || !Array.isArray(result.items) || result.items.length > 20) throw new Error('invalid lookup');
        const candidates = new Map(), seen = new Set();
        for (const item of result.items) {
          if (!item || !positive(item.id) || seen.has(item.id)) throw new Error('invalid lookup');
          seen.add(item.id);
          if (target === 'asset' && (item.status === 'Retired' || item.isArchived === true)) continue;
          const label = target === 'user' ? item.displayName : item.assetCode + ' — ' + item.name;
          if (typeof label !== 'string' || !label.trim() || label.length > 400 || target === 'asset' &&
            (typeof item.assetCode !== 'string' || typeof item.name !== 'string' || !['InStock', 'InUse', 'Maintenance', 'Broken'].includes(item.status))) throw new Error('invalid lookup');
          candidates.set(item.id, label);
        }
        choices = candidates; loadedTarget = target; selection.replaceChildren(option('', 'Chọn ' + labels[target].toLowerCase()),
          ...[...choices].map(([id, label]) => option(id, label))); selection.value = ''; selection.disabled = !choices.size;
        feedback.textContent = choices.size ? 'Chọn target từ kết quả; backend còn phải kiểm eligibility/capacity.' : 'Không tìm thấy target phù hợp.';
      } catch {
        if (revision === generation && alive()) { clear(); feedback.textContent = 'Không tải được target. Kiểm tra quyền/kết nối và thử lại.'; }
      }
    }).finally(() => { if (disposed) search.disabled = true; });
  }
  // A div/button avoids nesting forms in the future allocation form. Enter in
  // this lookup field searches only; it must never submit the parent's mutation.
  const root = h('div', { className: 'card-body' }, h('div', { className: 'field' }, h('label', { htmlFor: prefix + '-type' }, 'Loại target'), type),
  h('div', { className: 'field' }, h('label', { htmlFor: prefix + '-keyword' }, 'Tìm người hoặc tài sản'), keyword), search,
  h('div', { className: 'field' }, h('label', { htmlFor: prefix + '-selection' }, 'Target nhận license'), selection), feedback,
  h('p', { className: 'info-note' }, 'Chuẩn bị lựa chọn — chưa gửi yêu cầu cấp seat. Không có quantity nhập tay.'));
  return { element: root, dispose() { disposed = true; clear(); search.disabled = true; type.disabled = true; keyword.disabled = true; } };
}
