import { h } from '../utils/dom.js';
import { button } from '../components.js';
import { field, formAlert, busy, route } from './shared.js';

export function activationPage(services) {
  const token = new URLSearchParams(window.location.hash.split('?')[1] || '').get('token') || '';
  // The fragment is never sent to HTTP logs; remove it from visible history immediately.
  window.history.replaceState(null, '', window.location.pathname + window.location.search + '#/activate');
  const password = field({ name: 'password', label: 'Mật khẩu mới', type: 'password', required: true, maxLength: 256, hint: '12–256 ký tự.' });
  const confirmation = field({ name: 'confirmPassword', label: 'Nhập lại mật khẩu', type: 'password', required: true, maxLength: 256 });
  password.input.autocomplete = confirmation.input.autocomplete = 'new-password';
  const feedback = h('div', { 'aria-live': 'polite' });
  const submit = button('Kích hoạt tài khoản', { type: 'submit', disabled: !token || services.mode !== 'api' });
  const form = h('form', { noValidate: true, onSubmit: event => {
    event.preventDefault(); if (submit.disabled) return;
    if (password.input.value.length < 12 || password.input.value.length > 256 || !password.input.value.trim()) { feedback.replaceChildren(formAlert('Mật khẩu cần 12–256 ký tự.')); return; }
    if (password.input.value !== confirmation.input.value) { feedback.replaceChildren(formAlert('Mật khẩu nhập lại chưa khớp.')); return; }
    busy(submit, async () => {
      try { await services.auth.activate({ token, password: password.input.value, confirmPassword: confirmation.input.value }); form.replaceChildren(h('p', { role: 'status' }, 'Kích hoạt thành công. Bạn có thể đăng nhập bằng mật khẩu vừa đặt.'), button('Đến trang đăng nhập', { onClick: () => route('/login') })); }
      catch (error) { feedback.replaceChildren(formAlert(error.message), h('p', {}, 'Hãy liên hệ Admin để gửi lại liên kết. Lời mời mới sẽ thay thế lời mời cũ.')); }
      finally { password.input.value = ''; confirmation.input.value = ''; }
    });
  } }, password.element, confirmation.element, feedback, submit);
  return h('main', { className: 'activation-page' }, h('section', { className: 'card' }, h('div', { className: 'card-body' }, h('h1', {}, 'Kích hoạt tài khoản'), h('p', {}, 'Tạo mật khẩu của bạn. Liên kết dùng một lần và hết hạn sau 30 phút.'), !token ? formAlert('Liên kết thiếu token. Vui lòng mở liên kết trong email hoặc yêu cầu Admin gửi lại.') : null, form)));
}
