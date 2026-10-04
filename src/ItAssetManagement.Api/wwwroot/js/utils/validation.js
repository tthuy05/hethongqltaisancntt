import { ASSET_METADATA_FIELDS } from '../models/contracts.js';

const present = (value) => value !== undefined && value !== null && value !== '';
const isPositiveId = (value) => Number.isSafeInteger(Number(value)) && Number(value) > 0;
const error = (errors, field, message) => { errors[field] = [message]; };

function text(errors, dto, field, max, required = false) {
  const value = dto[field];
  if (required && (!present(value) || typeof value !== 'string' || !value.trim())) {
    error(errors, field, 'Vui lòng nhập thông tin bắt buộc.');
  } else if (present(value) && (typeof value !== 'string' || value.trim().length > max)) {
    error(errors, field, `Tối đa ${max} ký tự.`);
  }
}

function dateOnly(value) {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const date = new Date(`${value}T00:00:00.000Z`);
  return Number.isFinite(date.getTime()) && date.toISOString().slice(0, 10) === value;
}

// Preserve the decimal text until validation; never silently round a numeric(18,2) input.
function sameDecimalAfterNumberConversion(value) {
  const cents = raw => {
    const text = String(raw).trim();
    if (!/^\d{1,16}(?:\.\d{1,2})?$/.test(text)) return null;
    const [whole, fraction = ''] = text.split('.');
    return BigInt(whole) * 100n + BigInt(fraction.padEnd(2, '0'));
  };
  const original = cents(value);
  return original !== null && original === cents(Number(value));
}

/** Return RFC7807-style field errors, without retaining any field values. */
export function validateAsset(dto = {}, { isUpdate = false } = {}) {
  const errors = {};
  text(errors, dto, 'assetCode', 50, true);
  text(errors, dto, 'name', 200, true);
  for (const field of ['assetTypeId', 'owningDepartmentId']) {
    if (!present(dto[field]) || !isPositiveId(dto[field])) error(errors, field, 'Vui lòng chọn danh mục hợp lệ.');
  }
  for (const [field, length] of Object.entries({ serialNumber: 200, brand: 200, model: 200, specification: 4000, operatingSystem: 250, location: 500, note: 2000 })) {
    text(errors, dto, field, length);
  }
  for (const field of ['purchaseDate', 'warrantyExpirationDate']) {
    if (present(dto[field]) && !dateOnly(dto[field])) error(errors, field, 'Ngày không hợp lệ; dùng yyyy-MM-dd.');
  }
  if (dateOnly(dto.purchaseDate) && dateOnly(dto.warrantyExpirationDate) && dto.warrantyExpirationDate < dto.purchaseDate) {
    error(errors, 'warrantyExpirationDate', 'Ngày hết bảo hành không được trước ngày mua.');
  }
  if (present(dto.purchasePrice)) {
    const money = String(dto.purchasePrice).trim();
    if (!/^\d{1,16}(?:\.\d{1,2})?$/.test(money) || !Number.isFinite(Number(money))) {
      error(errors, 'purchasePrice', 'Giá mua phải không âm, tối đa 16 chữ số nguyên và 2 chữ số thập phân.');
    } else if (!sameDecimalAfterNumberConversion(money)) {
      error(errors, 'purchasePrice', 'Giá trị vượt độ chính xác số của giao diện. Chưa thể gửi giá này mà không mất dữ liệu; cần decimal transport được backend xác nhận.');
    }
  }
  if (isUpdate && (typeof dto.rowVersion !== 'string' || !dto.rowVersion)) error(errors, 'rowVersion', 'Tải lại tài sản để lấy phiên bản hiện tại.');
  const allowed = [...ASSET_METADATA_FIELDS, ...(isUpdate ? ['rowVersion'] : [])];
  for (const field of Object.keys(dto)) if (!allowed.includes(field)) error(errors, field, 'Không được cập nhật trường này qua form thông tin tài sản.');
  return errors;
}

export function validateMaster(dto = {}, options = {}) {
  const settings = typeof options === 'string' ? { kind: options } : options;
  const isUpdate = settings.isUpdate ?? Boolean(dto.rowVersion);
  const kind = ['department', 'departments'].includes(settings.kind ?? 'department') ? 'department' : 'assetType';
  const errors = {};
  text(errors, dto, 'code', 50, true);
  text(errors, dto, 'name', 200, true);
  text(errors, dto, 'description', 1000);
  if (kind === 'department' && present(dto.parentDepartmentId) && !isPositiveId(dto.parentDepartmentId)) {
    error(errors, 'parentDepartmentId', 'Phòng ban cha phải là ID hợp lệ.');
  }
  if (kind !== 'department' && present(dto.defaultUsefulLifeMonths) && !isPositiveId(dto.defaultUsefulLifeMonths)) {
    error(errors, 'defaultUsefulLifeMonths', 'Tuổi thọ phải là số nguyên dương.');
  }
  if (isUpdate && (typeof dto.rowVersion !== 'string' || !dto.rowVersion)) error(errors, 'rowVersion', 'Tải lại danh mục để lấy phiên bản hiện tại.');
  const allowed = ['code', 'name', 'description', ...(kind === 'department' ? ['parentDepartmentId'] : ['defaultUsefulLifeMonths']), ...(isUpdate ? ['rowVersion'] : [])];
  for (const field of Object.keys(dto)) if (!allowed.includes(field)) error(errors, field, 'Trường này không thuộc contract cập nhật danh mục.');
  return errors;
}

export function validateLogin({ email, password } = {}) {
  const errors = {};
  if (typeof email !== 'string' || email.trim().length > 320 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) error(errors, 'email', 'Vui lòng nhập email hợp lệ.');
  if (typeof password !== 'string' || !password.length) error(errors, 'password', 'Vui lòng nhập mật khẩu (chỉ luồng demo).');
  return errors;
}

export function validateUser(dto = {}, { isUpdate = false } = {}) {
  const errors = {};
  for (const [name, max] of [['username', 100], ['email', 320], ['displayName', 200]]) text(errors, dto, name, max, true);
  text(errors, dto, 'employeeCode', 50); text(errors, dto, 'phone', 30);
  if (typeof dto.email === 'string' && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(dto.email.trim())) error(errors, 'email', 'Email không hợp lệ.');
  if (present(dto.departmentId) && !isPositiveId(dto.departmentId)) error(errors, 'departmentId', 'Phòng ban phải là ID hợp lệ hoặc để trống.');
  if (isUpdate) {
    if (typeof dto.rowVersion !== 'string' || !/^[A-Za-z0-9+/]{22}==$/.test(dto.rowVersion)) error(errors, 'rowVersion', 'Tải lại hồ sơ trước khi lưu.');
  } else if (typeof dto.password !== 'string' || !dto.password.trim() || dto.password.length < 12 || dto.password.length > 256) error(errors, 'password', 'Mật khẩu phải có 12–256 ký tự, không chỉ gồm khoảng trắng.');
  const allowed = ['username', 'email', 'displayName', 'employeeCode', 'departmentId', 'phone', isUpdate ? 'rowVersion' : 'password'];
  for (const key of Object.keys(dto)) if (!allowed.includes(key)) error(errors, key, 'Trường này không thuộc hồ sơ người dùng.');
  return errors;
}
export function validateUserStatus(dto = {}) {
  const errors = {};
  if (!['Active', 'Inactive', 'Locked', 'Unlocked'].includes(dto.status)) error(errors, 'status', 'Chọn một lệnh trạng thái hợp lệ.');
  text(errors, dto, 'reason', 1000, true);
  if (typeof dto.rowVersion !== 'string' || !/^[A-Za-z0-9+/]{22}==$/.test(dto.rowVersion)) error(errors, 'rowVersion', 'Tải lại trạng thái tài khoản.');
  for (const key of Object.keys(dto)) if (!['status', 'reason', 'rowVersion'].includes(key)) error(errors, key, 'Trường không thuộc lệnh trạng thái.');
  return errors;
}
export function validateUserRoles(dto = {}) {
  const errors = {};
  if (!Array.isArray(dto.roleIds) || dto.roleIds.length > 3 || dto.roleIds.some(id => !Number.isSafeInteger(id) || id <= 0) || new Set(dto.roleIds).size !== dto.roleIds.length) error(errors, 'roleIds', 'Chọn tối đa 3 ID vai trò hợp lệ; không trùng.');
  if (typeof dto.rowVersion !== 'string' || !/^[A-Za-z0-9+/]{22}==$/.test(dto.rowVersion)) error(errors, 'rowVersion', 'Tải lại quyền của tài khoản.');
  for (const key of Object.keys(dto)) if (!['roleIds', 'rowVersion'].includes(key)) error(errors, key, 'Trường không thuộc lệnh gán vai trò.');
  return errors;
}
