import { ServiceError } from './service-error.js';

const invalid = (field, message) => { throw new ServiceError(400, 'VALIDATION_ERROR', message, { [field]: [message] }); };
const positive = value => Number.isSafeInteger(value) && value > 0;
const present = value => value !== undefined && value !== null;
const statuses = ['PENDING', 'IN_PROGRESS', 'RESOLVED', 'FAILED', 'CANCELLED'];
const priorities = ['LOW', 'MEDIUM', 'HIGH', 'CRITICAL'];
function object(value, allowed) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) invalid('body', 'Dữ liệu phải là object.');
  for (const key of Object.keys(value)) if (!allowed.includes(key)) invalid(key, 'Trường chưa được hỗ trợ.');
}
function text(value, field, max, required = false) {
  if (!present(value) && !required) return;
  if (typeof value !== 'string' || value.trim().length > max || required && !value.trim()) invalid(field, `Thông tin ${field} bắt buộc, tối đa ${max} ký tự.`);
}
function utc(value, field) {
  if (!present(value)) return;
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?Z$/.test(value) ||
      !Number.isFinite(Date.parse(value)) || new Date(value).toISOString().slice(0, 10) !== value.slice(0, 10)) invalid(field, 'Dùng thời gian ISO UTC kết thúc bằng Z.');
}
function money(value, field) {
  if (!present(value)) return;
  if (typeof value !== 'number' || !Number.isFinite(value) || value < 0 || !Number.isSafeInteger(Math.round(value * 100)) || Math.abs(value * 100 - Math.round(value * 100)) > 0.001) invalid(field, 'Chi phí không âm, tối đa hai chữ số thập phân và trong giới hạn chính xác.');
}
function version(body) {
  if (typeof body.rowVersion !== 'string' || !/^[A-Za-z0-9+/]{21}[AQgw]==$/.test(body.rowVersion)) invalid('rowVersion', 'Tải lại phiếu trước khi lưu.');
}
function search(query, allowed) {
  object(query, allowed); const page = query.page ?? 1, size = query.pageSize ?? 20;
  if (!positive(page) || !positive(size) || size > 100 || (page - 1) * size > 2147483647) invalid('page', 'Phân trang không hợp lệ.');
  for (const field of ['assetId', 'reporterId', 'technicianId']) if (present(query[field]) && !positive(query[field])) invalid(field, 'ID phải là số nguyên dương.');
  if (present(query.status) && !statuses.includes(query.status)) invalid('status', 'Trạng thái không hợp lệ.');
  if (present(query.priority) && !priorities.includes(query.priority)) invalid('priority', 'Mức ưu tiên không hợp lệ.');
  text(query.keyword, 'keyword', 200); utc(query.from, 'from'); utc(query.to, 'to');
  if (query.from && query.to && Date.parse(query.from) > Date.parse(query.to)) invalid('to', 'Khoảng thời gian không hợp lệ.');
  if (present(query.sortBy) && !['openedAt', 'dueAt', 'priority', 'id'].includes(query.sortBy)) invalid('sortBy', 'Cột sắp xếp không hợp lệ.');
  if (present(query.sortDirection) && !['asc', 'desc'].includes(query.sortDirection)) invalid('sortDirection', 'Chiều sắp xếp không hợp lệ.');
  if (present(query.eventType) && !['CREATED', 'ASSIGNED', 'STATUS_CHANGED', 'COMMENTED', 'COST_UPDATED', 'RESOLVED', 'FAILED', 'CANCELLED'].includes(query.eventType)) invalid('eventType', 'Loại sự kiện không hợp lệ.');
  const result = new URLSearchParams(); for (const [key, value] of Object.entries(query)) if (present(value)) result.set(key, String(value));
  return result.size ? '?' + result.toString() : '';
}

export function createMaintenanceService({ request, idPath }) {
  const path = id => { if (!positive(id)) invalid('id', 'Phiếu bảo trì không hợp lệ.'); return '/maintenance-tickets/' + idPath(id); };
  const mutate = async (id, action, dto) => {
    path(id);
    const fields = {
      assign: ['technicianId', 'note'], start: ['startedAt', 'note'], resolve: ['resolution', 'actualCost', 'completedAt'],
      fail: ['reason', 'cost', 'completedAt'], cancel: ['reason', 'note'],
      update: ['title', 'description', 'priority', 'dueAt', 'estimatedCost', 'actualCost', 'note']
    }[action];
    object(dto, ['rowVersion', ...fields]);
    version(dto);
    if (action === 'assign' && !positive(dto.technicianId)) invalid('technicianId', 'Chọn kỹ thuật viên hợp lệ.');
    text(dto.note, 'note', 1000, ['assign', 'start', 'update'].includes(action));
    text(dto.resolution, 'resolution', 2000, action === 'resolve'); text(dto.reason, 'reason', action === 'fail' ? 2000 : 1000, ['fail', 'cancel'].includes(action));
    text(dto.title, 'title', 200, Object.hasOwn(dto, 'title')); text(dto.description, 'description', 2000, Object.hasOwn(dto, 'description'));
    if (present(dto.priority) && !priorities.includes(dto.priority)) invalid('priority', 'Mức ưu tiên không hợp lệ.');
    for (const field of ['startedAt', 'completedAt', 'dueAt']) utc(dto[field], field);
    for (const field of ['estimatedCost', 'actualCost', 'cost']) money(dto[field], field);
    return request(path(id) + (action === 'update' ? '' : '/' + action), { method: action === 'update' ? 'PUT' : 'POST', body: dto });
  };
  return {
    list(query = {}) { return request('/maintenance-tickets' + search(query, ['page', 'pageSize', 'assetId', 'reporterId', 'technicianId', 'status', 'priority', 'from', 'to', 'keyword', 'sortBy', 'sortDirection'])); },
    get(id) { return request(path(id)); },
    history(id, query = {}) { return request(path(id) + '/history' + search(query, ['page', 'pageSize', 'from', 'to', 'eventType'])); },
    technicians(query = {}) { return request('/maintenance-tickets/technicians' + search(query, ['page', 'pageSize', 'keyword'])); },
    async create(dto) {
      object(dto, ['assetId', 'title', 'description', 'priority', 'dueAt', 'estimatedCost']);
      if (!positive(dto.assetId)) invalid('assetId', 'Chọn tài sản hợp lệ.');
      text(dto.title, 'title', 200, Object.hasOwn(dto, 'title')); text(dto.description, 'description', 2000, true);
      if (present(dto.priority) && !priorities.includes(dto.priority)) invalid('priority', 'Mức ưu tiên không hợp lệ.');
      utc(dto.dueAt, 'dueAt'); money(dto.estimatedCost, 'estimatedCost');
      return request('/maintenance-tickets', { method: 'POST', body: dto });
    },
    assign: (id, dto) => mutate(id, 'assign', dto), start: (id, dto) => mutate(id, 'start', dto),
    resolve: (id, dto) => mutate(id, 'resolve', dto), fail: (id, dto) => mutate(id, 'fail', dto),
    cancel: (id, dto) => mutate(id, 'cancel', dto), update: (id, dto) => mutate(id, 'update', dto)
  };
}
