import { h } from '../utils/dom.js';
import { button, pageHeader, badge, showToast } from '../components.js';
import { validateAsset } from '../utils/validation.js';
import { allActive, hasPermission, route, field, displayErrors, formAlert, busy } from './shared.js';
import { concurrencyFeedback } from '../components/concurrency-feedback.js';
import { ServiceError } from '../services/service-error.js';

export async function assetFormPage(services, id = null) {
  const initialSession = services.auth?.session;
  const writable = session => Number.isSafeInteger(session?.id) && session.id > 0 && hasPermission(session, 'assets.read') &&
    hasPermission(session, id ? 'assets.update' : 'assets.create');
  if (!writable(initialSession)) throw new ServiceError(403, 'FORBIDDEN', 'Bạn không có quyền mở form tài sản này.');
  const actorId = initialSession.id;
  let canWriteCost = false;
  const sameActor = () => { const current = services.auth?.session; return current?.id === actorId && writable(current) && (!canWriteCost || hasPermission(current, 'assets.cost.read')); };
  const [asset, types, departments] = await Promise.all([id ? services.assets.get(id) : null, allActive(services.assetTypes), allActive(services.departments)]);
  const loadedSession = services.auth?.session;
  if (loadedSession?.id !== actorId || !writable(loadedSession)) throw new ServiceError(403, 'SESSION_CHANGED', 'Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang.');
  canWriteCost = hasPermission(loadedSession, 'assets.cost.read');
  let blocked = false;
  const references = (list, current) => [{ value: '', label: 'Chọn danh mục' }, ...list.map(item => ({ value: item.id, label: item.name })), ...(current && !list.some(item => item.id === current.id) ? [{ value: current.id, label: current.name + ' (ngừng hoạt động — giữ tham chiếu cũ)' }] : [])];
  const fields = {};
  const add = config => { const result = field({ value: asset?.[config.name] ?? '', ...config }); fields[config.name] = result; return result.element; };
  const section = (title, description, children) => h('section', { className: 'card form-section' }, h('div', { className: 'card-header' }, h('div', {}, h('h2', {}, title), h('p', { className: 'meta' }, description))), h('div', { className: 'card-body form-grid' }, children));
  const feedback = h('div', { 'aria-live': 'polite' });
  const hasType = types.length > 0 || Boolean(asset?.assetType);
  const hasDepartment = departments.length > 0 || Boolean(asset?.owningDepartment);
  const submit = button(id ? 'Lưu thay đổi' : 'Tạo tài sản', { type: 'submit', iconName: 'check', disabled: !hasType || !hasDepartment });
  const form = h('form', { noValidate: true, onSubmit: event => {
    event.preventDefault(); if (submit.disabled || blocked || !form.isConnected) return;
    if (!sameActor()) { blocked = true; submit.disabled = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Vui lòng mở lại trang.')); return; }
    const submittedHash = window.location.hash;
    const active = () => form.isConnected && window.location.hash === submittedHash;
    const dto = {};
    for (const [key, item] of Object.entries(fields)) {
      const raw = item.input.value;
      dto[key] = ['assetTypeId', 'owningDepartmentId'].includes(key) ? (raw ? Number(raw) : null) : raw.trim() || null;
    }
    if (id) dto.rowVersion = asset.rowVersion;
    // Preserve decimal text to validate scale before Number conversion could erase it.
    const priceText = fields.purchasePrice?.input.value;
    const errors = validateAsset(dto, { isUpdate: Boolean(id) });
    if (priceText && !/^\d+(?:\.\d{1,2})?$/.test(priceText)) errors.purchasePrice = ['Giá mua phải là số không âm, tối đa 2 chữ số thập phân.'];
    displayErrors(fields, errors); feedback.replaceChildren();
    if (Object.keys(errors).length) return;
    if (Object.hasOwn(dto, 'purchasePrice') && dto.purchasePrice !== null) dto.purchasePrice = Number(dto.purchasePrice);
    // The request uses the captured DTO. Do not let edits while pending look
    // like they were part of that write; restore original states, not defaults.
    const pendingFields = Object.values(fields).map(({ input }) => ({ input, readOnly: input.readOnly, disabled: input.disabled }));
    for (const { input } of pendingFields) {
      if (input.tagName === 'SELECT') input.disabled = true; else input.readOnly = true;
    }
    busy(submit, async () => {
      try {
        const saved = id ? await services.assets.update(id, dto) : await services.assets.create(dto);
        if (!active()) return;
        if (!sameActor()) { blocked = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Hãy tải lại để kiểm tra kết quả thực tế.')); return; }
        if (!Number.isSafeInteger(saved?.id) || saved.id <= 0 || id && saved.id !== id) throw new ServiceError(502, 'INVALID_API_RESPONSE', 'Chưa xác nhận được kết quả lưu.');
        showToast(services.mode === 'mock' ? 'Đã lưu vào dữ liệu minh họa trong bộ nhớ.' : 'Đã lưu tài sản.'); route('/assets/' + saved.id);
      } catch (error) {
        if (!active()) return;
        if (!sameActor()) { blocked = true; feedback.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Hãy tải lại để kiểm tra kết quả thực tế.')); return; }
        const uncertain = services.mode === 'api' && (![400, 401, 403, 409, 413, 415, 422, 429].includes(error?.status) ||
          ['API_UNAVAILABLE', 'INVALID_API_RESPONSE', 'SESSION_CHANGED'].includes(error?.code));
        if (uncertain) {
          blocked = true;
          feedback.replaceChildren(formAlert('Chưa xác nhận được kết quả lưu; server có thể đã xử lý. Bản đang nhập được giữ, không gửi lại để tránh tạo trùng hoặc ghi đè.'),
            h('a', { className: 'button secondary', href: '#/assets', target: '_blank', rel: 'noopener noreferrer' }, 'Mở danh sách ở tab mới để đối chiếu'),
            h('p', { className: 'meta' }, 'Tab mới cần đăng nhập lại vì phiên chỉ giữ trong bộ nhớ; bản đang nhập vẫn ở tab này. Đối chiếu mã/tên/serial hoặc nhờ người quản lý kiểm tra trước khi mở lại form. Không tự động thử lại thao tác.'));
          return;
        }
        displayErrors(fields, error?.errors || {});
        feedback.append(formAlert(error?.message || 'Không thể lưu tài sản.'));
        if (error?.code === 'CONCURRENCY_CONFLICT') { blocked = true; feedback.append(concurrencyFeedback({ onRefresh: () => window.dispatchEvent(new Event('frontend-refresh')) })); }
      }
    }).finally(() => {
      for (const { input, readOnly, disabled } of pendingFields) { input.readOnly = readOnly; input.disabled = disabled; }
      if (form.isConnected && blocked) submit.disabled = true;
    });
  } },
  !types.length || !departments.length ? h('p', { className: 'info-note' }, id ? 'Danh mục đang hoạt động không đủ. Bạn vẫn được giữ tham chiếu cũ khi sửa; không chọn tham chiếu mới đã ngừng hoạt động.' : 'Cần ít nhất một loại tài sản và một phòng ban đang hoạt động. Tạo danh mục trước khi thêm tài sản.') : null,
  section('Thông tin chung', 'Những thông tin nhận diện thiết bị. Dấu * là bắt buộc.', [h('div', { className: 'field' }, h('span', {}, 'Mã tài sản'), h('strong', {}, asset?.assetCode || 'Mã tài sản sẽ được tự động tạo khi lưu')), add({ name: 'name', label: 'Tên tài sản', required: true, maxLength: 200 }), add({ name: 'assetTypeId', label: 'Loại tài sản', required: true, options: references(types, asset?.assetType) }), add({ name: 'serialNumber', label: 'Serial Number', maxLength: 200 }), add({ name: 'brand', label: 'Thương hiệu', maxLength: 200 }), add({ name: 'model', label: 'Model', maxLength: 200 })]),
  section('Thông tin kỹ thuật', 'Cấu hình và hệ điều hành của thiết bị.', [add({ name: 'specification', label: 'Cấu hình', type: 'textarea', maxLength: 4000 }), add({ name: 'operatingSystem', label: 'Hệ điều hành', maxLength: 250 })]),
  section('Thông tin mua sắm', canWriteCost ? 'Giá chưa biết để trống, không mặc định thành 0.' : id ? 'Bạn không có quyền xem/sửa giá mua. Giá mua hiện tại được giữ nguyên.' : 'Bạn không có quyền nhập giá mua. Giá mua để chưa biết, không mặc định thành 0.', [add({ name: 'purchaseDate', label: 'Ngày mua', type: 'date' }), canWriteCost ? add({ name: 'purchasePrice', label: 'Giá mua (VND)', type: 'text', hint: 'Số không âm, dùng dấu chấm cho phần thập phân; tối đa 2 chữ số.' }) : null, add({ name: 'warrantyExpirationDate', label: 'Ngày hết bảo hành', type: 'date' })]),
  section('Phân bổ & vị trí', 'Phòng ban sở hữu không đồng nghĩa với người đang được cấp phát thiết bị.', [add({ name: 'owningDepartmentId', label: 'Phòng ban sở hữu', required: true, options: references(departments, asset?.owningDepartment) }), add({ name: 'location', label: 'Vị trí', maxLength: 500 })]),
  section('Thông tin khác', 'Trạng thái được điều phối qua nghiệp vụ, không đổi trong form metadata.', [h('div', { className: 'field' }, h('span', {}, 'Trạng thái'), badge(asset?.status || 'InStock'), h('span', { className: 'field-hint' }, id ? 'Chỉ đọc. Giữ nguyên trạng thái khi sửa thông tin.' : 'Tài sản mới luôn ở trạng thái Trong kho (InStock).')), add({ name: 'note', label: 'Ghi chú', type: 'textarea', maxLength: 2000 })]),
  feedback, h('div', { className: 'form-actions' }, h('span', { className: 'meta' }, services.mode === 'mock' ? 'Chỉ lưu bản demo; tải lại trang sẽ đặt lại dữ liệu.' : 'Thông tin được gửi qua backend API.'), h('div', { className: 'actions' }, button('Hủy', { kind: 'secondary', onClick: () => route(id ? '/assets/' + id : '/assets') }), submit)));
  if (fields.purchasePrice) fields.purchasePrice.input.inputMode = 'decimal';
  return h('div', {}, pageHeader(id ? 'Chỉnh sửa tài sản' : 'Thêm tài sản', id ? 'Cập nhật thông tin ' + asset.assetCode : 'Khai báo thiết bị vào danh mục tài sản.', { eyebrow: 'Tài sản', actions: [button('Quay lại', { kind: 'secondary', iconName: 'arrow-left', onClick: () => route(id ? '/assets/' + id : '/assets') })] }), form);
}
