import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';
import { activationPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/activate.js';
import { isActivationToken, validateActivation } from '../src/ItAssetManagement.Api/wwwroot/js/utils/validation.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const token = 'A'.repeat(43), password = '  Synthetic-password-2026!  ';
const dto = () => ({ token, password, confirmPassword: password });
const controls = root => ({ inputs: descendants(root).filter(n => n.tagName === 'INPUT'),
  submit: descendants(root).find(n => n.tagName === 'BUTTON' && n.getAttribute('type') === 'submit'),
  form: descendants(root).find(n => n.tagName === 'FORM') });
function fill(root) { const { inputs } = controls(root); inputs.forEach(input => { input.value = password; }); }
test('activation token validator matches canonical32-byte Base64URL and rejects bad padding/characters/length', () => {
  for (let seed = 0; seed < 64; seed++) assert.equal(isActivationToken(Buffer.from(Array.from({ length: 32 }, (_, i) => seed + i)).toString('base64url')), true);
  for (const value of [null, 1, '', 'A'.repeat(42), 'A'.repeat(44), 'A'.repeat(42) + 'B', token + '=', '+' + token.slice(1), '/' + token.slice(1), 'đ' + token.slice(1)]) assert.equal(isActivationToken(value), false);
  assert.deepEqual(validateActivation(dto()), {});
});
test('activation transport is public no-store POST, sends exact untrimmed password contract and no bearer', async () => {
  const calls = []; const services = createApiServices({ fetchImpl: async (url, options) => { calls.push({ url, options }); return Response.json({ message: 'Fixture activated' }); } });
  const input = dto(); await services.auth.activate(input);
  assert.equal(calls.length, 1); assert.equal(calls[0].url, '/api/v1/auth/activate'); assert.equal(calls[0].options.method, 'POST');
  assert.equal(calls[0].options.cache, 'no-store'); assert.equal(calls[0].options.headers.Authorization, undefined);
  assert.deepEqual(JSON.parse(calls[0].options.body), dto()); assert.deepEqual(input, dto()); assert.equal(services.auth.session, null);
});
test('invalid activation DTO or overposting is rejected before fetch without echoing passwords/tokens', async () => {
  let calls = 0; const services = createApiServices({ fetchImpl: async () => { calls++; throw Error('must not fetch'); } });
  for (const input of [null, [], {}, { ...dto(), token: token.slice(1) }, { ...dto(), password: 'short' }, { ...dto(), password: 'x'.repeat(257) },
    { ...dto(), password: ' '.repeat(12), confirmPassword: ' '.repeat(12) }, { ...dto(), confirmPassword: 'different' }, { ...dto(), isActive: true }, { ...dto(), roleIds: [1] }]) {
    await assert.rejects(services.auth.activate(input), error => error.status === 400 && !JSON.stringify(error).includes(password) && !JSON.stringify(error).includes(token));
  }
  assert.equal(calls, 0);
});
test('missing, malformed or duplicate URL token is scrubbed and form disabled without any request', () => {
  for (const suffix of ['', '?token=bad', '?token=' + token + '&token=' + token]) {
    const dom = installDom('/activate' + suffix); try {
      let calls = 0; const root = activationPage({ mode: 'api', auth: { activate: async () => { calls++; } } }); dom.body.append(root);
      assert.equal(location.hash, '#/activate'); assert.ok(!root.textContent.includes(token));
      const { submit, inputs, form } = controls(root); assert.equal(submit.disabled, true); assert.ok(inputs.every(input => input.disabled));
      fill(root); form.fire('submit'); assert.equal(calls, 0);
    } finally { dom.restore(); }
  }
});
test('activation single-flights and locks password editing, then releases owned secret references and DOM values', async () => {
  const dom = installDom('/activate?token=' + token); try {
    let finish, retained, snapshot, calls = 0;
    const root = activationPage({ mode: 'api', auth: { activate: input => { calls++; retained = input; snapshot = structuredClone(input); return new Promise(resolve => { finish = resolve; }); } } }); dom.body.append(root);
    fill(root); const { form, inputs } = controls(root); form.fire('submit'); form.fire('submit'); assert.equal(calls, 1);
    assert.ok(inputs.every(input => input.readOnly)); assert.deepEqual(snapshot, dto()); finish();
    await until(() => root.textContent.includes('Kích hoạt thành công')); assert.deepEqual(retained, { token: '', password: '', confirmPassword: '' });
    assert.ok(inputs.every(input => input.value === '')); form.fire('submit'); assert.equal(calls, 1);
    assert.ok(!root.textContent.includes(token)); assert.equal(location.hash, '#/activate');
  } finally { dom.restore(); }
});
test('expired or consumed invitation closes the form without exposing server details or retrying', async () => {
  const dom = installDom('/activate?token=' + token); try {
    let calls = 0; const root = activationPage({ mode: 'api', auth: { activate: async () => { calls++; throw Object.assign(new Error('synthetic-secret'), { status: 400, code: 'ACTIVATION_INVALID_OR_EXPIRED' }); } } }); dom.body.append(root);
    fill(root); const { form, submit, inputs } = controls(root); form.fire('submit'); await until(() => submit.disabled && inputs.every(input => input.value === ''));
    assert.match(root.textContent, /yêu cầu Admin gửi lại/); assert.doesNotMatch(root.textContent, /synthetic-secret/);
    form.fire('submit'); assert.equal(calls, 1);
  } finally { dom.restore(); }
});
test('uncertain activation outcome blocks replay and offers explicit login without claiming success or failure', async () => {
  for (const status of [0, 503, 401]) {
    const dom = installDom('/activate?token=' + token); try {
      let calls = 0; const root = activationPage({ mode: 'api', auth: { activate: async () => { calls++; throw Object.assign(new Error('synthetic-secret'), { status, code: status === 401 ? 'SESSION_CHANGED' : 'FIXTURE_ERROR' }); } } }); dom.body.append(root);
      fill(root); const { form, submit, inputs } = controls(root); form.fire('submit'); await until(() => root.textContent.includes('server có thể đã xử lý'));
      assert.ok(inputs.every(input => input.value === '')); assert.equal(submit.disabled, true); form.fire('submit'); assert.equal(calls, 1);
      assert.doesNotMatch(root.textContent, /Kích hoạt thành công|synthetic-secret/); assert.equal(location.hash, '#/activate');
      descendants(root).find(n => n.tagName === 'BUTTON' && n.textContent === 'Đến trang đăng nhập').fire('click'); assert.match(location.hash, /^#?\/login$/);
    } finally { dom.restore(); }
  }
});
test('known validation failure clears passwords but allows correcting them with the same unexposed token', async () => {
  const dom = installDom('/activate?token=' + token); try {
    let calls = 0; const root = activationPage({ mode: 'api', auth: { activate: async () => { if (++calls === 1) throw Object.assign(new Error('synthetic-secret'), { status: 400, code: 'VALIDATION_ERROR' }); } } }); dom.body.append(root);
    fill(root); const { form, submit, inputs } = controls(root); form.fire('submit'); await until(() => !submit.disabled);
    assert.ok(inputs.every(input => input.value === '' && !input.disabled)); assert.doesNotMatch(root.textContent, /synthetic-secret/);
    fill(root); form.fire('submit'); await until(() => root.textContent.includes('Kích hoạt thành công')); assert.equal(calls, 2);
  } finally { dom.restore(); }
});
test('navigation or detached activation suppresses late UI changes but always clears password values', async () => {
  const dom = installDom('/activate?token=' + token); try {
    let finish; const root = activationPage({ mode: 'api', auth: { activate: () => new Promise(resolve => { finish = resolve; }) } }); dom.body.append(root);
    fill(root); const { form, inputs } = controls(root); form.fire('submit'); location.hash = '#/login'; finish();
    await until(() => inputs.every(input => input.value === '')); assert.doesNotMatch(root.textContent, /Kích hoạt thành công/); assert.equal(location.hash, '#/login'); root.remove();
  } finally { dom.restore(); }
});
test('demo mode cannot submit activation even with a well-formed synthetic token', () => {
  const dom = installDom('/activate?token=' + token); try {
    let calls = 0; const root = activationPage({ mode: 'mock', auth: { activate: async () => { calls++; } } }); dom.body.append(root);
    const { form, submit } = controls(root); assert.equal(submit.disabled, true); fill(root); form.fire('submit'); assert.equal(calls, 0);
  } finally { dom.restore(); }
});
