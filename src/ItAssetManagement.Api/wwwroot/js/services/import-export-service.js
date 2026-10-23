import { ServiceError } from './service-error.js';

const MIME = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
const MAX_FILE = 5 * 1024 * 1024;
const MAX_DOWNLOAD = 32 * 1024 * 1024;

// The token stays in the API adapter's closure, never in CurrentUser or URLs.
// No write retry: an interrupted import has an unknown outcome.
export function createImportExportService({ fetchImpl = globalThis.fetch?.bind(globalThis), baseUrl = '/api/v1',
  getSession, getToken, getRevision, onUnauthorized } = {}) {
  if (!/^\/[A-Za-z0-9/_-]*$/u.test(baseUrl)) throw new TypeError('Import/export requires a same-origin API path.');
  const changed = () => new ServiceError(401, 'SESSION_CHANGED', 'Phiên đã thay đổi. Vui lòng đăng nhập lại.');
  const invalid = () => new ServiceError(400, 'VALIDATION_ERROR', 'Tham số nhập/xuất không hợp lệ.');
  function capture(permission) {
    const session = getSession?.();
    if (!Number.isSafeInteger(session?.id) || session.id <= 0)
      throw new ServiceError(401, 'UNAUTHORIZED', 'Yêu cầu đăng nhập.');
    if (!Array.isArray(session.permissions) || !session.permissions.includes(permission))
      throw new ServiceError(403, 'FORBIDDEN', 'Không đủ quyền nhập/xuất dữ liệu.');
    const revision = getRevision?.();
    const costGrants = ['assets.cost.read', 'reports.cost.read'].filter(p => session.permissions.includes(p));
    return () => {
      const current = getSession?.();
      if (current?.id !== session.id || (getRevision && getRevision() !== revision) ||
          !current?.permissions?.includes(permission) || costGrants.some(p => !current?.permissions?.includes(p))) throw changed();
    };
  }
  function queryString(query, allowed) {
    if (!query || typeof query !== 'object' || Array.isArray(query) || Object.keys(query).some(k => !allowed.includes(k))) throw invalid();
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(query)) {
      if (value === undefined || value === null || value === '') continue;
      if (!['string', 'number', 'boolean'].includes(typeof value) || String(value).length > 200 ||
          (typeof value === 'number' && !Number.isSafeInteger(value))) throw invalid();
      params.set(key, String(value));
    }
    return params.size ? '?' + params : '';
  }
  async function exchange(path, { method = 'GET', body, download = false } = {}, verify) {
    verify();
    const token = getToken?.();
    if (typeof token !== 'string' || !token.trim()) throw new ServiceError(401, 'UNAUTHORIZED', 'Yêu cầu đăng nhập.');
    if (!fetchImpl) throw new ServiceError(0, 'NETWORK_ERROR', 'Không thể kết nối API.');
    let response;
    try {
      response = await fetchImpl(baseUrl.replace(/\/$/u, '') + path, {
        method, cache: 'no-store', credentials: 'omit',
        headers: { Accept: download ? MIME : 'application/json', Authorization: 'Bearer ' + token },
        ...(body ? { body } : {})
      });
    } catch {
      verify();
      throw new ServiceError(0, 'NETWORK_ERROR', 'Không thể kết nối API. Kiểm tra kết quả trước khi gửi lại thao tác nhập.');
    }
    verify();
    if (!response.ok) {
      let problem = null;
      try { problem = await response.json(); } catch { /* Safe status mapping below. */ }
      verify();
      if (response.status === 401) onUnauthorized?.();
      const messages = { 401: 'Phiên đã hết hạn.', 403: 'Không đủ quyền thực hiện.', 413: 'Tệp hoặc dữ liệu xuất vượt giới hạn.', 415: 'Định dạng tệp không hỗ trợ.', 409: 'Dữ liệu đã thay đổi; kiểm tra kết quả trước khi nhập lại.' };
      const codes = { 401: 'UNAUTHORIZED', 403: 'FORBIDDEN', 413: 'FILE_TOO_LARGE', 415: 'UNSUPPORTED_MEDIA_TYPE', 409: 'IMPORT_CONFLICT' };
      // Do not echo arbitrary provider details or workbook cell values.
      throw new ServiceError(response.status, codes[response.status] || 'IMPORT_EXPORT_FAILED',
        messages[response.status] || 'Không thể xử lý nhập/xuất dữ liệu.', {},
        { traceId: typeof problem?.traceId === 'string' ? problem.traceId : null });
    }
    if (download) {
      const contentType = response.headers?.get('Content-Type')?.split(';')[0].trim();
      if (contentType !== MIME || Number(response.headers?.get('Content-Length') || 0) > MAX_DOWNLOAD)
        throw new ServiceError(502, 'INVALID_API_RESPONSE', 'Tệp trả về không hợp lệ.');
      let blob;
      try { blob = await response.blob(); } catch { verify(); throw new ServiceError(502, 'INVALID_API_RESPONSE', 'Không đọc được tệp trả về.'); }
      verify();
      if (!Number.isFinite(blob.size) || blob.size <= 0 || blob.size > MAX_DOWNLOAD)
        throw new ServiceError(502, 'INVALID_API_RESPONSE', 'Dung lượng tệp trả về không hợp lệ.');
      const match = /filename="?([^";]+)"?/iu.exec(response.headers?.get('Content-Disposition') || '');
      return { blob, filename: match && /^[a-z0-9_-]{1,100}\.xlsx$/iu.test(match[1]) ? match[1] : 'export.xlsx' };
    }
    let result;
    try { result = await response.json(); } catch { verify(); throw new ServiceError(502, 'INVALID_API_RESPONSE', 'Kết quả nhập dữ liệu không hợp lệ; kiểm tra trước khi gửi lại.'); }
    verify();
    if (!result || !['totalRows', 'validRows', 'errorCount'].every(k => Number.isSafeInteger(result[k]) && result[k] >= 0) ||
        result.totalRows > 1000 || result.validRows > result.totalRows || typeof result.committed !== 'boolean' ||
        typeof result.dryRun !== 'boolean' || !Array.isArray(result.errors) || result.errorCount !== result.errors.length ||
        (result.dryRun && result.committed) || (result.committed && (result.errorCount > 0 || result.validRows !== result.totalRows)))
      throw new ServiceError(502, 'INVALID_API_RESPONSE', 'Kết quả nhập dữ liệu không khớp hợp đồng API.');
    if (result.errors.some(error => !error || !Number.isSafeInteger(error.rowNumber) || error.rowNumber < 2 ||
        !['field', 'errorCode', 'errorMessage'].every(k => typeof error[k] === 'string' && error[k].length <= 2000)))
      throw new ServiceError(502, 'INVALID_API_RESPONSE', 'Chi tiết lỗi nhập dữ liệu không hợp lệ.');
    return result;
  }
  function download(path, permission, query, allowed) {
    const verify = capture(permission);
    return exchange(path + queryString(query, allowed), { download: true }, verify);
  }
  return Object.freeze({
    template() { return download('/import/assets/template', 'import.assets.execute', {}, []); },
    async importAssets(file, { dryRun = false } = {}) {
      const verify = capture('import.assets.execute');
      if (!file || file.size <= 0) throw new ServiceError(400, 'EMPTY_FILE', 'Chọn tệp Excel có dữ liệu.');
      if (!Number.isSafeInteger(file.size) || file.size > MAX_FILE) throw new ServiceError(413, 'FILE_TOO_LARGE', 'Tệp tối đa 5MB.');
      if (!(file instanceof Blob) || typeof file.name !== 'string' || !file.name.toLowerCase().endsWith('.xlsx') ||
          (file.type && ![MIME, 'application/octet-stream'].includes(file.type)))
        throw new ServiceError(415, 'UNSUPPORTED_MEDIA_TYPE', 'Chỉ nhận tệp XLSX.');
      if (typeof dryRun !== 'boolean') throw invalid();
      const body = new FormData(); body.append('file', file, file.name);
      const result = await exchange('/import/assets?dryRun=' + dryRun, { method: 'POST', body }, verify);
      if (result.dryRun !== dryRun) throw new ServiceError(502, 'INVALID_API_RESPONSE', 'Chế độ nhập dữ liệu trả về không khớp yêu cầu.');
      return result;
    },
    exportAssets(query = {}) { return download('/export/assets', 'export.assets.execute', query, ['keyword', 'status', 'assetTypeId', 'departmentId', 'fromUtc', 'toUtc', 'sortBy', 'sortDirection']); },
    exportMaintenance(query = {}) { return download('/export/maintenance', 'export.maintenance.execute', query, ['fromUtc', 'toUtc', 'assetId', 'technicianId', 'status']); },
    exportAssignments(query = {}) { return download('/export/assignment-history', 'export.assignments.execute', query, ['fromUtc', 'toUtc', 'assetId', 'userId', 'departmentId']); },
    exportWarranty(query = {}) { return download('/export/warranty-expiration', 'export.assets.execute', query, ['fromUtc', 'toUtc', 'departmentId', 'assetTypeId']); },
    exportReplacement(query = {}) { return download('/export/replacement-report', 'export.replacement.execute', query, ['year', 'departmentId', 'priority', 'status']); }
  });
}
