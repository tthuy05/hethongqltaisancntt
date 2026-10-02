import services from './services/index.js';
import { h } from './utils/dom.js';
import { icon } from './icons.js';
import { button, pageHeader, loadingState, errorState } from './components.js';
import { loginPage } from './pages/login.js';
import { dashboardPage } from './pages/dashboard.js';
import { assetListPage, assetDetailPage } from './pages/assets.js';
import { assetFormPage } from './pages/asset-form.js';
import { masterPage } from './pages/masters.js';
import { can, route, formAlert } from './pages/shared.js';

const app = document.getElementById('app');
document.querySelector('.skip-link')?.addEventListener('click', event => {
  event.preventDefault();
  const main = document.getElementById('main-content');
  if (main) { main.tabIndex = -1; main.focus(); }
});
const menuItems = [
  ['/dashboard', 'Tổng quan', 'dashboard'], ['/assets', 'Tài sản', 'laptop'], ['/departments', 'Phòng ban', 'building'], ['/asset-types', 'Loại tài sản', 'tag'],
  ['/assignments', 'Phân bổ tài sản', 'users'], ['/maintenance', 'Bảo trì', 'wrench'], ['/licenses', 'Phần mềm & License', 'key'], ['/lifecycle', 'Vòng đời tài sản', 'refresh-cw'], ['/reports', 'Báo cáo', 'bar-chart']
];
let generation = 0;
let pendingPath = '/dashboard';
function parsedRoute() {
  const raw = window.location.hash.slice(1) || '/dashboard';
  return raw.split('?')[0].replace(/\/$/, '') || '/dashboard';
}
function shell(path) {
  const user = services.auth.session;
  const roleNames = { ADMIN_IT: 'Admin IT', SYSTEM_MANAGER: 'System Manager', TECHNICAL_SUPPORT: 'Technical Support' };
  const nav = h('nav', { 'aria-label': 'Điều hướng chính' }, menuItems.map(([target, label, iconName]) => h('a', { className: 'nav-link' + (path === target || (target === '/assets' && path.startsWith('/assets/')) ? ' active' : ''), href: '#' + target, ...(path === target || (target === '/assets' && path.startsWith('/assets/')) ? { 'aria-current': 'page' } : {}) }, icon(iconName), h('span', {}, label))));
  const sidebar = h('aside', { className: 'sidebar', id: 'main-sidebar' }, h('a', { className: 'brand', href: '#/dashboard' }, h('img', { src: './assets/logo.svg', alt: '', width: 40, height: 40 }), h('div', {}, h('span', { className: 'brand-title' }, 'Quản lý tài sản CNTT'), h('span', { className: 'brand-subtitle' }, 'Danh mục & hạ tầng'))), nav,
    h('div', { className: 'sidebar-footer' }, h('div', { className: 'avatar' }, icon('user')), h('div', {}, h('div', { className: 'profile-name' }, user?.displayName || 'Người dùng'), h('div', { className: 'profile-role' }, roleNames[user?.roles?.[0]] || ''))));
  const content = h('main', { className: 'main-content', id: 'main-content', tabIndex: -1 }, loadingState());
  const root = h('div', { className: 'app-shell' });
  const menu = button('Menu', { kind: 'ghost', className: 'menu-button', iconName: 'menu', 'aria-controls': 'main-sidebar', 'aria-expanded': 'false', onClick: () => toggleMenu(!root.classList.contains('menu-open')) });
  const overlay = h('button', { className: 'sidebar-overlay', type: 'button', tabIndex: -1, 'aria-label': 'Đóng menu', onClick: () => toggleMenu(false) });
  const viewport = window.matchMedia('(max-width: 1023px)');
  function toggleMenu(open) { root.classList.toggle('menu-open', open); menu.setAttribute('aria-expanded', String(open)); sidebar.inert = viewport.matches && !open; if (open) sidebar.querySelector('a')?.focus(); else menu.focus(); }
  sidebar.inert = viewport.matches;
  root.addEventListener('keydown', event => { if (event.key === 'Escape' && root.classList.contains('menu-open')) toggleMenu(false); });
  nav.addEventListener('click', event => {
    const link = event.target.closest('a');
    if (!viewport.matches || !link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    // Closing makes the sidebar inert before the browser activates the anchor.
    // Navigate explicitly so the selected mobile link cannot lose its default action.
    event.preventDefault();
    const target = link.getAttribute('href').slice(1);
    toggleMenu(false);
    route(target);
  });
  const search = h('input', { className: 'input', type: 'search', maxLength: 200, placeholder: 'Tìm kiếm tài sản…', 'aria-label': 'Tìm kiếm nhanh tài sản' });
  const header = h('header', { className: 'topbar' }, menu, h('form', { className: 'topbar-search', onSubmit: event => { event.preventDefault(); route('/assets?keyword=' + encodeURIComponent(search.value.trim())); } }, icon('search', 18), search), h('div', { className: 'topbar-actions' }, h('span', { className: 'demo-note' }, services.mode === 'mock' ? 'Dữ liệu minh họa · API chưa kết nối' : 'Chế độ API · Không tự dùng mock'), button('Đăng xuất', { kind: 'ghost', iconName: 'log-out', onClick: async () => { await services.auth.logout(); route('/login'); } })));
  root.append(sidebar, overlay, h('div', { className: 'main-shell' }, header, content));
  return { root, content, sidebar, viewport };
}

async function render() {
  const ownGeneration = ++generation;
  const path = parsedRoute();
  if (!services.auth.session || path === '/login') {
    if (path !== '/login') pendingPath = window.location.hash.slice(1) || '/dashboard';
    app.replaceChildren(loginPage(services, pendingPath));
    return;
  }
  const layout = shell(path);
  app.replaceChildren(layout.root);
  try {
    let page;
    if (path === '/dashboard') page = await dashboardPage(services);
    else if (path === '/assets') page = await assetListPage(services);
    else if (path === '/assets/new') {
      if (!can(services, 'assets.create')) throw Object.assign(new Error('Bạn không có quyền tạo tài sản.'), { status: 403 });
      page = await assetFormPage(services);
    } else if (/^\/assets\/\d+\/edit$/.test(path)) {
      if (!can(services, 'assets.update')) throw Object.assign(new Error('Bạn không có quyền sửa tài sản.'), { status: 403 });
      page = await assetFormPage(services, Number(path.split('/')[2]));
    } else if (/^\/assets\/\d+$/.test(path)) page = await assetDetailPage(services, Number(path.split('/')[2]));
    else if (path === '/departments') page = await masterPage(services, 'departments');
    else if (path === '/asset-types') page = await masterPage(services, 'assetTypes');
    else {
      const item = menuItems.find(([target]) => target === path);
      page = h('div', {}, pageHeader(item ? item[1] : 'Không tìm thấy trang', item ? 'Module được lên kế hoạch cho các giai đoạn tiếp theo.' : 'Đường dẫn không tồn tại.'), h('section', { className: 'card' }, h('div', { className: 'card-body empty-state' }, icon(item?.[2] || 'search', 40), h('h2', {}, item ? 'PLANNED — Chưa triển khai' : '404 — Không tìm thấy'), h('p', { className: 'meta' }, item ? 'Chưa có nghiệp vụ, dữ liệu hoặc backend cho module này. Không phải màn hình chức năng đã hoàn thành.' : 'Bạn có thể quay lại danh sách tài sản.'), button('Về tài sản', { kind: 'secondary', onClick: () => route('/assets') }))));
    }
    if (ownGeneration !== generation) return;
    layout.content.replaceChildren(page);
    layout.content.querySelector('h1')?.setAttribute('tabindex', '-1');
    layout.content.querySelector('h1')?.focus({ preventScroll: true });
    window.scrollTo({ top: 0, behavior: 'instant' });
  } catch (error) {
    if (ownGeneration !== generation) return;
    if (error.status === 401) { await services.auth.logout(); pendingPath = path; app.replaceChildren(loginPage(services, pendingPath)); return; }
    layout.content.replaceChildren(pageHeader(error.status === 404 ? 'Không tìm thấy tài sản' : 'Không thể tải nội dung', ''), errorState(error, () => render()), button('Về danh sách', { kind: 'secondary', onClick: () => route('/assets') }));
  }
}

window.addEventListener('hashchange', render);
window.addEventListener('frontend-refresh', render);
window.addEventListener('resize', () => { const sidebar = document.getElementById('main-sidebar'); if (sidebar) sidebar.inert = window.innerWidth < 1024 && !document.querySelector('.app-shell')?.classList.contains('menu-open'); });
render().catch(() => { app.replaceChildren(formAlert('Không thể khởi động giao diện. Vui lòng tải lại trang.')); });
