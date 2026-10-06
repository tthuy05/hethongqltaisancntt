import { ServiceError } from '../services/service-error.js';

const day = 86400000;
const queryKeys = new Set(['page', 'pageSize', 'userId', 'action', 'entityType', 'entityId', 'correlationId', 'outcome', 'from', 'to', 'sortBy', 'sortDirection']);
export function auditQuery(search = '', now = new Date()) {
  const values = new URLSearchParams(search);
  const query = { page: 1, pageSize: 20, sortBy: 'occurredAt', sortDirection: 'desc' };
  for (const [key, value] of values) {
    if (!queryKeys.has(key) || values.getAll(key).length !== 1 || !value.trim())
      throw new ServiceError(400, 'VALIDATION_ERROR', 'Bộ lọc nhật ký không hợp lệ. Hãy đặt lại bộ lọc.');
    query[key] = value;
  }
  // Freeze the time window before paging; the read itself appends an audit.view event.
  query.to ??= now.toISOString();
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})$/.test(query.to) || !Number.isFinite(Date.parse(query.to)))
    throw new ServiceError(400, 'VALIDATION_ERROR', 'Thời gian kết thúc phải có múi giờ hợp lệ. Hãy đặt lại bộ lọc.');
  query.from ??= new Date(Date.parse(query.to) - 7 * day).toISOString();
  return query;
}
export function localDateInput(value) {
  const instant = new Date(value);
  if (!Number.isFinite(instant.getTime())) return '';
  const pad = number => String(number).padStart(2, '0');
  return `${instant.getFullYear()}-${pad(instant.getMonth() + 1)}-${pad(instant.getDate())}T${pad(instant.getHours())}:${pad(instant.getMinutes())}:${pad(instant.getSeconds())}`;
}
export function localDateToUtc(value) {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?$/.exec(value);
  if (!match) return null;
  const [year, month, dayOfMonth, hour, minute] = match.slice(1, 6).map(Number);
  const second = Number(match[6] ?? 0);
  const date = new Date(year, month - 1, dayOfMonth, hour, minute, second);
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== dayOfMonth || date.getHours() !== hour || date.getMinutes() !== minute || date.getSeconds() !== second) return null;
  return date.toISOString();
}
export function auditDateErrors(from, to) {
  if (!from || !to) return { ...(!from ? { from: ['Chọn thời gian bắt đầu hợp lệ.'] } : {}), ...(!to ? { to: ['Chọn thời gian kết thúc hợp lệ.'] } : {}) };
  const duration = Date.parse(to) - Date.parse(from);
  return !Number.isFinite(duration) || duration <= 0 ? { to: ['Thời gian kết thúc phải sau thời gian bắt đầu.'] }
    : duration > 31 * day ? { to: ['Mỗi lần tra cứu tối đa 31 ngày.'] } : {};
}
