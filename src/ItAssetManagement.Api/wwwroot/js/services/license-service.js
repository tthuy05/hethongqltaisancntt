import { ServiceError } from './service-error.js';
import { licenseView, licensePageView } from '../models/license-view.js';

const fail = (field, message) => { throw new ServiceError(400, 'VALIDATION_ERROR', 'Vui lòng kiểm tra bộ lọc license.', { [field]: [message] }); };
const positive = value => Number.isSafeInteger(value) && value > 0;
const licenseTypes = ['PER_USER', 'PER_DEVICE', 'VOLUME', 'SUBSCRIPTION', 'OTHER'];

export function validVersion(value) {
  try { return typeof value === 'string' && atob(value).length === 16 && btoa(atob(value)) === value; } catch { return false; }
}

export function validateLicense(dto, update = false) {
  const errors = {};
  if (!dto || typeof dto !== 'object' || Array.isArray(dto)) return { softwareId: ['Thông tin license không hợp lệ.'] };
  const allowed = [
    'softwareId', 'licenseType', 'totalQuantity', 'vendor', 'notes',
    'purchasedAt', 'startsAt', 'expiresAt', 'purchaseCost', 'licenseKey',
    ...(update ? ['rowVersion'] : [])
  ];
  for (const key of Object.keys(dto)) {
    if (!allowed.includes(key)) errors[key] = ['Trường không được phép gửi.'];
  }
  if (!positive(Number(dto.softwareId))) errors.softwareId = ['Vui lòng chọn phần mềm hợp lệ.'];
  if (!dto.licenseType || !licenseTypes.includes(dto.licenseType)) errors.licenseType = ['Loại bản quyền không hợp lệ.'];
  const qty = Number(dto.totalQuantity);
  if (!Number.isSafeInteger(qty) || qty <= 0) errors.totalQuantity = ['Số lượng bản quyền phải là số nguyên lớn hơn 0.'];
  if (dto.vendor != null && typeof dto.vendor === 'string' && dto.vendor.trim().length > 250) errors.vendor = ['Nhà cung cấp tối đa 250 ký tự.'];
  if (dto.notes != null && typeof dto.notes === 'string' && dto.notes.trim().length > 2000) errors.notes = ['Ghi chú tối đa 2000 ký tự.'];
  for (const dateKey of ['purchasedAt', 'startsAt', 'expiresAt']) {
    if (dto[dateKey] != null && dto[dateKey] !== '' && !Number.isFinite(Date.parse(dto[dateKey]))) {
      errors[dateKey] = ['Thời điểm không hợp lệ.'];
    }
  }
  if (dto.startsAt && dto.expiresAt && !errors.startsAt && !errors.expiresAt && new Date(dto.expiresAt) <= new Date(dto.startsAt)) {
    errors.expiresAt = ['Thời điểm hết hạn phải sau thời điểm hiệu lực.'];
  }
  if (dto.purchaseCost != null && dto.purchaseCost !== '') {
    const cost = Number(dto.purchaseCost);
    if (!Number.isFinite(cost) || cost < 0 || cost > 9999999999999999.99 || Math.round(cost * 100) / 100 !== cost) {
      errors.purchaseCost = ['Chi phí không âm, tối đa 2 chữ số thập phân.'];
    }
  }
  if (dto.licenseKey != null && dto.licenseKey !== '') {
    const key = String(dto.licenseKey);
    if (key.length < 5 || /[\u0000-\u001F\u007F-\u009F]/.test(key)) {
      errors.licenseKey = ['Key phải có ít nhất 5 ký tự và không chứa ký tự điều khiển.'];
    }
  }
  if (update && !validVersion(dto.rowVersion)) errors.rowVersion = ['Tải lại dữ liệu để lấy phiên bản hợp lệ.'];
  return errors;
}

export function createLicenseReadService({ request, idPath }) {
  const path = id => `/software-licenses/${idPath(id)}`;
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
      const result = licenseView(await request(path(id)));
      if (result.id !== id) throw new ServiceError(502, 'INVALID_API_RESPONSE', 'License response không khớp ID.');
      return result;
    },
    async create(dto) {
      const errors = validateLicense(dto, false);
      if (Object.keys(errors).length) throw new ServiceError(400, 'VALIDATION_ERROR', 'Vui lòng kiểm tra thông tin bản quyền.', errors);
      const res = await request('/software-licenses', { method: 'POST', body: dto });
      return licenseView(res);
    },
    async update(id, dto) {
      if (!positive(id)) fail('id', 'ID phải là số nguyên dương.');
      const errors = validateLicense(dto, true);
      if (Object.keys(errors).length) throw new ServiceError(400, 'VALIDATION_ERROR', 'Vui lòng kiểm tra thông tin bản quyền.', errors);
      const res = await request(path(id), { method: 'PUT', body: dto, headers: { 'If-Match': `"${dto.rowVersion}"` } });
      return licenseView(res);
    },
    async archive(id, rowVersion) {
      if (!positive(id)) fail('id', 'ID phải là số nguyên dương.');
      if (!validVersion(rowVersion)) throw new ServiceError(400, 'VALIDATION_ERROR', 'Tải lại bản quyền trước khi ngừng sử dụng.');
      return request(path(id), { method: 'DELETE', headers: { 'If-Match': `"${rowVersion}"` } });
    },
    async softwareOptions(query = {}) {
      const search = new URLSearchParams();
      if (query.page) search.set('page', String(query.page));
      if (query.keyword) search.set('keyword', String(query.keyword).trim());
      return request(`/software-licenses/software-options${search.size ? '?' + search : ''}`);
    },
    async revealKey(id, reason) {
      if (!positive(id)) fail('id', 'ID phải là số nguyên dương.');
      if (!reason || typeof reason !== 'string' || !reason.trim() || reason.trim().length > 1000) {
        throw new ServiceError(400, 'VALIDATION_ERROR', 'Vui lòng cung cấp lý do xem khóa (1-1000 ký tự).', { reason: ['Lý do bắt buộc.'] });
      }
      return request(path(id) + '/reveal-key', { method: 'POST', body: { reason: reason.trim() } });
    }
  };
}

export { createLicenseReadService as createLicenseService };
