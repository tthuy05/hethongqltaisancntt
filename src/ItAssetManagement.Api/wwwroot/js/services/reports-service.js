import { ServiceError } from './service-error.js';

// Prepared Week 6 Reports reader (EP-076 – EP-086).
export function createReportsReader({ request, queryString, getSession }) {
  const requireGrant = (permission, message = 'Bạn không có quyền xem dữ liệu này.') => {
    const session = getSession();
    if (!Number.isSafeInteger(session?.id) || session.id <= 0)
      throw new ServiceError(401, 'UNAUTHENTICATED', 'Vui lòng đăng nhập để xem báo cáo.');
    if (!Array.isArray(session.permissions) || !session.permissions.includes(permission))
      throw new ServiceError(403, 'FORBIDDEN', message);
  };

  const positiveId = (value, name, label) => {
    if (value == null || value === '') return undefined;
    if (!Number.isSafeInteger(value) || value <= 0)
      throw new ServiceError(400, 'VALIDATION_ERROR', `${label} không hợp lệ.`, { [name]: ['ID phải là số nguyên dương.'] });
    return value;
  };

  const pageParam = (value, def = 1) => {
    if (value == null || value === '') return def;
    if (!Number.isSafeInteger(value) || value < 1)
      throw new ServiceError(400, 'VALIDATION_ERROR', 'Trang không hợp lệ.', { page: ['Trang phải là số nguyên dương.'] });
    return value;
  };

  const pageSizeParam = (value, def = 20) => {
    if (value == null || value === '') return def;
    if (!Number.isSafeInteger(value) || value < 1 || value > 100)
      throw new ServiceError(400, 'VALIDATION_ERROR', 'Số lượng dòng không hợp lệ.', { pageSize: ['PageSize phải từ 1 đến 100.'] });
    return value;
  };

  const dateOnly = (value, name) => {
    if (value == null || value === '') return undefined;
    const match = typeof value === 'string' && /^(\d{4})-(\d{2})-(\d{2})$/u.exec(value);
    if (!match) throw new ServiceError(400, 'VALIDATION_ERROR', 'Định dạng ngày không hợp lệ.', { [name]: ['Dùng định dạng YYYY-MM-DD.'] });
    const year = Number(match[1]), month = Number(match[2]), day = Number(match[3]);
    const parsed = new Date(Date.UTC(year, month - 1, day));
    if (parsed.getUTCFullYear() !== year || parsed.getUTCMonth() + 1 !== month || parsed.getUTCDate() !== day)
      throw new ServiceError(400, 'VALIDATION_ERROR', 'Ngày không tồn tại.', { [name]: ['Ngày không hợp lệ.'] });
    return value;
  };

  const utc = (value, name) => {
    if (value == null || value === '') return undefined;
    const match = typeof value === 'string' && /^(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)(?:\.(\d{1,3}))?Z$/u.exec(value);
    const parsed = match ? new Date(value) : null;
    const canonical = match ? `${match[1]}.${(match[2] || '').padEnd(3, '0')}Z` : null;
    if (!parsed || !Number.isFinite(parsed.getTime()) || parsed.toISOString() !== canonical)
      throw new ServiceError(400, 'VALIDATION_ERROR', 'Khoảng thời gian UTC không hợp lệ.', { [name]: ['Dùng thời gian ISO UTC.'] });
    return value;
  };

  return Object.freeze({
    // EP-076: GET /reports/asset-inventory
    assetInventory({ page = 1, pageSize = 20, keyword, assetTypeId, departmentId, status, purchasedFrom, purchasedTo, sortBy, sortDirection, includeArchived } = {}) {
      requireGrant('reports.assets.read');
      if (typeof sortBy === 'string' && sortBy.toLowerCase() === 'purchasecost')
        requireGrant('reports.cost.read', 'Yêu cầu quyền xem chi phí để sắp xếp theo giá mua.');

      const pFrom = dateOnly(purchasedFrom, 'purchasedFrom');
      const pTo = dateOnly(purchasedTo, 'purchasedTo');
      if (pFrom && pTo && pFrom > pTo)
        throw new ServiceError(400, 'VALIDATION_ERROR', 'Ngày mua bắt đầu lớn hơn kết thúc.', { purchasedTo: ['Chọn ngày kết thúc không sớm hơn bắt đầu.'] });

      return request(`/reports/asset-inventory${queryString({
        page: pageParam(page),
        pageSize: pageSizeParam(pageSize),
        keyword: keyword?.trim() || undefined,
        assetTypeId: positiveId(assetTypeId, 'assetTypeId', 'Loại tài sản'),
        departmentId: positiveId(departmentId, 'departmentId', 'Phòng ban'),
        status: status || undefined,
        purchasedFrom: pFrom,
        purchasedTo: pTo,
        sortBy: sortBy || undefined,
        sortDirection: sortDirection || undefined,
        includeArchived: includeArchived ? true : undefined
      })}`);
    },

    // EP-077: GET /reports/assets-by-department
    assetsByDepartment({ status, assetTypeId, departmentId } = {}) {
      requireGrant('reports.assets.read');
      return request(`/reports/assets-by-department${queryString({
        status: status || undefined,
        assetTypeId: positiveId(assetTypeId, 'assetTypeId', 'Loại tài sản'),
        departmentId: positiveId(departmentId, 'departmentId', 'Phòng ban')
      })}`);
    },

    // EP-078: GET /reports/assets-by-type
    assetsByType({ status, departmentId, assetTypeId } = {}) {
      requireGrant('reports.assets.read');
      return request(`/reports/assets-by-type${queryString({
        status: status || undefined,
        departmentId: positiveId(departmentId, 'departmentId', 'Phòng ban'),
        assetTypeId: positiveId(assetTypeId, 'assetTypeId', 'Loại tài sản')
      })}`);
    },

    // EP-079: GET /reports/asset-status
    assetStatus({ departmentId, assetTypeId, status } = {}) {
      requireGrant('reports.assets.read');
      return request(`/reports/asset-status${queryString({
        departmentId: positiveId(departmentId, 'departmentId', 'Phòng ban'),
        assetTypeId: positiveId(assetTypeId, 'assetTypeId', 'Loại tài sản'),
        status: status || undefined
      })}`);
    },

    // EP-080: GET /reports/assignment-history
    assignmentHistory({ page = 1, pageSize = 20, fromUtc, toUtc, assetId, userId, departmentId, includeArchived } = {}) {
      requireGrant('reports.assignments.read');
      const from = utc(fromUtc, 'fromUtc'), to = utc(toUtc, 'toUtc');
      if (from && to && Date.parse(from) > Date.parse(to))
        throw new ServiceError(400, 'VALIDATION_ERROR', 'Thời gian bắt đầu sau thời gian kết thúc.', { toUtc: ['Chọn thời gian kết thúc không sớm hơn bắt đầu.'] });

      return request(`/reports/assignment-history${queryString({
        page: pageParam(page),
        pageSize: pageSizeParam(pageSize),
        fromUtc: from,
        toUtc: to,
        assetId: positiveId(assetId, 'assetId', 'Mã tài sản'),
        userId: positiveId(userId, 'userId', 'Người dùng'),
        departmentId: positiveId(departmentId, 'departmentId', 'Phòng ban'),
        includeArchived: includeArchived ? true : undefined
      })}`);
    },

    // EP-081: GET /reports/maintenance-history
    maintenanceHistory({ page = 1, pageSize = 20, fromUtc, toUtc, assetId, technicianId, status, includeArchived } = {}) {
      requireGrant('reports.maintenance.read');
      const from = utc(fromUtc, 'fromUtc'), to = utc(toUtc, 'toUtc');
      if (from && to && Date.parse(from) > Date.parse(to))
        throw new ServiceError(400, 'VALIDATION_ERROR', 'Thời gian bắt đầu sau thời gian kết thúc.', { toUtc: ['Chọn thời gian kết thúc không sớm hơn bắt đầu.'] });

      return request(`/reports/maintenance-history${queryString({
        page: pageParam(page),
        pageSize: pageSizeParam(pageSize),
        fromUtc: from,
        toUtc: to,
        assetId: positiveId(assetId, 'assetId', 'Mã tài sản'),
        technicianId: positiveId(technicianId, 'technicianId', 'Kỹ thuật viên'),
        status: status || undefined,
        includeArchived: includeArchived ? true : undefined
      })}`);
    },

    // EP-082: GET /reports/maintenance-cost
    maintenanceCost({ fromUtc, toUtc, departmentId, assetTypeId, groupBy = 'month' } = {}) {
      requireGrant('reports.cost.read');
      if (!['month', 'type', 'department'].includes(groupBy))
        throw new ServiceError(400, 'VALIDATION_ERROR', 'Nhóm thống kê chi phí không hợp lệ.', { groupBy: ['Chỉ chọn month, type hoặc department.'] });

      const from = utc(fromUtc, 'fromUtc'), to = utc(toUtc, 'toUtc');
      if (from && to && Date.parse(from) > Date.parse(to))
        throw new ServiceError(400, 'VALIDATION_ERROR', 'Thời gian bắt đầu sau thời gian kết thúc.', { toUtc: ['Chọn thời gian kết thúc không sớm hơn bắt đầu.'] });

      return request(`/reports/maintenance-cost${queryString({
        fromUtc: from,
        toUtc: to,
        departmentId: positiveId(departmentId, 'departmentId', 'Phòng ban'),
        assetTypeId: positiveId(assetTypeId, 'assetTypeId', 'Loại tài sản'),
        groupBy
      })}`);
    },

    // EP-083: GET /reports/warranty-expiration
    warrantyExpiration({ page = 1, pageSize = 20, fromDate, toDate, departmentId, assetTypeId } = {}) {
      requireGrant('reports.assets.read');
      const fDate = dateOnly(fromDate, 'fromDate');
      const tDate = dateOnly(toDate, 'toDate');
      if (fDate && tDate && fDate > tDate)
        throw new ServiceError(400, 'VALIDATION_ERROR', 'Ngày bắt đầu lớn hơn kết thúc.', { toDate: ['Chọn ngày kết thúc không sớm hơn bắt đầu.'] });

      return request(`/reports/warranty-expiration${queryString({
        page: pageParam(page),
        pageSize: pageSizeParam(pageSize),
        fromDate: fDate,
        toDate: tDate,
        departmentId: positiveId(departmentId, 'departmentId', 'Phòng ban'),
        assetTypeId: positiveId(assetTypeId, 'assetTypeId', 'Loại tài sản')
      })}`);
    },

    // EP-084: GET /reports/license-expiration
    licenseExpiration({ page = 1, pageSize = 20, fromUtc, toUtc, softwareId, status } = {}) {
      requireGrant('reports.licenses.read');
      const from = utc(fromUtc, 'fromUtc'), to = utc(toUtc, 'toUtc');
      if (from && to && Date.parse(from) > Date.parse(to))
        throw new ServiceError(400, 'VALIDATION_ERROR', 'Thời gian bắt đầu sau thời gian kết thúc.', { toUtc: ['Chọn thời gian kết thúc không sớm hơn bắt đầu.'] });

      return request(`/reports/license-expiration${queryString({
        page: pageParam(page),
        pageSize: pageSizeParam(pageSize),
        fromUtc: from,
        toUtc: to,
        softwareId: positiveId(softwareId, 'softwareId', 'Phần mềm'),
        status: status || undefined
      })}`);
    },

    // EP-085: GET /reports/replacement-recommendations
    replacementRecommendations({ page = 1, pageSize = 20, generatedFromUtc, generatedToUtc, departmentId, priority, disposition, plannedYear } = {}) {
      requireGrant('reports.replacement.read');
      const from = utc(generatedFromUtc, 'generatedFromUtc'), to = utc(generatedToUtc, 'generatedToUtc');
      if (from && to && Date.parse(from) > Date.parse(to))
        throw new ServiceError(400, 'VALIDATION_ERROR', 'Thời gian bắt đầu sau thời gian kết thúc.', { generatedToUtc: ['Chọn thời gian kết thúc không sớm hơn bắt đầu.'] });

      return request(`/reports/replacement-recommendations${queryString({
        page: pageParam(page),
        pageSize: pageSizeParam(pageSize),
        generatedFromUtc: from,
        generatedToUtc: to,
        departmentId: positiveId(departmentId, 'departmentId', 'Phòng ban'),
        priority: priority || undefined,
        disposition: disposition || undefined,
        plannedYear: plannedYear || undefined
      })}`);
    },

    // EP-086: GET /reports/replacement-budget
    replacementBudget({ year, departmentId, priority } = {}) {
      requireGrant('reports.cost.read');
      if (!Number.isSafeInteger(year) || year < 2000 || year > 2100)
        throw new ServiceError(400, 'VALIDATION_ERROR', 'Năm ngân sách không hợp lệ.', { year: ['Năm phải từ 2000 đến 2100.'] });

      return request(`/reports/replacement-budget${queryString({
        year,
        departmentId: positiveId(departmentId, 'departmentId', 'Phòng ban'),
        priority: priority || undefined
      })}`);
    }
  });
}
