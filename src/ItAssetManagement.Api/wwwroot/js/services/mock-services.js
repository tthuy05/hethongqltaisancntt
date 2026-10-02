import { ASSET_METADATA_FIELDS, ASSET_SORT_FIELDS, ASSET_STATUSES, ROLE_CODES, copy, rolePermissions } from '../models/contracts.js';
import { validateAsset, validateLogin, validateMaster } from '../utils/validation.js';
import { createSeed } from '../mock/seed.js';
import { ServiceError } from './service-error.js';

const delay = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));
const normalized = (value) => String(value ?? '').trim();
const nullable = (value) => value === undefined || value === null || value === '' ? null : typeof value === 'string' ? value.trim() || null : value;
const sameCode = (left, right) => normalized(left).toLocaleLowerCase() === normalized(right).toLocaleLowerCase();
const positiveId = (value) => Number.isSafeInteger(Number(value)) && Number(value) > 0;

function version() {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  return btoa(String.fromCharCode(...bytes));
}

function validate(errors) {
  if (Object.keys(errors).length) throw new ServiceError(400, 'VALIDATION_ERROR', 'Vui lòng kiểm tra các trường thông tin.', errors);
}

function pagination(records, query, fields, defaultSort) {
  const page = query.page === undefined || query.page === '' ? 1 : Number(query.page);
  const pageSize = query.pageSize === undefined || query.pageSize === '' ? 20 : Number(query.pageSize);
  const sortBy = query.sortBy || defaultSort;
  const sortDirection = query.sortDirection || 'asc';
  const errors = {};
  if (!Number.isSafeInteger(page) || page < 1) errors.page = ['Trang phải là số nguyên từ 1.'];
  if (!Number.isInteger(pageSize) || pageSize < 1 || pageSize > 100) errors.pageSize = ['Số dòng mỗi trang phải từ 1 đến 100.'];
  if (!fields.includes(sortBy)) errors.sortBy = ['Trường sắp xếp không được hỗ trợ.'];
  if (!['asc', 'desc'].includes(sortDirection)) errors.sortDirection = ['Chiều sắp xếp phải là asc hoặc desc.'];
  validate(errors);
  const direction = sortDirection === 'asc' ? 1 : -1;
  const sorted = [...records].sort((a, b) => {
    const left = a[sortBy] ?? '';
    const right = b[sortBy] ?? '';
    const comparison = typeof left === 'number' && typeof right === 'number' ? left - right : String(left).localeCompare(String(right), 'vi', { numeric: true, sensitivity: 'base' });
    return direction * (comparison || a.id - b.id);
  });
  const totalItems = sorted.length;
  return { items: sorted.slice((page - 1) * pageSize, page * pageSize), page, pageSize, totalItems, totalPages: Math.ceil(totalItems / pageSize) };
}

function keyword(query) {
  if (query.keyword !== undefined && typeof query.keyword !== 'string') throw new ServiceError(400, 'VALIDATION_ERROR', 'Từ khóa không hợp lệ.', { keyword: ['Từ khóa phải là chuỗi.'] });
  const value = normalized(query.keyword);
  if (value.length > 200) throw new ServiceError(400, 'VALIDATION_ERROR', 'Từ khóa quá dài.', { keyword: ['Tối đa 200 ký tự.'] });
  return value.toLocaleLowerCase();
}

function checkVersion(record, supplied) {
  if (typeof supplied !== 'string' || !supplied) throw new ServiceError(400, 'VALIDATION_ERROR', 'Thiếu phiên bản bản ghi.', { rowVersion: ['Tải lại bản ghi để lấy phiên bản hiện tại.'] });
  if (record.rowVersion !== supplied) throw new ServiceError(409, 'CONCURRENCY_CONFLICT', 'Dữ liệu đã thay đổi. Vui lòng tải lại trước khi lưu.');
}

/** Isolated, synthetic in-memory development flow. Never authentication/security enforcement. */
export function createMockServices({ hostname, latency = 150 } = {}) {
  if (!['localhost', '127.0.0.1'].includes(hostname)) throw new ServiceError(403, 'MOCK_DISABLED', 'Mock chỉ được bật trên máy phát triển localhost.');
  const state = createSeed(version);
  let session = null;
  let expiresAt = null;
  let sessionRevision = 0;

  function currentSession() {
    if (expiresAt && Date.parse(expiresAt) <= Date.now()) { session = null; expiresAt = null; sessionRevision++; }
    return session;
  }

  function requirePermission(permission) {
    const current = currentSession();
    if (!current) throw new ServiceError(401, 'UNAUTHENTICATED', 'Vui lòng đăng nhập luồng demo.');
    if (!current.permissions.includes(permission)) throw new ServiceError(403, 'FORBIDDEN', 'Bạn không có quyền thực hiện thao tác này.');
  }

  async function run(permission, action) {
    // Check both before and after latency: a logout must invalidate an in-flight call.
    requirePermission(permission);
    const revision = sessionRevision;
    if (latency > 0) await delay(latency);
    requirePermission(permission);
    if (revision !== sessionRevision) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên demo đã thay đổi. Vui lòng thử lại.');
    return copy(action());
  }

  function find(collection, id, code) {
    if (!positiveId(id)) throw new ServiceError(400, 'VALIDATION_ERROR', 'ID không hợp lệ.', { id: ['ID phải là số nguyên dương.'] });
    const item = collection.find((record) => record.id === Number(id));
    if (!item) throw new ServiceError(404, code, 'Không tìm thấy bản ghi hoặc bạn không được phép xem.');
    return item;
  }

  const reference = (record) => ({ id: record.id, code: record.code, name: record.name, isActive: record.isActive });
  const includeCost = () => currentSession().permissions.includes('assets.cost.read');

  function assetResponse(record, detailed = false) {
    const result = {
      id: record.id, assetCode: record.assetCode, name: record.name, serialNumber: record.serialNumber,
      assetType: reference(find(state.assetTypes, record.assetTypeId, 'ASSET_TYPE_NOT_FOUND')),
      owningDepartment: reference(find(state.departments, record.owningDepartmentId, 'DEPARTMENT_NOT_FOUND')),
      status: record.status, location: record.location, createdAt: record.createdAt, updatedAt: record.updatedAt,
    };
    if (includeCost()) result.purchasePrice = record.purchasePrice;
    if (detailed) {
      for (const field of ASSET_METADATA_FIELDS) if (field !== 'purchasePrice' || includeCost()) result[field] = record[field];
      Object.assign(result, { rowVersion: record.rowVersion, isArchived: record.isArchived, currentAssignment: record.currentAssignment });
    }
    return result;
  }

  function activeAsset(id) {
    const record = find(state.assets, id, 'ASSET_NOT_FOUND');
    if (record.isArchived) throw new ServiceError(404, 'ASSET_NOT_FOUND', 'Tài sản đã được archive hoặc không tồn tại.');
    return record;
  }

  function prepareAsset(dto, previous = null) {
    validate(validateAsset(dto, { isUpdate: Boolean(previous) }));
    const assetType = state.assetTypes.find((item) => item.id === Number(dto.assetTypeId));
    const department = state.departments.find((item) => item.id === Number(dto.owningDepartmentId));
    const referenceErrors = {};
    if (!assetType) referenceErrors.assetTypeId = ['Loại tài sản không tồn tại.'];
    else if (!assetType.isActive && assetType.id !== previous?.assetTypeId) referenceErrors.assetTypeId = ['Loại tài sản được chọn mới phải active.'];
    if (!department) referenceErrors.owningDepartmentId = ['Phòng ban không tồn tại.'];
    else if (!department.isActive && department.id !== previous?.owningDepartmentId) referenceErrors.owningDepartmentId = ['Phòng ban được chọn mới phải active.'];
    validate(referenceErrors);
    const next = Object.fromEntries(ASSET_METADATA_FIELDS.map((field) => [field, nullable(dto[field])]));
    next.assetCode = normalized(dto.assetCode).toUpperCase();
    next.name = normalized(dto.name);
    next.assetTypeId = assetType.id;
    next.owningDepartmentId = department.id;
    next.serialNumber = next.serialNumber ? normalized(next.serialNumber).toUpperCase() : null;
    next.purchasePrice = next.purchasePrice === null ? null : Number(next.purchasePrice);
    if (state.assets.some((asset) => asset.id !== previous?.id && sameCode(asset.assetCode, next.assetCode))) {
      throw new ServiceError(409, 'ASSET_CODE_CONFLICT', 'Mã tài sản đã tồn tại.', { assetCode: ['Mã tài sản đã tồn tại, kể cả bản ghi archive.'] });
    }
    if (next.serialNumber && state.assets.some((asset) => asset.id !== previous?.id && asset.serialNumber && sameCode(asset.serialNumber, next.serialNumber))) {
      throw new ServiceError(409, 'ASSET_SERIAL_CONFLICT', 'Serial Number đã tồn tại.', { serialNumber: ['Serial Number đã tồn tại.'] });
    }
    return next;
  }

  const auth = {
    get session() { return copy(currentSession()); },
    async login(dto) {
      validate(validateLogin(dto));
      const role = dto.role || 'ADMIN_IT';
      if (!ROLE_CODES.includes(role)) throw new ServiceError(400, 'VALIDATION_ERROR', 'Vai trò demo không hợp lệ.', { role: ['Chọn một trong ba vai trò demo.'] });
      const revision = ++sessionRevision;
      if (latency > 0) await delay(latency);
      if (revision !== sessionRevision) throw new ServiceError(401, 'SESSION_CHANGED', 'Phiên demo đã thay đổi. Vui lòng thử lại.');
      // Intentionally do not retain email/password. There is no account verification or JWT.
      session = { id: ROLE_CODES.indexOf(role) + 1, displayName: `${role === 'ADMIN_IT' ? 'Quản trị IT' : role === 'SYSTEM_MANAGER' ? 'Quản lý hệ thống' : 'Hỗ trợ kỹ thuật'} · Demo`, roles: [role], permissions: rolePermissions(role), departmentId: 1 };
      expiresAt = new Date(Date.now() + 60 * 60 * 1000).toISOString();
      return { accessToken: 'development-mock-not-a-jwt', tokenType: 'Bearer', expiresAt, user: copy(session) };
    },
    me() { return run('assets.read', () => currentSession()); },
    async logout() { sessionRevision++; session = null; expiresAt = null; },
  };

  const assets = {
    list(query = {}) {
      return run('assets.read', () => {
        const errors = {};
        const term = keyword(query);
        for (const field of ['departmentId', 'assetTypeId']) if (query[field] !== undefined && query[field] !== '' && !positiveId(query[field])) errors[field] = ['ID lọc phải là số nguyên dương.'];
        if (query.status && !ASSET_STATUSES.includes(query.status)) errors.status = ['Trạng thái tài sản không hợp lệ.'];
        const allowed = ['keyword', 'departmentId', 'assetTypeId', 'status', 'page', 'pageSize', 'sortBy', 'sortDirection'];
        for (const field of Object.keys(query)) if (!allowed.includes(field)) errors[field] = ['Bộ lọc này chưa thuộc contract M1.'];
        validate(errors);
        const records = state.assets.filter((asset) => !asset.isArchived
          && (!term || [asset.assetCode, asset.name, asset.serialNumber].some((value) => String(value ?? '').toLocaleLowerCase().includes(term)))
          && (!query.departmentId || asset.owningDepartmentId === Number(query.departmentId))
          && (!query.assetTypeId || asset.assetTypeId === Number(query.assetTypeId))
          && (!query.status || asset.status === query.status));
        const result = pagination(records, query, ASSET_SORT_FIELDS, 'assetCode');
        return { ...result, items: result.items.map((asset) => assetResponse(asset)) };
      });
    },
    get(id) { return run('assets.read', () => assetResponse(activeAsset(id), true)); },
    create(dto) {
      return run('assets.create', () => {
        const record = { ...prepareAsset(dto), id: Math.max(0, ...state.assets.map((asset) => asset.id)) + 1, status: 'InStock', createdAt: new Date().toISOString(), updatedAt: null, rowVersion: version(), isArchived: false, currentAssignment: null };
        state.assets.push(record);
        return assetResponse(record, true);
      });
    },
    update(id, dto) {
      return run('assets.update', () => {
        const record = activeAsset(id);
        checkVersion(record, dto.rowVersion);
        Object.assign(record, prepareAsset(dto, record), { updatedAt: new Date().toISOString(), rowVersion: version() });
        return assetResponse(record, true);
      });
    },
    archive(id, rowVersion) {
      return run('assets.archive', () => {
        const record = activeAsset(id);
        if (!rowVersion) throw new ServiceError(428, 'PRECONDITION_REQUIRED', 'Tải lại tài sản để lấy phiên bản trước khi archive.');
        if (typeof rowVersion !== 'string' || !/^[A-Za-z0-9+/]{22}==$/.test(rowVersion)) throw new ServiceError(400, 'VALIDATION_ERROR', 'Phiên bản archive không hợp lệ.');
        checkVersion(record, rowVersion);
        if (state.activeWorkflowAssetIds.has(record.id)) throw new ServiceError(409, 'ASSET_ACTIVE_WORKFLOW', 'Không thể archive tài sản đang có cấp phát hoặc bảo trì chưa hoàn tất (tình huống demo).');
        Object.assign(record, { isArchived: true, updatedAt: new Date().toISOString(), rowVersion: version() });
        return undefined;
      });
    },
  };

  function masterService(kind) {
    const isDepartment = kind === 'department';
    const collection = isDepartment ? state.departments : state.assetTypes;
    const policy = isDepartment ? 'departments' : 'asset-types';
    const notFound = isDepartment ? 'DEPARTMENT_NOT_FOUND' : 'ASSET_TYPE_NOT_FOUND';
    function prepare(dto, previous = null) {
      validate(validateMaster(dto, { kind, isUpdate: Boolean(previous) }));
      const code = normalized(dto.code).toUpperCase();
      if (previous && isDepartment && code !== previous.code) throw new ServiceError(400, 'VALIDATION_ERROR', 'Mã phòng ban không được thay đổi.', { code: ['Mã phòng ban bất biến theo thiết kế hiện tại.'] });
      if (collection.some((item) => item.id !== previous?.id && sameCode(item.code, code))) throw new ServiceError(409, 'MASTER_CODE_CONFLICT', 'Mã danh mục đã tồn tại.', { code: ['Mã danh mục đã tồn tại.'] });
      const next = { code, name: normalized(dto.name), description: nullable(dto.description) };
      if (isDepartment) {
        next.parentDepartmentId = nullable(dto.parentDepartmentId) === null ? null : Number(dto.parentDepartmentId);
        if (next.parentDepartmentId !== null) {
          const parent = collection.find((item) => item.id === next.parentDepartmentId);
          if (!parent) validate({ parentDepartmentId: ['Phòng ban cha không tồn tại.'] });
          if (!parent.isActive && parent.id !== previous?.parentDepartmentId) validate({ parentDepartmentId: ['Phòng ban cha được chọn mới phải active.'] });
          let ancestor = parent;
          const visited = new Set();
          while (ancestor) {
            if (ancestor.id === previous?.id || visited.has(ancestor.id)) throw new ServiceError(409, 'DEPARTMENT_CYCLE', 'Phòng ban cha tạo quan hệ vòng.', { parentDepartmentId: ['Không được chọn chính phòng ban hoặc phòng ban con.'] });
            visited.add(ancestor.id);
            ancestor = collection.find((item) => item.id === ancestor.parentDepartmentId);
          }
        }
      } else next.defaultUsefulLifeMonths = nullable(dto.defaultUsefulLifeMonths) === null ? null : Number(dto.defaultUsefulLifeMonths);
      return next;
    }
    return {
      list(query = {}) {
        return run(`${policy}.read`, () => {
          const term = keyword(query);
          if (query.status && !['Active', 'Inactive'].includes(query.status)) validate({ status: ['Trạng thái danh mục phải là Active hoặc Inactive.'] });
          for (const field of Object.keys(query)) if (!['keyword', 'status', 'page', 'pageSize', 'sortBy', 'sortDirection'].includes(field)) validate({ [field]: ['Bộ lọc không thuộc contract danh mục.'] });
          return pagination(collection.filter((item) => (!term || `${item.code} ${item.name}`.toLocaleLowerCase().includes(term)) && (!query.status || item.isActive === (query.status === 'Active'))), query, ['code', 'name'], 'name');
        });
      },
      get(id) { return run(`${policy}.read`, () => find(collection, id, notFound)); },
      create(dto) {
        return run(`${policy}.create`, () => {
          const record = { ...prepare(dto), id: Math.max(0, ...collection.map((item) => item.id)) + 1, isActive: true, rowVersion: version() };
          collection.push(record);
          return record;
        });
      },
      update(id, dto) {
        return run(`${policy}.update`, () => {
          const record = find(collection, id, notFound);
          checkVersion(record, dto.rowVersion);
          Object.assign(record, prepare(dto, record), { rowVersion: version() });
          return record;
        });
      },
      setStatus(id, dto) {
        return run(`${policy}.archive`, () => {
          const record = find(collection, id, notFound);
          checkVersion(record, dto.rowVersion);
          const errors = {};
          if (typeof dto.isActive !== 'boolean') errors.isActive = ['Trạng thái active phải là boolean.'];
          if (typeof dto.reason !== 'string' || !dto.reason.trim() || dto.reason.trim().length > 500) errors.reason = ['Vui lòng nhập lý do, tối đa 500 ký tự.'];
          validate(errors);
          Object.assign(record, { isActive: dto.isActive, rowVersion: version() });
          return record;
        });
      },
    };
  }

  const dashboard = {
    summary() {
      return run('dashboard.read', () => {
        const available = state.assets.filter((asset) => !asset.isArchived);
        const count = (status) => available.filter((asset) => asset.status === status).length;
        return {
          totalAssets: available.length, inUse: count('InUse'), maintenance: count('Maintenance'),
          // Explicit fixture statistic, not a lifecycle evaluation implementation.
          replacementNeeded: count('Broken') + count('Retired'), replacementStatus: 'MOCK',
          byStatus: ASSET_STATUSES.map((status) => ({ status, count: count(status) })),
          recentAssets: [...available].sort((a, b) => b.createdAt.localeCompare(a.createdAt) || b.id - a.id).slice(0, 5).map((asset) => assetResponse(asset)),
          dataMode: 'mock',
        };
      });
    },
  };
  return { mode: 'mock', auth, assets, departments: masterService('department'), assetTypes: masterService('assetType'), dashboard };
}
