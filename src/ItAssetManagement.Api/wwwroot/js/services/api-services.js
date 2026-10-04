import { ASSET_STATUSES, copy } from '../models/contracts.js';
import { validateAsset, validateLogin, validateMaster, validateUser, validateUserStatus, validateUserRoles } from '../utils/validation.js';
import { ServiceError } from './service-error.js';

/** Same-origin client for M1 backend APIs; no automatic mock fallback. */
export function createApiServices({ fetchImpl = globalThis.fetch?.bind(globalThis), baseUrl = '/api/v1' } = {}) {
  let token = null;
  let session = null;
  let expiresAt = null;
  let sessionRevision = 0;

  function clearSession() { sessionRevision++; token = null; session = null; expiresAt = null; }
  function currentSession() {
    if (expiresAt && Date.parse(expiresAt) <= Date.now()) clearSession();
    return session;
  }
  function validate(errors) {
    if (Object.keys(errors).length) throw new ServiceError(400, 'VALIDATION_ERROR', 'Vui lòng kiểm tra các trường thông tin.', errors);
  }
  function idPath(id) {
    if (!Number.isSafeInteger(Number(id)) || Number(id) <= 0) throw new ServiceError(400, 'VALIDATION_ERROR', 'ID không hợp lệ.', { id: ['ID phải là số nguyên dương.'] });
    return encodeURIComponent(String(id));
  }
  function queryString(query = {}) {
    const search = new URLSearchParams();
    for (const [field, value] of Object.entries(query)) if (value !== undefined && value !== null && value !== '') search.set(field, String(value));
    const serialized = search.toString();
    return serialized ? `?${serialized}` : '';
  }
  async function request(path, { method = 'GET', body, headers = {}, publicRequest = false } = {}) {
    if (!publicRequest && !currentSession()) throw new ServiceError(401, 'UNAUTHENTICATED', 'Phiên đăng nhập chưa có hoặc đã hết hạn. Vui lòng đăng nhập lại.');
    const revision = sessionRevision;
    if (!fetchImpl) throw new ServiceError(0, 'NETWORK_ERROR', 'Trình duyệt không hỗ trợ kết nối API.');
    let response;
    try {
      response = await fetchImpl(`${baseUrl.replace(/\/$/, '')}${path}`, {
        method, cache: 'no-store', credentials: 'omit',
        headers: { Accept: 'application/json', ...(body === undefined ? {} : { 'Content-Type': 'application/json' }), ...(!publicRequest ? { Authorization: `Bearer ${token}` } : {}), ...headers },
        ...(body === undefined ? {} : { body: JSON.stringify(body) }),
      });
    } catch {
      throw new ServiceError(0, 'NETWORK_ERROR', 'Không thể kết nối API. Kiểm tra máy chủ và thử lại; không có chuyển sang mock tự động.');
    }
    if (revision !== sessionRevision) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên đã thay đổi. Vui lòng đăng nhập lại.');
    if (response.status === 204) return undefined;
    let payload;
    try { payload = await response.json(); } catch {
      if (revision !== sessionRevision) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên đã thay đổi.');
      if (response.status === 401) clearSession();
      throw new ServiceError(response.ok ? 502 : response.status, 'API_UNAVAILABLE', 'API chưa sẵn sàng hoặc không trả JSON theo contract.');
    }
    if (revision !== sessionRevision) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên đã thay đổi. Vui lòng đăng nhập lại.');
    if (!response.ok) {
      if (response.status === 401) clearSession();
      throw new ServiceError(response.status, payload.code || 'API_ERROR', payload.detail || payload.title || 'Không thể thực hiện yêu cầu.', payload.errors || {}, { traceId: payload.traceId, retryAfter: response.headers.get('Retry-After') });
    }
    return payload;
  }
  const auth = {
    get session() { return copy(currentSession()); },
    async login({ email, password }) {
      validate(validateLogin({ email, password }));
      clearSession();
      const response = await request('/auth/login', { method: 'POST', body: { email: email.trim(), password }, publicRequest: true });
      if (!response?.accessToken || response.tokenType !== 'Bearer' || !Number.isFinite(Date.parse(response.expiresAt)) || !response.user?.permissions || !response.user?.roles) {
        throw new ServiceError(502, 'INVALID_API_RESPONSE', 'Login response không khớp contract đã thiết kế.');
      }
      token = response.accessToken;
      expiresAt = response.expiresAt;
      session = response.user;
      return copy(response);
    },
    async me() { const revision = sessionRevision; const user = await request('/auth/me'); if (revision !== sessionRevision) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên đã thay đổi.'); session = user; return copy(session); },
    async logout() { clearSession(); },
  };
  const assets = {
    list(query = {}) { return request(`/assets${queryString(query)}`); },
    get(id) { return request(`/assets/${idPath(id)}`); },
    async create(dto) { validate(validateAsset(dto)); return request('/assets', { method: 'POST', body: { ...dto, ...(dto.purchasePrice == null ? {} : { purchasePrice: Number(dto.purchasePrice) }) } }); },
    async update(id, dto) { validate(validateAsset(dto, { isUpdate: true })); return request(`/assets/${idPath(id)}`, { method: 'PUT', body: { ...dto, ...(dto.purchasePrice == null ? {} : { purchasePrice: Number(dto.purchasePrice) }) } }); },
    archive(id, rowVersion) {
      if (!rowVersion) return Promise.reject(new ServiceError(428, 'PRECONDITION_REQUIRED', 'Tải lại tài sản trước khi archive.'));
      if (typeof rowVersion !== 'string' || !/^[A-Za-z0-9+/]{22}==$/.test(rowVersion)) return Promise.reject(new ServiceError(400, 'VALIDATION_ERROR', 'Phiên bản archive không hợp lệ.'));
      return request(`/assets/${idPath(id)}`, { method: 'DELETE', headers: { 'If-Match': `"${rowVersion}"` } });
    },
  };
  function masterService(kind) {
    const path = kind === 'department' ? '/departments' : '/asset-types';
    return {
      list(query = {}) { return request(`${path}${queryString(query)}`); },
      get(id) { return request(`${path}/${idPath(id)}`); },
      async create(dto) { validate(validateMaster(dto, { kind })); return request(path, { method: 'POST', body: dto }); },
      async update(id, dto) { validate(validateMaster(dto, { kind, isUpdate: true })); return request(`${path}/${idPath(id)}`, { method: 'PUT', body: dto }); },
      setStatus(id, dto) {
        // M1 ChangeStatusRequest: confirmed by live HTTP tests; independent Thiện review pending.
        return request(`${path}/${idPath(id)}/status`, { method: 'PATCH', body: { status: dto.isActive ? 'Active' : 'Inactive', reason: dto.reason, rowVersion: dto.rowVersion } });
      },
    };
  }
  async function changeUser(id, path, method, dto) {
    const self = currentSession()?.id === Number(id);
    const result = await request(path, { method, body: dto });
    if (self) clearSession();
    return result;
  }
  const users = {
    list(query = {}) { return request(`/users${queryString(query)}`); },
    get(id) { return request(`/users/${idPath(id)}`); },
    account(id) { return request(`/users/${idPath(id)}/account`); },
    async create(dto) { validate(validateUser(dto)); return request('/users', { method: 'POST', body: dto }); },
    async update(id, dto) { validate(validateUser(dto, { isUpdate: true })); return changeUser(id, `/users/${idPath(id)}`, 'PUT', dto); },
    async setStatus(id, dto) { validate(validateUserStatus(dto)); return changeUser(id, `/users/${idPath(id)}/status`, 'PATCH', dto); },
    async replaceRoles(id, dto) { validate(validateUserRoles(dto)); return changeUser(id, `/users/${idPath(id)}/roles`, 'PUT', dto); },
  };
  const roles = {
    list(query = {}) { return request(`/roles${queryString(query)}`); },
    get(id) { return request(`/roles/${idPath(id)}`); },
    permissions(query = {}) { return request(`/roles/permissions${queryString(query)}`); },
  };
  const dashboard = {
    async summary() {
      // M1 has assets API, not a runtime Dashboard/Replacement module. No invented endpoint.
      const [recent, ...counts] = await Promise.all([
        assets.list({ page: 1, pageSize: 5, sortBy: 'createdAt', sortDirection: 'desc' }),
        ...ASSET_STATUSES.map((status) => assets.list({ page: 1, pageSize: 1, status })),
      ]);
      const byStatus = ASSET_STATUSES.map((status, index) => ({ status, count: counts[index].totalItems }));
      return { totalAssets: recent.totalItems, inUse: byStatus.find((item) => item.status === 'InUse').count, maintenance: byStatus.find((item) => item.status === 'Maintenance').count, replacementNeeded: null, replacementStatus: 'PLANNED', byStatus, recentAssets: recent.items, dataMode: 'api' };
    },
  };
  return { mode: 'api', auth, assets, departments: masterService('department'), assetTypes: masterService('assetType'), users, roles, dashboard };
}
