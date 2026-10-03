// ── Personaggio a scheletro: sagoma unica (arti che partono dentro la giacca), vista frontale o di profilo ──
const R = Math.PI / 180;
const FAR = C.ink; // arti lontani in nero: sagoma piena
function figure(parent, { tie = C.teal, ink = C.ink } = {}) {
  const g = el('g', {}, parent);
  const mk = (stroke, w) => el('polyline', { fill: 'none', stroke, 'stroke-width': w, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' }, g);
  // ordine di disegno: arti lontani, busto, arti vicini, testa
  const legFar = mk(FAR, 46), armFar = mk(FAR, 38);
  const legNear0 = mk(ink, 46);
  const torso = el('path', { fill: ink }, g);
  const shirt = el('path', { fill: C.shirt }, g);
  const tieEl = el('path', { fill: tie }, g);
  const legNear = mk(ink, 46), armNear = mk(ink, 38);
  const head = el('circle', { r: 40, fill: ink }, g);

  // punto finale di una catena di segmenti con angoli assoluti (0 = verso il basso, positivo = in avanti)
  const chain = (x, y, segs) => { const p = [[x, y]]; for (const [len, a] of segs) { x += Math.sin(a) * len; y += Math.cos(a) * len; p.push([x, y]); } return p; };
  const pts = p => p.map(q => q[0].toFixed(1) + ',' + q[1].toFixed(1)).join(' ');

  return {
    g,
    /**
     * view: 'front' | 'side'. dir: 1 verso destra, -1 verso sinistra (profilo).
     * phase: fase del passo (radianti) — 0 se fermo. talk: 0..1 voce. gesture: 0..1 mano che gesticola. t: tempo per il respiro.
     */
    set({ x = 0, y = 0, view = 'front', dir = 1, phase = 0, walking = 0, talk = 0, gesture = 0, reach = 0, sit = 0, t = 0, s = 1 }) {
      const breath = Math.sin(t * 2.2) * 3;
      if (view === 'side') {
        const w = walking;
        const lean = w * 4 * R;
        const hip = [0, -250];
        const leg = ph => {
          const thigh = w * 0.44 * Math.sin(ph);
          const knee = w * (0.12 + 0.6 * Math.max(0, Math.sin(ph + 1.25)));
          const shin = thigh - knee;
          const p = chain(hip[0], hip[1], [[128, thigh], [122, shin]]);
          const f = p[2];
          return [...p, [f[0] + 30 * Math.cos(shin * 0.6), f[1] + 2]];
        };
        // seduto: coscia orizzontale, stinco verticale
        const seated = () => chain(hip[0], hip[1], [[128, Math.PI / 2], [122, 0.05]]);
        const lf = sit ? seated() : leg(phase + Math.PI), ln = sit ? seated() : leg(phase);
        if (sit) { const f = ln[2]; ln.push([f[0] + 30, f[1] + 2]); lf.push([f[0] + 30, f[1] + 2]); }
        // il piede più basso tocca sempre terra: il saliscendi del corpo viene da sé
        const ground = Math.max(lf[2][1], ln[2][1], lf[3][1], ln[3][1]);
        g.setAttribute('transform', `translate(${x} ${y - ground * s}) scale(${dir * s} ${s})`);
        legFar.setAttribute('points', pts(lf));
        legNear.setAttribute('points', pts(ln));
        legNear0.setAttribute('points', '');
        armFar.setAttribute('stroke', FAR); legFar.setAttribute('stroke', FAR);
        const sh = [6 + Math.sin(lean) * 160, -398 + breath * .3];
        const arm = ph => sit
          ? chain(sh[0], sh[1], [[118, 0.22], [108, 1.35]])   // mani sulle ginocchia
          : (() => { const up = -w * 0.42 * Math.sin(ph); return chain(sh[0], sh[1], [[118, up], [108, up + 0.25 + w * 0.25 * Math.max(0, -Math.sin(ph))]]); })();
        armFar.setAttribute('points', pts(arm(phase + Math.PI)));
        armNear.setAttribute('points', pts(arm(phase)));
        // busto di profilo, leggermente inclinato in avanti
        torso.setAttribute('d', `M -36 -246 Q -44 -330 -34 -398 Q -26 -424 6 -424 Q 40 -424 46 -396 Q 54 -330 44 -246 Q 40 -232 26 -232 L -22 -232 Q -34 -232 -36 -246 Z`);
        torso.setAttribute('transform', `rotate(${lean / R} 0 -250)`);
        shirt.setAttribute('d', 'M 26 -420 L 48 -402 L 40 -360 Z');
        shirt.setAttribute('transform', torso.getAttribute('transform'));
        tieEl.setAttribute('d', 'M 40 -404 L 49 -398 L 47 -338 L 40 -330 Z');
        tieEl.setAttribute('transform', torso.getAttribute('transform'));
        head.setAttribute('cx', 16 + Math.sin(lean) * 230); head.setAttribute('cy', -468 + breath * .3);
      } else {
        g.setAttribute('transform', `translate(${x} ${y}) scale(${s})`);
        armFar.setAttribute('stroke', ink);
        const nod = talk * Math.sin(t * 9) * 4;
        // gambe dritte, appena divaricate, che partono sotto la giacca
        legNear0.setAttribute('points', '-30,-240 -32,-120 -34,0');
        legNear.setAttribute('points', '30,-240 32,-120 34,0');
        legFar.setAttribute('points', '');
        torso.setAttribute('transform', ''); shirt.setAttribute('transform', ''); tieEl.setAttribute('transform', '');
        torso.setAttribute('d', `M -76 ${-372 + breath} Q -76 ${-414 + breath} -34 ${-416 + breath} L 34 ${-416 + breath} Q 76 ${-414 + breath} 76 ${-372 + breath} L 66 -238 Q 64 -224 50 -224 L -50 -224 Q -64 -224 -66 -238 Z`);
        shirt.setAttribute('d', `M -28 ${-416 + breath} L 28 ${-416 + breath} L 0 ${-336 + breath} Z`);
        tieEl.setAttribute('d', `M -9 ${-412 + breath} L 9 ${-412 + breath} L 12 ${-340 + breath} L 0 ${-322 + breath} L -12 ${-340 + breath} Z`);
        // braccia: partono dalla spalla dentro la giacca; il destro gesticola
        armFar.setAttribute('points', pts(chain(-58, -396 + breath, [[122, 0.14], [112, 0.06]])));
        // gesto da oratore: l'avambraccio sale davanti al petto e accompagna le parole
        const gx = Math.sin(t * 3.1) * 0.22 * gesture, gy = Math.sin(t * 4.7) * 0.08 * gesture;
        const upper = -lerp(-0.14, 0.3, gesture) - gy, fore = -lerp(-0.06, 3.55, gesture) - gx;
        // reach: la mano destra va al microfono (gomito in fuori, poi in su)
        const a1 = lerp(-upper, 1.2, reach), a2 = lerp(-fore, 3.85, reach);
        armNear.setAttribute('points', pts(chain(58, -396 + breath, [[122, a1], [112, a2]])));
        head.setAttribute('cx', 0); head.setAttribute('cy', -466 + breath + nod);
      }
    },
  };
}
