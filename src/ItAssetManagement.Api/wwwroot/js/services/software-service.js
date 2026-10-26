import { ServiceError } from './service-error.js';

export function validateSoftware(dto, update = false) {
  const errors = {};
  if (!dto || typeof dto !== 'object' || Array.isArray(dto)) return { name: ['Thông tin phần mềm không hợp lệ.'] };
  for (const key of Object.keys(dto)) if (!['name', 'publisher', 'version', 'description', ...(update ? ['rowVersion'] : [])].includes(key)) errors[key] = ['Trường không được phép gửi.'];
  for (const [key, limit] of [['name', 250], ['publisher', 250], ['version', 100], ['description', 2000]]) {
    const value = dto[key];
    if (key === 'name' && (typeof value !== 'string' || !value.trim())) errors[key] = ['Vui lòng nhập tên phần mềm.'];
    else if (value != null && (typeof value !== 'string' || value.trim().length > limit)) errors[key] = ['Tối đa ' + limit + ' ký tự.'];
  }
  if (update && !validVersion(dto.rowVersion)) errors.rowVersion = ['Tải lại dữ liệu để lấy phiên bản hợp lệ.'];
  return errors;
}
function validVersion(value) {
  try { return typeof value === 'string' && atob(value).length === 16 && btoa(atob(value)) === value; } catch { return false; }
}
export function createSoftwareService({ request, idPath }) {
  const path = id => '/software/' + idPath(id);
  const write = async (id, dto) => {
    const errors = validateSoftware(dto, id != null);
    if (Object.keys(errors).length) throw new ServiceError(400, 'VALIDATION_ERROR', 'Vui lòng kiểm tra thông tin phần mềm.', errors);
    return request(id == null ? '/software' : path(id), { method: id == null ? 'POST' : 'PUT', body: dto });
  };
  return {
    async list(query = {}) {
      const params = new URLSearchParams();
      for (const [key, value] of Object.entries(query)) {
        if (!['page', 'pageSize', 'keyword', 'status', 'vendor', 'sortBy', 'sortDirection'].includes(key)) throw new ServiceError(400, 'VALIDATION_ERROR', 'Bộ lọc không hợp lệ.');
        if (value != null && value !== '') params.set(key, String(value));
      }
      return request('/software' + (params.size ? '?' + params : ''));
    },
    get: id => request(path(id)), create: dto => write(null, dto), update: (id, dto) => write(id, dto),
    async archive(id, rowVersion) {
      if (!validVersion(rowVersion)) throw new ServiceError(400, 'VALIDATION_ERROR', 'Tải lại phần mềm trước khi ngừng sử dụng.');
      return request(path(id), { method: 'DELETE', headers: { 'If-Match': '"' + rowVersion + '"' } });
    }
  };
}
