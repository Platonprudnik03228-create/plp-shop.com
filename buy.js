// PLP Pro checkout: Paddle overlay → licence key from the PLP licence server, shown right on the page.
(function () {
  const CFG = {
    token: 'live_7ad476497b7867b581e65773ef3',          // Paddle client-side token (live_...), public by design
    price: 'pri_01m79geawfv2rnh2apzgkj1ztz',          // Paddle price id (pri_...)
    api: 'https://plp-license.prudnyk0369.workers.dev',            // licence server, e.g. https://plp-license.<name>.workers.dev
    env: 'production',
  };
  const ru = /^(ru|uk|be)/i.test(document.documentElement.lang || navigator.language || '');
  const T = ru ? {
    wait: 'Оплата прошла. Готовим ключ…', title: 'Спасибо! Ваш ключ PLP Pro', copy: 'Скопировать ключ', copied: 'Скопировано',
    how: 'Откройте PLP Seasons → снежинка в строке меню → «Ввести ключ…» → вставьте ключ. Ключ продлевается сам каждый месяц, пока идёт подписка. Письмо с чеком Paddle пришлёт на вашу почту — сохраните его. Нужна версия 0.4.86 или новее: <a href="https://github.com/Platonprudnik03228-create/plp-shop.com/releases/download/v0.4.86/PLP-Seasons-0.4.86-macOS-arm64.zip">скачать</a>.',
    fail: 'Оплата прошла, но ключ не удалось получить. Напишите нам (страница «Контакты») и укажите номер заказа:', close: 'Закрыть',
  } : {
    wait: 'Payment received. Preparing your key…', title: 'Thank you! Your PLP Pro key', copy: 'Copy key', copied: 'Copied',
    how: 'Open PLP Seasons → snowflake in the menu bar → “Enter key…” → paste the key. It renews by itself every month while the subscription runs. Paddle emails you the receipt — keep it. Needs version 0.4.86 or newer: <a href="https://github.com/Platonprudnik03228-create/plp-shop.com/releases/download/v0.4.86/PLP-Seasons-0.4.86-macOS-arm64.zip">download</a>.',
    fail: 'Payment went through, but the key could not be fetched. Write to us (Contact page) with your order number:', close: 'Close',
  };
  const ready = CFG.token && CFG.price && CFG.api;
  let loaded = null;
  function load() {
    if (loaded) return loaded;
    loaded = new Promise((res, rej) => {
      const s = document.createElement('script'); s.src = 'https://cdn.paddle.com/paddle/v2/paddle.js';
      s.onload = () => {
        if (CFG.env !== 'production') Paddle.Environment.set(CFG.env);
        Paddle.Initialize({ token: CFG.token, eventCallback: onEvent });
        res();
      };
      s.onerror = rej; document.head.appendChild(s);
    });
    return loaded;
  }
  function box(html) {
    let m = document.getElementById('plpKeyModal');
    if (!m) {
      m = document.createElement('div'); m.id = 'plpKeyModal';
      m.style.cssText = 'position:fixed;inset:0;z-index:99999;background:rgba(8,14,28,.55);display:flex;align-items:center;justify-content:center;padding:20px';
      document.body.appendChild(m);
    }
    m.innerHTML = '<div style="background:#fff;color:#0b1220;max-width:520px;width:100%;border-radius:18px;padding:26px;box-shadow:0 20px 60px rgba(0,0,0,.3);font:15px/1.5 Inter,system-ui,sans-serif">' + html + '</div>';
    return m;
  }
  async function getKey(txn) {
    box('<p style="margin:0">' + T.wait + '</p>');
    for (let i = 0; i < 4; i++) {
      try {
        const r = await fetch(CFG.api + '/api/paddle/claim', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ transaction_id: txn }) });
        const j = await r.json();
        if (j.license) {
          try { localStorage.setItem('plpLicense', j.license); } catch (e) {}
          const m = box('<h3 style="margin:0 0 12px">' + T.title + '</h3><textarea readonly id="plpKeyText" style="width:100%;height:110px;font:12px/1.4 ui-monospace,Menlo,monospace;border:1px solid #cdd6e4;border-radius:10px;padding:10px;resize:none"></textarea>' +
            '<button id="plpKeyCopy" style="margin:12px 0;padding:10px 16px;border:0;border-radius:10px;background:#1769d2;color:#fff;font-weight:600;cursor:pointer">' + T.copy + '</button>' +
            '<p style="margin:0 0 14px;color:#334">' + T.how + '</p><button id="plpKeyClose" style="padding:8px 14px;border:1px solid #cdd6e4;border-radius:10px;background:#fff;cursor:pointer">' + T.close + '</button>');
          m.querySelector('#plpKeyText').value = j.license;
          m.querySelector('#plpKeyCopy').onclick = (e) => { navigator.clipboard.writeText(j.license); e.target.textContent = T.copied; };
          m.querySelector('#plpKeyClose').onclick = () => m.remove();
          return;
        }
        if (j.error !== 'pending') break;
      } catch (e) {}
      await new Promise(r => setTimeout(r, 4000));
    }
    const m = box('<p style="margin:0 0 12px">' + T.fail + '</p><code style="user-select:all">' + txn + '</code><p><a href="legal.html">' + (ru ? 'Контакты' : 'Contact') + '</a></p><button id="plpKeyClose" style="padding:8px 14px;border:1px solid #cdd6e4;border-radius:10px;background:#fff;cursor:pointer">' + T.close + '</button>');
    m.querySelector('#plpKeyClose').onclick = () => m.remove();
  }
  function onEvent(ev) {
    if (ev && ev.name === 'checkout.completed' && ev.data && ev.data.transaction_id) {
      setTimeout(() => { try { Paddle.Checkout.close(); } catch (e) {} getKey(ev.data.transaction_id); }, 1500);
    }
  }
  document.addEventListener('click', async (e) => {
    const a = e.target.closest && e.target.closest('a[data-i18n="ctaStart"], .buy-pro');
    if (!a || !ready) return;
    e.preventDefault();
    try {
      await load();
      Paddle.Checkout.open({ items: [{ priceId: CFG.price, quantity: 1 }], settings: { displayMode: 'overlay', theme: 'light', locale: ru ? 'ru' : 'en' } });
    } catch (err) { location.href = a.getAttribute('href'); }
  });
  // show the real price from Paddle on the buttons (currency of the visitor)
  if (ready) load().then(() => Paddle.PricePreview({ items: [{ priceId: CFG.price, quantity: 1 }] })).then((r) => {
    const li = r && r.data && r.data.details && r.data.details.lineItems && r.data.details.lineItems[0];
    const price = li && li.formattedTotals && li.formattedTotals.total;
    if (!price) return;
    document.querySelectorAll('a[data-i18n="ctaStart"]').forEach((a) => { a.textContent = a.textContent.replace(/[$€£]?\s?\d+[.,]\d{2}\s?[$€£]?/, price); });
    document.querySelectorAll('.plan-heading strong').forEach((s) => { const n = s.firstChild; if (n && n.nodeType === 3 && /\d/.test(n.textContent)) n.textContent = price; });
  }).catch(() => {});
})();
