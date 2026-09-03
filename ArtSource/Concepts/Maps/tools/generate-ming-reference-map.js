const fs = require('fs');
const path = require('path');

const outDir = path.resolve(__dirname, '..');
const svgPath = path.join(outDir, '01-ming-mid-china-story-reference-map.svg');

const W = 2400;
const H = 1600;
const X0 = 120;
const Y0 = 170;
const SX = 55;
const SY = 50;

const C = {
  paperLight: '#f2e6c9',
  paperMid: '#d8c7a5',
  paperShade: '#b4a17f',
  paperDeep: '#776b59',
  ink: '#211f1b',
  inkDeep: '#3a3730',
  inkMid: '#5a554a',
  inkLight: '#7d7565',
  inkWash: '#a49a84',
  woodInk: '#4a3028',
  woodDeep: '#714635',
  woodMid: '#9a6846',
  ochreLight: '#d2ad78',
  foliageInk: '#263f32',
  foliageDeep: '#3e6243',
  bamboo: '#65805a',
  foliageMid: '#8e9c68',
  foliageLight: '#bcc38b',
  blueGray: '#4e7180',
  vermilion: '#a33b2b',
  ochreGold: '#b28435',
  jade: '#4d8070',
  seal: '#8b5a47',
};

const esc = (s) => String(s).replace(/[&<>"']/g, (ch) => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[ch]));
const p = ([lon, lat]) => [X0 + (lon - 95) * SX, Y0 + (44 - lat) * SY];
const pts = (arr) => arr.map((v) => p(v).map((n) => n.toFixed(1)).join(',')).join(' ');
const linePath = (arr) => arr.map((v, i) => `${i ? 'L' : 'M'} ${p(v)[0].toFixed(1)} ${p(v)[1].toFixed(1)}`).join(' ');

function textAt(label, lon, lat, cls, dx = 0, dy = 0, anchor = 'middle', extra = '') {
  const [x, y] = p([lon, lat]);
  return `<text x="${x + dx}" y="${y + dy}" text-anchor="${anchor}" class="${cls}" ${extra}>${esc(label)}</text>`;
}

function mountain(lon, lat, size, label, dx = 0, dy = 0) {
  const [x, y] = p([lon, lat]);
  const s = size === 'large' ? 1.15 : size === 'medium' ? 0.86 : 0.62;
  const klass = `mountain ${size}`;
  const labelClass = size === 'large' ? 'mountain-label major' : 'mountain-label';
  return `
    <g class="${klass}" transform="translate(${x},${y}) scale(${s})">
      <path d="M-32,17 C-21,2 -16,-22 -2,-35 C3,-25 8,-14 13,-7 C19,-16 24,-22 30,-27 C37,-11 45,5 55,17" />
      <path class="ridge-highlight" d="M-25,12 C-16,1 -12,-15 -2,-27 C3,-17 4,-9 11,-3" />
      <path class="wash" d="M-34,19 C-11,13 25,11 57,19 C34,28 -8,28 -34,19 Z" />
    </g>
    <text x="${x + dx}" y="${y + 34 * s + dy}" text-anchor="middle" class="${labelClass}">${esc(label)}</text>`;
}

function mountainRange(points, label, labelLon, labelLat, rotation = 0) {
  const d = linePath(points);
  const peaks = points.map((coord, i) => {
    if (i === 0 || i === points.length - 1) return '';
    const [x, y] = p(coord);
    const sc = 0.58 + (i % 3) * 0.08;
    return `<path d="M${x - 17 * sc},${y + 10 * sc} Q${x - 6 * sc},${y - 14 * sc} ${x},${y - 21 * sc} Q${x + 10 * sc},${y - 7 * sc} ${x + 20 * sc},${y + 10 * sc}" />`;
  }).join('');
  return `<g class="range"><path class="range-wash" d="${d}"/><path class="range-spine" d="${d}"/>${peaks}</g>${textAt(label, labelLon, labelLat, 'range-label', 0, 0, 'middle', `transform="rotate(${rotation} ${p([labelLon,labelLat])[0]} ${p([labelLon,labelLat])[1]})"`)}`;
}

const cityStyle = {
  capital: {r: 12, cls: 'capital'},
  seat: {r: 9, cls: 'seat'},
  major: {r: 7, cls: 'major-city'},
  medium: {r: 5, cls: 'medium-city'},
  small: {r: 3.5, cls: 'small-city'},
};

function city(name, lon, lat, type, dx = 12, dy = -10, anchor = 'start') {
  const [x, y] = p([lon, lat]);
  const st = cityStyle[type];
  let glyph = '';
  if (type === 'capital') {
    glyph = `<rect x="${x - 9}" y="${y - 9}" width="18" height="18" rx="2"/><circle cx="${x}" cy="${y}" r="14"/>`;
  } else if (type === 'seat') {
    glyph = `<path d="M${x},${y - 11} L${x + 11},${y} L${x},${y + 11} L${x - 11},${y} Z"/><circle cx="${x}" cy="${y}" r="3"/>`;
  } else if (type === 'small') {
    glyph = `<rect x="${x - st.r}" y="${y - st.r}" width="${st.r * 2}" height="${st.r * 2}"/>`;
  } else {
    glyph = `<circle cx="${x}" cy="${y}" r="${st.r}"/>`;
  }
  return `<g class="city ${st.cls}">${glyph}<text x="${x + dx}" y="${y + dy}" text-anchor="${anchor}">${esc(name)}</text></g>`;
}

const mingOutline = [
  [95.6,39.4],[96.8,40.4],[99.4,41.7],[102.2,41.6],[105.2,40.7],[108.1,40.8],
  [111.2,41.2],[114.2,41.0],[116.4,41.6],[118.8,41.3],[120.4,40.7],[121.8,41.0],
  [124.4,42.2],[125.5,41.3],[125.0,39.8],[123.2,39.2],[121.3,39.1],[120.7,38.1],
  [122.2,37.5],[122.8,36.7],[122.3,35.8],[121.0,35.0],[120.5,33.8],[121.2,32.0],
  [122.1,30.4],[121.3,28.4],[120.6,27.0],[119.8,25.3],[118.4,24.2],[117.1,23.1],
  [115.4,22.5],[113.5,22.3],[112.0,21.2],[110.1,21.0],[108.6,21.6],[107.0,21.6],
  [105.3,22.4],[103.7,22.5],[102.2,22.0],[100.8,21.6],[99.4,22.8],[98.3,24.3],
  [98.2,26.7],[99.2,28.1],[100.2,29.4],[100.8,31.0],[101.0,32.6],[100.1,34.0],
  [99.3,35.2],[99.4,36.4],[98.3,37.2],[97.2,38.0]
];

const provinceBoundaries = [
  [[116.0,40.8],[115.2,39.4],[115.4,37.8],[116.0,36.2]],
  [[119.0,40.8],[118.4,39.7],[119.2,38.1],[118.2,36.8],[116.2,36.0]],
  [[110.7,40.5],[111.2,38.7],[111.0,36.9],[110.4,35.1]],
  [[107.0,40.0],[106.8,37.9],[108.1,36.3],[109.4,34.7],[108.0,32.9]],
  [[110.4,35.1],[112.0,35.5],[114.2,35.0],[116.2,36.0],[116.8,33.8]],
  [[116.8,33.8],[118.0,34.0],[119.4,33.0],[121.0,32.6]],
  [[117.0,32.4],[116.2,30.8],[117.0,29.2],[118.2,28.2]],
  [[118.2,28.2],[119.0,27.2],[118.6,25.1]],
  [[113.8,31.5],[114.8,29.8],[114.6,27.7],[113.2,25.1]],
  [[109.0,32.0],[111.0,31.2],[113.8,31.5],[113.2,28.0],[111.8,25.4]],
  [[105.1,33.0],[106.5,31.2],[108.7,30.8],[109.0,28.0],[107.8,26.2]],
  [[101.0,31.8],[103.3,31.6],[105.1,33.0],[106.5,31.2],[105.0,28.4],[103.0,27.0],[101.0,27.6]],
  [[103.0,27.0],[105.0,28.4],[107.8,26.2],[107.2,24.5],[105.0,23.0]],
  [[107.8,26.2],[110.0,25.4],[111.8,25.4],[112.0,22.0]],
  [[113.2,25.1],[115.8,24.9],[117.1,23.1]],
];

const rivers = [
  {name:'황하', cls:'yellow-river', points:[[103.7,36.1],[105.8,36.8],[106.3,38.6],[108.8,39.7],[110.4,39.2],[110.2,37.4],[111.8,35.6],[113.7,34.9],[115.2,35.2],[117.0,36.2],[119.0,37.1]], label:[111.0,37.0]},
  {name:'장강', cls:'water', points:[[99.4,32.0],[101.7,31.4],[104.0,30.6],[106.5,29.6],[108.6,29.8],[110.3,30.4],[112.4,30.3],[114.3,30.6],[116.0,29.7],[117.5,30.6],[118.8,31.9],[120.8,31.2]], label:[108.2,29.2]},
  {name:'회수', cls:'water minor-river', points:[[111.5,33.0],[113.5,32.9],[115.5,33.0],[117.3,33.2],[119.0,33.4]], label:[115.0,32.5]},
  {name:'서강', cls:'water minor-river', points:[[106.2,24.2],[108.0,23.8],[110.0,23.4],[112.1,23.2],[113.2,23.0]], label:[109.5,23.0]},
];

const regions = [
  ['북직례 北直隸',116.1,38.7,-8], ['남직례 南直隸',118.2,32.3,-4],
  ['산동 山東',118.7,36.5,0], ['산서 山西',112.2,37.6,-5], ['하남 河南',113.6,34.0,0],
  ['섬서 陝西',108.2,35.7,-7], ['절강 浙江',120.0,29.0,-18], ['강서 江西',115.6,27.2,-2],
  ['호광 湖廣',112.0,28.3,-3], ['사천 四川',104.0,29.4,-5], ['복건 福建',118.1,25.6,-13],
  ['광동 廣東',113.0,22.7,-2], ['광서 廣西',108.4,24.6,-4], ['운남 雲南',101.6,24.6,-7],
  ['귀주 貴州',106.2,27.0,-4], ['요동도사 遼東都司',122.5,40.5,-8],
];

const cities = [
  ['북경 · 순천부',116.40,39.90,'capital',18,-14,'start'],
  ['남경 · 응천부',118.80,32.06,'capital',18,24,'start'],
  ['제남부',117.00,36.67,'seat',12,-12,'start'], ['태원부',112.55,37.87,'seat',12,-12,'start'],
  ['개봉부',114.31,34.80,'seat',12,-12,'start'], ['서안부',108.94,34.34,'seat',12,-12,'start'],
  ['성도부',104.07,30.67,'seat',12,-12,'start'], ['무창부',114.30,30.55,'seat',12,-12,'start'],
  ['남창부',115.85,28.68,'seat',12,-12,'start'], ['항주부',120.15,30.27,'seat',12,22,'start'],
  ['복주부',119.30,26.08,'seat',12,-12,'start'], ['광주부',113.26,23.13,'seat',12,22,'start'],
  ['계림부',110.29,25.27,'seat',12,-12,'start'], ['운남부',102.71,25.04,'seat',12,-12,'start'],
  ['귀주선위사',106.63,26.65,'seat',12,22,'start'],
  ['소주부',120.62,31.31,'major',12,-10,'start'], ['양주부',119.42,32.39,'major',12,-10,'start'],
  ['대동부',113.30,40.08,'major',12,-10,'start'], ['낙양',112.45,34.62,'major',-12,20,'end'],
  ['중경부',106.55,29.56,'major',12,22,'start'], ['장사부',112.94,28.23,'major',12,20,'start'],
  ['양양부',112.14,32.04,'major',-12,-10,'end'], ['천주부',118.67,24.88,'major',12,18,'start'],
  ['보정부',115.47,38.87,'medium',12,-10,'start'], ['진정부',114.58,38.15,'medium',12,18,'start'],
  ['청주부',118.48,36.68,'medium',12,18,'start'], ['임청주',115.70,36.84,'medium',-12,-10,'end'],
  ['회안부',119.02,33.60,'medium',12,-10,'start'], ['휘주부',118.33,29.72,'medium',-12,20,'end'],
  ['영파부',121.55,29.87,'medium',12,-10,'start'], ['온주부',120.70,28.00,'medium',12,18,'start'],
  ['구강부',116.00,29.70,'medium',-12,-10,'end'], ['형주부',112.24,30.33,'medium',-12,20,'end'],
  ['한중부',107.02,33.07,'medium',12,-10,'start'], ['난주',103.84,36.06,'medium',12,-10,'start'],
  ['대리부',100.27,25.60,'medium',12,-10,'start'], ['유주부',109.42,24.33,'medium',12,20,'start'],
  ['남녕부',108.32,22.82,'medium',12,20,'start'], ['장주부',117.65,24.52,'medium',-12,18,'end'],
  ['산해관',119.78,40.00,'small',12,-8,'start'], ['가욕관',98.29,39.77,'small',12,-8,'start'],
  ['경덕진',117.18,29.27,'small',12,16,'start'], ['악양',113.13,29.37,'small',12,-8,'start'],
  ['조주부',116.62,23.66,'small',12,16,'start'], ['영창부',99.17,25.12,'small',12,16,'start'],
];

const ranges = [
  [[[106.0,40.1],[108.0,40.4],[110.0,40.0],[111.8,39.8]],'음산산맥',108.8,40.8,0],
  [[[112.6,39.6],[113.2,38.5],[113.5,37.4],[113.6,36.2],[113.2,35.2]],'태행산맥',113.9,37.3,78],
  [[[116.5,40.6],[117.5,40.4],[118.6,40.2],[119.5,40.0]],'연산',118.0,40.8,0],
  [[[105.0,33.8],[107.2,33.7],[109.2,33.7],[111.2,33.4]],'진령산맥',108.2,34.2,0],
  [[[106.0,32.0],[108.0,32.0],[109.7,31.6],[111.0,31.2]],'대파산맥',108.5,32.3,-7],
  [[[100.1,32.2],[100.6,30.5],[100.5,28.6],[99.7,27.1],[99.3,25.4]],'횡단산맥',99.5,28.8,84],
  [[[117.0,29.0],[117.4,27.9],[117.8,26.8],[118.2,25.7]],'무이산맥',117.1,27.1,73],
  [[[108.3,25.0],[110.1,25.0],[112.0,24.9],[114.0,24.7]],'남령산맥',111.2,25.5,0],
];

const mountains = [
  [117.1,36.25,'large','태산',0,2], [110.1,34.50,'large','화산',0,4],
  [112.95,34.48,'large','숭산',0,4], [113.6,39.0,'large','오대산',0,3],
  [103.35,29.55,'large','아미산',0,3], [111.0,32.4,'large','무당산',0,4],
  [118.15,30.15,'large','황산',0,3], [115.95,29.55,'large','여산',0,4],
  [112.65,27.25,'large','형산',0,3],
  [103.55,30.95,'medium','청성산',0,2], [108.6,33.9,'medium','종남산',0,2],
  [110.9,31.1,'medium','무산',0,2], [121.05,29.15,'medium','천태산',0,2],
  [120.95,28.45,'medium','안탕산',0,2], [113.95,23.25,'medium','나부산',0,2],
  [106.55,35.55,'medium','공동산',0,2], [100.05,25.75,'medium','점창산',0,2],
  [116.1,39.8,'small','반산',0,1], [116.95,28.15,'small','용호산',0,1],
  [118.1,25.0,'small','청원산',0,1], [107.6,24.9,'small','도양산',0,1],
];

let svg = `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">
<defs>
  <linearGradient id="paper" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0" stop-color="${C.paperLight}"/><stop offset="0.55" stop-color="#eadbbd"/><stop offset="1" stop-color="${C.paperMid}"/>
  </linearGradient>
  <filter id="paperNoise" x="-10%" y="-10%" width="120%" height="120%">
    <feTurbulence type="fractalNoise" baseFrequency="0.7" numOctaves="3" seed="23" result="noise"/>
    <feColorMatrix in="noise" type="saturate" values="0" result="gray"/>
    <feComponentTransfer in="gray" result="faint"><feFuncA type="table" tableValues="0 0.055"/></feComponentTransfer>
    <feBlend in="SourceGraphic" in2="faint" mode="multiply"/>
  </filter>
  <filter id="inkBleed" x="-20%" y="-20%" width="140%" height="140%">
    <feTurbulence type="fractalNoise" baseFrequency="0.035" numOctaves="2" seed="7" result="t"/>
    <feDisplacementMap in="SourceGraphic" in2="t" scale="2.3" xChannelSelector="R" yChannelSelector="G"/>
  </filter>
  <clipPath id="mingClip"><polygon points="${pts(mingOutline)}"/></clipPath>
</defs>
<style><![CDATA[
  text { font-family: "Noto Serif CJK KR", "Malgun Gothic", "Batang", serif; fill: ${C.ink}; }
  .title { font-size: 50px; font-weight: 800; letter-spacing: 3px; }
  .subtitle { font-size: 22px; fill: ${C.inkMid}; letter-spacing: 1px; }
  .map-outline { fill: #e8dbbd; stroke: ${C.inkDeep}; stroke-width: 5; stroke-linejoin: round; }
  .coast-wash { fill: none; stroke: ${C.blueGray}; stroke-opacity: .2; stroke-width: 21; }
  .province-border { fill: none; stroke: ${C.inkLight}; stroke-width: 2.2; stroke-dasharray: 8 8; opacity: .82; }
  .region-label { font-size: 27px; font-weight: 800; letter-spacing: 3px; fill: ${C.inkDeep}; opacity: .48; paint-order: stroke; stroke: ${C.paperLight}; stroke-width: 6px; }
  .frontier-label { font-size: 19px; font-weight: 700; fill: ${C.inkMid}; letter-spacing: 2px; }
  .river { fill:none; stroke-linecap:round; stroke-linejoin:round; opacity:.74; }
  .water { stroke:${C.blueGray}; stroke-width:8; }
  .yellow-river { stroke:${C.ochreGold}; stroke-width:9; }
  .minor-river { stroke-width:5; opacity:.55; }
  .river-label { font-size:18px; font-weight:700; fill:${C.blueGray}; paint-order:stroke; stroke:${C.paperLight}; stroke-width:5px; }
  .river-label.yellow { fill:${C.ochreGold}; }
  .canal { fill:none; stroke:${C.blueGray}; stroke-width:4; stroke-dasharray:7 7; opacity:.72; }
  .wall { fill:none; stroke:${C.inkMid}; stroke-width:4; stroke-dasharray:2 9; stroke-linecap:square; }
  .wall-label { font-size:17px; fill:${C.inkMid}; letter-spacing:2px; paint-order:stroke; stroke:${C.paperLight}; stroke-width:4px; }
  .range path { fill:none; stroke:${C.foliageDeep}; stroke-linecap:round; stroke-linejoin:round; }
  .range-wash { stroke:${C.foliageLight}!important; stroke-width:28; opacity:.23; }
  .range-spine { stroke-width:5; opacity:.82; }
  .range > path:not(.range-wash):not(.range-spine) { stroke-width:3.5; opacity:.82; }
  .range-label { font-size:16px; font-weight:800; fill:${C.foliageInk}; letter-spacing:2px; paint-order:stroke; stroke:${C.paperLight}; stroke-width:5px; }
  .mountain path { fill:none; stroke:${C.foliageInk}; stroke-width:5; stroke-linecap:round; stroke-linejoin:round; }
  .mountain .ridge-highlight { stroke:${C.bamboo}; stroke-width:3.2; }
  .mountain .wash { fill:${C.foliageMid}; opacity:.22; stroke:none; }
  .mountain.small path { stroke-width:4; }
  .mountain-label { font-size:14px; font-weight:700; fill:${C.foliageInk}; paint-order:stroke; stroke:${C.paperLight}; stroke-width:5px; }
  .mountain-label.major { font-size:17px; font-weight:900; }
  .city text { font-size:15px; font-weight:700; paint-order:stroke; stroke:${C.paperLight}; stroke-width:5px; }
  .city circle, .city rect, .city path { vector-effect:non-scaling-stroke; }
  .capital rect { fill:${C.vermilion}; stroke:${C.ink}; stroke-width:2; }
  .capital circle { fill:none; stroke:${C.vermilion}; stroke-width:3; }
  .capital text { font-size:20px; font-weight:900; fill:${C.vermilion}; }
  .seat path { fill:${C.ochreGold}; stroke:${C.ink}; stroke-width:2; }
  .seat circle { fill:${C.paperLight}; stroke:none; }
  .seat text { font-size:17px; font-weight:900; fill:${C.woodInk}; }
  .major-city circle { fill:${C.inkDeep}; stroke:${C.paperLight}; stroke-width:2; }
  .medium-city circle { fill:${C.paperLight}; stroke:${C.inkDeep}; stroke-width:3; }
  .small-city rect { fill:${C.inkLight}; stroke:none; }
  .legend-title { font-size:27px; font-weight:900; letter-spacing:2px; }
  .legend-text { font-size:18px; fill:${C.inkDeep}; }
  .legend-note { font-size:16px; fill:${C.inkMid}; }
  .note-title { font-size:20px; font-weight:900; fill:${C.woodInk}; }
  .frame { fill:none; stroke:${C.woodDeep}; stroke-width:3; }
  .thin-frame { fill:none; stroke:${C.paperDeep}; stroke-width:1.5; }
]]></style>

<rect width="${W}" height="${H}" fill="url(#paper)" filter="url(#paperNoise)"/>
<rect x="28" y="28" width="2344" height="1544" rx="8" class="frame"/>
<rect x="43" y="43" width="2314" height="1514" rx="6" class="thin-frame"/>

<g transform="translate(82,76)">
  <text class="title" x="0" y="0">명 중기 중국 개략도</text>
  <text class="subtitle" x="4" y="38">약 1530년경 · 양경십삼성 · 스토리 동선 및 세계관 참고용</text>
</g>

<g id="map">
  <polyline points="${pts(mingOutline)}" class="coast-wash"/>
  <polygon points="${pts(mingOutline)}" class="map-outline"/>
  <g clip-path="url(#mingClip)">
    <path d="M80 240 C620 360 810 130 1320 280 S2110 340 2190 170" fill="none" stroke="${C.paperShade}" stroke-width="45" opacity=".08"/>
    <path d="M150 1120 C620 940 900 1170 1350 990 S1920 980 2050 840" fill="none" stroke="${C.foliageMid}" stroke-width="55" opacity=".06"/>
    ${provinceBoundaries.map((b) => `<path d="${linePath(b)}" class="province-border"/>`).join('\n')}
    ${regions.map(([label,lon,lat,rot]) => textAt(label,lon,lat,'region-label',0,0,'middle',`transform="rotate(${rot} ${p([lon,lat])[0]} ${p([lon,lat])[1]})"`)).join('\n')}
    ${rivers.map((r) => `<path d="${linePath(r.points)}" class="river ${r.cls}"/>`).join('\n')}
    <path d="${linePath([[120.15,30.27],[120.62,31.31],[119.42,32.39],[119.02,33.60],[117.2,34.8],[116.5,36.0],[115.7,36.84],[116.4,39.9]])}" class="canal"/>
    ${ranges.map((r) => mountainRange(...r)).join('\n')}
    ${mountains.map((m) => mountain(...m)).join('\n')}
  </g>

  <path d="${linePath([[98.3,39.77],[102.0,39.8],[106.0,39.5],[110.0,39.3],[113.3,40.1],[116.4,40.4],[119.78,40.0]])}" class="wall"/>
  ${textAt('명 북방 변경 · 만리장성',109.4,40.45,'wall-label',0,-16)}
  ${textAt('대운하',118.1,35.0,'river-label',0,0,'middle',`transform="rotate(72 ${p([118.1,35])[0]} ${p([118.1,35])[1]})"`)}
  ${rivers.map((r) => textAt(r.name, r.label[0], r.label[1], `river-label ${r.cls === 'yellow-river' ? 'yellow' : ''}`)).join('\n')}

  ${cities.map((v) => city(...v)).join('\n')}

  <ellipse cx="${p([110.25,19.2])[0]}" cy="${p([110.25,19.2])[1]}" rx="55" ry="31" transform="rotate(-18 ${p([110.25,19.2])[0]} ${p([110.25,19.2])[1]})" fill="#e8dbbd" stroke="${C.inkDeep}" stroke-width="4"/>
  ${textAt('해남도',110.25,19.15,'frontier-label',0,6)}
  ${textAt('동해',123.9,29.3,'frontier-label',0,0)}
  ${textAt('남해',115.0,20.7,'frontier-label',0,0)}
  ${textAt('몽골 제부',108.0,42.5,'frontier-label',0,0)}
  ${textAt('여진 제부',124.1,43.0,'frontier-label',0,0)}
  ${textAt('토번계 제부',97.4,32.7,'frontier-label',0,0)}
</g>

<g id="legend" transform="translate(1945,190)">
  <rect x="0" y="0" width="375" height="1170" rx="12" fill="${C.paperLight}" fill-opacity=".82" stroke="${C.paperDeep}" stroke-width="2"/>
  <text x="28" y="48" class="legend-title">범례</text>
  <path d="M25 67 H350" stroke="${C.paperShade}" stroke-width="2"/>

  <text x="28" y="110" class="note-title">도시</text>
  <g class="city capital"><rect x="32" y="134" width="18" height="18" rx="2"/><circle cx="41" cy="143" r="14"/><text x="72" y="150">양경 · 수도</text></g>
  <g class="city seat"><path d="M41 177 L52 188 L41 199 L30 188 Z"/><circle cx="41" cy="188" r="3"/><text x="72" y="195">성도 · 행정 중심</text></g>
  <g class="city major-city"><circle cx="41" cy="235" r="7"/><text x="72" y="242">대도시</text></g>
  <g class="city medium-city"><circle cx="41" cy="280" r="5"/><text x="72" y="287">중소도시</text></g>
  <g class="city small-city"><rect x="37.5" y="319.5" width="7" height="7"/><text x="72" y="329">소도시 · 관문</text></g>

  <text x="28" y="382" class="note-title">산세</text>
  <g transform="translate(44,424) scale(1.05)" class="mountain large"><path d="M-32,17 C-21,2 -16,-22 -2,-35 C3,-25 8,-14 13,-7 C19,-16 24,-22 30,-27 C37,-11 45,5 55,17"/><path class="ridge-highlight" d="M-25,12 C-16,1 -12,-15 -2,-27 C3,-17 4,-9 11,-3"/></g><text x="125" y="430" class="legend-text">큰산 · 대표 명산</text>
  <g transform="translate(44,486) scale(.78)" class="mountain medium"><path d="M-32,17 C-21,2 -16,-22 -2,-35 C3,-25 8,-14 13,-7 C19,-16 24,-22 30,-27 C37,-11 45,5 55,17"/></g><text x="125" y="492" class="legend-text">중간산</text>
  <g transform="translate(44,540) scale(.52)" class="mountain small"><path d="M-32,17 C-21,2 -16,-22 -2,-35 C3,-25 8,-14 13,-7 C19,-16 24,-22 30,-27 C37,-11 45,5 55,17"/></g><text x="125" y="546" class="legend-text">작은산</text>
  <g class="range"><path class="range-wash" d="M28 598 C85 570 115 620 165 588"/><path class="range-spine" d="M28 598 C85 570 115 620 165 588"/></g><text x="195" y="600" class="legend-text">산맥</text>

  <text x="28" y="662" class="note-title">물길과 경계</text>
  <path d="M30 700 H115" class="river water"/><text x="135" y="707" class="legend-text">장강 · 주요 수계</text>
  <path d="M30 746 H115" class="river yellow-river"/><text x="135" y="753" class="legend-text">황하</text>
  <path d="M30 792 H115" class="canal"/><text x="135" y="799" class="legend-text">대운하</text>
  <path d="M30 838 H115" class="wall"/><text x="135" y="845" class="legend-text">장성 · 북방 변경</text>
  <path d="M30 884 H115" class="province-border"/><text x="135" y="891" class="legend-text">행정 경계(근사)</text>

  <text x="28" y="950" class="note-title">읽는 법</text>
  <text x="28" y="986" class="legend-note">• 양경십삼성의 큰 틀을 우선합니다.</text>
  <text x="28" y="1016" class="legend-note">• 도시 좌표·경계·산줄기는 스토리</text>
  <text x="44" y="1042" class="legend-note">동선용으로 단순화한 근사입니다.</text>
  <text x="28" y="1072" class="legend-note">• 귀주의 행정 중심은 당시 명칭인</text>
  <text x="44" y="1098" class="legend-note">‘귀주선위사’로 표기했습니다.</text>
  <text x="28" y="1128" class="legend-note">• 현대 국경·현대 성명을 적용하지</text>
  <text x="44" y="1154" class="legend-note">않은 세계관 참고용 개략도입니다.</text>
</g>

<g transform="translate(83,1488)">
  <text class="legend-note" x="0" y="0">기준: 가정 연간 전반(16세기 전반) · 명대 지도 전통의 산·성곽 기호를 현대적 범례로 재구성</text>
  <text class="legend-note" x="0" y="27">제작 상태: 프로젝트 콘셉트 참고용 / Unity 빌드 제외 / SVG 원본 우선</text>
</g>

<g transform="translate(2260,1450)">
  <circle cx="0" cy="0" r="43" fill="none" stroke="${C.inkMid}" stroke-width="2"/>
  <path d="M0 -39 L9 -7 L0 -14 L-9 -7 Z" fill="${C.vermilion}" stroke="${C.ink}" stroke-width="1.5"/>
  <path d="M0 39 L8 8 L0 14 L-8 8 Z" fill="${C.paperLight}" stroke="${C.ink}" stroke-width="1.5"/>
  <text x="0" y="-50" text-anchor="middle" class="legend-text">북</text>
</g>
</svg>`;

fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(svgPath, svg, 'utf8');
console.log(svgPath);
