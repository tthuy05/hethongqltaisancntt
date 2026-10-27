import { h } from '../utils/dom.js';
import { pageHeader, button, dataTable, pagination, badge, showToast } from '../components.js';
import { can, hasPermission, date, route, setQuery, queryFrom, field, displayErrors, formAlert, openDialog, option } from './shared.js';
import { licenseView, licensePageView } from '../models/license-view.js';
import { ServiceError } from '../services/service-error.js';
import { validateLicense } from '../services/license-service.js';
import { licenseAllocationForm } from '../components/license-allocation-form.js';
import { licenseAllocationPanel } from '../components/license-allocation-panel.js';

const missing = value => value == null ? 'Chưa có dữ liệu' : String(value);
const planned = () => h('div', {}, pageHeader('License phần mềm', 'Chuẩn bị giao diện theo kế hoạch Tuần5.'),
  h('p', { className: 'info-note' }, 'PLANNED — chưa mở License API/UI. Không hiển thị dữ liệu minh họa như dữ liệu thật.'));

const licenseTypeLabels = {
  PER_USER: 'Theo người dùng (Per User)',
  PER_DEVICE: 'Theo thiết bị (Per Device)',
  VOLUME: 'Số lượng lớn (Volume)',
  SUBSCRIPTION: 'Thuê bao (Subscription)',
  OTHER: 'Khác (Other)'
};

const statusLabels = {
  ACTIVE: 'Đang hoạt động',
  INACTIVE: 'Ngừng hoạt động',
  EXPIRED: 'Hết hạn',
  EXPIRING_SOON: 'Sắp hết hạn',
  EXHAUSTED: 'Hết chỗ',
  NOT_EFFECTIVE: 'Chưa hiệu lực'
};

function statusBadge(status, isActive) {
  if (isActive === false || status === 'INACTIVE') return badge(false, 'Ngừng hoạt động');
  if (status === 'EXPIRED') return h('span', { className: 'badge badge-danger' }, 'Hết hạn');
  if (status === 'EXPIRING_SOON') return h('span', { className: 'badge badge-warning' }, 'Sắp hết hạn');
  if (status === 'EXHAUSTED') return h('span', { className: 'badge badge-warning' }, 'Hết chỗ');
  if (status === 'NOT_EFFECTIVE') return h('span', { className: 'badge badge-info' }, 'Chưa hiệu lực');
  return badge(true, 'Đang hoạt động');
}

function requireRead(services) {
  const session = services.auth?.session;
  if (services.mode !== 'api' || !Number.isSafeInteger(session?.id) || session.id <= 0 ||
      !hasPermission(session, 'licenses.read')) throw new ServiceError(403, 'FORBIDDEN', 'Bạn không có quyền xem license.');
  if (!services.licenses) throw new ServiceError(503, 'MODULE_NOT_READY', 'License API chưa sẵn sàng.');
  return session.id;
}

export async function licenseListPage(services, { enabled = false, query = { page: 1, pageSize: 20 } } = {}) {
  if (enabled !== true && services.licenseEnabled !== true) return planned();
  const actorId = requireRead(services);
  if (!query || typeof query !== 'object' || Array.isArray(query)) throw new ServiceError(400, 'VALIDATION_ERROR', 'Bộ lọc phải là object.');
  const snapshot = Object.freeze({ ...query });
  const payload = await services.licenses.list(snapshot);
  if (services.auth.session?.id !== actorId) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên đã thay đổi.');
  requireRead(services);
  const result = licensePageView(payload);

  const hash = window.location.hash;
  const current = () => services.auth.session?.id === actorId && can(services, 'licenses.read');
  const alive = () => root.isConnected && window.location.hash === hash && current();

  async function openEditDialog(item = null) {
    const grant = item ? 'licenses.update' : 'licenses.create';
    if (!can(services, grant)) return;
    let fresh = item;
    if (item?.id) {
      try { fresh = await services.licenses.get(item.id); } catch { fresh = item; }
    }
    let modal, pending = false, blocked = false, closed = false;
    const fields = {}, message = h('div', { 'aria-live': 'polite' });
    const isAdmin = services.auth.session?.roles?.includes('ADMIN_IT');
    const canManageKey = isAdmin && can(services, 'licenses.key.manage');
    const canCost = can(services, 'assets.cost.read') || can(services, 'reports.cost.read');

    const softwareSelect = h('select', { id: 'field-license-software', className: 'select', name: 'softwareId', required: true, disabled: Boolean(item), value: item?.software?.id ?? '' });
    if (item) {
      softwareSelect.append(option(item.software?.id ?? '', item.software?.name ?? 'Phần mềm đã chọn', true));
    } else {
      softwareSelect.append(option('', '— Chọn phần mềm —'));
      try {
        const softwareData = await services.licenses.softwareOptions?.({ page: 1 }) ?? { items: [] };
        const list = Array.isArray(softwareData) ? softwareData : (softwareData.items || []);
        for (const s of list) {
          softwareSelect.append(option(s.id, s.name));
        }
      } catch { /* dropdown fallback */ }
    }

    const typeSelect = h('select', { id: 'field-license-type', className: 'select', name: 'licenseType', value: item?.licenseType || 'PER_USER' },
      ...Object.entries(licenseTypeLabels).map(([val, lbl]) => option(val, lbl, item?.licenseType === val)));

    const addField = (name, label, val = '', type = 'text', max = 250, req = false) => {
      const f = field({ name: 'license-' + name, label, maxLength: max, type, value: val ?? '', required: req });
      fields[name] = f;
      return f.element;
    };

    const confirm = button(item ? 'Cập nhật' : 'Tạo bản quyền', { type: 'submit' });
    const stop = text => {
      blocked = true; confirm.disabled = true;
      message.replaceChildren(formAlert(text), button('Đóng và tải lại', { kind: 'secondary', onClick: () => { modal.close(); window.dispatchEvent(new Event('frontend-refresh')); } }));
    };

    const form = h('form', { noValidate: true, onSubmit: async event => {
      event.preventDefault();
      if (pending || blocked || closed) return;
      if (!current() || !can(services, grant)) { stop('Phiên hoặc quyền đã thay đổi. Hãy tải lại.'); return; }

      const dto = {
        softwareId: item ? item.software?.id : Number(softwareSelect.value) || 0,
        licenseType: typeSelect.value,
        totalQuantity: Number(fields.totalQuantity?.input?.value) || 0,
        vendor: fields.vendor?.input?.value?.trim() || null,
        notes: fields.notes?.input?.value?.trim() || null,
        startsAt: fields.startsAt?.input?.value ? new Date(fields.startsAt.input.value).toISOString() : null,
        expiresAt: fields.expiresAt?.input?.value ? new Date(fields.expiresAt.input.value).toISOString() : null,
        purchasedAt: fields.purchasedAt?.input?.value ? new Date(fields.purchasedAt.input.value).toISOString() : null,
      };

      if (canCost && fields.purchaseCost?.input?.value) {
        dto.purchaseCost = Number(fields.purchaseCost.input.value);
      }
      if (canManageKey && fields.licenseKey?.input?.value?.trim()) {
        dto.licenseKey = fields.licenseKey.input.value.trim();
      }
      if (item) {
        dto.rowVersion = item.rowVersion;
      }

      const errors = validateLicense(dto, Boolean(item));
      displayErrors(fields, errors);
      if (Object.keys(errors).length) {
        message.replaceChildren(formAlert('Vui lòng kiểm tra lại thông tin nhập.'));
        return;
      }

      pending = true; confirm.disabled = true; confirm.setAttribute('aria-busy', 'true');
      try {
        if (item) await services.licenses.update(item.id, dto);
        else await services.licenses.create(dto);
        modal.close();
        showToast(item ? 'Đã cập nhật bản quyền.' : 'Đã tạo bản quyền thành công.');
        window.dispatchEvent(new Event('frontend-refresh'));
      } catch (error) {
        if (error.status === 400) {
          message.replaceChildren(formAlert(error.message));
          displayErrors(fields, error.errors || {});
        } else if (error.status === 409) {
          stop(error.code === 'QUANTITY_BELOW_ACTIVE_ALLOCATIONS' ? error.message :
               error.code === 'CONCURRENCY_CONFLICT' ? 'Dữ liệu đã thay đổi bởi người khác. Hãy tải lại.' :
               error.code === 'LICENSE_CODE_CONFLICT' ? 'Mã bản quyền bị xung đột. Hãy thử lại.' :
               error.code === 'LICENSE_TYPE_IN_USE' ? 'Không thể đổi loại khi còn bản quyền đang cấp phát.' : error.message);
        } else {
          stop('Chưa xác nhận được kết quả. Hãy tải lại trước khi thao tác tiếp.');
        }
      } finally {
        pending = false; confirm.removeAttribute('aria-busy');
        if (!blocked) confirm.disabled = false;
      }
    } },
    h('div', { className: 'dialog-body form-grid' },
      h('p', { className: 'info-note' }, item?.licenseCode ? `Mã bản quyền: ${item.licenseCode}` : 'Mã BQxxxxxx sẽ được hệ thống tự động sinh khi lưu.'),
      h('div', { className: 'field' }, h('label', { className: 'label' }, 'Phần mềm *'), softwareSelect),
      h('div', { className: 'field' }, h('label', { className: 'label' }, 'Loại bản quyền *'), typeSelect),
      addField('totalQuantity', 'Tổng số lượng seat *', item?.totalQuantity ?? '1', 'number', 10, true),
      addField('vendor', 'Nhà cung cấp', item?.vendor ?? '', 'text', 250),
      addField('startsAt', 'Ngày hiệu lực', item?.startsAt ? item.startsAt.slice(0, 10) : '', 'date'),
      addField('expiresAt', 'Ngày hết hạn', item?.expiresAt ? item.expiresAt.slice(0, 10) : '', 'date'),
      addField('purchasedAt', 'Ngày mua', item?.purchasedAt ? item.purchasedAt.slice(0, 10) : '', 'date'),
      canCost ? addField('purchaseCost', 'Chi phí mua (VNĐ)', item?.purchaseCost != null ? String(item.purchaseCost) : '', 'number', 20) : null,
      canManageKey ? addField('licenseKey', item ? 'Cập nhật Key (để trống nếu giữ nguyên)' : 'Khóa bản quyền (Key)', '', 'text', 4096) : null,
      addField('notes', 'Ghi chú', item?.notes ?? '', 'textarea', 2000),
      message),
    h('div', { className: 'dialog-footer' },
      button('Hủy', { kind: 'secondary', onClick: () => modal.close() }),
      confirm));

    modal = openDialog({ title: item ? 'Sửa bản quyền' : 'Thêm bản quyền', body: form, onClose: () => { closed = true; } });
  }

  async function openArchiveDialog(item) {
    if (!can(services, 'licenses.archive')) return;
    let fresh = item;
    try { fresh = await services.licenses.get(item.id); } catch { fresh = item; }
    let modal, pending = false, closed = false;
    const message = h('div', { 'aria-live': 'polite' });
    const hasActiveSeats = (fresh.usedQuantity ?? 0) > 0;

    const confirm = button('Xác nhận ngừng sử dụng', { type: 'submit', kind: 'danger', disabled: hasActiveSeats });
    const form = h('form', { onSubmit: async event => {
      event.preventDefault();
      if (pending || closed || hasActiveSeats) return;
      if (!current() || !can(services, 'licenses.archive')) return;
      pending = true; confirm.disabled = true; confirm.setAttribute('aria-busy', 'true');
      try {
        await services.licenses.archive(fresh.id, fresh.rowVersion);
        modal.close();
        showToast('Đã ngừng sử dụng bản quyền.');
        window.dispatchEvent(new Event('frontend-refresh'));
      } catch (error) {
        if (error.code === 'LICENSE_HAS_ACTIVE_ALLOCATIONS' || error.status === 409) {
          message.replaceChildren(formAlert(error.message || 'Không thể ngừng sử dụng bản quyền còn đang cấp phát.'));
        } else {
          message.replaceChildren(formAlert(error.message || 'Lỗi khi ngừng sử dụng bản quyền.'));
        }
      } finally {
        pending = false; confirm.removeAttribute('aria-busy');
        if (!hasActiveSeats) confirm.disabled = false;
      }
    } },
    h('div', { className: 'dialog-body' },
      h('p', {}, `Ngừng sử dụng bản quyền ${fresh.licenseCode} (${fresh.software?.name ?? ''})?`),
      h('p', { className: 'meta' }, 'Thông tin và lịch sử cấp phát sẽ được lưu trữ, không bị xóa khỏi hệ thống.'),
      hasActiveSeats ? h('p', { className: 'alert alert-warning' }, `Bản quyền hiện có ${fresh.usedQuantity} chỗ đang được cấp phát. Vui lòng thu hồi tất cả trước khi ngừng sử dụng.`) : null,
      message),
    h('div', { className: 'dialog-footer' },
      button('Hủy', { kind: 'secondary', onClick: () => modal.close() }),
      confirm));

    modal = openDialog({ title: 'Ngừng sử dụng bản quyền', body: form, onClose: () => { closed = true; } });
  }

  async function openDetailDialog(row) {
    try {
      const fresh = await services.licenses.get(row.id);
      openDialog({
        title: `Chi tiết bản quyền — ${fresh.licenseCode}`,
        body: h('div', { className: 'dialog-body detail-grid' },
          h('div', { className: 'detail-item' }, h('dt', {}, 'Phần mềm'), h('dd', {}, fresh.software?.name || 'Chưa có dữ liệu')),
          h('div', { className: 'detail-item' }, h('dt', {}, 'Loại bản quyền'), h('dd', {}, licenseTypeLabels[fresh.licenseType] || fresh.licenseType)),
          h('div', { className: 'detail-item' }, h('dt', {}, 'Trạng thái'), h('dd', {}, statusBadge(fresh.status, fresh.isActive))),
          h('div', { className: 'detail-item' }, h('dt', {}, 'Tổng số seat'), h('dd', {}, String(fresh.totalQuantity))),
          h('div', { className: 'detail-item' }, h('dt', {}, 'Đã sử dụng'), h('dd', {}, missing(fresh.usedQuantity))),
          h('div', { className: 'detail-item' }, h('dt', {}, 'Còn khả dụng'), h('dd', {}, missing(fresh.availableQuantity))),
          h('div', { className: 'detail-item' }, h('dt', {}, 'Nhà cung cấp'), h('dd', {}, fresh.vendor || '—')),
          h('div', { className: 'detail-item' }, h('dt', {}, 'Khóa bản quyền (Key)'), h('dd', {}, fresh.maskedKey ?? 'Chưa có thông tin key')),
          h('div', { className: 'detail-item' }, h('dt', {}, 'Hiệu lực từ'), h('dd', {}, date(fresh.startsAt))),
          h('div', { className: 'detail-item' }, h('dt', {}, 'Hết hạn'), h('dd', {}, date(fresh.expiresAt))),
          fresh.notes ? h('div', { className: 'detail-item span-full' }, h('dt', {}, 'Ghi chú'), h('dd', {}, fresh.notes)) : null)
      });
    } catch (err) { showToast(err.message || 'Không thể tải chi tiết bản quyền.'); }
  }

  const keyword = h('input', { className: 'input', name: 'keyword', value: snapshot.keyword ?? '', maxLength: 200,
    placeholder: 'Mã license hoặc phần mềm…', 'aria-label': 'Tìm license' });
  const vendor = h('input', { className: 'input', name: 'vendor', value: snapshot.vendor ?? '', maxLength: 250,
    placeholder: 'Nhà cung cấp…', 'aria-label': 'Lọc nhà cung cấp' });
  const statusSelect = h('select', { className: 'select', name: 'status', 'aria-label': 'Trạng thái' },
    option('', 'Tất cả trạng thái'),
    option('ACTIVE', 'Đang hoạt động', snapshot.status === 'ACTIVE'),
    option('EXPIRING_SOON', 'Sắp hết hạn', snapshot.status === 'EXPIRING_SOON'),
    option('EXHAUSTED', 'Hết chỗ', snapshot.status === 'EXHAUSTED'),
    option('EXPIRED', 'Hết hạn', snapshot.status === 'EXPIRED'),
    option('NOT_EFFECTIVE', 'Chưa hiệu lực', snapshot.status === 'NOT_EFFECTIVE'),
    option('INACTIVE', 'Ngừng hoạt động', snapshot.status === 'INACTIVE'));

  const filters = h('form', { className: 'filters', onSubmit: event => {
    event.preventDefault();
    setQuery('/licenses', { ...snapshot, keyword: keyword.value.trim() || null, vendor: vendor.value.trim() || null, status: statusSelect.value || null, page: 1 });
  } }, keyword, vendor, statusSelect, button('Tìm kiếm', { type: 'submit', iconName: 'search' }),
    button('Đặt lại', { kind: 'secondary', onClick: () => setQuery('/licenses', { page: 1, pageSize: snapshot.pageSize ?? 20 }) }));

  const columns = [
    { key: 'licenseCode', label: 'Mã license', className: 'nowrap', render: item => button(item.licenseCode, { kind: 'ghost', className: 'nowrap', onClick: () => route('/licenses/' + item.id) }) },
    { key: 'software', label: 'Phần mềm', className: 'name-cell', render: item => item.software?.name || 'Chưa có dữ liệu' },
    { key: 'licenseType', label: 'Loại license', className: 'nowrap', render: item => licenseTypeLabels[item.licenseType] || item.licenseType },
    { key: 'totalQuantity', label: 'Tổng seat', className: 'text-right nowrap' },
    { key: 'usedQuantity', label: 'Đã dùng', className: 'text-right nowrap', render: item => missing(item.usedQuantity) },
    { key: 'availableQuantity', label: 'Còn lại', className: 'text-right nowrap', render: item => missing(item.availableQuantity) },
    { key: 'maskedKey', label: 'Key', className: 'nowrap', render: item => item.maskedKey ?? 'Chưa có thông tin key' },
    { key: 'expiresAt', label: 'Hết hạn', className: 'nowrap', render: item => date(item.expiresAt) },
    { key: 'status', label: 'Trạng thái', className: 'nowrap', render: item => statusBadge(item.status, item.isActive) },
    { key: 'actions', label: 'Thao tác', className: 'actions-cell nowrap', render: item => h('div', { className: 'actions' },
      button('Chi tiết', { kind: 'ghost', onClick: () => openDetailDialog(item) }),
      item.isActive && can(services, 'licenses.update') ? button('Sửa', { kind: 'ghost', onClick: () => openEditDialog(item) }) : null,
      item.isActive && can(services, 'licenses.archive') ? button('Ngừng dùng', { kind: 'ghost', onClick: () => openArchiveDialog(item) }) : null
    ) }
  ];

  const headerActions = [];
  if (can(services, 'licenses.create')) {
    headerActions.push(button('Thêm bản quyền', { iconName: 'plus', onClick: () => openEditDialog(null) }));
  }

  const root = h('div', {}, pageHeader('License phần mềm', 'Capacity dùng số liệu backend; key luôn masked.', { actions: headerActions }),
    h('section', { className: 'card filters-card' }, filters),
    h('section', { className: 'card' }, result.items.length ? dataTable({ columns, rows: result.items }) :
      h('p', { className: 'table-empty', role: 'status' }, 'Chưa có license phù hợp.'),
      pagination(result, page => setQuery('/licenses', { ...snapshot, page }))));
  return root;
}

export async function licenseDetailPage(services, id, { enabled = false, allocationPreparation = null } = {}) {
  if (enabled !== true && services.licenseEnabled !== true) return planned();
  const actorId = requireRead(services);
  let payload = await services.licenses.get(id);
  if (services.auth.session?.id !== actorId) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên đã thay đổi.');
  requireRead(services);
  let value = licenseView(payload);
  const info = (label, content) => h('div', { className: 'detail-item' }, h('dt', {}, label), h('dd', {}, content));

  const draftForm = allocationPreparation?.enabled === true && can(services, 'license-assignments.assign') ?
    licenseAllocationForm(services, { enabled: true, license: value, allowedTargets: allocationPreparation.allowedTargets,
      onPrepared: allocationPreparation.onPrepared }) : null;

  async function openRevealDialog() {
    let modal, pending = false;
    const reasonInput = h('textarea', { id: 'field-reveal-reason', className: 'textarea', placeholder: 'Nhập lý do xem khóa bản quyền (bắt buộc, 1-1000 ký tự)...', maxLength: 1000, required: true, rows: 3 });
    const message = h('div', { 'aria-live': 'polite' });
    const confirm = button('Giải mã khóa', { type: 'submit' });
    const form = h('form', { onSubmit: async event => {
      event.preventDefault();
      const reason = reasonInput.value.trim();
      if (!reason) { message.replaceChildren(formAlert('Vui lòng nhập lý do.')); return; }
      pending = true; confirm.disabled = true; confirm.setAttribute('aria-busy', 'true');
      try {
        const res = await services.licenses.revealKey(value.id, reason);
        modal.close();
        openDialog({
          title: `Khóa bản quyền — ${value.licenseCode}`,
          body: h('div', { className: 'dialog-body' },
            h('p', { className: 'alert alert-warning' }, 'Khóa đã giải mã cho phiên làm việc này và được ghi nhận trong audit log. Không sao chép hoặc chia sẻ ngoài quy định.'),
            h('div', { className: 'field' }, h('label', { className: 'label' }, 'Plaintext License Key:'),
              h('input', { className: 'input monospace', value: res.licenseKey, readonly: true, selectOnFocus: true })))
        });
      } catch (err) {
        message.replaceChildren(formAlert(err.message || 'Không thể giải mã khóa.'));
      } finally {
        pending = false; confirm.removeAttribute('aria-busy'); confirm.disabled = false;
      }
    } },
    h('div', { className: 'dialog-body form-grid' },
      h('p', {}, `Yêu cầu quyền Admin IT và ghi nhật ký kiểm toán cho thao tác giải mã khóa của bản quyền ${value.licenseCode}.`),
      h('div', { className: 'field' }, h('label', { className: 'label' }, 'Lý do truy cập *'), reasonInput),
      message),
    h('div', { className: 'dialog-footer' }, button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm));
    modal = openDialog({ title: 'Xác thực xem khóa bản quyền', body: form });
  }

  const actions = [button('Danh sách', { kind: 'secondary', onClick: () => route('/licenses') })];
  const isAdmin = services.auth.session?.roles?.includes('ADMIN_IT');
  if (value.maskedKey && isAdmin && can(services, 'licenses.key.reveal')) {
    actions.push(button('Xem khóa', { kind: 'ghost', iconName: 'key', onClick: () => openRevealDialog() }));
  }

  const allocation = await licenseAllocationPanel(services, value);
  if (services.auth.session?.id !== actorId) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên đã thay đổi.');
  const root = h('div', {}, pageHeader(value.licenseCode, 'Thông tin license — không reveal key từ trang metadata.',
    { actions }),
    h('section', { className: 'card' }, h('dl', { className: 'card-body detail-grid' },
      info('Phần mềm', value.software?.name || 'Chưa có dữ liệu'), info('Loại', licenseTypeLabels[value.licenseType] || value.licenseType),
      info('Tổng seat', String(value.totalQuantity)),
      h('div', { className: 'detail-item' }, h('dt', {}, 'Đã dùng'), h('dd', { id: 'detail-used-quantity' }, missing(value.usedQuantity))),
      h('div', { className: 'detail-item' }, h('dt', {}, 'Còn lại'), h('dd', { id: 'detail-available-quantity' }, missing(value.availableQuantity))),
      info('Key', value.maskedKey ?? 'Chưa có thông tin key'),
      info('Hiệu lực', date(value.startsAt)), info('Hết hạn', date(value.expiresAt)),
      info('Trạng thái', statusBadge(value.status, value.isActive)))),
    allocation?.element,
    draftForm?.element,
    allocationPreparation?.enabled === true && !can(services, 'license-assignments.assign') ?
      h('p', { className: 'info-note' }, 'Bạn có quyền xem metadata nhưng không có quyền chuẩn bị cấp license.') : null,
    (!allocation) ?
      h('p', { className: 'info-note' }, 'Phân bổ/thu hồi/chuyển seat: PLANNED hoặc cần quyền license-assignments.read.') : null);

  root.dispose = () => { draftForm?.dispose(); allocation?.dispose(); };
  return root;
}

export async function licensePage(services, id = null) {
  if (services.licenseEnabled !== true) throw Object.assign(new Error('Phân hệ bản quyền chưa được mở.'), { status: 404 });
  if (!can(services, 'licenses.read')) throw Object.assign(new Error('Bạn không có quyền xem bản quyền.'), { status: 403 });
  if (id != null) return licenseDetailPage(services, id, { enabled: true });
  return licenseListPage(services, { enabled: true, query: { page: 1, pageSize: 20, ...queryFrom() } });
}
