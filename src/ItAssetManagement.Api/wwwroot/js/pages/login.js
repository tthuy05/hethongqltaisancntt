import { h } from '../utils/dom.js';
import { icon } from '../icons.js';
import { button } from '../components.js';
import { field, displayErrors, formAlert, busy, route } from './shared.js';
import { validateLogin } from '../utils/validation.js';

export function loginPage(services, next = '/dashboard') {
  const destination = typeof next === 'string' && next.startsWith('/') && !next.startsWith('//') &&
    !/[\\#\u0000-\u001f\u007f]/.test(next) && next.length <= 2048 ? next : '/dashboard';
  const fields = {
    email: field({ name: 'email', label: 'Email', type: 'email', required: true, maxLength: 320 }),
    password: field({ name: 'password', label: 'Mật khẩu', type: 'password', required: true, maxLength: 256 })
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
    event.preventDefault(); if (submit.disabled || !form.isConnected) return;
    const submittedHash = window.location.hash;
    const active = () => form.isConnected && window.location.hash === submittedHash;
    const email = fields.email.input.value.trim();
    let password = fields.password.input.value;
    const errors = validateLogin({ email, password });
    displayErrors(fields, errors); feedback.replaceChildren();
    if (Object.keys(errors).length) return;
    fields.email.input.readOnly = fields.password.input.readOnly = true; toggle.disabled = true; role.input.disabled = true;
    busy(submit, async () => {
      try {
        await services.auth.login({ email, password, ...(services.mode === 'mock' ? { role: role.input.value } : {}) });
        if (!active()) return;
        await services.auth.me();
        if (active()) route(destination);
      } catch (error) {
        if (!active()) return;
        // The adapter owns session invalidation (401/invalid me contract).
        // A UI catch must never logout a newer attempt/session, and a transient
        // /me network failure is not proof that the server revoked a valid login.
        const message = error?.code === 'SESSION_CHANGED' ? 'Phiên đăng nhập đã thay đổi. Vui lòng mở lại trang.' : error?.status === 401
          ? 'Email hoặc mật khẩu không hợp lệ. Liên hệ Admin nếu cần hỗ trợ tài khoản.' : error?.status === 429
            ? 'Quá nhiều yêu cầu đăng nhập. Vui lòng chờ rồi thử lại.' : error?.status === 400
              ? 'Thông tin đăng nhập không hợp lệ. Vui lòng kiểm tra email và mật khẩu.' : 'Chưa hoàn tất xác minh đăng nhập. Kiểm tra kết nối rồi thử lại.';
        feedback.replaceChildren(formAlert(message));
      } finally {
        password = ''; fields.password.input.value = ''; fields.password.input.type = 'password';
        toggle.textContent = 'Hiện'; toggle.setAttribute('aria-pressed', 'false'); toggle.disabled = false;
        fields.email.input.readOnly = fields.password.input.readOnly = false; role.input.disabled = false;
      }
    });
  } }, fields.email.element, fields.password.element, services.mode === 'mock' ? role.element : null, feedback, submit);
  const art = h('div', { className: 'login-art', 'aria-hidden': 'true' }, h('div', { className: 'login-illustration' }, h('img', { src: './assets/logo.svg', alt: '', width: 80, height: 80 }), h('div', { className: 'illustration-row' }, icon('laptop', 48), icon('monitor', 48), icon('server', 48))), h('h2', {}, 'Mọi tài sản. Một nơi quản lý.'), h('p', {}, 'Theo dõi thiết bị, kết nối phòng ban và chăm sóc hạ tầng CNTT một cách rõ ràng, gọn gàng.'), h('div', { className: 'login-points' }, h('span', {}, icon('check', 18), 'Thông tin tập trung'), h('span', {}, icon('check', 18), 'Quy trình nhất quán')));
  return h('main', { id: 'main-content', className: 'login-page' }, h('div', { className: 'login-card' }, art, h('section', { className: 'login-panel' }, h('div', { className: 'brand' }, h('img', { src: './assets/logo.svg', alt: '', width: 42, height: 42 }), h('span', {}, 'Quản lý tài sản CNTT')), h('h1', {}, 'Chào mừng bạn trở lại'), h('p', { className: 'meta' }, services.mode === 'mock' ? 'Khám phá giao diện trước khi kết nối hệ thống.' : 'Đăng nhập để tiếp tục quản lý tài sản.'), services.mode === 'mock' ? h('p', { className: 'info-note' }, 'Bản xem trước · Mock development flow. Dùng thông tin thử nghiệm, không nhập mật khẩu thật. Dữ liệu chỉ nằm trong bộ nhớ và sẽ đặt lại khi tải trang.') : null, form, services.mode === 'mock' ? h('p', { className: 'meta login-footnote' }, 'Demo độc lập · Không đăng nhập backend.') : null)));
}
