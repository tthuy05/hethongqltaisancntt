import { ServiceError } from './service-error.js';

// Prepared Week 6 reader. The M1 dashboard remains the routed implementation
// until the reviewed Dashboard API is published and enabled deliberately.
export function createDashboardV2Reader({ request, queryString, getSession }) {
  const requireGrant = permission => {
    const session = getSession();
    if (!Number.isSafeInteger(session?.id) || session.id <= 0)
      throw new ServiceError(401, 'UNAUTHENTICATED', 'Vui lòng đăng nhập để xem tổng quan.');
    if (!Array.isArray(session.permissions) || !session.permissions.includes(permission))
      throw new ServiceError(403, 'FORBIDDEN', 'Bạn không có quyền xem dữ liệu này.');
  };
  const department = value => {
    if (value == null || value === '') return undefined;
    if (!Number.isSafeInteger(value) || value <= 0)
      throw new ServiceError(400, 'VALIDATION_ERROR', 'ID phòng ban không hợp lệ.', { departmentId: ['ID phải là số nguyên dương.'] });
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
    summary({ departmentId } = {}) {
      requireGrant('dashboard.read');
      return request(`/dashboard/summary${queryString({ departmentId: department(departmentId) })}`);
    },
    assetGroups({ groupBy = 'status', departmentId } = {}) {
      requireGrant('dashboard.read');
      if (!['department', 'type', 'status'].includes(groupBy))
        throw new ServiceError(400, 'VALIDATION_ERROR', 'Nhóm thống kê không hợp lệ.', { groupBy: ['Chỉ chọn department, type hoặc status.'] });
      return request(`/dashboard/assets${queryString({ groupBy, departmentId: department(departmentId) })}`);
    },
    alerts({ departmentId, daysAhead = 30 } = {}) {
      requireGrant('dashboard.read');
      if (!Number.isSafeInteger(daysAhead) || daysAhead < 1 || daysAhead > 365)
        throw new ServiceError(400, 'VALIDATION_ERROR', 'Khoảng cảnh báo không hợp lệ.', { daysAhead: ['Chọn từ 1 đến 365 ngày.'] });
      return request(`/dashboard/alerts${queryString({ departmentId: department(departmentId), daysAhead })}`);
    },
    costs({ departmentId, fromUtc, toUtc } = {}) {
      requireGrant('dashboard.cost.read');
      const from = utc(fromUtc, 'fromUtc'), to = utc(toUtc, 'toUtc');
      if (from && to && Date.parse(from) > Date.parse(to))
        throw new ServiceError(400, 'VALIDATION_ERROR', 'Thời gian bắt đầu sau thời gian kết thúc.', { toUtc: ['Chọn thời gian kết thúc không sớm hơn bắt đầu.'] });
      return request(`/dashboard/costs${queryString({ departmentId: department(departmentId), fromUtc: from, toUtc: to })}`);
    }
  });
}
