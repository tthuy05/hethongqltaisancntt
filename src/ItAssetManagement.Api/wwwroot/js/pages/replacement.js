import { h } from '../utils/dom.js';
import { button, pageHeader, dataTable, pagination, showToast } from '../components.js';
import { can, route, queryFrom, setQuery, option, field, displayErrors, formAlert, openDialog, money, date } from './shared.js';
import { severities, validateRule } from '../services/replacement-service.js';

const labels = { LOW: 'Thấp', MEDIUM: 'Trung bình', HIGH: 'Cao', CRITICAL: 'Khẩn cấp', ACTIVE: 'Đang xem xét', PLANNED: 'Đã lập kế hoạch', DISMISSED: 'Đã bỏ qua', SUPERSEDED: 'Đã được thay thế' };
export async function replacementPage(services, rules = false) {
  const read = rules ? 'replacement-rules.read' : 'replacement-recommendations.read';
  if (services.replacementEnabled !== true) throw Object.assign(new Error('Chức năng thay thế chưa được mở.'), { status: 404 });
  if (!can(services, read) || (!rules && !can(services, 'assets.read'))) throw Object.assign(new Error('Bạn không có quyền xem chức năng này.'), { status: 403 });
  const actor = services.auth.session.id, hash = window.location.hash, path = rules ? '/replacement/rules' : '/replacement';
  const current = () => services.auth.session?.id === actor && services.replacementEnabled === true && can(services, read) && (rules || can(services, 'assets.read'));
  const query = { page: 1, pageSize: 20, ...queryFrom() };
  const result = await (rules ? services.replacement.rules(query) : services.replacement.list(query));
  if (!current()) throw Object.assign(new Error('Phiên đã thay đổi.'), { status: 401 });
  const root = h('div', {}), feedback = h('div', { 'aria-live': 'polite' });
  const alive = () => root.isConnected && current() && window.location.hash === hash;
  const financial = () => can(services, 'assets.cost.read') || can(services, 'reports.cost.read');
  const admin = () => services.auth.session?.roles?.includes('ADMIN_IT') === true;
  function formDialog(title, grant, build, send, requiresAdmin = false) {
    let modal, pending = false, blocked = false, closed = false;
    const fields = {}, message = h('div', { 'aria-live': 'polite' });
    const active = () => !closed && alive() && message.isConnected;
    const allowed = () => current() && can(services, grant) && (!requiresAdmin || admin());
    const confirm = button('Xác nhận', { type: 'submit' });
    const add = (name, label, value = '', options = {}) => {
      const item = field({ name: 'replacement-' + name, label, value, ...options }); fields[name] = item; return item.element;
    };
    const body = build(add, fields);
    const form = h('form', { noValidate: true, onSubmit: async event => {
      event.preventDefault(); if (pending || blocked || !active()) return;
      if (!allowed()) { message.replaceChildren(formAlert('Phiên hoặc quyền đã thay đổi. Hãy đóng và tải lại.')); blocked = true; confirm.disabled = true; return; }
      pending = true; confirm.disabled = true; message.replaceChildren();
      try {
        const result = await send(fields);
        if (!active()) return;
        modal.close(); if (allowed()) { showToast(result?.message || 'Đã cập nhật dữ liệu thay thế.'); window.dispatchEvent(new Event('frontend-refresh')); }
      } catch (error) {
        if (!active()) return;
        message.replaceChildren(formAlert(error.message || 'Chưa xác nhận được kết quả.'));
        if (error.status === 400 && allowed()) displayErrors(fields, error.errors || {});
        else { blocked = true; message.append(button('Đóng và tải lại', { kind: 'secondary', onClick: () => { modal.close(); if (alive()) window.dispatchEvent(new Event('frontend-refresh')); } })); }
      } finally { pending = false; if (active()) confirm.disabled = blocked; }
    } }, h('div', { className: 'dialog-body form-grid' }, ...body, message),
      h('div', { className: 'dialog-footer' }, button('Hủy', { kind: 'secondary', onClick: () => modal.close() }), confirm));
    modal = openDialog({ title, body: form, onClose: () => { closed = true; } });
    return { active, message };
  }
  async function editRule(row = null) {
    const grant = row ? 'replacement-rules.update' : 'replacement-rules.create';
    if (!alive() || !admin() || !can(services, grant)) return;
    try {
      const item = row ? await services.replacement.rule(row.id) : null;
      const types = can(services, 'asset-types.read') ? (await services.assetTypes.list({ page: 1, pageSize: 100 })).items : [];
      if (!alive() || !admin() || !can(services, grant)) return;
      const costAllowed = financial();
      formDialog(item ? 'Tạo phiên bản quy tắc mới' : 'Tạo quy tắc thay thế', grant, add => [
        h('p', {}, item ? `${item.code} — phiên bản ${item.version}. Bản cũ và các đề xuất trước đó được giữ lại.` : 'Mã quy tắc được hệ thống tự tạo.'),
        add('name', 'Tên quy tắc', item?.name || '', { required: true, maxLength: 250 }),
        add('severity', 'Mức ưu tiên của đề xuất', item?.severity || '', { required: true, options: [{ value: '', label: 'Chọn mức ưu tiên' }, ...severities.map(value => ({ value, label: labels[value] }))] }),
        add('assetTypeId', 'Áp dụng cho loại tài sản', item?.assetTypeId ?? '', { options: [{ value: '', label: 'Tất cả loại tài sản' }, ...types.filter(x => x.isActive || x.id === item?.assetTypeId).map(x => ({ value: x.id, label: x.name })), ...(item?.assetTypeId && !types.some(x => x.id === item.assetTypeId) ? [{ value: item.assetTypeId, label: 'Giữ loại tài sản hiện tại' }] : [])] }),
        add('minimumAgeMonths', 'Tuổi tài sản tối thiểu (tháng)', item?.minimumAgeMonths ?? '', { type: 'number' }),
        add('minimumMaintenanceCount', 'Số lần bảo trì đã kết thúc tối thiểu', item?.minimumMaintenanceCount ?? '', { type: 'number', hint: 'Chỉ tính phiếu đã giải quyết hoặc thất bại, kể cả đã lưu trữ.' }),
        add('maximumMaintenanceCostRatio', 'Tỷ lệ chi phí bảo trì / giá mua', item?.maximumMaintenanceCostRatio ?? '', { type: 'number', hint: 'Từ 0 đến 1; ví dụ 0,5 tương ứng 50%.' }),
        add('requireWarrantyExpired', 'Điều kiện hết bảo hành', String(item?.requireWarrantyExpired ?? false), { options: [{ value: 'false', label: 'Không sử dụng' }, { value: 'true', label: 'Hết bảo hành và đạt số lần thất bại' }] }),
        add('minimumFailureCount', 'Số lần xử lý thất bại tối thiểu', item?.minimumFailureCount ?? '', { type: 'number' }),
        ...(costAllowed ? [add('estimatedUnitCost', 'Giá thay thế ước tính (VNĐ)', item?.estimatedUnitCost ?? '', { type: 'number' })] : []),
        add('priority', 'Thứ tự khi cùng mức ưu tiên', item?.priority ?? 100, { type: 'number', hint: 'Số nhỏ được chọn trước. Các điều kiện trong một quy tắc kết hợp HOẶC.' }),
        add('description', 'Mô tả', item?.description || '', { type: 'textarea', maxLength: 2000 })
      ], async fields => {
        if (costAllowed && !financial()) throw Object.assign(new Error('Quyền xem giá đã thay đổi.'), { status: 403 });
        const number = key => fields[key].input.value.trim() === '' ? null : Number(fields[key].input.value);
        const dto = { name: fields.name.input.value.trim(), severity: fields.severity.input.value,
          assetTypeId: number('assetTypeId'), minimumAgeMonths: number('minimumAgeMonths'), minimumMaintenanceCount: number('minimumMaintenanceCount'),
          maximumMaintenanceCostRatio: number('maximumMaintenanceCostRatio'), requireWarrantyExpired: fields.requireWarrantyExpired.input.value === 'true',
          minimumFailureCount: number('minimumFailureCount'), priority: number('priority'), description: fields.description.input.value.trim() || null,
          ...(costAllowed ? { estimatedUnitCost: number('estimatedUnitCost') } : {}), ...(item ? { rowVersion: item.rowVersion } : {}) };
        const errors = validateRule(dto, Boolean(item));
        if (Object.keys(errors).length) throw Object.assign(new Error('Kiểm tra cấu hình quy tắc.'), { status: 400, errors });
        return item ? services.replacement.reviseRule(item.id, dto) : services.replacement.createRule(dto);
      }, true);
    } catch (error) { if (alive()) feedback.replaceChildren(formAlert(error.message)); }
  }
  async function statusRule(row) {
    if (!alive() || !admin() || !can(services, 'replacement-rules.update')) return;
    try {
      const item = await services.replacement.rule(row.id); if (!alive()) return;
      formDialog('Đổi trạng thái quy tắc', 'replacement-rules.update', () => [h('p', {}, `${item.isActive ? 'Ngừng' : 'Kích hoạt'} ${item.code} v${item.version}? Các đề xuất hiện có vẫn được giữ lại.`)],
        () => services.replacement.status(item.id, !item.isActive, item.rowVersion), true);
    } catch (error) { if (alive()) feedback.replaceChildren(formAlert(error.message)); }
  }
  async function detail(row) {
    if (!alive()) return;
    try {
      const item = await services.replacement.get(row.id); if (!alive()) return;
      let evidence = null;
      if (financial() && item.evaluationSnapshotJson) {
        try { evidence = h('details', {}, h('summary', {}, 'Dữ liệu đối chiếu'), h('pre', {}, JSON.stringify(JSON.parse(item.evaluationSnapshotJson), null, 2))); } catch { evidence = formAlert('Dữ liệu đối chiếu không hợp lệ.'); }
      }
      openDialog({ title: 'Căn cứ đề xuất thay thế', body: h('div', { className: 'dialog-body' }, h('p', {}, item.reason),
        h('p', {}, 'Mức ưu tiên: ', labels[item.priority] || item.priority), h('p', {}, 'Trạng thái: ', labels[item.disposition] || item.disposition),
        financial() ? h('p', {}, 'Ước tính: ', money(item.estimatedReplacementCost)) : null, h('p', {}, item.dispositionNote || ''), evidence) });
    } catch (error) { if (alive()) feedback.replaceChildren(formAlert(error.message)); }
  }
  async function ruleDetail(row) {
    if (!alive()) return;
    try {
      const item = await services.replacement.rule(row.id); if (!alive()) return;
      openDialog({ title: 'Chi tiết quy tắc thay thế', body: h('div', { className: 'dialog-body' },
        h('h3', {}, item.name), h('p', {}, `${item.code} — phiên bản ${item.version}`),
        h('p', {}, 'Ưu tiên: ', labels[item.severity] || 'Chưa cấu hình'), h('p', {}, 'Tuổi tối thiểu (tháng): ', item.minimumAgeMonths ?? 'Không sử dụng'),
        h('p', {}, 'Số lần bảo trì đã kết thúc: ', item.minimumMaintenanceCount ?? 'Không sử dụng'), h('p', {}, 'Tỷ lệ chi phí/giá mua: ', item.maximumMaintenanceCostRatio ?? 'Không sử dụng'),
        h('p', {}, 'Hết bảo hành và số lần thất bại: ', item.requireWarrantyExpired ? item.minimumFailureCount : 'Không sử dụng'),
        financial() ? h('p', {}, 'Giá thay thế: ', money(item.estimatedUnitCost)) : null, h('p', {}, item.description || ''),
        h('p', {}, 'Hiệu lực từ: ', date(item.effectiveFrom)), h('p', {}, 'Kết thúc: ', date(item.effectiveTo))) });
    } catch (error) { if (alive()) feedback.replaceChildren(formAlert(error.message)); }
  }
  async function disposition(row) {
    if (!alive() || !can(services, 'replacement-recommendations.update')) return;
    try {
      const item = await services.replacement.get(row.id); if (!alive()) return;
      formDialog('Xử lý đề xuất', 'replacement-recommendations.update', add => [
        h('p', {}, 'Đây là ghi nhận kế hoạch; không tự ngừng tài sản hoặc tạo khoản chi.'),
        add('disposition', 'Quyết định', 'PLANNED', { options: [{ value: 'PLANNED', label: 'Lập kế hoạch' }, ...(item.disposition === 'ACTIVE' ? [{ value: 'DISMISSED', label: 'Bỏ qua đề xuất' }] : [])] }),
        add('year', 'Năm dự kiến thay thế', item.plannedReplacementYear ?? new Date().getUTCFullYear(), { type: 'number' }),
        add('note', 'Lý do', '', { required: true, type: 'textarea', maxLength: 2000 })
      ], fields => services.replacement.disposition(item.id, { disposition: fields.disposition.input.value,
        ...(fields.disposition.input.value === 'PLANNED' ? { plannedReplacementYear: Number(fields.year.input.value) } : {}),
        note: fields.note.input.value.trim(), rowVersion: item.rowVersion }));
    } catch (error) { if (alive()) feedback.replaceChildren(formAlert(error.message)); }
  }
  function evaluate() {
    if (!alive() || !can(services, 'replacement-recommendations.evaluate') || !can(services, 'assets.cost.read')) return;
    const selected = new Set(), list = h('div', {}), summary = h('p', { 'aria-live': 'polite' }, 'Chưa chọn tài sản.');
    let searchId = 0;
    const dialog = formDialog('Đánh giá tài sản thay thế', 'replacement-recommendations.evaluate', add => [
      h('p', {}, 'Chọn tối đa 100 tài sản. Kết quả mới giữ lịch sử cũ; tài sản không tự ngừng sử dụng.'),
      add('keyword', 'Tìm mã hoặc tên tài sản'), button('Tìm tài sản', { type: 'button', kind: 'secondary', onClick: () => load(document.getElementById('field-replacement-keyword')?.value || '') }), summary, list
    ], async () => {
      if (!can(services, 'assets.cost.read')) throw Object.assign(new Error('Quyền đánh giá đã thay đổi.'), { status: 403 });
      const result = await services.replacement.evaluate([...selected]);
      if (!Number.isSafeInteger(result?.evaluated) || !Number.isSafeInteger(result?.created) || !Array.isArray(result?.results))
        throw Object.assign(new Error('Chưa xác nhận được kết quả đánh giá. Hãy tải lại.'), { status: 502 });
      const missing = result.results.filter(row => row.outcome === 'INSUFFICIENT_DATA').length;
      return { message: `Đã đánh giá ${result.evaluated} tài sản, tạo ${result.created} đề xuất mới; ${missing} tài sản chưa đủ dữ liệu.` };
    });
    async function load(keyword = '') {
      const revision = ++searchId;
      try {
        const rows = await services.assets.list({ page: 1, pageSize: 100, keyword });
        if (!dialog.active() || revision !== searchId) return;
        list.replaceChildren(h('p', {}, 'Hiển thị tối đa 100 kết quả. Tìm cụ thể hơn nếu chưa thấy tài sản.'), ...rows.items.map(item => h('label', { className: 'field' },
          h('input', { type: 'checkbox', checked: selected.has(item.id), onChange: event => {
            if (event.target.checked) selected.add(item.id); else selected.delete(item.id);
            summary.textContent = 'Đã chọn ' + selected.size + ' tài sản.';
          } }), item.assetCode + ' — ' + item.name)));
      } catch (error) { if (dialog.active() && revision === searchId) list.replaceChildren(formAlert(error.message)); }
    }
    load();
  }
  const keyword = h('input', { className: 'input', value: query.keyword || '', 'aria-label': 'Tìm quy tắc' });
  const status = h('select', { className: 'select', 'aria-label': 'Trạng thái thay thế' }, option('', 'Tất cả trạng thái'),
    ...(rules ? [['Active', 'Đang bật'], ['Inactive', 'Đã ngừng']] : ['ACTIVE', 'PLANNED', 'DISMISSED', 'SUPERSEDED'].map(value => [value, labels[value]])).map(([value, label]) => option(value, label, query.status === value)));
  root.append(pageHeader(rules ? 'Quy tắc thay thế' : 'Đề xuất thay thế', 'Đánh giá có căn cứ, giữ lịch sử và không tự ngừng tài sản.', { actions: rules ? admin() && can(services, 'replacement-rules.create') ? [button('Tạo quy tắc', { onClick: () => editRule() })] : [] : can(services, 'replacement-recommendations.evaluate') && can(services, 'assets.cost.read') ? [button('Đánh giá tài sản', { onClick: evaluate })] : [] }),
    h('nav', { className: 'actions', 'aria-label': 'Các phần thay thế' }, can(services, 'replacement-recommendations.read') ? h('a', { href: '#/replacement' }, 'Đề xuất') : null, can(services, 'replacement-rules.read') ? h('a', { href: '#/replacement/rules' }, 'Quy tắc') : null), feedback,
    h('form', { className: 'filters', onSubmit: event => { event.preventDefault(); if (alive()) setQuery(path, { ...query, page: 1, status: status.value, ...(rules ? { keyword: keyword.value.trim() } : {}) }); } }, rules ? keyword : null, status, button('Lọc', { type: 'submit' }), button('Đặt lại', { kind: 'secondary', onClick: () => { if (alive()) route(path); } })),
    h('section', { className: 'card' }, dataTable({ rows: result.items, columns: rules ? [
      { key: 'code', label: 'Mã' }, { key: 'name', label: 'Quy tắc' }, { key: 'version', label: 'Phiên bản' },
      { label: 'Ưu tiên', render: row => labels[row.severity] || 'Chưa cấu hình' }, { label: 'Trạng thái', render: row => row.isActive ? 'Đang bật' : 'Đã ngừng' },
      { label: 'Thao tác', render: row => h('div', { className: 'actions' }, button('Chi tiết', { kind: 'ghost', onClick: () => ruleDetail(row) }),
        admin() && can(services, 'replacement-rules.update') && !row.effectiveTo ? button('Tạo phiên bản mới', { kind: 'ghost', onClick: () => editRule(row) }) : null,
        admin() && can(services, 'replacement-rules.update') && !row.effectiveTo && row.severity ? button('Đổi trạng thái', { kind: 'ghost', onClick: () => statusRule(row) }) : null) }
    ] : [
      { label: 'Tài sản', render: row => h('a', { href: '#/assets/' + row.assetId }, 'Tài sản #' + row.assetId) }, { label: 'Ưu tiên', render: row => labels[row.priority] || row.priority },
      { label: 'Trạng thái', render: row => labels[row.disposition] || row.disposition }, { key: 'plannedReplacementYear', label: 'Năm kế hoạch' },
      ...(financial() ? [{ label: 'Ước tính', render: row => money(row.estimatedReplacementCost) }] : []),
      { label: 'Ngày đánh giá', render: row => date(row.recommendedAt) },
      { label: 'Thao tác', render: row => h('div', { className: 'actions' }, button('Căn cứ', { kind: 'ghost', onClick: () => detail(row) }), row.isCurrent && ['ACTIVE', 'PLANNED'].includes(row.disposition) && can(services, 'replacement-recommendations.update') ? button(row.disposition === 'PLANNED' ? 'Điều chỉnh kế hoạch' : 'Lập kế hoạch / bỏ qua', { kind: 'ghost', onClick: () => disposition(row) }) : null) }
    ] }), pagination(result, page => { if (alive()) setQuery(path, { ...query, page }); })));
  return root;
}
