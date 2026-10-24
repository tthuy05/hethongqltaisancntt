import { h } from '../utils/dom.js';
import { button, dataTable, showToast } from '../components.js';
import { can, openDialog } from '../pages/shared.js';

export const excelAvailable = services => services.mode === 'api' && services.importExportEnabled === true && Boolean(services.importExport) &&
  Number.isSafeInteger(services.auth?.session?.id) && services.auth.session.id > 0;

export function saveWorkbook({ blob, filename }) {
  const url = URL.createObjectURL(blob);
  const link = h('a', { href: url, download: filename });
  try { document.body.append(link); link.click(); }
  finally { link.remove(); setTimeout(() => URL.revokeObjectURL(url), 0); }
}

// Recheck actor, permission, financial grants and page before releasing a download.
export function excelDownloadButton(services, { label = 'Xuất Excel', permission, method, query = {}, active = () => true, save = saveWorkbook }) {
  if (!excelAvailable(services) || !can(services, permission) || typeof services.importExport[method] !== 'function') return null;
  const actor = services.auth?.session?.id;
  const costs = ['assets.cost.read', 'reports.cost.read'].filter(p => can(services, p));
  const valid = () => excelAvailable(services) && active() && services.auth?.session?.id === actor && can(services, permission) && costs.every(p => can(services, p));
  let pending = false;
  const control = button(label, { kind: 'secondary', iconName: 'download', onClick: async () => {
    if (pending || control.disabled || !control.isConnected || !valid()) return;
    pending = true; control.disabled = true; control.setAttribute('aria-busy', 'true');
    try {
      const file = await services.importExport[method](query);
      if (control.isConnected && valid()) save(file);
    } catch (error) {
      if (control.isConnected && valid()) showToast(error.status === 404 ? 'API Excel chưa được mở trên máy chủ.' : error.message || 'Không tải được tệp Excel.', 'error');
    } finally { pending = false; control.disabled = !valid(); control.removeAttribute('aria-busy'); }
  } });
  return control;
}

export function assetImportDialog(services, onDone) {
  if (!excelAvailable(services) || !can(services, 'import.assets.execute')) return null;
  const actor = services.auth.session.id, hash = window.location.hash;
  let modal, closed = false, pending = false, blocked = false, reviewed = null;
  const valid = () => !closed && body.isConnected && window.location.hash === hash && excelAvailable(services) &&
    services.auth?.session?.id === actor && can(services, 'import.assets.execute');
  const feedback = h('div', { 'aria-live': 'polite' });
  const file = h('input', { type: 'file', accept: '.xlsx', 'aria-label': 'Chọn tệp XLSX', onChange: () => {
    reviewed = null; confirm.disabled = true; feedback.replaceChildren();
  } });
  const body = h('div', { className: 'dialog-body' },
    h('p', {}, 'Chỉ thêm mới, tối đa 5MB / 1000 dòng. Mã AST do hệ thống cấp; không nhập mã tài sản. ID loại tài sản và phòng ban phải đang hoạt động.'),
    h('p', {}, 'Ngày: yyyy-MM-dd; giá: dấu chấm thập phân. Toàn bộ tệp được nhập trong một giao dịch; một lỗi sẽ không nhập tài sản nào.'),
    !can(services, 'assets.cost.read') ? h('p', {}, 'Bạn không có quyền nhập chi phí; không điền cột PurchasePrice.') : null,
    excelDownloadButton(services, { label: 'Tải mẫu Excel', permission: 'import.assets.execute', method: 'template', active: () => valid() }),
    h('label', {}, 'Tệp cần nhập', file), feedback);
  function controls() { file.disabled = pending || blocked; preview.disabled = pending || blocked; confirm.disabled = pending || blocked || !reviewed; }
  function resultView(result, selected) {
    feedback.replaceChildren(h('p', {}, `${selected.name}: ${result.totalRows} dòng — ${result.validRows} hợp lệ, ${result.errorCount} lỗi.`));
    if (result.errors.length) feedback.append(dataTable({ label: 'Lỗi nhập Excel',
      columns: [{ key: 'rowNumber', label: 'Dòng Excel' }, { key: 'field', label: 'Trường' }, { key: 'errorCode', label: 'Mã lỗi' }, { key: 'errorMessage', label: 'Nội dung' }],
      rows: result.errors.slice(0, 100) }));
    if (result.errors.length > 100) feedback.append(h('p', {}, 'Chỉ hiển thị 100 lỗi đầu. Sửa tệp rồi kiểm tra lại.'));
  }
  const preview = button('Kiểm tra / Xem trước', { kind: 'secondary', onClick: async () => {
    if (pending || blocked || !valid()) return;
    reviewed = null; const selected = file.files?.[0];
    if (!selected) { feedback.textContent = 'Vui lòng chọn một tệp XLSX.'; controls(); return; }
    pending = true; controls();
    try {
      const result = await services.importExport.importAssets(selected, { dryRun: true });
      if (!valid()) return;
      resultView(result, selected);
      if (file.files?.[0] === selected && result.dryRun && !result.committed && result.totalRows > 0 && !result.errorCount && result.validRows === result.totalRows) {
        reviewed = selected;
        feedback.append(h('p', {}, 'Kiểm tra hợp lệ, chưa ghi dữ liệu hoặc cấp mã. Bấm Xác nhận nhập để nhập toàn bộ tệp; máy chủ sẽ kiểm tra lại.'));
      }
    } catch (error) { if (valid()) feedback.textContent = error.status === 404 ? 'API Excel chưa được mở trên máy chủ.' : error.message || 'Không kiểm tra được tệp.'; }
    finally { pending = false; controls(); }
  } });
  const confirm = button('Xác nhận nhập', { disabled: true, onClick: async () => {
    if (pending || blocked || !reviewed || !valid() || file.files?.[0] !== reviewed) return;
    const selected = reviewed; reviewed = null; pending = true; controls();
    try {
      const result = await services.importExport.importAssets(selected, { dryRun: false });
      if (!valid()) return;
      resultView(result, selected);
      if (result.committed && !result.dryRun) {
        modal.close(); showToast(`Đã nhập ${result.validRows} tài sản.`); onDone?.();
      } else feedback.append(h('p', {}, 'Không có tài sản nào được nhập. Sửa tệp và kiểm tra lại trước khi xác nhận.'));
    } catch (error) {
      // Lost/invalid response may follow COMMIT. Never offer a blind write retry.
      blocked = true;
      if (valid()) feedback.replaceChildren(h('p', { role: 'alert' }, 'Chưa xác nhận được kết quả nhập. Không gửi lại tệp ngay. Tải lại danh sách, đối chiếu serial và nhật ký trước khi thử lại.'),
        button('Đóng và tải lại danh sách', { kind: 'secondary', onClick: () => { modal.close(); onDone?.(); } }));
    } finally { pending = false; controls(); }
  } });
  modal = openDialog({ title: 'Nhập tài sản từ Excel', body, footer: [preview, confirm], onClose: () => { closed = true; } });
  return modal;
}

export function assetExcelActions(services, query, active) {
  if (!excelAvailable(services)) return [];
  const actions = [];
  if (can(services, 'import.assets.execute')) actions.push(button('Nhập Excel', { kind: 'secondary', iconName: 'upload', onClick: () => {
    if (active()) assetImportDialog(services, () => window.dispatchEvent(new Event('frontend-refresh')));
  } }));
  const supportedSort = ['assetCode', 'name', 'createdAt'].includes(query.sortBy);
  const exportButton = excelDownloadButton(services, { permission: 'export.assets.execute', method: 'exportAssets', active,
    query: Object.fromEntries(['keyword', 'status', 'assetTypeId', 'departmentId', 'sortBy', 'sortDirection'].map(k => [k, query[k]])) });
  if (exportButton) {
    if (!supportedSort) { exportButton.disabled = true; exportButton.setAttribute('title', 'Chọn sắp xếp theo mã, tên hoặc ngày tạo trước khi xuất. API chưa hỗ trợ kiểu sắp xếp này.'); }
    actions.push(exportButton);
  }
  return actions;
}
