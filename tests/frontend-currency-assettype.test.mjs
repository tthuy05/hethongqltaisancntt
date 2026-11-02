import test from 'node:test';
import assert from 'node:assert/strict';
import { money, moneyExact, moneyCompact, moneyCell } from '../src/ItAssetManagement.Api/wwwroot/js/utils/dom.js';
import { suggestAssetTypeCode, normalizeAssetTypeCode } from '../src/ItAssetManagement.Api/wwwroot/js/utils/validation.js';
import { statCard } from '../src/ItAssetManagement.Api/wwwroot/js/components.js';
import { dashboardPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/dashboard.js';
import { masterPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/masters.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

// Clean non-breaking spaces (\u00A0) for easy comparison with standard spaces
const cleanNbsp = str => (typeof str === 'string' ? str.replace(/\u00A0/g, ' ') : str);
const findButton = (root, text) => descendants(root).find(n => n.tagName === 'BUTTON' && n.textContent?.includes(text));

test('money() standard currency formatting for tables and detail views', () => {
  assert.equal(money(null), 'Chưa có giá');
  assert.equal(money(undefined), 'Chưa có giá');
  assert.equal(money(''), 'Chưa có giá');
  assert.equal(money('abc'), 'Chưa có giá');

  assert.equal(cleanNbsp(money(0)), '0 ₫');
  assert.equal(cleanNbsp(money('0')), '0 ₫');
  assert.equal(cleanNbsp(money(500000)), '500.000 ₫');
  assert.equal(cleanNbsp(money(527845683.25)), '527.845.683 ₫');
  assert.equal(cleanNbsp(money(1250000000)), '1.250.000.000 ₫');
  assert.equal(cleanNbsp(money(-1000000)), '-1.000.000 ₫');
});

test('moneyExact() exact currency formatting with decimals for tooltip inspection', () => {
  assert.equal(moneyExact(null), 'Chưa có giá');
  assert.equal(moneyExact(undefined), 'Chưa có giá');
  assert.equal(moneyExact(''), 'Chưa có giá');

  assert.equal(cleanNbsp(moneyExact(0)), '0 ₫');
  assert.equal(cleanNbsp(moneyExact(527845683.25)), '527.845.683,25 ₫');
  assert.equal(cleanNbsp(moneyExact(1250000000)), '1.250.000.000 ₫');
  assert.equal(cleanNbsp(moneyExact(-527845683.25)), '-527.845.683,25 ₫');
});

test('moneyCompact() compact currency formatting for dashboard and KPI stat cards', () => {
  assert.equal(moneyCompact(null), 'Chưa có giá');
  assert.equal(moneyCompact(undefined), 'Chưa có giá');
  assert.equal(moneyCompact(''), 'Chưa có giá');

  assert.equal(cleanNbsp(moneyCompact(0)), '0 ₫');
  assert.equal(cleanNbsp(moneyCompact(500000)), '500.000 ₫');
  assert.equal(cleanNbsp(moneyCompact(1500000)), '1,5 triệu ₫');
  assert.equal(cleanNbsp(moneyCompact(527845683.25)), '527,85 triệu ₫');
  assert.equal(cleanNbsp(moneyCompact(1000000000)), '1 tỷ ₫');
  assert.equal(cleanNbsp(moneyCompact(1250000000)), '1,25 tỷ ₫');

  // Negative numbers
  assert.equal(cleanNbsp(moneyCompact(-527845683.25)), '-527,85 triệu ₫');
  assert.equal(cleanNbsp(moneyCompact(-1250000000)), '-1,25 tỷ ₫');
  assert.equal(cleanNbsp(moneyCompact(-500000)), '-500.000 ₫');
});

test('moneyCell() renders a span with tooltip title containing exact money value', () => {
  const dom = installDom();
  try {
    const cellWithDecimals = moneyCell(527845683.25);
    assert.equal(cleanNbsp(cellWithDecimals.getAttribute('title')), '527.845.683,25 ₫');
    assert.equal(cleanNbsp(cellWithDecimals.textContent), '527.845.683 ₫');

    const cellNull = moneyCell(null);
    assert.equal(cellNull.getAttribute('title'), 'Chưa có giá');
    assert.equal(cellNull.textContent, 'Chưa có giá');

    const cellZero = moneyCell(0);
    assert.equal(cleanNbsp(cellZero.getAttribute('title')), '0 ₫');
    assert.equal(cleanNbsp(cellZero.textContent), '0 ₫');
  } finally {
    dom.restore();
  }
});

test('statCard() supports title attribute for native tooltip', () => {
  const dom = installDom();
  try {
    const card = statCard({
      label: 'Tổng giá trị tài sản',
      value: '527,85 triệu ₫',
      title: '527.845.683,25 ₫',
      detail: '4/36 tài sản chưa có giá',
      tone: 'blue'
    });
    const statValue = card.querySelector('.stat-value');
    assert.ok(statValue, 'stat-value element should be present');
    assert.equal(statValue.textContent, '527,85 triệu ₫');
    assert.equal(statValue.getAttribute('title'), '527.845.683,25 ₫');
  } finally {
    dom.restore();
  }
});

test('suggestAssetTypeCode() generates uppercase semantic ASCII codes with underscores', () => {
  assert.equal(suggestAssetTypeCode('Laptop'), 'LAPTOP');
  assert.equal(suggestAssetTypeCode('Desktop'), 'DESKTOP');
  assert.equal(suggestAssetTypeCode('Máy in đa năng'), 'MAY_IN_DA_NANG');
  assert.equal(suggestAssetTypeCode('Màn hình 24 inch'), 'MAN_HINH_24_INCH');
  assert.equal(suggestAssetTypeCode('Thiết bị mạng (Switch / Router)'), 'THIET_BI_MANG_SWITCH_ROUTER');
  assert.equal(suggestAssetTypeCode('  Ổ cứng SSD - Kingston!  '), 'O_CUNG_SSD_KINGSTON');
  assert.equal(suggestAssetTypeCode('Đầu đọc thẻ RFID'), 'DAU_DOC_THE_RFID');
  assert.equal(suggestAssetTypeCode(''), '');
  assert.equal(suggestAssetTypeCode(null), '');
  assert.equal(suggestAssetTypeCode(undefined), '');
});

test('normalizeAssetTypeCode() normalizes manually typed codes', () => {
  assert.equal(normalizeAssetTypeCode('laptop'), 'LAPTOP');
  assert.equal(normalizeAssetTypeCode('desktop_pc-1'), 'DESKTOP_PC_1');
  assert.equal(normalizeAssetTypeCode('  máy in  '), 'MAY_IN');
  assert.equal(normalizeAssetTypeCode('ABC@#$DEF'), 'ABC_DEF');
  assert.equal(normalizeAssetTypeCode('SWITCH_24PORT'), 'SWITCH_24PORT');
  assert.equal(normalizeAssetTypeCode(''), '');
  assert.equal(normalizeAssetTypeCode(null), '');
});

test('dashboardPage displays compact currency, exact title, and missing price warning when missing count > 0', async () => {
  const dom = installDom('/dashboard');
  try {
    const services = {
      mode: 'api',
      reportingEnabled: true,
      auth: {
        session: {
          id: 1,
          displayName: 'Admin User',
          roles: ['ADMIN_IT'],
          permissions: ['dashboard.read', 'dashboard.cost.read']
        }
      },
      dashboard: {
        summary: async () => ({
          totalAssets: 36,
          inUse: 20,
          maintenance: 4,
          byStatus: [],
          recentAssets: []
        })
      },
      dashboardV2: {
        summary: async () => ({
          totalAssets: 36,
          inUse: 20,
          maintenance: 4,
          inStock: 12,
          openTickets: 2,
          activeLicenses: 5,
          pendingReplacements: 1,
          byStatus: [],
          recentAssets: []
        }),
        alerts: async () => ({
          expiringWarrantiesCount: 0,
          expiringLicensesCount: 0,
          openTicketsCount: 0,
          highPriorityTicketsCount: 0
        }),
        costs: async () => ({
          currency: 'VND',
          totalAssetValue: 527845683.25,
          missingPurchaseCostCount: 4,
          assetCount: 36,
          maintenanceCostYtd: 15500000,
          missingMaintenanceCostCountYtd: 1,
          maintenanceTicketCountYtd: 5,
          totalBudgetForecast: 45000000,
          missingEstimateCount: 0
        })
      }
    };

    const root = await dashboardPage(services);
    dom.body.append(root);

    // Verify compact values
    assert.match(root.textContent, /527,85 triệu ₫/);
    assert.match(root.textContent, /15,5 triệu ₫/);
    assert.match(root.textContent, /45 triệu ₫/);

    // Verify tooltip exact titles
    const cards = root.querySelectorAll('.stat-card');
    let foundTotalCard = false;
    for (const card of cards) {
      const val = card.querySelector('.stat-value');
      if (val && val.textContent.includes('527,85 triệu ₫')) {
        foundTotalCard = true;
        assert.equal(cleanNbsp(val.getAttribute('title')), '527.845.683,25 ₫');
      }
    }
    assert.ok(foundTotalCard, 'Total asset card with exact title should be found');

    // Verify warning alert is present outside the cards
    const warning = root.querySelector('.missing-price-alert');
    assert.ok(warning, 'Missing price warning banner should be displayed');
    assert.match(warning.textContent, /4\/36 tài sản chưa có giá mua/);
  } finally {
    dom.restore();
  }
});

test('dashboardPage does not render warning banner when all assets have purchase prices', async () => {
  const dom = installDom('/dashboard');
  try {
    const services = {
      mode: 'api',
      reportingEnabled: true,
      auth: {
        session: {
          id: 1,
          displayName: 'Admin User',
          roles: ['ADMIN_IT'],
          permissions: ['dashboard.read', 'dashboard.cost.read']
        }
      },
      dashboardV2: {
        summary: async () => ({
          totalAssets: 36,
          inUse: 20,
          maintenance: 4,
          inStock: 12,
          openTickets: 2,
          activeLicenses: 5,
          pendingReplacements: 0,
          byStatus: [],
          recentAssets: []
        }),
        alerts: async () => ({
          expiringWarrantiesCount: 0,
          expiringLicensesCount: 0,
          openTicketsCount: 0,
          highPriorityTicketsCount: 0
        }),
        costs: async () => ({
          currency: 'VND',
          totalAssetValue: 602845683.25,
          missingPurchaseCostCount: 0,
          assetCount: 36,
          maintenanceCostYtd: 15500000,
          missingMaintenanceCostCountYtd: 0,
          maintenanceTicketCountYtd: 5,
          totalBudgetForecast: 45000000,
          missingEstimateCount: 0
        })
      }
    };

    const root = await dashboardPage(services);
    dom.body.append(root);

    // Verify compact values
    assert.match(root.textContent, /602,85 triệu ₫/);

    // Verify warning alert is NOT present
    const warning = root.querySelector('.missing-price-alert');
    assert.equal(warning, null, 'Warning banner should NOT be rendered when missingPurchaseCostCount is 0');
  } finally {
    dom.restore();
  }
});

test('asset type create leaves code to the backend', async () => {
  const dom = installDom('/asset-types');
  try {
    let created;
    const services = { mode: 'api', auth: { session: { id: 1, permissions: ['asset-types.create'] } },
      assetTypes: { list: async () => ({items:[],totalPages:1}), create: async dto => { created=dto;return {id:1,code:'LTS000001',...dto}; } } };
    const page=await masterPage(services,'assetTypes');dom.body.append(page);
    findButton(page,'Thêm loại tài sản').fire('click');
    await until(()=>descendants(dom.body).some(n=>n.tagName==='DIALOG'));
    const dialog=descendants(dom.body).find(n=>n.tagName==='DIALOG');
    const inputs=descendants(dialog);
    const code=inputs.find(n=>n.tagName==='INPUT'&&n.getAttribute('name')==='code');
    assert.equal(code.readOnly,true);
    assert.match(dialog.textContent,/LTSxxxxxx/);
    inputs.find(n=>n.tagName==='INPUT'&&n.getAttribute('name')==='name').value='Máy in';
    code.value='FORGED'; // Even a DOM edit is not sent.
    inputs.find(n=>n.tagName==='FORM').fire('submit');
    await until(()=>created);
    assert.equal(created.name,'Máy in');assert.equal(Object.hasOwn(created,'code'),false);
  } finally {dom.restore();}
});
