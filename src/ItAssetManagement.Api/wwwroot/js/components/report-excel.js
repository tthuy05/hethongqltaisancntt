import { h } from '../utils/dom.js';
import { excelAvailable, excelDownloadButton } from './asset-excel.js';
import { reportUtc } from '../pages/reports-query.js';

function exportPanel(controls, note) {
  if (!controls.length) return null;
  return h('section', { className: 'card report-export', 'aria-label': 'Xuất báo cáo Excel' },
    h('div', { className: 'toolbar report-export-actions' }, ...controls),
    h('p', { className: 'meta' }, note));
}

// Only existing export APIs. Never pretend an unsupported report filter was applied.
export function reportExcelActions(services, tab, query, active) {
  if (!excelAvailable(services)) return null;
  const id = key => query[key] ? Number(query[key]) : undefined;
  const shared = { departmentId: id('departmentId') };
  const multiple = specs => {
    const controls = specs.map(spec => excelDownloadButton(services, { ...spec, active })).filter(Boolean);
    return exportPanel(controls, 'Xuất toàn bộ dữ liệu khớp bộ lọc, tối đa 5000 dòng; mỗi nút tải một tệp.');
  };
  if (tab === 'groups') return multiple(['department', 'type', 'status'].map((groupBy, i) => ({
    label: ['Xuất theo phòng ban', 'Xuất theo loại', 'Xuất theo trạng thái'][i], method: 'exportAssetGroups', permission: 'export.assets.execute',
    query: { ...shared, assetTypeId: id('assetTypeId'), status: query.status, groupBy }
  })));
  if (tab === 'cost') return multiple([
    { label: 'Xuất chi phí bảo trì', method: 'exportMaintenanceCost', permission: 'export.maintenance.execute', query: {
      ...shared, assetTypeId: id('assetTypeId'), groupBy: query.groupBy || 'month', fromUtc: reportUtc(query.costFrom), toUtc: reportUtc(query.costTo) } },
    { label: 'Xuất dự toán thay thế', method: 'exportReplacementBudget', permission: 'export.replacement.execute', query: {
      ...shared, year: id('year') || Math.min(2100, Math.max(2000, new Date().getUTCFullYear() + 1)), priority: query.priority } }
  ]);
  if (tab === 'expiry') return multiple([
    ...(services.auth?.session?.permissions?.includes('reports.assets.read') ? [{ label: 'Xuất hạn bảo hành', method: 'exportWarranty', permission: 'export.assets.execute', query: {
      ...shared, assetTypeId: id('assetTypeId'), fromUtc: query.warrantyFrom ? reportUtc(query.warrantyFrom + 'T00:00') : undefined,
      toUtc: query.warrantyTo ? reportUtc(query.warrantyTo + 'T00:00') : undefined } }] : []),
    ...(services.auth?.session?.permissions?.includes('reports.licenses.read') ? [{ label: 'Xuất thời hạn License', method: 'exportLicenseReport', permission: 'export.assets.execute', query: {
      softwareId: id('softwareId'), status: query.licenseStatus, fromUtc: reportUtc(query.licenseFrom), toUtc: reportUtc(query.licenseTo) } }] : [])
  ]);
  let spec, note = 'Xuất tất cả dòng khớp bộ lọc, không chỉ trang hiện tại; tối đa 5000 dòng.';
  if (tab === 'inventory') {
    if (query.sortBy === 'purchaseCost' && !services.auth?.session?.permissions?.some(p => ['assets.cost.read', 'reports.cost.read'].includes(p)))
      return h('p', { className: 'meta' }, 'Không có quyền xuất theo thứ tự giá.');
    const statuses = { IN_STOCK: 'InStock', IN_USE: 'InUse', MAINTENANCE: 'Maintenance', BROKEN: 'Broken', RETIRED: 'Retired' };
    spec = { method: 'exportAssets', permission: 'export.assets.execute', query: { ...shared, keyword: query.keyword, status: statuses[query.status],
      assetTypeId: id('assetTypeId'), purchasedFrom: query.purchasedFrom, purchasedTo: query.purchasedTo, includeArchived: query.includeArchived === 'true', sortBy: query.sortBy || 'createdAt', sortDirection: query.sortDirection || 'desc' } };
  } else if (tab === 'assignments') spec = { method: 'exportAssignments', permission: 'export.assignments.execute', query: {
    ...shared, assetId: id('assetId'), assetCode: query.assetCode, userId: id('userId'), fromUtc: reportUtc(query.assignmentFrom), toUtc: reportUtc(query.assignmentTo) } };
  else if (tab === 'maintenance') spec = { method: 'exportMaintenance', permission: 'export.maintenance.execute', query: {
    assetId: id('assetId'), assetCode: query.assetCode, technicianId: id('technicianId'), status: query.maintenanceStatus, fromUtc: reportUtc(query.maintenanceFrom), toUtc: reportUtc(query.maintenanceTo) } };
  else if (tab === 'replacement') {
    spec = { label: 'Xuất lịch sử đề xuất', method: 'exportReplacementHistory', permission: 'export.replacement.execute', query: {
      ...shared, plannedYear: id('plannedYear'), priority: query.priority, disposition: query.disposition,
      generatedFromUtc: reportUtc(query.generatedFrom), generatedToUtc: reportUtc(query.generatedTo) } };
    note += ' Bao gồm lịch sử như bảng báo cáo; dự toán tổng hợp được xuất riêng ở tab Chi phí & Ngân sách.';
  }
  if (!spec) return null;
  const control = excelDownloadButton(services, { ...spec, active });
  return exportPanel(control ? [control] : [], note);
}
