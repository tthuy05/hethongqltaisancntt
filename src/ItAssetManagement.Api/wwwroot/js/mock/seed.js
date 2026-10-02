/** Synthetic fixtures only. No person, secret or production identifier is represented. */
export function createSeed(rowVersion) {
  const departments = [
    { id: 1, code: 'IT', name: 'Công nghệ thông tin', parentDepartmentId: null, description: 'Phòng ban demo', isActive: true },
    { id: 2, code: 'HR', name: 'Nhân sự', parentDepartmentId: null, description: null, isActive: true },
    { id: 3, code: 'FIN', name: 'Tài chính', parentDepartmentId: null, description: null, isActive: true },
    { id: 4, code: 'OPS', name: 'Vận hành', parentDepartmentId: null, description: 'Danh mục inactive để kiểm tra lịch sử demo', isActive: false },
  ].map((item) => ({ ...item, rowVersion: rowVersion() }));
  const assetTypes = [
    { id: 1, code: 'LAPTOP', name: 'Laptop', defaultUsefulLifeMonths: 48, isActive: true },
    { id: 2, code: 'DESKTOP', name: 'Máy tính để bàn', defaultUsefulLifeMonths: 60, isActive: true },
    { id: 3, code: 'SERVER', name: 'Máy chủ', defaultUsefulLifeMonths: 60, isActive: true },
    { id: 4, code: 'NETWORK', name: 'Thiết bị mạng', defaultUsefulLifeMonths: 48, isActive: true },
    { id: 5, code: 'PRINTER', name: 'Máy in', defaultUsefulLifeMonths: 60, isActive: false },
  ].map((item) => ({ ...item, description: null, rowVersion: rowVersion() }));
  const statuses = ['InStock', 'InUse', 'InUse', 'InUse', 'Maintenance', 'InStock', 'Broken', 'Retired'];
  const names = ['Dell Latitude 5440', 'HP ProDesk 400 G9', 'Dell PowerEdge R350', 'Cisco Catalyst 9200', 'HP LaserJet Pro'];
  const brands = ['Dell', 'HP', 'Dell', 'Cisco', 'HP'];
  const models = ['Latitude 5440', 'ProDesk 400 G9', 'PowerEdge R350', 'Catalyst 9200', 'LaserJet Pro'];
  const assets = Array.from({ length: 24 }, (_, index) => {
    const type = index % 5;
    const id = index + 1;
    const year = 2021 + index % 5;
    return {
      id, assetCode: `TS-${String(id).padStart(4, '0')}`, name: `${names[type]} · ${String(id).padStart(2, '0')}`,
      assetTypeId: type + 1, owningDepartmentId: index % 4 + 1,
      serialNumber: `DEMO-SN-${String(id).padStart(5, '0')}`, brand: brands[type], model: models[type],
      specification: type === 0 ? 'Intel Core i5 · 16GB RAM · 512GB SSD (dữ liệu demo)' : 'Cấu hình minh họa — chưa lấy từ hệ thống thật',
      operatingSystem: type < 2 ? 'Windows 11 Pro' : type === 2 ? 'Ubuntu Server' : null,
      purchaseDate: `${year}-03-15`, purchasePrice: [23500000, 18000000, 78000000, 42000000, null][type],
      warrantyExpirationDate: `${year + 3}-03-15`, location: `Tầng ${index % 3 + 1} · Khu ${String.fromCharCode(65 + index % 3)}`,
      note: 'MOCK / DEMO DATA — thay đổi chỉ giữ trong bộ nhớ của tab hiện tại.',
      status: statuses[index % statuses.length], createdAt: `2026-09-${String(1 + index).padStart(2, '0')}T08:00:00.000Z`, updatedAt: null,
      rowVersion: rowVersion(), isArchived: false, currentAssignment: null,
    };
  });
  // Private fixtures simulate workflow preconditions; no Assignment/Maintenance module is implemented.
  const activeWorkflowAssetIds = new Set(assets.filter((asset) => ['InUse', 'Maintenance'].includes(asset.status)).map((asset) => asset.id));
  return { departments, assetTypes, assets, activeWorkflowAssetIds };
}
