import { ServiceError } from './service-error.js';
import { licenseView, licensePageView } from '../models/license-view.js';

const fail = (field, message) => { throw new ServiceError(400, 'VALIDATION_ERROR', 'Vui lòng kiểm tra bộ lọc license.', { [field]: [message] }); };
const positive = value => Number.isSafeInteger(value) && value > 0;

// Planned EP-051/053 read adapter. No startup request, mock fallback, mutation or
// reveal; owner DTO must be reviewed before enabling the frontend route.
export function createLicenseReadService({ request, idPath }) {
  return {
    async list(query = {}) {
      if (!query || typeof query !== 'object' || Array.isArray(query)) fail('query', 'Bộ lọc phải là object.');
      const fields = ['page', 'pageSize', 'softwareId', 'status', 'vendor', 'keyword'];
      for (const key of Object.keys(query)) if (!fields.includes(key)) fail(key, 'Bộ lọc chưa được adapter chuẩn bị hỗ trợ.');
      const page = query.page ?? 1, size = query.pageSize ?? 20;
      if (!positive(page) || !positive(size) || size > 100 || (page - 1) * size > 2147483647) fail('page', 'Trang >=1 và pageSize 1..100.');
      if (query.softwareId != null && !positive(query.softwareId)) fail('softwareId', 'ID phải là số nguyên dương.');
      for (const key of ['status', 'vendor', 'keyword']) if (query[key] != null &&
        (typeof query[key] !== 'string' || !query[key].trim() || query[key].length > (key === 'vendor' ? 250 : 200))) fail(key, 'Bộ lọc phải là chuỗi không rỗng trong giới hạn.');
      const search = new URLSearchParams({ page: String(page), pageSize: String(size) });
      for (const key of ['softwareId', 'status', 'vendor', 'keyword']) if (query[key] != null) search.set(key, String(query[key]));
      const result = licensePageView(await request(`/software-licenses?${search}`));
      if (result.page !== page || result.pageSize !== size) throw new ServiceError(502, 'INVALID_API_RESPONSE', 'Phân trang license không khớp request.');
      return result;
    },
    async get(id) {
      if (!positive(id)) fail('id', 'ID phải là số nguyên dương.');
      const result = licenseView(await request(`/software-licenses/${idPath(id)}`));
      if (result.id !== id) throw new ServiceError(502, 'INVALID_API_RESPONSE', 'License response không khớp ID.');
      return result;
    }
  };
}
