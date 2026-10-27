import { h } from '../utils/dom.js';
import { button, dataTable, pagination, badge, showToast } from '../components.js';
import { can, date, field, formAlert, openDialog, option } from '../pages/shared.js';
import { licenseTargetPicker } from './license-target-picker.js';

export async function licenseAllocationPanel(services, license) {
  if (services.licenseAllocationEnabled !== true || !can(services, 'licenses.read') || !can(services, 'license-assignments.read')) return null;
  const actor = services.auth.session.id, hash = window.location.hash;
  let disposed = false, requestId = 0, page = 1, status = 'All';
  const disposers = new Set();
  const current = () => !disposed && services.licenseAllocationEnabled === true && services.auth.session?.id === actor && window.location.hash === hash && can(services, 'licenses.read') && can(services, 'license-assignments.read');
  const first = await services.licenseAssignments.listByLicense(license.id, { page, pageSize: 20, status });
  if (!current()) throw Object.assign(new Error('Phiên đã thay đổi.'), { status: 401 });
  const body = h('div', { className: 'card-body' }), feedback = h('div', { 'aria-live': 'polite' });
  const root = h('section', { className: 'card allocations-card' });
  const alive = () => current() && root.isConnected;
  const grants = { assign: 'license-assignments.assign', revoke: 'license-assignments.revoke', transfer: 'license-assignments.transfer' };
  const names = { assign: 'Phân bổ seat', revoke: 'Thu hồi', transfer: 'Điều chuyển' };
  const targets = () => [license.licenseType !== 'PER_DEVICE' && can(services, 'users.lookup') ? 'user' : null,
    license.licenseType !== 'PER_USER' && can(services, 'assets.read') ? 'asset' : null].filter(Boolean);
  const effective = license.isActive && (!license.startsAt || Date.parse(license.startsAt) <= Date.now()) && (!license.expiresAt || Date.parse(license.expiresAt) >= Date.now());
  async function edit(mode, row = null) {
    if (!alive() || !can(services, grants[mode])) return;
    try {
      const fresh = row ? await services.licenseAssignments.get(row.id) : null;
      if (alive() && can(services, grants[mode])) dialog(mode, fresh);
    } catch (error) { if (alive()) feedback.replaceChildren(formAlert(error.message)); }
  }
  function dialog(mode, row) {
    let modal, pending = false, blocked = false, closed = false, selected = null;
    const message = h('div', { 'aria-live': 'polite' });
    const reason = field({ name: 'allocation-' + (mode === 'assign' ? 'notes' : 'reason'), label: mode === 'assign' ? 'Ghi chú' : 'Lý do', type: 'textarea', maxLength: 1000 });
    const picker = mode === 'revoke' ? null : licenseTargetPicker(services, { enabled: true, licenseType: license.licenseType, allowedTargets: targets(), onChange: value => { selected = value; } });
    if (picker) disposers.add(picker.dispose);
    const active = () => !closed && alive() && message.isConnected;
    const allowed = () => current() && can(services, grants[mode]);
    const confirm = button(mode === 'assign' ? 'Xác nhận phân bổ' : mode === 'revoke' ? 'Xác nhận thu hồi' : 'Xác nhận điều chuyển', { type: 'submit' });
    const stop = text => {
      blocked = true; confirm.disabled = true;
      message.replaceChildren(formAlert(text), button('Đóng và tải lại', { kind: 'secondary', onClick: () => { if (!active()) return; modal.close(); window.dispatchEvent(new Event('frontend-refresh')); } }));
    };
    const form = h('form', { noValidate: true, onSubmit: async event => {
      event.preventDefault(); if (!active() || pending || blocked) return;
      if (!allowed()) { stop('Phiên hoặc quyền đã thay đổi. Hãy tải lại.'); return; }
      if (mode !== 'revoke' && !selected) { message.replaceChildren(formAlert('Vui lòng tìm và chọn đối tượng nhận bản quyền.')); return; }
      pending = true; confirm.disabled = true; confirm.setAttribute('aria-busy', 'true');
      try {
        if (mode === 'assign') await services.licenseAssignments.assign({ softwareLicenseId: license.id, userId: selected.userId, assetId: selected.assetId, notes: reason.input.value.trim() || null });
        else if (mode === 'revoke') await services.licenseAssignments.revoke(row.id, { reason: reason.input.value.trim() || null, rowVersion: row.rowVersion });
        else await services.licenseAssignments.transfer(row.id, { userId: selected.userId, assetId: selected.assetId, reason: reason.input.value.trim() || null, rowVersion: row.rowVersion });
        if (!active()) return;
        if (!allowed()) { stop('Phiên đã thay đổi. Hãy kiểm tra kết quả thực tế.'); return; }
        modal.close(); showToast('Đã cập nhật phân bổ bản quyền.'); window.dispatchEvent(new Event('frontend-refresh'));
      } catch (error) {
        if (!active()) return;
        if (error.status === 400 && allowed()) message.replaceChildren(formAlert(error.message));
        else stop(error.code === 'LICENSE_CAPACITY_EXHAUSTED' ? 'Bản quyền đã hết số lượng khả dụng.' : error.code === 'DUPLICATE_ASSIGNMENT' ? 'Đối tượng đã được cấp bản quyền này.' : error.status === 409 ? 'Dữ liệu, trạng thái hoặc số lượng đã thay đổi. Hãy tải lại.' : 'Chưa xác nhận được kết quả. Hãy tải lại trước khi thao tác tiếp.');
      } finally { pending = false; confirm.removeAttribute('aria-busy'); if (active()) confirm.disabled = blocked; }
    } }, h('div', { className: 'dialog-body form-grid' }, h('p', {}, license.licenseCode, row ? ' — ' + (row.assignedUserFullName || row.assignedAssetName || 'Đối tượng hiện tại') : ''), picker?.element, reason.element, message),
      h('div', { className: 'dialog-footer' }, button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm));
    modal = openDialog({ title: names[mode] + ' bản quyền', body: form, onClose: () => { closed = true; picker?.dispose(); if (picker) disposers.delete(picker.dispose); } });
  }
  function render(data) {
    const columns = [
      { label: 'Đối tượng nhận', render: row => row.assignedUserId ? h('span', {}, row.assignedUserFullName || 'Người dùng', can(services, 'users.lookup') && row.assignedUserEmail ? h('small', { className: 'meta' }, ' (' + row.assignedUserEmail + ')') : null) : h('span', {}, row.assignedAssetCode || '', ' — ', row.assignedAssetName || 'Tài sản') },
      { label: 'Ngày cấp', render: row => date(row.assignedAtUtc) },
      { label: 'Trạng thái', render: row => row.revokedAtUtc != null || row.isArchived ? h('span', { className: 'badge neutral' }, 'Đã thu hồi') : badge(true) },
      { label: 'Ghi chú / Thu hồi', render: row => row.revokedAtUtc ? 'Thu hồi: ' + date(row.revokedAtUtc) + (row.notes ? ' — ' + row.notes : '') : row.notes || '—' },
      { label: 'Thao tác', render: row => row.revokedAtUtc != null || row.isArchived ? null : h('div', { className: 'actions' },
        effective && targets().length && can(services, grants.transfer) ? button('Điều chuyển', { kind: 'ghost', onClick: () => edit('transfer', row) }) : null,
        can(services, grants.revoke) ? button('Thu hồi', { kind: 'ghost', onClick: () => edit('revoke', row) }) : null) }
    ];
    body.replaceChildren(data.items.length ? dataTable({ columns, rows: data.items }) : h('p', { role: 'status', className: 'table-empty' }, 'Chưa có phân bổ bản quyền nào.'),
      pagination(data, value => { if (alive()) { page = value; void reload(); } }));
  }
  async function reload() {
    if (!alive()) return; const own = ++requestId;
    body.replaceChildren(h('p', { role: 'status' }, 'Đang tải danh sách phân bổ…'));
    try { const result = await services.licenseAssignments.listByLicense(license.id, { page, pageSize: 20, status }); if (alive() && own === requestId) render(result); }
    catch (error) { if (alive() && own === requestId) body.replaceChildren(formAlert(error.message)); }
  }
  const statusSelect = h('select', { className: 'select', 'aria-label': 'Trạng thái phân bổ', onChange: event => { if (alive()) { status = event.target.value; page = 1; void reload(); } } }, option('All', 'Tất cả trạng thái'), option('Active', 'Đang sử dụng'), option('Revoked', 'Đã thu hồi'));
  root.append(h('div', { className: 'card-header' }, h('h3', {}, 'Danh sách phân bổ (Seat Allocations)'), h('div', { className: 'actions' }, statusSelect,
    can(services, grants.assign) && targets().length ? button('Phân bổ seat', { iconName: 'plus', disabled: !effective || !(license.availableQuantity > 0), onClick: () => edit('assign') }) : null)), feedback, body);
  render(first);
  return { element: root, dispose() { disposed = true; requestId++; for (const dispose of disposers) dispose(); disposers.clear(); } };
}
