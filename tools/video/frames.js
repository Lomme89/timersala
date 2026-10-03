const { chromium } = require('playwright');
(async () => {
  const [page, dir, fps = 30, times] = process.argv.slice(2);
  const b = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium' });
  const p = await b.newPage({ viewport: { width: +(process.env.W || 1280), height: Math.round((process.env.W || 1280) * 9 / 16) } });
  p.on('pageerror', e => console.log('ERR', e.message));
  await p.goto('file://' + __dirname + '/' + page + '?t=0', { waitUntil: 'networkidle' });
  await p.evaluate(() => document.fonts.ready);
  const list = times ? times.split(',').map(Number) : Array.from({ length: Math.round(await p.evaluate(() => window.DUR) * fps) }, (_, i) => i / fps);
  for (let i = 0; i < list.length; i++) {
    await p.evaluate(t => window.render(t), list[i]);
    await p.screenshot({ path: `${dir}/${times ? 't' + list[i] : 'f' + String(i).padStart(4, '0')}.png` });
  }
  await b.close();
})();
