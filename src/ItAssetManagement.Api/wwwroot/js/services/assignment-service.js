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

function listQuery(query, history = false) {
  object(query, ['page', 'pageSize', ...(history ? [] : ['assetId', 'userId', 'departmentId', 'active']), 'status', 'from', 'to', 'sortBy', 'sortDirection']);
  const page = query.page ?? 1, size = query.pageSize ?? 20;
  if (!positiveId(page) || !positiveId(size) || size > 100 || (page - 1) * size > 2147483647) fail('page', 'Trang hoặc kích thước trang không hợp lệ.');
  for (const field of ['assetId', 'userId', 'departmentId']) if (present(query[field]) && !positiveId(query[field])) fail(field, 'ID phải là số nguyên dương.');
  if (present(query.active) && typeof query.active !== 'boolean') fail('active', 'active phải là boolean.');
  if (present(query.status) && !['Active', 'Returned'].includes(query.status) || query.active === true && query.status === 'Returned') fail('status', 'Trạng thái không hợp lệ.');
  utc(query.from, 'from'); utc(query.to, 'to');
  if (query.from && query.to && Date.parse(query.from) > Date.parse(query.to)) fail('to', 'Khoảng thời gian không hợp lệ.');
  if (present(query.sortBy) && !['assignedAt', 'returnedAt', 'id'].includes(query.sortBy)) fail('sortBy', 'Cột sắp xếp không hợp lệ.');
  if (present(query.sortDirection) && !['asc', 'desc'].includes(query.sortDirection)) fail('sortDirection', 'Chiều sắp xếp không hợp lệ.');
  const search = new URLSearchParams();
  for (const [field, value] of Object.entries(query)) if (present(value)) search.set(field, String(value));
  return search.size ? '?' + search.toString() : '';
}
function expectedReturn(dto, start) {
  utc(dto.expectedReturnAt, 'expectedReturnAt');
  if (dto.expectedReturnAt && Date.parse(dto.expectedReturnAt) < (dto[start] ? Date.parse(dto[start]) : Date.now())) fail('expectedReturnAt', 'Hạn trả phải sau thời điểm bàn giao.');
}
function condition(value, field) {
  if (present(value) && (typeof value !== 'string' || value.trim().length > 200)) fail(field, 'Tình trạng tối đa 200 ký tự.');
}
function version(dto) {
  if (typeof dto.rowVersion !== 'string' || !/^[A-Za-z0-9+/]{21}[AQgw]==$/.test(dto.rowVersion)) fail('rowVersion', 'Tải lại cấp phát trước khi lưu.');
}

/** Transport/session handling stays in api-services. No mutation is automatically retried. */
export function createAssignmentService({ request, idPath }) {
  return {
    async list(query = {}) {
      return request(`/asset-assignments${listQuery(query)}`);
    },
    async get(id) {
      if (!positiveId(id)) fail('id', 'ID assignment phải là số nguyên dương.');
      return request(`/asset-assignments/${idPath(id)}`);
    },
    async assign(dto) {
      object(dto, ['assetId', 'userId', 'departmentId', 'assignedAt', 'expectedReturnAt', 'note']);
      if (!positiveId(dto.assetId)) fail('assetId', 'Chọn tài sản hợp lệ.');
      if (present(dto.userId) === present(dto.departmentId)) fail('userId', 'Chọn đúng một người nhận hoặc phòng ban.');
      for (const field of ['userId', 'departmentId']) if (present(dto[field]) && !positiveId(dto[field])) fail(field, 'ID phải là số nguyên dương.');
      utc(dto.assignedAt, 'assignedAt');
      expectedReturn(dto, 'assignedAt');
      return request('/asset-assignments', { method: 'POST', body: { ...dto, note: note(dto.note) } });
    },
    async returnAsset(id, dto) {
      if (!positiveId(id)) fail('id', 'ID assignment phải là số nguyên dương.');
      object(dto, ['rowVersion', 'returnedAt', 'note', 'condition']);
      // Canonical Base64 for exactly 16 bytes, including the zero padding bits.
      if (typeof dto.rowVersion !== 'string' || !/^[A-Za-z0-9+/]{21}[AQgw]==$/.test(dto.rowVersion))
        fail('rowVersion', 'Tải lại assignment trước khi thu hồi.');
      utc(dto.returnedAt, 'returnedAt');
      condition(dto.condition, 'condition');
      return request(`/asset-assignments/${idPath(id)}/return`, {
        method: 'POST', body: { ...dto, note: note(dto.note, true) },
      });
    },
    async transfer(id, dto) {
      if (!positiveId(id)) fail('id', 'Cấp phát không hợp lệ.');
      object(dto, ['userId', 'departmentId', 'transferredAt', 'expectedReturnAt', 'returnCondition', 'note', 'rowVersion']);
      if (present(dto.userId) === present(dto.departmentId)) fail('target', 'Chọn đúng một người hoặc phòng ban nhận.');
      for (const field of ['userId', 'departmentId']) if (present(dto[field]) && !positiveId(dto[field])) fail(field, 'Người/phòng ban nhận không hợp lệ.');
      version(dto); utc(dto.transferredAt, 'transferredAt'); expectedReturn(dto, 'transferredAt'); condition(dto.returnCondition, 'returnCondition');
      return request(`/asset-assignments/${idPath(id)}/transfer`, { method: 'POST', body: { ...dto, note: note(dto.note, true) } });
    },
    async history(assetId, query = {}) {
      if (!positiveId(assetId)) fail('assetId', 'Tài sản không hợp lệ.');
      return request(`/assets/${idPath(assetId)}/assignments${listQuery(query, true)}`);
    },
  };
}
