import { h } from '../utils/dom.js';
import { button } from '../components.js';
import { can } from '../pages/shared.js';
import { licenseView } from '../models/license-view.js';
import { licenseAllocationDraft } from '../models/license-allocation-draft.js';
import { licenseTargetPicker } from './license-target-picker.js';
import { ServiceError } from '../services/service-error.js';

let serial = 0;

// A local prepare step only: no fetch/POST, storage, notification or seat claim.
// onPrepared receives an immutable internal draft, not an approved backend DTO.
export function licenseAllocationForm(services, { enabled = false, license, allowedTargets = [], onPrepared } = {}) {
  if (enabled !== true) return { element: h('p', { className: 'info-note' }, 'License allocation form — PLANNED.'), dispose() {} };
  const authorize = () => services.mode === 'api' && Number.isSafeInteger(services.auth.session?.id) && services.auth.session.id > 0 &&
    can(services, 'licenses.read') && can(services, 'license-assignments.assign');
  if (!authorize()) throw new ServiceError(403, 'FORBIDDEN', 'Bạn không có quyền chuẩn bị cấp license.');
  if (onPrepared != null && typeof onPrepared !== 'function') throw new TypeError('onPrepared must be a function.');
  const metadata = licenseView(license); const actorId = services.auth.session?.id;
  let target = null, disposed = false, pending = false, revision = 0;
  const status = h('p', { className: 'meta', role: 'status', 'aria-live': 'polite' });
  const prefix = 'license-allocation-' + ++serial;
  const note = h('textarea', { id: prefix + '-note', className: 'textarea', maxLength: 1000, rows: 3, onInput: () => { revision++; } });
  const picker = licenseTargetPicker(services, { enabled: true, licenseType: metadata.licenseType, allowedTargets,
    onChange: value => { target = value; revision++; } });
  const prepare = button('Chuẩn bị yêu cầu', { type: 'submit', disabled: !onPrepared || !metadata.isActive });
  const alive = () => !disposed && root.isConnected && services.auth.session?.id === actorId;
  const root = h('form', { noValidate: true, className: 'card', 'aria-label': 'Chuẩn bị cấp license', onSubmit: async event => {
    event.preventDefault(); if (pending || !alive() || !onPrepared) return;
    if (!authorize()) { status.textContent = 'Quyền đã thay đổi. Vui lòng tải lại.'; return; }
    if (!metadata.isActive) { status.textContent = 'License đã ngừng hoạt động.'; return; }
    if (!target) { status.textContent = 'Tìm và chọn người dùng hoặc tài sản trước.'; return; }
    let draft;
    try { draft = licenseAllocationDraft({ licenseId: metadata.id, ...target, note: note.value }); }
    catch { status.textContent = 'Target không hợp lệ hoặc ghi chú vượt1000 ký tự.'; return; }
    const snapshot = revision; pending = true; prepare.disabled = true; status.textContent = 'Đang chuẩn bị yêu cầu…';
    try {
      await onPrepared(draft);
      if (alive() && authorize() && revision === snapshot) status.textContent = 'Bản chuẩn bị đã được chuyển cho bước review; chưa cấp seat hoặc ghi database.';
    } catch {
      if (alive() && revision === snapshot) status.textContent = 'Không chuẩn bị được yêu cầu. Dữ liệu đang nhập vẫn được giữ.';
    } finally {
      pending = false; prepare.disabled = disposed || !metadata.isActive || !alive() || !authorize();
      if (alive() && !authorize()) status.textContent = 'Quyền đã thay đổi. Vui lòng tải lại.';
      else if (alive() && revision !== snapshot) status.textContent = 'Bản nhập đã thay đổi. Hãy chuẩn bị lại.';
    }
  } }, h('div', { className: 'card-header' }, h('h2', {}, 'Chuẩn bị cấp license · ' + metadata.licenseCode)), picker.element,
  h('div', { className: 'card-body' }, h('div', { className: 'field' }, h('label', { htmlFor: prefix + '-note' }, 'Ghi chú'), note),
    h('p', { className: 'info-note' }, 'Một yêu cầu tương ứng một seat. Backend còn phải kiểm quyền, loại target, hiệu lực và capacity; chưa có lệnh cấp license.'),
    !onPrepared ? h('p', { className: 'info-note' }, 'PLANNED — bước nhận bản chuẩn bị chưa được nối.') : null,
    !metadata.isActive ? h('p', { className: 'info-note' }, 'License đã ngừng hoạt động, không chuẩn bị cấp mới.') : null, prepare, status));
  return { element: root, dispose() { disposed = true; picker.dispose(); prepare.disabled = true; note.disabled = true; } };
}
