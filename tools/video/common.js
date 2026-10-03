// ── TimerSala: motion · elementi condivisi ──
const NS = 'http://www.w3.org/2000/svg';
const C = { paper: '#f1ece3', paper2: '#e3dacb', ink: '#141414', go: '#17a556', warn: '#f2b400', stop: '#e0362b', teal: '#2f7f93', wine: '#a5332d', gold: '#c4920a', shirt: '#fbf8f2' };
const el = (tag, attrs = {}, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); parent?.appendChild(e); return e; };
const clamp = (v, a = 0, b = 1) => Math.max(a, Math.min(b, v));
const lerp = (a, b, k) => a + (b - a) * clamp(k);
const prog = (t, a, b) => clamp((t - a) / (b - a));
const eio = k => { k = clamp(k); return k < .5 ? 4 * k ** 3 : 1 - (-2 * k + 2) ** 3 / 2; };
const eout = k => 1 - (1 - clamp(k)) ** 3;
const back = k => { k = clamp(k); const c = 1.9; return 1 + (c + 1) * (k - 1) ** 3 + c * (k - 1) ** 2; };

// Pittogramma in giacca e cravatta: testa staccata, giacca con scollo a V, braccia e gambe a capsula.
// Origine ai piedi; altezza circa 520.
function pictogram(parent, { tie = C.teal, ink = C.ink } = {}) {
  const g = el('g', {}, parent);
  const legL = el('line', { stroke: ink, 'stroke-width': 44, 'stroke-linecap': 'round' }, g);
  const legR = el('line', { stroke: ink, 'stroke-width': 44, 'stroke-linecap': 'round' }, g);
  const armL = el('polyline', { fill: 'none', stroke: ink, 'stroke-width': 36, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' }, g);
  const armR = el('polyline', { fill: 'none', stroke: ink, 'stroke-width': 36, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' }, g);
  el('path', { d: 'M -78 -372 Q -78 -414 -36 -414 L 36 -414 Q 78 -414 78 -372 L 66 -236 Q 64 -222 50 -222 L -50 -222 Q -64 -222 -66 -236 Z', fill: ink }, g);
  el('path', { d: 'M -30 -414 L 30 -414 L 0 -330 Z', fill: C.shirt }, g);
  el('path', { d: 'M -10 -410 L 10 -410 L 13 -336 L 0 -318 L -13 -336 Z', fill: tie }, g);
  el('circle', { cx: 0, cy: -468, r: 40, fill: ink }, g);
  return {
    g,
    set({ x = 0, y = 0, walk = 0, s = 1, armL: aL, armR: aR } = {}) {
      g.setAttribute('transform', `translate(${x} ${y}) scale(${s})`);
      // passo: le gambe si aprono a V e si richiudono, come nei pittogrammi
      const sw = walk ? Math.abs(Math.sin(walk)) * 0.36 : 0;
      const leg = (e, side, ph) => { const hx = side * 26; e.setAttribute('x1', hx); e.setAttribute('y1', -196); e.setAttribute('x2', hx + Math.sin(ph) * 196); e.setAttribute('y2', -196 + Math.cos(ph) * 182); };
      leg(legL, -1, -sw); leg(legR, 1, sw);
      armL.setAttribute('points', aL || `-100,-386 ${-108 - sw * 50},-300 ${-104 - sw * 110},-214`);
      armR.setAttribute('points', aR || `100,-386 ${108 + sw * 50},-300 ${104 + sw * 110},-214`);
    },
  };
}
