// Sezione «In rete»: tablet e telefono in 3D (three.js) con gli screenshot veri della pagina web come schermo.
// Se WebGL manca o three.js non si carica, restano le cornici 2D già nella pagina (.flat): qui non succede nulla.
import * as THREE from 'three';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';

const fig = document.getElementById('devices');
const canvas = fig?.querySelector('canvas');
const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;

if (fig && canvas) {
  try { start(); } catch (e) { console.warn('dispositivi 3D non disponibili:', e); }
}

function start() {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: 'low-power' });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.05;

  const scene = new THREE.Scene();
  const pmrem = new THREE.PMREMGenerator(renderer);
  scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;   // riflessi su metallo e vetro

  const camera = new THREE.PerspectiveCamera(24, 1, 10, 4000);
  const key = new THREE.DirectionalLight(0xffffff, 1.4);
  key.position.set(-300, 400, 600);
  scene.add(key, new THREE.AmbientLight(0xffffff, 0.25));

  const loader = new THREE.TextureLoader();
  const maxAniso = renderer.capabilities.getMaxAnisotropy();
  const texture = url => new Promise((ok, ko) => loader.load(url, t => {
    t.colorSpace = THREE.SRGBColorSpace;
    t.anisotropy = maxAniso;
    ok(t);
  }, undefined, ko));

  // misure in millimetri, più o meno quelle vere
  const tablet = device({ w: 250, h: 178, depth: 6.5, radius: 15, bezel: 6.2, screenRadius: 9, camera: 'bezel' });
  const phone = device({ w: 74, h: 156, depth: 8, radius: 11.5, bezel: 2.6, screenRadius: 9.5, camera: 'island', buttons: true });

  // il gruppo ruota attorno al centro della composizione: col movimento nessuno dei due esce dal riquadro
  const CX = -19, CY = -10, TY = 32, PY = -42;
  tablet.position.set(-29, TY, -40);
  tablet.rotation.set(-0.06, 0.32, 0.035);
  phone.position.set(117, PY, 46);
  phone.rotation.set(-0.04, -0.34, -0.06);
  const group = new THREE.Group();
  group.position.set(CX, CY, 0);
  group.add(tablet, phone);
  scene.add(group);

  Promise.all([texture('images/tablet.webp'), texture('images/telefono.webp')]).then(([tt, tp]) => {
    tablet.userData.screen.material.map = tt; tablet.userData.screen.material.needsUpdate = true;
    phone.userData.screen.material.map = tp; phone.userData.screen.material.needsUpdate = true;
    resize();
    render();
    fig.classList.add('is-3d');          // solo ora la scena prende il posto delle cornici 2D
    if (!reduce) loop();
  }).catch(e => console.warn('screenshot non caricati:', e));

  // ── un dispositivo: scocca in alluminio, vetro nero davanti, schermo con lo screenshot ──
  function device({ w, h, depth, radius, bezel, screenRadius, camera: cam, buttons }) {
    const g = new THREE.Group();
    const body = new THREE.ExtrudeGeometry(rounded(w, h, radius), {
      depth, bevelEnabled: true, bevelThickness: 1.2, bevelSize: 1.1, bevelSegments: 6, curveSegments: 32,
    });
    body.translate(0, 0, -depth / 2);
    const glass = new THREE.MeshPhysicalMaterial({ color: 0x050506, roughness: 0.08, metalness: 0, clearcoat: 1, clearcoatRoughness: 0.05 });
    const metal = new THREE.MeshPhysicalMaterial({ color: 0x9a9ca1, roughness: 0.32, metalness: 1 });
    g.add(new THREE.Mesh(body, [glass, metal]));    // gruppi dell'estrusione: 0 = facce, 1 = bordi

    const sw = w - bezel * 2, sh = h - bezel * 2;
    const screen = new THREE.Mesh(withUv(new THREE.ShapeGeometry(rounded(sw, sh, screenRadius), 24)),
      new THREE.MeshBasicMaterial({ color: 0xffffff, toneMapped: false }));
    screen.position.z = depth / 2 + 1.25;
    g.add(screen);
    g.userData.screen = screen;

    // riflesso morbido sul vetro, appena accennato
    const sheen = new THREE.Mesh(new THREE.ShapeGeometry(rounded(sw, sh, screenRadius), 24),
      new THREE.MeshPhysicalMaterial({ color: 0xffffff, transparent: true, opacity: 0.06, roughness: 0.05, metalness: 0, depthWrite: false }));
    sheen.position.z = depth / 2 + 1.35;
    g.add(sheen);

    const black = new THREE.MeshBasicMaterial({ color: 0x000000 });
    if (cam === 'island') {        // isola della fotocamera in alto sullo schermo
      const island = new THREE.Mesh(new THREE.ShapeGeometry(rounded(20, 5.6, 2.8), 16), black);
      island.position.set(0, sh / 2 - 6, depth / 2 + 1.3);
      g.add(island);
    } else {                        // fotocamera nella cornice
      const lens = new THREE.Mesh(new THREE.CircleGeometry(1.1, 24), new THREE.MeshBasicMaterial({ color: 0x1b2430 }));
      lens.position.set(0, h / 2 - bezel / 2, depth / 2 + 1.25);
      g.add(lens);
    }
    if (buttons) {                  // tasti laterali
      const btn = (x, y, len) => {
        const m = new THREE.Mesh(new THREE.CapsuleGeometry(0.9, len, 4, 12), metal);
        m.position.set(x, y, 0);
        g.add(m);
      };
      btn(w / 2 + 1.4, 28, 16);
      btn(-w / 2 - 1.4, 40, 9);
      btn(-w / 2 - 1.4, 26, 9);
    }
    return g;
  }

  function rounded(w, h, r) {
    const s = new THREE.Shape(), x = -w / 2, y = -h / 2;
    s.moveTo(x + r, y);
    s.lineTo(x + w - r, y); s.quadraticCurveTo(x + w, y, x + w, y + r);
    s.lineTo(x + w, y + h - r); s.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
    s.lineTo(x + r, y + h); s.quadraticCurveTo(x, y + h, x, y + h - r);
    s.lineTo(x, y + r); s.quadraticCurveTo(x, y, x + r, y);
    return s;
  }

  // le coordinate UV di ShapeGeometry sono in millimetri: riportate a 0..1 sul rettangolo dello schermo
  function withUv(geo) {
    geo.computeBoundingBox();
    const { min, max } = geo.boundingBox, pos = geo.attributes.position, uv = geo.attributes.uv;
    for (let i = 0; i < pos.count; i++)
      uv.setXY(i, (pos.getX(i) - min.x) / (max.x - min.x), (pos.getY(i) - min.y) / (max.y - min.y));
    return geo;
  }

  // ── inquadratura: tutta la composizione nello spazio del riquadro, qualunque sia la sua forma ──
  function resize() {
    const r = canvas.getBoundingClientRect();
    if (!r.width || !r.height) return;
    renderer.setSize(r.width, r.height, false);
    camera.aspect = r.width / r.height;
    // centro e mezze misure della composizione (tablet + telefono), con un po' di margine per il movimento
    const cx = CX, cy = CY, halfW = 182, halfH = 138;
    const t = Math.tan(THREE.MathUtils.degToRad(camera.fov / 2));
    camera.position.set(cx, cy, Math.max(halfH / t, halfW / (t * camera.aspect)));
    camera.lookAt(cx, cy, 0);
    camera.updateProjectionMatrix();
  }
  new ResizeObserver(() => { resize(); render(); }).observe(canvas);

  // ── movimento: segue piano il puntatore e «respira»; fermo fuori vista ──
  let tx = 0, ty = 0, rx = 0, ry = 0, visible = true, running = false;
  addEventListener('pointermove', e => {
    const r = canvas.getBoundingClientRect();
    tx = Math.max(-1, Math.min(1, (e.clientX - (r.left + r.width / 2)) / r.width));
    ty = Math.max(-1, Math.min(1, (e.clientY - (r.top + r.height / 2)) / r.height));
  }, { passive: true });
  new IntersectionObserver(([en]) => { visible = en.isIntersecting; if (visible && !reduce) loop(); }).observe(canvas);

  function render() { renderer.render(scene, camera); }

  function loop() {
    if (running) return;
    running = true;
    const t0 = performance.now();
    const frame = now => {
      if (!visible) { running = false; return; }
      const t = (now - t0) / 1000;
      rx += (ty * 0.12 - rx) * 0.05;
      ry += (tx * 0.22 - ry) * 0.05;
      group.rotation.set(rx, ry, 0);
      tablet.position.y = TY + Math.sin(t * 0.9) * 2.2;
      phone.position.y = PY + Math.sin(t * 0.9 + 1.7) * 3;
      render();
      requestAnimationFrame(frame);
    };
    requestAnimationFrame(frame);
  }
}
