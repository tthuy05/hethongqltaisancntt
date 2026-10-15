import { h } from '../utils/dom.js';
import { pageHeader, button, dataTable, pagination } from '../components.js';
import { can, hasPermission, date, route, setQuery } from './shared.js';
import { licenseView, licensePageView } from '../models/license-view.js';
import { ServiceError } from '../services/service-error.js';
import { licenseAllocationForm } from '../components/license-allocation-form.js';

const missing = value => value == null ? 'Chưa có dữ liệu' : String(value);
const planned = () => h('div', {}, pageHeader('License phần mềm', 'Chuẩn bị giao diện theo kế hoạch Tuần5.'),
  h('p', { className: 'info-note' }, 'PLANNED — chưa mở License API/UI. Không hiển thị dữ liệu minh họa như dữ liệu thật.'));
function requireRead(services) {
  const session = services.auth?.session;
  if (services.mode !== 'api' || !Number.isSafeInteger(session?.id) || session.id <= 0 ||
      !hasPermission(session, 'licenses.read')) throw new ServiceError(403, 'FORBIDDEN', 'Bạn không có quyền xem license.');
  if (!services.licenses) throw new ServiceError(503, 'MODULE_NOT_READY', 'License API chưa sẵn sàng.');
  return session.id;
}
// Isolated component preparation only; app.js has no License menu/route. Enabling
// a production route requires owner DTO review and approved backend readiness.
export async function licenseListPage(services, { enabled = false, query = { page: 1, pageSize: 20 } } = {}) {
  if (enabled !== true) return planned();
  const actorId = requireRead(services);
  if (!query || typeof query !== 'object' || Array.isArray(query)) throw new ServiceError(400, 'VALIDATION_ERROR', 'Bộ lọc phải là object.');
  const snapshot = Object.freeze({ ...query });
  const payload = await services.licenses.list(snapshot);
  if (services.auth.session?.id !== actorId) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên đã thay đổi.');
  requireRead(services);
  const result = licensePageView(payload);
  const keyword = h('input', { className: 'input', name: 'keyword', value: snapshot.keyword ?? '', maxLength: 200,
    placeholder: 'Mã license hoặc phần mềm…', 'aria-label': 'Tìm license' });
  const vendor = h('input', { className: 'input', name: 'vendor', value: snapshot.vendor ?? '', maxLength: 250,
    placeholder: 'Nhà cung cấp…', 'aria-label': 'Lọc nhà cung cấp' });
  const filters = h('form', { className: 'filters', onSubmit: event => {
    event.preventDefault(); setQuery('/licenses', { ...snapshot, keyword: keyword.value.trim() || null, vendor: vendor.value.trim() || null, page: 1 });
  } }, keyword, vendor, button('Tìm kiếm', { type: 'submit', iconName: 'search' }),
    button('Đặt lại', { kind: 'secondary', onClick: () => setQuery('/licenses', { page: 1, pageSize: snapshot.pageSize ?? 20 }) }));
  const columns = [
    { key: 'licenseCode', label: 'Mã license', className: 'nowrap', render: item => button(item.licenseCode, { kind: 'ghost', className: 'nowrap', onClick: () => route('/licenses/' + item.id) }) },
    { key: 'software', label: 'Phần mềm', className: 'name-cell', render: item => item.software?.name || 'Chưa có dữ liệu' },
    { key: 'licenseType', label: 'Loại license', className: 'nowrap' },
    { key: 'totalQuantity', label: 'Tổng seat', className: 'text-right nowrap' },
    { key: 'usedQuantity', label: 'Đã dùng', className: 'text-right nowrap', render: item => missing(item.usedQuantity) },
    { key: 'availableQuantity', label: 'Còn lại', className: 'text-right nowrap', render: item => missing(item.availableQuantity) },
    { key: 'maskedKey', label: 'Key', className: 'nowrap', render: item => item.maskedKey ?? 'Chưa có thông tin key' },
    { key: 'expiresAt', label: 'Hết hạn', className: 'nowrap', render: item => date(item.expiresAt) }
  ];
  return h('div', {}, pageHeader('License phần mềm', 'Capacity dùng số liệu backend; key luôn masked.'),
    h('section', { className: 'card filters-card' }, filters),
    h('section', { className: 'card' }, result.items.length ? dataTable({ columns, rows: result.items }) :
      h('p', { className: 'table-empty', role: 'status' }, 'Chưa có license phù hợp.'),
      pagination(result, page => setQuery('/licenses', { ...snapshot, page }))));
}
export async function licenseDetailPage(services, id, { enabled = false, allocationPreparation = null } = {}) {
  if (enabled !== true) return planned();
  const actorId = requireRead(services);
  const payload = await services.licenses.get(id);
  if (services.auth.session?.id !== actorId) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên đã thay đổi.');
  requireRead(services);
  const value = licenseView(payload);
  const info = (label, content) => h('div', { className: 'detail-item' }, h('dt', {}, label), h('dd', {}, content));
  const draftForm = allocationPreparation?.enabled === true && can(services, 'license-assignments.assign') ?
    licenseAllocationForm(services, { enabled: true, license: value, allowedTargets: allocationPreparation.allowedTargets,
      onPrepared: allocationPreparation.onPrepared }) : null;
  const root = h('div', {}, pageHeader(value.licenseCode, 'Thông tin license — không reveal key từ trang metadata.',
    { actions: [button('Danh sách', { kind: 'secondary', onClick: () => route('/licenses') })] }),
    h('section', { className: 'card' }, h('dl', { className: 'card-body detail-grid' },
      info('Phần mềm', value.software?.name || 'Chưa có dữ liệu'), info('Loại', value.licenseType),
      info('Tổng seat', String(value.totalQuantity)), info('Đã dùng', missing(value.usedQuantity)),
      info('Còn lại', missing(value.availableQuantity)), info('Key', value.maskedKey ?? 'Chưa có thông tin key'),
      info('Hiệu lực', date(value.startsAt)), info('Hết hạn', date(value.expiresAt)))),
    draftForm?.element,
    allocationPreparation?.enabled === true && !can(services, 'license-assignments.assign') ?
      h('p', { className: 'info-note' }, 'Bạn có quyền xem metadata nhưng không có quyền chuẩn bị cấp license.') : null,
    h('p', { className: 'info-note' }, 'Phân bổ/thu hồi/chuyển seat và reveal: PLANNED, chờ Allocation API/DTO của Thiện.'));
  root.dispose = () => draftForm?.dispose();
  return root;
}
