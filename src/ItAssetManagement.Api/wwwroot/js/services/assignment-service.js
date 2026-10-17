import { ServiceError } from './service-error.js';

const fail = (field, message) => { throw new ServiceError(400, 'VALIDATION_ERROR', 'Vui lòng kiểm tra thông tin cấp phát.', { [field]: [message] }); };
const positiveId = value => Number.isSafeInteger(value) && value > 0;
const present = value => value !== undefined && value !== null;
function object(value, allowed) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) fail('body', 'Dữ liệu phải là object.');
  for (const field of Object.keys(value)) if (!allowed.includes(field)) fail(field, 'Trường chưa được API hiện tại hỗ trợ.');
}
function note(value, required = false) {
  if (value == null && !required) return value;
  if (typeof value !== 'string' || value.trim().length > 1000 || required && !value.trim()) fail('note', 'Ghi chú tối đa 1000 ký tự; thu hồi cần lý do.');
  return value.trim() || null;
}
function utc(value, field) {
  if (!present(value)) return;
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?Z$/.test(value) ||
      !Number.isFinite(Date.parse(value)) || new Date(value).toISOString().slice(0, 10) !== value.slice(0, 10))
    fail(field, 'Thời điểm phải là ISO UTC kết thúc bằng Z.');
}

/** Current four-route contract only; transport/session handling stays in api-services. */
export function createAssignmentService({ request, idPath }) {
  return {
    async list(query = {}) {
      object(query, ['page', 'pageSize', 'assetId', 'userId', 'departmentId', 'active']);
      const page = query.page ?? 1, size = query.pageSize ?? 20;
      if (!positiveId(page) || !positiveId(size) || size > 100 || (page - 1) * size > 2147483647)
        fail('page', 'Trang >= 1, pageSize 1..100 và offset hợp lệ.');
      for (const field of ['assetId', 'userId', 'departmentId']) if (present(query[field]) && !positiveId(query[field])) fail(field, 'ID phải là số nguyên dương.');
      if (present(query.active) && typeof query.active !== 'boolean') fail('active', 'active phải là boolean.');
      const search = new URLSearchParams();
      for (const [field, value] of Object.entries(query)) if (present(value)) search.set(field, String(value));
      return request(`/asset-assignments${search.size ? '?' + search.toString() : ''}`);
    },
    async get(id) {
      if (!positiveId(id)) fail('id', 'ID assignment phải là số nguyên dương.');
      return request(`/asset-assignments/${idPath(id)}`);
    },
    async assign(dto) {
      object(dto, ['assetId', 'userId', 'departmentId', 'assignedAt', 'note']);
      if (!positiveId(dto.assetId)) fail('assetId', 'Chọn tài sản hợp lệ.');
      if (present(dto.userId) === present(dto.departmentId)) fail('userId', 'Chọn đúng một người nhận hoặc phòng ban.');
      for (const field of ['userId', 'departmentId']) if (present(dto[field]) && !positiveId(dto[field])) fail(field, 'ID phải là số nguyên dương.');
      utc(dto.assignedAt, 'assignedAt');
      return request('/asset-assignments', { method: 'POST', body: { ...dto, note: note(dto.note) } });
    },
    async returnAsset(id, dto) {
      if (!positiveId(id)) fail('id', 'ID assignment phải là số nguyên dương.');
      object(dto, ['rowVersion', 'returnedAt', 'note']);
      // Canonical Base64 for exactly 16 bytes, including the zero padding bits.
      if (typeof dto.rowVersion !== 'string' || !/^[A-Za-z0-9+/]{21}[AQgw]==$/.test(dto.rowVersion))
        fail('rowVersion', 'Tải lại assignment trước khi thu hồi.');
      utc(dto.returnedAt, 'returnedAt');
      return request(`/asset-assignments/${idPath(id)}/return`, {
        method: 'POST', body: { ...dto, note: note(dto.note, true) },
      });
    },
  };
}
