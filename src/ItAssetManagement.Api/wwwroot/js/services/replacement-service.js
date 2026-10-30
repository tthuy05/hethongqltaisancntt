import { ServiceError } from './service-error.js';

export const severities = ['LOW', 'MEDIUM', 'HIGH', 'CRITICAL'];
function invalid(message) { throw new ServiceError(400, 'VALIDATION_ERROR', message); }
function version(value) {
  try { if (typeof value === 'string' && atob(value).length === 16 && btoa(atob(value)) === value) return; } catch { /* reject */ }
  invalid('Tải lại dữ liệu để lấy phiên bản hợp lệ.');
}
export function validateRule(dto, update = false) {
  const errors = {};
  if (!dto || typeof dto !== 'object' || Array.isArray(dto)) return { name: ['Dữ liệu không hợp lệ.'] };
  const allowed = ['name', 'assetTypeId', 'severity', 'minimumAgeMonths', 'maximumMaintenanceCostRatio', 'minimumMaintenanceCount', 'requireWarrantyExpired', 'minimumFailureCount', 'estimatedUnitCost', 'priority', 'description', ...(update ? ['rowVersion'] : [])];
  for (const key of Object.keys(dto)) if (!allowed.includes(key)) errors[key] = ['Trường không được phép gửi.'];
  if (typeof dto.name !== 'string' || !dto.name.trim() || dto.name.trim().length > 250) errors.name = ['Nhập tên tối đa 250 ký tự.'];
  if (!severities.includes(dto.severity)) errors.severity = ['Chọn mức ưu tiên.'];
  for (const key of ['assetTypeId', 'minimumAgeMonths', 'minimumMaintenanceCount', 'minimumFailureCount'])
    if (dto[key] != null && (!Number.isSafeInteger(dto[key]) || dto[key] <= 0)) errors[key] = ['Nhập số nguyên dương.'];
  if (dto.maximumMaintenanceCostRatio != null && (!Number.isFinite(dto.maximumMaintenanceCostRatio) || dto.maximumMaintenanceCostRatio < 0 || dto.maximumMaintenanceCostRatio > 1)) errors.maximumMaintenanceCostRatio = ['Nhập tỷ lệ từ 0 đến 1.'];
  if (dto.estimatedUnitCost != null && (!Number.isFinite(dto.estimatedUnitCost) || dto.estimatedUnitCost < 0 || dto.estimatedUnitCost > Number.MAX_SAFE_INTEGER / 100)) errors.estimatedUnitCost = ['Giá không âm và trong phạm vi hiển thị an toàn.'];
  if (typeof dto.requireWarrantyExpired !== 'boolean' || dto.requireWarrantyExpired !== (dto.minimumFailureCount != null)) errors.minimumFailureCount = ['Hết bảo hành phải đi cùng số lần thất bại.'];
  if (dto.minimumAgeMonths == null && dto.minimumMaintenanceCount == null && dto.maximumMaintenanceCostRatio == null && !dto.requireWarrantyExpired) errors.minimumAgeMonths = ['Nhập ít nhất một điều kiện.'];
  if (!Number.isSafeInteger(dto.priority) || dto.priority < 0) errors.priority = ['Nhập số thứ tự không âm.'];
  if (dto.description != null && (typeof dto.description !== 'string' || dto.description.length > 2000)) errors.description = ['Tối đa 2000 ký tự.'];
  if (update) { try { version(dto.rowVersion); } catch { errors.rowVersion = ['Phiên bản không hợp lệ.']; } }
  return errors;
}
export function createReplacementService({ request, idPath }) {
  function list(path, query, allowed) {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(query)) {
      if (!allowed.includes(key)) invalid('Bộ lọc không hợp lệ.');
      if (value != null && value !== '') params.set(key, String(value));
    }
    return request(path + (params.size ? '?' + params : ''));
  }
  async function write(id, dto) {
    const errors = validateRule(dto, id != null);
    if (Object.keys(errors).length) throw new ServiceError(400, 'VALIDATION_ERROR', 'Kiểm tra cấu hình quy tắc.', errors);
    return request('/replacement-rules' + (id == null ? '' : '/' + idPath(id)), { method: id == null ? 'POST' : 'PUT', body: dto });
  }
  return {
    rules: query => list('/replacement-rules', query ?? {}, ['page', 'pageSize', 'keyword', 'status', 'assetTypeId', 'sortBy', 'sortDirection']),
    rule: id => request('/replacement-rules/' + idPath(id)), createRule: dto => write(null, dto), reviseRule: (id, dto) => write(id, dto),
    status(id, isActive, rowVersion) { version(rowVersion); if (typeof isActive !== 'boolean') invalid('Trạng thái không hợp lệ.'); return request('/replacement-rules/' + idPath(id) + '/status', { method: 'PATCH', body: { isActive, rowVersion } }); },
    list: query => list('/replacement-recommendations', query ?? {}, ['page', 'pageSize', 'assetId', 'departmentId', 'priority', 'status', 'isCurrent', 'generatedFrom', 'generatedTo', 'sortBy', 'sortDirection']),
    get: id => request('/replacement-recommendations/' + idPath(id)),
    evaluate(assetIds) {
      if (!Array.isArray(assetIds) || assetIds.length < 1 || assetIds.length > 100 || assetIds.some(id => !Number.isSafeInteger(id) || id <= 0) || new Set(assetIds).size !== assetIds.length) invalid('Chọn từ 1 đến 100 tài sản khác nhau.');
      return request('/replacement-recommendations/evaluate', { method: 'POST', body: { assetIds } });
    },
    disposition(id, dto) {
      version(dto.rowVersion);
      if (!['PLANNED', 'DISMISSED'].includes(dto.disposition) || typeof dto.note !== 'string' || !dto.note.trim() || dto.note.length > 2000) invalid('Chọn quyết định và nhập lý do tối đa 2000 ký tự.');
      if (dto.disposition === 'PLANNED' && (!Number.isInteger(dto.plannedReplacementYear) || dto.plannedReplacementYear < 2000 || dto.plannedReplacementYear > 2100)) invalid('Chọn năm từ 2000 đến 2100.');
      return request('/replacement-recommendations/' + idPath(id), { method: 'PATCH', body: dto });
    }
  };
}
