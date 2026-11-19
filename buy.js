// PLP Seasons: bought once ($4.99, Mac + Windows + Android, forever). Paddle overlay → key from the PLP licence server,
// shown at once with the downloads, kept in this browser and always reachable on mykey.html.
(function () {
  const CFG = {
    token: 'live_7ad476497b7867b581e65773ef3',   // Paddle client-side token, public by design
    price: 'pri_01m7pzqw71bhmfa4pr3b8z1xsx',     // one-time $4.99
    api: 'https://plp-license.prudnyk0369.workers.dev',
    env: 'production',
  };
  const DL = {
    mac: 'https://github.com/Platonprudnik03228-create/plp-shop.com/releases/download/v0.4.94/PLP-Seasons-0.4.94-macOS-arm64.zip',
    win: 'https://github.com/Platonprudnik03228-create/plp-shop.com/releases/download/windows-v0.5.7/PLP-Seasons-0.5.7-Windows-x64.zip',
    android: 'android.html',
  };
  const ru = /^(ru|uk|be)/i.test(document.documentElement.lang || navigator.language || '');
  const T = ru ? {
    wait: 'Оплата прошла. Готовим ключ…', title: 'Спасибо! Ваш ключ PLP Seasons', copy: 'Скопировать ключ', copied: 'Скопировано ✓',
    open: 'Открыть в PLP Seasons (Mac / Android)', mac: 'Скачать для Mac', win: 'Скачать для Windows', android: 'Android (.apk)',
    how: 'Ключ подходит для Mac, Windows и Android и работает навсегда. Скачайте приложение, откройте его и вставьте ключ (на Mac можно просто нажать «Открыть в PLP Seasons»). Ключ сохранён на этом сайте: <a href="mykey.html">«Мой ключ»</a>.',
    fail: 'Оплата прошла, но ключ не удалось получить сразу. Откройте <a href="mykey.html">«Мой ключ»</a> и введите номер заказа:', close: 'Закрыть',
  } : {
    wait: 'Payment received. Preparing your key…', title: 'Thank you! Your PLP Seasons key', copy: 'Copy key', copied: 'Copied ✓',
    open: 'Open in PLP Seasons (Mac / Android)', mac: 'Download for Mac', win: 'Download for Windows', android: 'Android (.apk)',
    how: 'The key works on Mac, Windows and Android, forever. Download the app, open it and paste the key (on a Mac you can just press “Open in PLP Seasons”). Your key is saved on this site: <a href="mykey.html">“My key”</a>.',
    fail: 'Payment went through, but the key could not be fetched right away. Open <a href="mykey.html">“My key”</a> and enter your order number:', close: 'Close',
  };
  const ready = CFG.token && CFG.price && CFG.api;
  let loaded = null;
  function load() {
    if (loaded) return loaded;
    loaded = new Promise((res, rej) => {
      const s = document.createElement('script'); s.src = 'https://cdn.paddle.com/paddle/v2/paddle.js';
      s.onload = () => { if (CFG.env !== 'production') Paddle.Environment.set(CFG.env); Paddle.Initialize({ token: CFG.token, eventCallback: onEvent }); res(); };
      s.onerror = rej; document.head.appendChild(s);
    });
    return loaded;
  }
  function save(key, txn) { try { localStorage.setItem('plpLicense', key); if (txn) localStorage.setItem('plpOrder', txn); } catch (e) {} }
  const btn = 'display:inline-block;margin:6px 8px 6px 0;padding:11px 16px;border-radius:10px;font-weight:700;text-decoration:none;';
  function keyHtml(key) {
    return '<textarea readonly class="plpKeyText" style="width:100%;height:96px;font:12px/1.4 ui-monospace,Menlo,monospace;border:1px solid #cdd6e4;border-radius:10px;padding:10px;resize:none;box-sizing:border-box">' + key + '</textarea>' +
      '<p style="margin:10px 0"><button class="plpKeyCopy" style="padding:8px 14px;border:1px solid #cdd6e4;border-radius:10px;background:#fff;color:#0b1220;cursor:pointer">' + T.copy + '</button></p>' +
      '<p style="margin:6px 0"><a style="' + btn + 'background:#1769d2;color:#fff" href="plpseasons://activate?key=' + encodeURIComponent(key) + '">' + T.open + '</a></p>' +
      '<p style="margin:6px 0"><a style="' + btn + 'background:#0b1220;color:#fff" href="' + DL.mac + '">' + T.mac + '</a><a style="' + btn + 'background:#0b1220;color:#fff" href="' + DL.win + '">' + T.win + '</a><a style="' + btn + 'background:#2e8b57;color:#fff" href="' + DL.android + '">' + T.android + '</a></p>';
  }
  function wire(root, key) {
    root.querySelectorAll('.plpKeyCopy').forEach((b) => b.onclick = () => { navigator.clipboard.writeText(key); b.textContent = T.copied; });
  }
  function box(html) {
    let m = document.getElementById('plpKeyModal');
    if (!m) {
      m = document.createElement('div'); m.id = 'plpKeyModal';
      m.style.cssText = 'position:fixed;inset:0;z-index:99999;background:rgba(8,14,28,.55);display:flex;align-items:center;justify-content:center;padding:20px';
      document.body.appendChild(m);
    }
    m.innerHTML = '<div style="background:#fff;color:#0b1220;max-width:560px;width:100%;border-radius:18px;padding:26px;box-shadow:0 20px 60px rgba(0,0,0,.3);font:15px/1.5 Inter,system-ui,sans-serif;max-height:92vh;overflow:auto">' + html + '</div>';
    return m;
  }
  async function claim(txn) {
    const r = await fetch(CFG.api + '/api/paddle/claim', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ transaction_id: txn }) });
    return r.json();
  }
  async function getKey(txn) {
    box('<p style="margin:0">' + T.wait + '</p>');
    try { localStorage.setItem('plpOrder', txn); } catch (e) {}
    for (let i = 0; i < 4; i++) {
      try {
        const j = await claim(txn);
        if (j.license) {
          save(j.license, txn);
          const m = box('<h3 style="margin:0 0 12px">' + T.title + '</h3>' + keyHtml(j.license) + '<p style="margin:12px 0;color:#334">' + T.how + '</p><button class="plpClose" style="padding:8px 14px;border:1px solid #cdd6e4;border-radius:10px;background:#fff;cursor:pointer">' + T.close + '</button>');
          wire(m, j.license); m.querySelector('.plpClose').onclick = () => m.remove();
          return;
        }
        if (j.error !== 'pending') break;
      } catch (e) {}
      await new Promise(r => setTimeout(r, 4000));
    }
    const m = box('<p style="margin:0 0 12px">' + T.fail + '</p><code style="user-select:all">' + txn + '</code><p><button class="plpClose" style="padding:8px 14px;border:1px solid #cdd6e4;border-radius:10px;background:#fff;cursor:pointer">' + T.close + '</button></p>');
    m.querySelector('.plpClose').onclick = () => m.remove();
  }
  function onEvent(ev) {
    if (ev && ev.name === 'checkout.completed' && ev.data && ev.data.transaction_id) {
      setTimeout(() => { try { Paddle.Checkout.close(); } catch (e) {} getKey(ev.data.transaction_id); }, 1500);
    }
  }
  function openCheckout() {
    return load().then(() => Paddle.Checkout.open({ items: [{ priceId: CFG.price, quantity: 1 }], settings: { displayMode: 'overlay', theme: 'light', locale: ru ? 'ru' : 'en' } }));
  }
  document.addEventListener('click', async (e) => {
    const a = e.target.closest && e.target.closest('a[data-i18n="ctaStart"], .buy-pro');
    if (!a || !ready) return;
    e.preventDefault();
    try { await openCheckout(); } catch (err) { location.href = 'mykey.html'; }
  });
  // plp-shop.com/?buy=1 (the app's Buy button) → checkout right away
  if (ready && /[?&]buy=(1|pro)\b/.test(location.search)) openCheckout().catch(() => {});
  window.PLPBuy = { CFG, DL, T, ru, keyHtml, wire, claim, save };
})();
