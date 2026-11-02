/** Small DOM helpers: dynamic content is always text, never executable markup. */
export function h(tag, props = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(props ?? {})) {
    if (value === null || value === undefined) continue;
    if (value === false && !/^(aria|data)[A-Z-]/.test(key) &&
      !['checked', 'selected', 'disabled', 'readOnly', 'multiple', 'required', 'hidden', 'autofocus'].includes(key)) continue;
    if (/^on[A-Za-z]/.test(key)) {
      if (typeof value !== 'function') throw new TypeError('Event handlers must be functions.');
      node.addEventListener(key.slice(2).toLowerCase(), value);
    } else if (key === 'className' || key === 'class') {
      node.className = Array.isArray(value) ? value.filter(Boolean).join(' ') : value;
    } else if (key === 'dataset') {
      for (const [name, dataValue] of Object.entries(value)) {
        if (dataValue !== undefined && dataValue !== null) node.dataset[name] = String(dataValue);
      }
    } else if (key === 'style' && typeof value === 'object') {
      for (const [name, cssValue] of Object.entries(value)) {
        if (name.startsWith('--')) node.style.setProperty(name, String(cssValue));
        else node.style[name] = cssValue;
      }
    } else if (key === 'textContent') {
      node.textContent = String(value);
    } else if (key === 'innerHTML' || key === 'outerHTML') {
      throw new TypeError('HTML injection is not supported. Use children instead.');
    } else if (key === 'ref' && typeof value === 'function') {
      value(node);
    } else if (key === 'value') {
      // Apply after children: a select cannot choose an option before it exists.
    } else if (['checked', 'selected', 'disabled', 'readOnly', 'multiple', 'required', 'hidden', 'autofocus'].includes(key)) {
      node[key] = Boolean(value);
    } else {
      const attribute = key === 'htmlFor' ? 'for'
        : /^(aria|data)[A-Z]/.test(key) ? key.replace(/[A-Z]/g, letter => `-${letter.toLowerCase()}`)
          : key;
      node.setAttribute(attribute, value === true ? '' : String(value));
    }
  }
  append(node, children);
  if (props?.value !== null && props?.value !== undefined) node.value = props.value;
  return node;
}

export function append(parent, children) {
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || typeof child === 'boolean') continue;
    parent.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return parent;
}

export function clear(node) {
  node.replaceChildren();
  return node;
}

export function money(value) {
  if (value === null || value === undefined || value === '' || !Number.isFinite(Number(value))) {
    return 'Chưa có giá';
  }
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(Number(value));
}

export function moneyExact(value) {
  if (value === null || value === undefined || value === '' || !Number.isFinite(Number(value))) {
    return 'Chưa có giá';
  }
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', minimumFractionDigits: 0, maximumFractionDigits: 2 }).format(Number(value));
}

export function moneyCompact(value) {
  if (value === null || value === undefined || value === '' || !Number.isFinite(Number(value))) {
    return 'Chưa có giá';
  }
  const num = Number(value);
  const abs = Math.abs(num);
  const sign = num < 0 ? '-' : '';
  const nf = n => new Intl.NumberFormat('vi-VN', { minimumFractionDigits: 0, maximumFractionDigits: 2 }).format(n);

  if (abs >= 999995000) {
    return `${sign}${nf(abs / 1e9)} tỷ ₫`;
  }
  if (abs >= 999950) {
    return `${sign}${nf(abs / 1e6)} triệu ₫`;
  }
  return money(num);
}

export function moneyCell(value) {
  return h('span', { title: moneyExact(value) }, money(value));
}

export function date(value) {
  if (!value) return 'Chưa cập nhật';
  // Business dates are rendered directly; no timezone conversion can shift the day.
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(value));
  if (match) return `${match[3]}/${match[2]}/${match[1]}`;
  const parsed = new Date(value);
  return Number.isNaN(parsed.valueOf()) ? 'Chưa cập nhật' : parsed.toLocaleDateString('vi-VN');
}
