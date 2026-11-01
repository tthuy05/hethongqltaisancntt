import { h } from '../utils/dom.js';
import { excelAvailable, excelDownloadButton } from './asset-excel.js';
import { reportUtc } from '../pages/reports-query.js';

// Only existing export APIs. Never pretend an unsupported report filter was applied.
export function reportExcelActions(services, tab, query, active) {
  if (!excelAvailable(services)) return null;
  const id = key => query[key] ? Number(query[key]) : undefined;
  const shared = { departmentId: id('departmentId') };
  let spec, note = 'Xuất tất cả dòng khớp bộ lọc, không chỉ trang hiện tại; tối đa 5000 dòng.';
  if (tab === 'inventory') {
    if (query.sortBy === 'purchaseCost' && !services.auth?.session?.permissions?.some(p => ['assets.cost.read', 'reports.cost.read'].includes(p)))
      return h('p', { className: 'meta' }, 'Không có quyền xuất theo thứ tự giá.');
    const statuses = { IN_STOCK: 'InStock', IN_USE: 'InUse', MAINTENANCE: 'Maintenance', BROKEN: 'Broken', RETIRED: 'Retired' };
    spec = { method: 'exportAssets', permission: 'export.assets.execute', query: { ...shared, keyword: query.keyword, status: statuses[query.status],
      assetTypeId: id('assetTypeId'), purchasedFrom: query.purchasedFrom, purchasedTo: query.purchasedTo, includeArchived: query.includeArchived === 'true', sortBy: query.sortBy || 'createdAt', sortDirection: query.sortDirection || 'desc' } };
  } else if (tab === 'assignments') spec = { method: 'exportAssignments', permission: 'export.assignments.execute', query: {
    ...shared, assetId: id('assetId'), userId: id('userId'), fromUtc: reportUtc(query.assignmentFrom), toUtc: reportUtc(query.assignmentTo) } };
  else if (tab === 'maintenance') spec = { method: 'exportMaintenance', permission: 'export.maintenance.execute', query: {
    assetId: id('assetId'), technicianId: id('technicianId'), status: query.maintenanceStatus, fromUtc: reportUtc(query.maintenanceFrom), toUtc: reportUtc(query.maintenanceTo) } };
  else if (tab === 'expiry') {
    spec = { label: 'Xuất hạn bảo hành', method: 'exportWarranty', permission: 'export.assets.execute', query: {
      ...shared, assetTypeId: id('assetTypeId'), fromUtc: query.warrantyFrom ? reportUtc(query.warrantyFrom + 'T00:00') : undefined,
      toUtc: query.warrantyTo ? reportUtc(query.warrantyTo + 'T00:00') : undefined } };
    note += ' Chỉ hạn bảo hành; API xuất thời hạn License chưa có, không xuất bảng License.';
  } else if (tab === 'replacement') {
    spec = { label: 'Xuất đề xuất hiện hành', method: 'exportReplacement', permission: 'export.replacement.execute', query: {
      ...shared, year: id('plannedYear'), priority: query.priority, status: query.disposition,
      generatedFromUtc: reportUtc(query.generatedFrom), generatedToUtc: reportUtc(query.generatedTo) } };
    note += ' Chỉ đề xuất hiện hành, không bao gồm lịch sử; không phải báo cáo dự toán tổng hợp.';
  }
  if (!spec) return null;
  const control = excelDownloadButton(services, { ...spec, active });
  return control ? h('section', { className: 'card card-body', 'aria-label': 'Xuất báo cáo Excel' }, control, h('p', { className: 'meta' }, note)) : null;
}
