import { ServiceError } from '../services/service-error.js';

const numeric = { page: [1, 2147483647], warrantyPage: [1, 2147483647], licensePage: [1, 2147483647], pageSize: [1, 100],
  departmentId: [1, Number.MAX_SAFE_INTEGER], assetTypeId: [1, Number.MAX_SAFE_INTEGER], softwareId: [1, Number.MAX_SAFE_INTEGER],
  year: [2000, 2100], plannedYear: [2000, 2100] };
const choices = { licenseStatus: ['Expired', 'Expiring', 'Valid'], priority: ['LOW', 'MEDIUM', 'HIGH', 'CRITICAL'],
  disposition: ['ACTIVE', 'PLANNED', 'DISMISSED', 'SUPERSEDED'], groupBy: ['month', 'department', 'type'],
  status: ['IN_STOCK', 'IN_USE', 'MAINTENANCE', 'BROKEN', 'RETIRED'],
  sortBy: ['assetCode', 'name', 'purchaseDate', 'purchaseCost', 'createdAt'], sortDirection: ['asc', 'desc'] };
const dates = ['warrantyFrom', 'warrantyTo'];
const times = ['licenseFrom', 'licenseTo', 'generatedFrom', 'generatedTo'];
const allowed = new Set(['tab', 'keyword', ...Object.keys(numeric), ...Object.keys(choices), ...dates, ...times]);
const invalid = message => new ServiceError(400, 'VALIDATION_ERROR', message);

export function reportNumber(value, fallback) { return value === undefined || value === '' ? fallback : Number(value); }
export function reportUtc(value) {
  if (!value) return undefined;
  const match = /^(\d{4}-\d\d-\d\dT\d\d:\d\d)(?::(\d\d)(?:\.(\d{1,3}))?)?$/u.exec(value);
  if (!match || Number(value.slice(0, 4)) < 1) throw invalid('Thời điểm phải theo định dạng ngày/giờ UTC.');
  const canonical = `${match[1]}:${match[2] || '00'}.${(match[3] || '').padEnd(3, '0')}Z`;
  const parsed = new Date(canonical);
  if (!Number.isFinite(parsed.getTime()) || parsed.toISOString() !== canonical) throw invalid('Ngày hoặc giờ không tồn tại.');
  return canonical;
}

export function validateReportQuery(values) {
  const query = {};
  for (const [name, value] of Object.entries(values)) {
    if (!allowed.has(name)) throw invalid('Bộ lọc không được hỗ trợ.');
    if (value === undefined || value === '') continue;
    if (!['string', 'number'].includes(typeof value) || String(value).length > 200) throw invalid('Bộ lọc không hợp lệ hoặc quá dài.');
    const text = String(value);
    if (numeric[name]) {
      const [min, max] = numeric[name], number = Number(text);
      if (!/^[1-9][0-9]*$/u.test(text) || !Number.isSafeInteger(number) || number < min || number > max)
        throw invalid(name === 'year' || name === 'plannedYear' ? 'Năm phải là số nguyên từ 2000 đến 2100.' : 'Trang, số dòng hoặc ID không hợp lệ.');
    }
    if (choices[name] && !choices[name].includes(text)) throw invalid('Giá trị bộ lọc không được hỗ trợ.');
    if (dates.includes(name)) {
      const parsed = new Date(text + 'T00:00:00.000Z');
      if (!/^\d{4}-\d\d-\d\d$/u.test(text) || Number(text.slice(0, 4)) < 1 || !Number.isFinite(parsed.getTime()) || parsed.toISOString().slice(0, 10) !== text)
        throw invalid('Ngày không tồn tại; dùng định dạng YYYY-MM-DD.');
    }
    if (times.includes(name)) reportUtc(text);
    query[name] = text;
  }
  for (const [from, to, utc] of [['warrantyFrom', 'warrantyTo', false], ['licenseFrom', 'licenseTo', true], ['generatedFrom', 'generatedTo', true]]) {
    if (query[from] && query[to] && (utc ? reportUtc(query[from]) > reportUtc(query[to]) : query[from] > query[to]))
      throw invalid('Thời điểm bắt đầu không được sau thời điểm kết thúc.');
  }
  const size = reportNumber(query.pageSize, 20);
  for (const name of ['page', 'warrantyPage', 'licensePage'])
    if ((reportNumber(query[name], 1) - 1) * size > 2147483647) throw invalid('Trang vượt giới hạn truy vấn.');
  return query;
}

export function readReportQuery() {
  const params = new URLSearchParams(window.location.hash.split('?')[1] || '');
  if ([...params.keys()].some(key => params.getAll(key).length !== 1)) throw invalid('Bộ lọc bị lặp.');
  return validateReportQuery(Object.fromEntries(params));
}
