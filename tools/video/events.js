const { chromium } = require('playwright');
(async () => {
  const [page, out] = process.argv.slice(2);
  const b = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium' });
  const p = await b.newPage();
  await p.goto('file://' + __dirname + '/' + page + '?t=0');
  const ev = await p.evaluate(() => ({ dur: window.DUR, events: window.events() }));
  require('fs').writeFileSync(out, JSON.stringify(ev, null, 1));
  console.log(ev.events.length, 'eventi');
  await b.close();
})();
