// Local SVG line icons replace remote Material Symbols font requests.
const paths = {
  devices: [['rect', { x: 3, y: 4, width: 14, height: 10, rx: 2 }], ['path', { d: 'M7 20h6M10 14v6' }], ['rect', { x: 17, y: 9, width: 4, height: 11, rx: 1 }]],
  dashboard: [['rect', { x: 3, y: 3, width: 7, height: 7, rx: 1.5 }], ['rect', { x: 14, y: 3, width: 7, height: 7, rx: 1.5 }], ['rect', { x: 3, y: 14, width: 7, height: 7, rx: 1.5 }], ['rect', { x: 14, y: 14, width: 7, height: 7, rx: 1.5 }]],
  laptop: [['rect', { x: 4, y: 4, width: 16, height: 12, rx: 2 }], ['path', { d: 'M2 20h20l-2-4H4l-2 4Z' }]],
  desktop: [['rect', { x: 3, y: 3, width: 18, height: 13, rx: 2 }], ['path', { d: 'M8 21h8M12 16v5' }]],
  apartment: [['path', { d: 'M5 21V3h14v18M2 21h20M9 7h1m4 0h1M9 11h1m4 0h1M9 15h1m4 0h1M10 21v-3h4v3' }]],
  category: [['path', { d: 'm12 3 5 8H7l5-8Z' }], ['circle', { cx: 6, cy: 18, r: 3 }], ['rect', { x: 15, y: 15, width: 6, height: 6, rx: 1 }]],
  assignment: [['rect', { x: 5, y: 4, width: 14, height: 17, rx: 2 }], ['rect', { x: 9, y: 2, width: 6, height: 4, rx: 1 }], ['path', { d: 'm9 14 2 2 4-4' }]],
  maintenance: [['path', { d: 'M14.5 5.5a5 5 0 0 0-6 6L3 17a2.8 2.8 0 0 0 4 4l5.5-5.5a5 5 0 0 0 6-6l-3 3-4-4 3-3Z' }]],
  license: [['rect', { x: 3, y: 4, width: 18, height: 16, rx: 2 }], ['path', { d: 'M3 9h18M7 14h5m-5 3h3m6-3h1' }]],
  lifecycle: [['path', { d: 'M3 10a9 9 0 0 1 16-4l2 2M21 3v5h-5M21 14A9 9 0 0 1 5 20l-2-2M3 21v-5h5' }]],
  chart: [['path', { d: 'M3 3v18h18M7 16v-4m5 4V7m5 9V4' }]],
  person: [['circle', { cx: 12, cy: 8, r: 4 }], ['path', { d: 'M4 21v-2a8 8 0 0 1 16 0v2' }]],
  search: [['circle', { cx: 10.5, cy: 10.5, r: 6.5 }], ['path', { d: 'm16 16 5 5' }]],
  menu: [['path', { d: 'M3 6h18M3 12h18M3 18h18' }]],
  close: [['path', { d: 'm6 6 12 12M6 18 18 6' }]],
  chevron_left: [['path', { d: 'm15 5-7 7 7 7' }]],
  chevron_right: [['path', { d: 'm9 5 7 7-7 7' }]],
  chevron_down: [['path', { d: 'm5 9 7 7 7-7' }]],
  arrow_back: [['path', { d: 'm10 5-7 7 7 7M3 12h18' }]],
  arrow_right: [['path', { d: 'm14 5 7 7-7 7M3 12h18' }]],
  arrow_up: [['path', { d: 'm5 10 7-7 7 7M12 3v18' }]],
  plus: [['path', { d: 'M12 5v14M5 12h14' }]],
  edit: [['path', { d: 'm14 5 5 5M3 21l5-1L21 7a3.5 3.5 0 0 0-5-5L3 15v6Z' }]],
  archive: [['rect', { x: 3, y: 3, width: 18, height: 4, rx: 1 }], ['path', { d: 'M5 7v14h14V7M9 12h6' }]],
  eye: [['path', { d: 'M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z' }], ['circle', { cx: 12, cy: 12, r: 3 }]],
  eye_off: [['path', { d: 'm3 3 18 18M10 5c6.5-1 12 7 12 7s-1.2 2.4-3.5 4.4M6.3 6.3C3.5 8.5 2 12 2 12s3.5 7 10 7c1.5 0 2.8-.3 4-.8M9.9 9.9a3 3 0 0 0 4.2 4.2' }]],
  check: [['path', { d: 'm5 12 4 4L19 6' }]],
  check_circle: [['circle', { cx: 12, cy: 12, r: 9 }], ['path', { d: 'm8 12 3 3 5-6' }]],
  warning: [['path', { d: 'm12 3 10 18H2L12 3ZM12 9v5m0 3h.01' }]],
  error: [['circle', { cx: 12, cy: 12, r: 9 }], ['path', { d: 'M12 7v6m0 4h.01' }]],
  info: [['circle', { cx: 12, cy: 12, r: 9 }], ['path', { d: 'M12 11v6m0-10h.01' }]],
  logout: [['path', { d: 'M9 21H4V3h5M14 8l5 4-5 4M8 12h11' }]],
  refresh: [['path', { d: 'M20 7a8 8 0 1 0 1 9M20 3v5h-5' }]],
  download: [['path', { d: 'M12 3v12m-5-5 5 5 5-5M3 16v5h18v-5' }]],
  upload: [['path', { d: 'M12 15V3m-5 5 5-5 5 5M3 16v5h18v-5' }]],
  location: [['path', { d: 'M20 10c0 6-8 12-8 12S4 16 4 10a8 8 0 0 1 16 0Z' }], ['circle', { cx: 12, cy: 10, r: 3 }]],
  calendar: [['rect', { x: 3, y: 5, width: 18, height: 16, rx: 2 }], ['path', { d: 'M7 3v4m10-4v4M3 11h18' }]],
  clock: [['circle', { cx: 12, cy: 12, r: 9 }], ['path', { d: 'M12 7v5l4 2' }]],
  mail: [['rect', { x: 3, y: 5, width: 18, height: 14, rx: 2 }], ['path', { d: 'm3 6 9 7 9-7' }]],
  lock: [['rect', { x: 5, y: 10, width: 14, height: 11, rx: 2 }], ['path', { d: 'M8 10V7a4 4 0 0 1 8 0v3M12 14v3' }]],
  shield: [['path', { d: 'm12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6l8-3Z' }], ['path', { d: 'm8 12 3 3 5-6' }]],
  save: [['path', { d: 'M3 3h15l3 3v15H3V3ZM7 3v6h10V3M7 21v-7h10v7' }]],
  filter: [['path', { d: 'M3 4h18l-7 8v8l-4-2v-6L3 4Z' }]],
  memory: [['rect', { x: 6, y: 6, width: 12, height: 12, rx: 2 }], ['path', { d: 'M9 3v3m6-3v3M9 18v3m6-3v3M3 9h3m-3 6h3m12-6h3m-3 6h3' }]],
  folder: [['path', { d: 'M3 5h7l2 3h9v13H3V5Z' }]],
  more: [['circle', { cx: 5, cy: 12, r: 1 }], ['circle', { cx: 12, cy: 12, r: 1 }], ['circle', { cx: 19, cy: 12, r: 1 }]],
};

const aliases = {
  building: 'apartment', tag: 'category', wrench: 'maintenance', 'alert-triangle': 'warning',
  'arrow-left': 'arrow_back', 'arrow-right': 'arrow_right', pencil: 'edit',
  'log-out': 'logout', 'rotate-ccw': 'refresh', 'refresh-cw': 'lifecycle',
  'bar-chart': 'chart', user: 'person', users: 'assignment', key: 'license', x: 'close',
  server: 'memory',
  laptop_mac: 'laptop', desktop_windows: 'desktop', monitor: 'desktop', label: 'category',
  build: 'maintenance', bar_chart: 'chart', visibility: 'eye', 'eye-off': 'eye_off',
  add: 'plus', add_circle: 'plus', inventory_2: 'archive', apartment: 'apartment',
  assignments: 'assignment', replacement: 'lifecycle', reports: 'chart', types: 'category',
  reset: 'refresh', restart_alt: 'refresh', file_download: 'download', arrow_upward: 'arrow_up',
  expand_more: 'chevron_down', location_on: 'location', history: 'clock', payments: 'license',
  person_check: 'person', devices_other: 'devices', archive_outlined: 'archive',
};

export function icon(name, size = 20) {
  const ns = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(ns, 'svg');
  for (const [key, value] of Object.entries({
    viewBox: '0 0 24 24', width: size, height: size, fill: 'none', stroke: 'currentColor',
    'stroke-width': 1.8, 'stroke-linecap': 'round', 'stroke-linejoin': 'round',
    'aria-hidden': 'true', focusable: 'false', class: 'icon',
  })) svg.setAttribute(key, String(value));
  for (const [tag, attrs] of paths[aliases[name] || name] || paths.devices) {
    const shape = document.createElementNS(ns, tag);
    for (const [key, value] of Object.entries(attrs)) shape.setAttribute(key, String(value));
    svg.append(shape);
  }
  return svg;
}
