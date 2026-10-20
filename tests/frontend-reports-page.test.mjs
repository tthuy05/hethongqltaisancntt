import test from 'node:test';
import assert from 'node:assert/strict';
import { reportsPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/reports.js';
import { dashboardPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/dashboard.js';
import { installDom, descendants } from './support/test-dom.mjs';

function fakeServices({ roles = ['ADMIN_IT'], permissions = ['reports.assets.read', 'reports.cost.read', 'dashboard.read', 'dashboard.cost.read'] } = {}) {
  return {
    mode: 'api',
    reportingEnabled: true,
    auth: {
      session: {
        id: 7,
        displayName: 'Test Admin',
        roles,
        permissions
      }
    },
    dashboard: {
      summary: async () => ({
        totalAssets: 10,
        inUse: 5,
        maintenance: 2,
        byStatus: [{ status: 'InUse', count: 5 }, { status: 'Maintenance', count: 2 }],
        recentAssets: []
      })
    },
    dashboardV2: {
      summary: async () => ({
        totalAssets: 10,
        inUse: 5,
        maintenance: 2,
        inStock: 3,
        openTickets: 1,
        activeLicenses: 4,
        pendingReplacements: 1,
        byStatus: [{ status: 'InUse', count: 5 }, { status: 'Maintenance', count: 2 }],
        recentAssets: []
      }),
      alerts: async () => ({
        expiringWarrantiesCount: 2,
        expiringLicensesCount: 1,
        openTicketsCount: 3,
        highPriorityTicketsCount: 1
      }),
      costs: async () => ({
        currency: 'VND',
        totalAssetValue: 500000000,
        maintenanceCostYtd: 15000000,
        totalBudgetForecast: 45000000,
        missingEstimateCount: 0
      })
    },
    reports: {
      assetInventory: async () => ({
        items: [
          { id: 1, assetCode: 'AST-001', name: 'Laptop Dell', assetTypeName: 'Laptop', departmentName: 'IT', currentStatus: 'IN_USE', purchaseDate: '2026-01-01', purchaseCost: 25000000, warrantyEndDate: '2027-01-01' }
        ],
        page: 1,
        pageSize: 20,
        totalItems: 1,
        totalPages: 1
      }),
      assignmentHistory: async () => ({
        items: [
          { id: 1, assetId: 1, assetCode: 'AST-001', assetName: 'Laptop Dell', assignedUserName: 'Nguyen Van A', assignedDepartmentName: 'IT', assignedAtUtc: '2026-01-01T00:00:00Z', returnedAtUtc: null, status: 'ASSIGNED' }
        ],
        page: 1,
        pageSize: 20,
        totalItems: 1,
        totalPages: 1
      }),
      maintenanceHistory: async () => ({
        items: [
          { id: 1, ticketCode: 'TCK-001', assetCode: 'AST-001', title: 'Sửa bàn phím', priority: 'HIGH', status: 'RESOLVED', technicianName: 'Tran Van B', openedAtUtc: '2026-02-01T00:00:00Z', resolvedAtUtc: '2026-02-02T00:00:00Z', cost: 500000 }
        ],
        page: 1,
        pageSize: 20,
        totalItems: 1,
        totalPages: 1
      }),
      warrantyExpiration: async () => ({ items: [] }),
      licenseExpiration: async () => ({ items: [] }),
      maintenanceCost: async () => ({ currency: 'VND', totalCost: 15000000, groups: [] }),
      replacementBudget: async () => ({ year: 2027, currency: 'VND', totalRecommendations: 2, totalBudget: 45000000, missingEstimateCount: 0, byPriority: [] })
    }
  };
}

test('prepared reports remain closed and failed cost queries never display fabricated zero totals', async () => {
  const dom = installDom();
  try {
    const services = fakeServices(); services.reportingEnabled = false;
    await assert.rejects(reportsPage(services), e => e.status === 404);
    services.reportingEnabled = true; window.location.hash = '#/reports?tab=cost';
    services.reports.maintenanceCost = async () => { throw new Error('synthetic failure'); };
    await assert.rejects(reportsPage(services), /synthetic failure/);
  } finally { dom.restore(); }
});

test('reports suppress late data after a session or route change', async () => {
  const dom = installDom();
  try {
    const services = fakeServices(); window.location.hash = '#/reports';
    let release;
    services.reports.assetInventory = () => new Promise(resolve => { release = resolve; });
    const pending = reportsPage(services);
    services.auth.session = { ...services.auth.session, id: 99 }; window.location.hash = '#/assets';
    release({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 });
    await assert.rejects(pending, e => e.code === 'SESSION_CHANGED');
  } finally { dom.restore(); }
});

test('reportsPage denies access to unauthenticated user', async () => {
  const dom = installDom();
  try {
    const services = fakeServices();
    services.auth.session = null;
    await assert.rejects(reportsPage(services), error => error.status === 401);
  } finally {
    dom.restore();
  }
});

test('reportsPage denies access to user without any report permissions', async () => {
  const dom = installDom();
  try {
    const services = fakeServices({ permissions: ['dashboard.read'] });
    await assert.rejects(reportsPage(services), error => error.status === 403);
  } finally {
    dom.restore();
  }
});

test('reportsPage renders inventory tab and masks purchase cost without cost permission', async () => {
  const dom = installDom('/reports?tab=inventory');
  try {
    const services = fakeServices({ permissions: ['reports.assets.read'] });
    const root = await reportsPage(services);
    assert.match(root.textContent, /Kiểm kê tài sản/);
    assert.match(root.textContent, /AST-001/);
    assert.match(root.textContent, /Laptop Dell/);
    // Cost column header should not be present when lacking reports.cost.read
    assert.doesNotMatch(root.textContent, /Giá mua/);
  } finally {
    dom.restore();
  }
});

test('reportsPage shows purchase cost when user has reports.cost.read', async () => {
  const dom = installDom('/reports?tab=inventory');
  try {
    const services = fakeServices({ permissions: ['reports.assets.read', 'reports.cost.read'] });
    const root = await reportsPage(services);
    assert.match(root.textContent, /Kiểm kê tài sản/);
    assert.match(root.textContent, /Giá mua/);
  } finally {
    dom.restore();
  }
});

test('dashboardPage displays operational KPIs and includes financial cards only with cost permission', async () => {
  const dom = installDom('/dashboard');
  try {
    // 1. Without dashboard.cost.read (e.g. Technical Support)
    const supportServices = fakeServices({ permissions: ['dashboard.read'] });
    const supportView = await dashboardPage(supportServices);
    assert.match(supportView.textContent, /Tổng quan hệ thống/);
    assert.match(supportView.textContent, /Tổng tài sản/);
    assert.match(supportView.textContent, /Cảnh báo vận hành/);
    assert.doesNotMatch(supportView.textContent, /Tổng giá trị tài sản/);
    assert.doesNotMatch(supportView.textContent, /Chi phí bảo trì \(YTD\)/);

    // 2. With dashboard.cost.read (e.g. Admin/Manager)
    const adminServices = fakeServices({ permissions: ['dashboard.read', 'dashboard.cost.read'] });
    const adminView = await dashboardPage(adminServices);
    assert.match(adminView.textContent, /Tổng giá trị tài sản/);
    assert.match(adminView.textContent, /Chi phí bảo trì \(YTD\)/);
    assert.match(adminView.textContent, /Dự toán thay thế/);
  } finally {
    dom.restore();
  }
});
