import { h } from '../utils/dom.js';
import { icon } from '../icons.js';
import { button } from '../components.js';
import { field, displayErrors, formAlert, busy, route } from './shared.js';

export function loginPage(services, next = '/dashboard') {
  const fields = {
    email: field({ name: 'email', label: 'Email', type: 'email', required: true, maxLength: 320 }),
    password: field({ name: 'password', label: 'Mật khẩu', type: 'password', required: true })
  };
  fields.email.input.autocomplete = services.mode === 'mock' ? 'off' : 'username';
  fields.email.input.placeholder = services.mode === 'mock' ? 'ban@example.test' : 'Email công việc';
  fields.password.input.autocomplete = services.mode === 'mock' ? 'off' : 'current-password';
  const toggle = button('Hiện', { kind: 'ghost', onClick: () => {
    const show = fields.password.input.type === 'password';
    fields.password.input.type = show ? 'text' : 'password'; toggle.textContent = show ? 'Ẩn' : 'Hiện';
    toggle.setAttribute('aria-pressed', String(show));
  }, 'aria-label': 'Hiện hoặc ẩn mật khẩu', 'aria-pressed': 'false' });
  fields.password.element.append(toggle);
  const role = field({ name: 'role', label: 'Vai trò xem thử', value: 'ADMIN_IT', options: [
    { value: 'ADMIN_IT', label: 'Admin IT' }, { value: 'SYSTEM_MANAGER', label: 'System Manager' }, { value: 'TECHNICAL_SUPPORT', label: 'Technical Support (chỉ xem)' }
  ], hint: 'Chỉ dùng để kiểm tra UI demo, không phải phân quyền backend.' });
  const feedback = h('div', { 'aria-live': 'polite' });
  const submit = button(services.mode === 'mock' ? 'Vào bản xem thử' : 'Đăng nhập', { type: 'submit', iconName: 'arrow-right' });
  const form = h('form', { className: 'login-form', noValidate: true, onSubmit: event => {
    event.preventDefault(); if (submit.disabled) return;
    const email = fields.email.input.value.trim();
    const password = fields.password.input.value;
    const errors = {};
    if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) errors.email = ['Vui lòng nhập email hợp lệ.'];
    if (email.length > 320) errors.email = ['Email không quá 320 ký tự.'];
    if (!password) errors.password = ['Vui lòng nhập mật khẩu.'];
    displayErrors(fields, errors); feedback.replaceChildren();
    if (Object.keys(errors).length) return;
    busy(submit, async () => {
      try {
        await services.auth.login({ email, password, ...(services.mode === 'mock' ? { role: role.input.value } : {}) });
        await services.auth.me();
        fields.password.input.value = ''; route(next);
      } catch (error) { await services.auth.logout(); fields.password.input.value = ''; feedback.append(formAlert(error.message)); displayErrors(fields, error.errors || {}); }
    });
  } }, fields.email.element, fields.password.element, services.mode === 'mock' ? role.element : null, feedback, submit);
  const art = h('div', { className: 'login-art', 'aria-hidden': 'true' }, h('div', { className: 'login-illustration' }, h('img', { src: './assets/logo.svg', alt: '', width: 80, height: 80 }), h('div', { className: 'illustration-row' }, icon('laptop', 48), icon('monitor', 48), icon('server', 48))), h('h2', {}, 'Mọi tài sản. Một nơi quản lý.'), h('p', {}, 'Theo dõi thiết bị, kết nối phòng ban và chăm sóc hạ tầng CNTT một cách rõ ràng, gọn gàng.'), h('div', { className: 'login-points' }, h('span', {}, icon('check', 18), 'Thông tin tập trung'), h('span', {}, icon('check', 18), 'Quy trình nhất quán')));
  return h('main', { id: 'main-content', className: 'login-page' }, h('div', { className: 'login-card' }, art, h('section', { className: 'login-panel' }, h('div', { className: 'brand' }, h('img', { src: './assets/logo.svg', alt: '', width: 42, height: 42 }), h('span', {}, 'Quản lý tài sản CNTT')), h('h1', {}, 'Chào mừng bạn trở lại'), h('p', { className: 'meta' }, services.mode === 'mock' ? 'Khám phá giao diện trước khi kết nối hệ thống.' : 'Đăng nhập để tiếp tục quản lý tài sản.'), services.mode === 'mock' ? h('p', { className: 'info-note' }, 'Bản xem trước · Mock development flow. Dùng thông tin thử nghiệm, không nhập mật khẩu thật. Dữ liệu chỉ nằm trong bộ nhớ và sẽ đặt lại khi tải trang.') : null, form, services.mode === 'mock' ? h('p', { className: 'meta login-footnote' }, 'Demo độc lập · Không đăng nhập backend.') : null)));
}
