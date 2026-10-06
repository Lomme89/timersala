// Numero dell'ultima versione stabile accanto ai pulsanti «Scarica» (elementi con data-version).
(async () => {
  const slots = document.querySelectorAll('[data-version]');
  if (!slots.length) return;
  try {
    const key = 'timersala-versione', cached = JSON.parse(sessionStorage.getItem(key) || 'null');
    let tag = cached && Date.now() - cached.at < 3600e3 ? cached.tag : null;
    if (!tag) {
      const res = await fetch('https://api.github.com/repos/Lomme89/timersala/releases/latest');
      if (!res.ok) return;
      tag = (await res.json()).tag_name;
      try { sessionStorage.setItem(key, JSON.stringify({ tag, at: Date.now() })); } catch {}
    }
    if (tag) slots.forEach(s => { s.textContent = tag; });
  } catch { /* senza versione il pulsante funziona lo stesso */ }
})();
