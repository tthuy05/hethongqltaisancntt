/** Frontend contracts mirror docs/api-spec.md, not database column names. */
export const ASSET_STATUSES = Object.freeze(['InStock', 'InUse', 'Maintenance', 'Broken', 'Retired']);
export const ROLE_CODES = Object.freeze(['ADMIN_IT', 'SYSTEM_MANAGER', 'TECHNICAL_SUPPORT']);
export const ASSET_SORT_FIELDS = Object.freeze(['assetCode', 'name', 'status', 'createdAt', 'updatedAt']);
export const ASSET_METADATA_FIELDS = Object.freeze([
  'assetCode', 'name', 'assetTypeId', 'owningDepartmentId', 'serialNumber', 'brand', 'model',
  'specification', 'operatingSystem', 'purchaseDate', 'purchasePrice', 'warrantyExpirationDate', 'location', 'note',
]);

/** @typedef {{id:number, code:string, name:string, isActive:boolean}} ReferenceSummary */
/** @typedef {{id:number, displayName:string, roles:string[], permissions:string[], departmentId:number|null}} CurrentUser */
/** @typedef {{email:string, password:string, role?:string}} LoginRequest role is MOCK ONLY. */
/** @typedef {{accessToken:string, tokenType:'Bearer', expiresAt:string, user:CurrentUser}} LoginResponse */
/** @typedef {{id:number, code:string, name:string, parentDepartmentId:number|null, description:string|null, isActive:boolean, rowVersion:string}} Department */
/** @typedef {{id:number, code:string, name:string, description:string|null, defaultUsefulLifeMonths:number|null, isActive:boolean, rowVersion:string}} AssetType */
/** @typedef {{assetCode:string, name:string, assetTypeId:number, owningDepartmentId:number, serialNumber?:string|null, brand?:string|null, model?:string|null, specification?:string|null, operatingSystem?:string|null, purchaseDate?:string|null, purchasePrice?:number|null, warrantyExpirationDate?:string|null, location?:string|null, note?:string|null}} CreateAssetRequest */
/** @typedef {CreateAssetRequest & {rowVersion:string}} UpdateAssetRequest */
/** @typedef {{id:number, assetCode:string, name:string, serialNumber:string|null, assetType:ReferenceSummary, owningDepartment:ReferenceSummary, status:string, location:string|null, createdAt:string, updatedAt:string|null, purchasePrice?:number|null}} AssetSummary */
/** @typedef {AssetSummary & CreateAssetRequest & {rowVersion:string, isArchived:boolean, currentAssignment:object|null}} AssetDetail */
/** @template T @typedef {{items:T[], page:number, pageSize:number, totalItems:number, totalPages:number}} Paged */
/** @typedef {{keyword?:string, departmentId?:number, assetTypeId?:number, status?:string, page?:number, pageSize?:number, sortBy?:string, sortDirection?:'asc'|'desc'}} AssetQuery */

export function copy(value) {
  return value == null ? value : structuredClone(value);
}

export function rolePermissions(role) {
  const readable = ['assets.read', 'assets.history.read', 'departments.read', 'asset-types.read', 'dashboard.read'];
  if (role === 'TECHNICAL_SUPPORT') return readable;
  const operator = [...readable, 'assets.create', 'assets.update', 'assets.archive', 'assets.status.manage', 'assets.cost.read', 'dashboard.cost.read'];
  if (role === 'SYSTEM_MANAGER') return operator;
  return [...operator, 'departments.create', 'departments.update', 'departments.archive', 'asset-types.create', 'asset-types.update', 'asset-types.archive'];
}
