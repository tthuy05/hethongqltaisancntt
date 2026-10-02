import { h } from './utils/dom.js';
import { icon } from './icons.js';

export function button(label, { kind = 'primary', iconName, onClick, className = '', ...props } = {}) {
  return h('button', { type: 'button', className: `button ${kind} ${className}`.trim(), onClick, ...props },
    iconName ? icon(iconName, 18) : null,
    label ? h('span', {}, label) : null);
}

export function pageHeader(title, description, { actions = [], eyebrow = '' } = {}) {
  return h('div', { className: 'page-header' },
    h('div', { className: 'page-heading' },
      eyebrow ? h('p', { className: 'eyebrow' }, eyebrow) : null,
      h('h1', {}, title), description ? h('p', { className: 'meta' }, description) : null),
    actions.length ? h('div', { className: 'actions' }, actions) : null);
}

export function statCard({ label, value, detail, iconName = 'devices', tone = 'blue' }) {
  return h('section', { className: `card stat-card ${tone}` },
    h('div', { className: 'stat-heading' }, h('span', { className: 'stat-label' }, label),
      h('span', { className: `stat-icon ${tone}` }, icon(iconName, 21))),
    h('p', { className: 'stat-value' }, value),
    detail ? h('p', { className: 'stat-detail' }, detail) : null);
}

const statuses = {
  InStock: ['Trong kho', 'neutral'], IN_STOCK: ['Trong kho', 'neutral'],
  InUse: ['Đang sử dụng', 'green'], IN_USE: ['Đang sử dụng', 'green'],
  Maintenance: ['Đang bảo trì', 'amber'], MAINTENANCE: ['Đang bảo trì', 'amber'],
  Broken: ['Hỏng / cần xử lý', 'red'], BROKEN: ['Hỏng / cần xử lý', 'red'],
  Retired: ['Ngừng sử dụng', 'neutral'], RETIRED: ['Ngừng sử dụng', 'neutral'],
  Archived: ['Đã lưu trữ', 'neutral'], archived: ['Đã lưu trữ', 'neutral'],
  active: ['Đang hoạt động', 'green'], inactive: ['Tạm ngưng', 'neutral'],
  Pending: ['Chờ xử lý', 'amber'], InProgress: ['Đang xử lý', 'blue'],
  Resolved: ['Đã hoàn tất', 'green'], Failed: ['Không thành công', 'red'], Cancelled: ['Đã hủy', 'neutral'],
};

export function badge(status) {
  const normalized = typeof status === 'boolean' ? (status ? 'active' : 'inactive') : status;
  const [label, tone] = statuses[normalized] || [normalized || 'Chưa cập nhật', 'neutral'];
  return h('span', { className: `badge ${tone}` }, h('span', { className: 'badge-dot' }), label);
}

export function dataTable({ columns, rows, emptyMessage = 'Không có dữ liệu phù hợp.', label = 'Danh sách dữ liệu' }) {
  const table = h('table', { className: 'data-table', 'aria-label': label },
    h('thead', {}, h('tr', {}, columns.map(column => h('th', {
      scope: 'col', className: column.className || '',
    }, column.label)))),
    h('tbody', {}, rows.length ? rows.map((row, index) => h('tr', { dataset: { id: row.id ?? index } },
      columns.map(column => h('td', { className: column.className || '' },
        column.render ? column.render(row, index) : row[column.key] ?? '—'))))
      : h('tr', {}, h('td', { colSpan: columns.length, className: 'table-empty' }, emptyMessage))));
  return h('div', { className: 'table-wrap', tabIndex: 0, 'aria-label': 'Bảng dữ liệu, có thể cuộn ngang' }, table);
}

export function pagination(meta, onChange) {
  const totalItems = Math.max(0, Number(meta.totalItems ?? meta.totalCount) || 0);
  const pageSize = Math.max(1, Number(meta.pageSize) || 10);
  const totalPages = Math.max(1, Number(meta.totalPages) || Math.ceil(totalItems / pageSize));
  const page = Math.max(1, Number(meta.page) || 1);
  const hasItems = totalItems > 0 && page <= totalPages;
  const first = hasItems ? (page - 1) * pageSize + 1 : 0;
  const last = hasItems ? Math.min(page * pageSize, totalItems) : 0;
  const pages = [...new Set([1, page - 1, page, page + 1, totalPages])]
    .filter(item => item > 0 && item <= totalPages).sort((a, b) => a - b);
  const controls = [];
  let previous = 0;
  for (const number of pages) {
    if (previous && number - previous > 1) controls.push(h('span', { className: 'pagination-ellipsis', 'aria-hidden': 'true' }, '…'));
    controls.push(button(String(number), {
      kind: number === page ? 'primary page-button' : 'ghost page-button',
      'aria-label': `Trang ${number}`, 'aria-current': number === page ? 'page' : null,
      onClick: () => onChange(number),
    }));
    previous = number;
  }
  return h('div', { className: 'pagination' },
    h('p', { className: 'meta', role: 'status' }, `Hiển thị ${first}–${last} trong ${totalItems} mục`),
    h('nav', { className: 'pagination-controls', 'aria-label': 'Phân trang' },
      button('', { kind: 'ghost page-button', iconName: 'chevron_left', 'aria-label': 'Trang trước',
        disabled: page <= 1, onClick: () => onChange(Math.min(page - 1, totalPages)) }),
      controls,
      button('', { kind: 'ghost page-button', iconName: 'chevron_right', 'aria-label': 'Trang sau',
        disabled: page >= totalPages, onClick: () => onChange(page + 1) })));
}

export function loadingState() {
  return h('div', { className: 'loading-state', role: 'status', 'aria-live': 'polite', 'aria-busy': 'true' },
    h('span', { className: 'spinner', 'aria-hidden': 'true' }), h('p', {}, 'Đang tải dữ liệu…'));
}

export function emptyState({ title = 'Chưa có dữ liệu', description = '', action = null } = {}) {
  return h('div', { className: 'empty-state' },
    h('span', { className: 'state-icon' }, icon('folder', 30)),
    h('h2', {}, title), description ? h('p', { className: 'meta' }, description) : null,
    action);
}

export function errorState(error, onRetry) {
  return h('div', { className: 'error-state', role: 'alert' },
    h('span', { className: 'state-icon red' }, icon('warning', 30)),
    h('h2', {}, 'Chưa thể tải dữ liệu'),
    h('p', { className: 'meta' }, error?.userMessage || error?.message || 'Vui lòng thử lại sau.'),
    onRetry ? button('Thử lại', { kind: 'secondary', iconName: 'refresh', onClick: onRetry }) : null);
}

export function showToast(message, type = 'success') {
  let container = document.getElementById('app-toasts');
  if (!container) {
    container = h('div', { id: 'app-toasts', className: 'toast-container', 'aria-label': 'Thông báo' });
    document.body.append(container);
  }
  const isError = type === 'error';
  const toast = h('div', { className: `toast ${type}`, role: isError ? 'alert' : 'status' },
    icon(isError ? 'error' : type === 'warning' ? 'warning' : type === 'info' ? 'info' : 'check_circle'),
    h('p', {}, message), button('', { kind: 'ghost icon-button', iconName: 'close',
      'aria-label': 'Đóng thông báo', onClick: () => toast.remove() }));
  container.append(toast);
  while (container.children.length > 4) container.firstElementChild.remove();
  window.setTimeout(() => toast.remove(), isError ? 7000 : 4500);
  return toast;
}
