import { ServiceError } from '../services/service-error.js';

const invalid = () => { throw new ServiceError(502, 'INVALID_API_RESPONSE', 'Dữ liệu license chưa khớp contract hiển thị an toàn.'); };
const positiveId = value => Number.isSafeInteger(value) && value > 0;
const types = ['PER_USER', 'PER_DEVICE', 'VOLUME', 'SUBSCRIPTION', 'OTHER'];
const secretFields = new Set(['licensekey', 'key', 'licensekeyciphertext', 'ciphertext', 'password', 'passwordhash', 'accesstoken']);

// Prepared view contract for owner review, not an EF entity/production API claim.
// Reject leaked secrets anywhere before projecting the response for a page.
export function rejectLicenseSecrets(value) {
  let visited = 0;
  function walk(item, depth) {
    if (++visited > 20000 || depth > 12) invalid();
    if (!item || typeof item !== 'object') return;
    for (const [name, child] of Object.entries(item)) {
      if (secretFields.has(name.toLowerCase())) invalid();
      walk(child, depth + 1);
    }
  }
  walk(value, 0);
}
function text(value, max, optional = false) {
  if (optional && value == null) return null;
  if (typeof value !== 'string' || value.length > max || !optional && !value.trim()) invalid();
  return value;
}
function timestamp(value) {
  if (value == null) return null;
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?Z$/.test(value) ||
      !Number.isFinite(Date.parse(value)) || new Date(value).toISOString().slice(0, 19) !== value.slice(0, 19)) invalid();
  return value;
}
export function licenseView(value) {
  rejectLicenseSecrets(value);
  if (!value || Array.isArray(value) || !positiveId(value.id) || !types.includes(value.licenseType) ||
      !positiveId(value.totalQuantity) || typeof value.isActive !== 'boolean') invalid();
  const used = value.usedQuantity ?? null;
  if (used != null && (!Number.isSafeInteger(used) || used < 0 || used > value.totalQuantity)) invalid();
  if (value.availableQuantity != null && (used == null || value.availableQuantity !== value.totalQuantity - used)) invalid();
  if (value.purchaseCost != null && (typeof value.purchaseCost !== 'number' || !Number.isFinite(value.purchaseCost) || value.purchaseCost < 0)) invalid();
  const maskedKey = value.maskedKey ?? null;
  if (maskedKey != null && (typeof maskedKey !== 'string' || !/^\*{4}-[^\p{Cc}]{4}$/u.test(maskedKey))) invalid();
  const software = value.software == null ? null : Object.freeze({
    id: positiveId(value.software.id) ? value.software.id : invalid(), name: text(value.software.name, 250)
  });
  return Object.freeze({ id: value.id, licenseCode: text(value.licenseCode, 100), software,
    licenseType: value.licenseType, totalQuantity: value.totalQuantity, usedQuantity: used,
    availableQuantity: used == null ? null : value.totalQuantity - used, isActive: value.isActive,
    startsAt: timestamp(value.startsAt), expiresAt: timestamp(value.expiresAt), maskedKey,
    vendor: text(value.vendor, 250, true), notes: text(value.notes, 2000, true),
    purchasedAt: timestamp(value.purchasedAt),
    ...(Object.hasOwn(value, 'purchaseCost') ? { purchaseCost: value.purchaseCost } : {}), status: text(value.status, 50, true),
    rowVersion: text(value.rowVersion, 100, true) });
}
export function licensePageView(value) {
  rejectLicenseSecrets(value);
  if (!value || !Array.isArray(value.items) || !positiveId(value.page) || !positiveId(value.pageSize) || value.pageSize > 100 ||
      !Number.isSafeInteger(value.totalItems) || value.totalItems < 0 || !Number.isSafeInteger(value.totalPages) || value.totalPages < 0 ||
      value.items.length > value.pageSize || value.items.length > value.totalItems ||
      value.items.length > 0 && value.page > value.totalPages || value.totalPages !== Math.ceil(value.totalItems / value.pageSize)) invalid();
  return Object.freeze({ items: Object.freeze(value.items.map(licenseView)), page: value.page, pageSize: value.pageSize,
    totalItems: value.totalItems, totalPages: value.totalPages });
}
