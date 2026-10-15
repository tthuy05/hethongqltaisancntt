import { ServiceError } from '../services/service-error.js';

const invalid = (field, message) => { throw new ServiceError(400, 'VALIDATION_ERROR', 'Kiểm tra bản chuẩn bị cấp license.', { [field]: [message] }); };
const positive = value => Number.isSafeInteger(value) && value > 0;
const present = value => value != null;

// Internal UI draft, NOT an enabled POST contract. The module owner maps this to
// the approved DTO later; one draft represents one seat, never client quantity.
export function licenseAllocationDraft(input) {
  if (!input || typeof input !== 'object' || Array.isArray(input)) invalid('body', 'Bản chuẩn bị phải là object.');
  for (const field of Object.keys(input)) if (!['licenseId', 'userId', 'assetId', 'note'].includes(field)) invalid(field, 'Trường chưa được hỗ trợ.');
  if (!positive(input.licenseId)) invalid('licenseId', 'License ID phải là số nguyên dương.');
  if (present(input.userId) === present(input.assetId)) invalid('target', 'Chọn đúng một người dùng hoặc tài sản.');
  for (const field of ['userId', 'assetId']) if (present(input[field]) && !positive(input[field])) invalid(field, 'ID phải là số nguyên dương.');
  if (input.note != null && (typeof input.note !== 'string' || input.note.trim().length > 1000)) invalid('note', 'Ghi chú tối đa1000 ký tự.');
  return Object.freeze({ licenseId: input.licenseId, userId: input.userId ?? null, assetId: input.assetId ?? null, note: input.note?.trim() || null });
}
