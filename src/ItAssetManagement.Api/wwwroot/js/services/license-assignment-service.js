import { ServiceError } from './service-error.js';

const fail = (field, message) => { throw new ServiceError(400, 'VALIDATION_ERROR', 'Vui lòng kiểm tra dữ liệu cấp phát bản quyền.', { [field]: [message] }); };
const positive = value => Number.isSafeInteger(value) && value > 0;

export function validVersion(value) {
  try { return typeof value === 'string' && atob(value).length === 16 && btoa(atob(value)) === value; } catch { return false; }
}

export function validateAssign(dto) {
  const errors = {};
  if (!dto || typeof dto !== 'object' || Array.isArray(dto)) return { body: ['Dữ liệu không hợp lệ.'] };
  if (!positive(dto.softwareLicenseId)) errors.softwareLicenseId = ['Vui lòng chọn bản quyền hợp lệ.'];
  const hasUser = positive(dto.userId);
  const hasAsset = positive(dto.assetId);
  for (const key of Object.keys(dto)) if (!['softwareLicenseId', 'userId', 'assetId', 'notes', 'assignedAt'].includes(key)) errors[key] = ['Trường không được phép gửi.'];
  if (dto.userId != null && !positive(dto.userId) || dto.assetId != null && !positive(dto.assetId)) errors.target = ['ID đối tượng phải là số nguyên dương.'];
  if (hasUser === hasAsset) errors.target = ['Phải chọn đúng 1 đối tượng nhận (Người dùng hoặc Tài sản).'];
  if (dto.notes != null && (typeof dto.notes !== 'string' || dto.notes.trim().length > 1000)) errors.notes = ['Ghi chú tối đa 1000 ký tự.'];
  if (dto.assignedAt != null && (typeof dto.assignedAt !== 'string' || !/^\d{4}-\d{2}-\d{2}T.*Z$/.test(dto.assignedAt) || !Number.isFinite(Date.parse(dto.assignedAt)))) errors.assignedAt = ['Ngày cấp phải là thời điểm UTC.'];
  return errors;
}

export function createLicenseAssignmentService({ request, idPath = String }) {
  const path = id => `/license-assignments/${idPath(id)}`;
  return {
    async list(query = {}) {
      if (!query || typeof query !== 'object' || Array.isArray(query)) fail('query', 'Bộ lọc phải là object.');
      const fields = ['page', 'pageSize', 'licenseId', 'userId', 'assetId', 'status', 'targetType', 'from', 'to', 'sortBy', 'sortDirection'];
      for (const key of Object.keys(query)) if (!fields.includes(key)) fail(key, 'Bộ lọc không hỗ trợ.');
      const page = query.page ?? 1, size = query.pageSize ?? 20;
      if (!positive(page) || !positive(size) || size > 100 || (page - 1) * size > 2147483647) fail('page', 'Trang >= 1 và pageSize từ 1 đến 100.');
      const search = new URLSearchParams({ page: String(page), pageSize: String(size) });
      for (const key of ['licenseId', 'userId', 'assetId', 'status', 'targetType', 'from', 'to', 'sortBy', 'sortDirection']) {
        if (query[key] != null && query[key] !== '') search.set(key, String(query[key]));
      }
      return request(`/license-assignments?${search}`);
    },
    async get(id) {
      if (!positive(id)) fail('id', 'ID phải là số nguyên dương.');
      return request(path(id));
    },
    async assign(dto) {
      const errors = validateAssign(dto);
      if (Object.keys(errors).length) throw new ServiceError(400, 'VALIDATION_ERROR', 'Vui lòng kiểm tra dữ liệu cấp phát.', errors);
      return request('/license-assignments', { method: 'POST', body: dto });
    },
    async revoke(id, { reason, rowVersion } = {}) {
      if (!positive(id)) fail('id', 'ID phải là số nguyên dương.');
      if (!validVersion(rowVersion)) fail('rowVersion', 'Tải lại dữ liệu trước khi thu hồi.');
      if (reason != null && (typeof reason !== 'string' || reason.trim().length > 1000)) fail('reason', 'Lý do tối đa 1000 ký tự.');
      const body = { reason: reason?.trim() || null, rowVersion };
      return request(path(id) + '/revoke', {
        method: 'POST',
        body,
        headers: { 'If-Match': `"${rowVersion}"` }
      });
    },
    async transfer(id, { userId, assetId, reason, rowVersion } = {}) {
      if (!positive(id)) fail('id', 'ID phải là số nguyên dương.');
      if (!validVersion(rowVersion)) fail('rowVersion', 'Tải lại dữ liệu trước khi điều chuyển.');
      const hasUser = positive(userId);
      const hasAsset = positive(assetId);
      if (hasUser === hasAsset) fail('target', 'Phải chọn đúng 1 đối tượng nhận mới (Người dùng hoặc Tài sản).');
      if (userId != null && !positive(userId) || assetId != null && !positive(assetId)) fail('target', 'ID đối tượng không hợp lệ.');
      if (reason != null && (typeof reason !== 'string' || reason.trim().length > 1000)) fail('reason', 'Lý do tối đa 1000 ký tự.');
      const body = {
        userId: hasUser ? Number(userId) : null,
        assetId: hasAsset ? Number(assetId) : null,
        reason: reason?.trim() || null,
        rowVersion
      };
      return request(path(id) + '/transfer', {
        method: 'POST',
        body,
        headers: { 'If-Match': `"${rowVersion}"` }
      });
    },
    async listByLicense(licenseId, query = {}) {
      if (!positive(licenseId)) fail('licenseId', 'ID license phải là số nguyên dương.');
      if (!query || typeof query !== 'object' || Array.isArray(query)) fail('query', 'Bộ lọc không hợp lệ.');
      for (const key of Object.keys(query)) if (!['page', 'pageSize', 'status', 'targetType', 'from', 'to', 'sortBy', 'sortDirection'].includes(key)) fail(key, 'Bộ lọc không hỗ trợ.');
      const search = new URLSearchParams();
      const page = query.page ?? 1, size = query.pageSize ?? 20;
      if (!positive(page) || !positive(size) || size > 100 || (page - 1) * size > 2147483647) fail('page', 'Phân trang không hợp lệ.');
      search.set('page', String(page));
      search.set('pageSize', String(size));
      for (const key of ['status', 'targetType', 'from', 'to', 'sortBy', 'sortDirection']) {
        if (query[key] != null && query[key] !== '') search.set(key, String(query[key]));
      }
      return request(`/software-licenses/${idPath(licenseId)}/assignments?${search}`);
    }
  };
}
