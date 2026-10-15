import { ServiceError } from './service-error.js';

const positive = value => Number.isSafeInteger(value) && value > 0;
const statuses = ['InStock', 'InUse', 'Maintenance', 'Broken', 'Retired'];
const sources = ['ASSIGNMENT', 'MAINTENANCE', 'ADMIN', 'IMPORT', 'SYSTEM'];
const invalid = () => { throw new ServiceError(502, 'INVALID_API_RESPONSE', 'Lịch sử trạng thái chưa khớp contract API.'); };
const optionalText = (value, max) => value == null ? null : typeof value === 'string' && value.length <= max ? value : invalid();

// Active read-only endpoint, not the planned Assignment/Maintenance history APIs.
export function createAssetHistoryReader({ request, idPath }) {
  return async (id, query = {}) => {
    if (!positive(id) || !query || typeof query !== 'object' || Array.isArray(query) ||
        Object.keys(query).some(key => !['page', 'pageSize'].includes(key))) {
      throw new ServiceError(400, 'VALIDATION_ERROR', 'Lịch sử chỉ hỗ trợ ID, page và pageSize.');
    }
    const page = query.page ?? 1, pageSize = query.pageSize ?? 20;
    if (!positive(page) || !positive(pageSize) || pageSize > 100 || (page - 1) * pageSize > 2147483647) {
      throw new ServiceError(400, 'VALIDATION_ERROR', 'Trang >=1 và pageSize 1..100.');
    }
    const result = await request(`/assets/${idPath(id)}/status-history?page=${page}&pageSize=${pageSize}`);
    if (!result || !Array.isArray(result.items) || result.page !== page || result.pageSize !== pageSize ||
        !Number.isSafeInteger(result.totalItems) || result.totalItems < 0 ||
        result.totalPages !== Math.ceil(result.totalItems / pageSize) ||
        result.items.length !== Math.min(pageSize, Math.max(0, result.totalItems - (page - 1) * pageSize))) invalid();
    const seen = new Set();
    const items = result.items.map(row => {
      if (!row || !positive(row.id) || seen.has(row.id) || !statuses.includes(row.toStatus) ||
          row.fromStatus != null && !statuses.includes(row.fromStatus) || !sources.includes(row.source) ||
          row.changedByUserId != null && !positive(row.changedByUserId) ||
          typeof row.changedAtUtc !== 'string' || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?Z$/.test(row.changedAtUtc) ||
          !Number.isFinite(Date.parse(row.changedAtUtc)) || new Date(row.changedAtUtc).toISOString().slice(0, 19) !== row.changedAtUtc.slice(0, 19) ||
          typeof row.correlationId !== 'string' || !/^[a-f\d]{8}-(?:[a-f\d]{4}-){3}[a-f\d]{12}$/i.test(row.correlationId)) invalid();
      seen.add(row.id);
      // Explicit projection: no financial fields, user email or extra metadata retained.
      return Object.freeze({ id: row.id, fromStatus: row.fromStatus ?? null, toStatus: row.toStatus,
        source: row.source, reason: optionalText(row.reason, 1000), changedAtUtc: row.changedAtUtc,
        changedByUserId: row.changedByUserId ?? null,
        changedByDisplayName: optionalText(row.changedByDisplayName, 200), correlationId: row.correlationId });
    });
    return Object.freeze({ items: Object.freeze(items), page, pageSize, totalItems: result.totalItems, totalPages: result.totalPages });
  };
}
