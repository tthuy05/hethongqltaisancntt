import { h } from '../utils/dom.js';
import { button } from '../components.js';
import { field, formAlert, busy, route } from './shared.js';
import { isActivationToken, validateActivation } from '../utils/validation.js';

export function activationPage(services) {
  const values = new URLSearchParams(window.location.hash.split('?')[1] || '').getAll('token');
  let token = values.length === 1 && isActivationToken(values[0]) ? values[0] : '';
  values.fill('');
  let blocked = false;
  // The fragment is never sent to HTTP logs; remove it from visible history immediately.
  window.history.replaceState(null, '', window.location.pathname + window.location.search + '#/activate');
  const activationHash = window.location.hash;
  const password = field({ name: 'password', label: 'Mật khẩu mới', type: 'password', required: true, maxLength: 256, hint: '12–256 ký tự.' });
  const confirmation = field({ name: 'confirmPassword', label: 'Nhập lại mật khẩu', type: 'password', required: true, maxLength: 256 });
  password.input.autocomplete = confirmation.input.autocomplete = 'new-password';
  const feedback = h('div', { 'aria-live': 'polite' });
  const submit = button('Kích hoạt tài khoản', { type: 'submit', disabled: !token || services.mode !== 'api' });
  password.input.disabled = confirmation.input.disabled = submit.disabled;
  const form = h('form', { noValidate: true, onSubmit: event => {
    event.preventDefault(); if (submit.disabled || blocked || !token || !form.isConnected || window.location.hash !== activationHash) return;
    const dto = { token, password: password.input.value, confirmPassword: confirmation.input.value };
    const errors = validateActivation(dto);
    if (Object.keys(errors).length) { feedback.replaceChildren(formAlert(Object.values(errors).flat().join(' '))); return; }
    const active = () => form.isConnected && window.location.hash === activationHash;
    password.input.readOnly = confirmation.input.readOnly = true;
    feedback.replaceChildren();
    busy(submit, async () => {
      try {
        await services.auth.activate(dto); token = ''; blocked = true;
        if (active()) form.replaceChildren(h('p', { role: 'status' }, 'Kích hoạt thành công. Bạn có thể đăng nhập bằng mật khẩu vừa đặt.'), button('Đến trang đăng nhập', { onClick: () => route('/login') }));
      } catch (error) {
        const invalid = error?.code === 'ACTIVATION_INVALID_OR_EXPIRED';
        const uncertain = ![400, 413, 415, 422, 429].includes(error?.status) || ['API_UNAVAILABLE', 'INVALID_API_RESPONSE', 'SESSION_CHANGED'].includes(error?.code);
        if (invalid || uncertain) { token = ''; blocked = true; }
        if (active()) feedback.replaceChildren(formAlert(invalid ? 'Liên kết không hợp lệ hoặc đã hết hạn. Hãy yêu cầu Admin gửi lại lời mời.' : uncertain
          ? 'Chưa xác nhận được kết quả kích hoạt; server có thể đã xử lý. Không tự gửi lại. Bạn có thể thử đăng nhập bằng mật khẩu vừa đặt hoặc yêu cầu Admin liên kết mới.'
          : error?.status === 429 ? 'Quá nhiều yêu cầu. Vui lòng chờ trước khi thử lại.' : 'Chưa thể kích hoạt. Kiểm tra mật khẩu và nhập lại; liên hệ Admin nếu liên kết không còn hiệu lực.'),
          ...(blocked ? [button('Đến trang đăng nhập', { kind: 'secondary', onClick: () => route('/login') })] : []));
      } finally {
        // Release our references as well as DOM values, even after navigation.
        // JavaScript strings/transport buffers cannot be securely zeroized here.
        dto.password = ''; dto.confirmPassword = ''; dto.token = '';
        password.input.value = ''; confirmation.input.value = '';
        password.input.readOnly = confirmation.input.readOnly = false;
      }
    }).finally(() => { if (blocked) { submit.disabled = true; password.input.disabled = confirmation.input.disabled = true; } });
  } }, password.element, confirmation.element, feedback, submit);
  return h('main', { className: 'activation-page' }, h('section', { className: 'card' }, h('div', { className: 'card-body' }, h('h1', {}, 'Kích hoạt tài khoản'), h('p', {}, 'Tạo mật khẩu của bạn. Liên kết dùng một lần và hết hạn sau 30 phút.'), !token ? formAlert('Liên kết thiếu token hoặc không hợp lệ. Vui lòng mở liên kết trong email hoặc yêu cầu Admin gửi lại.') : null, form)));
}
