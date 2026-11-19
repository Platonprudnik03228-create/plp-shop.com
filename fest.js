// PLP Seasons — the New Year tricks of the app, playable right in the demo on the home page (owner: "let people try it on
// the site: roll a snowball with the mouse, knock the icicles down, Santa flies over every 20–30 s and drops a gift with
// "click the gift", a snowman comes out with "double-click — it falls apart"). The demo itself is the instructions.
(function () {
  const TT = [{ gift: 'Нажми на подарок 🎁', man: 'Кликни 2 раза — развалится', roll: 'Зажми снег и катай — снежок растёт', throw: 'Отпусти — бросок!', ice: 'Проведи мышкой по сосулькам',
    wishes: ['С наступающим Новым годом!', 'Одевайся теплее — шапку не забудь', 'Пусть в новом году всё получится', 'Загадай желание — сбудется!', 'Больше тепла, меньше дедлайнов', 'Выпей какао и отдохни', 'Позвони близким — им будет приятно', 'Слепи снеговика — это полезно!', 'Мандарины — лучший витамин C', 'Ты молодец. Правда.'] }
    , { gift: 'Click the gift 🎁', man: 'Double-click — it falls apart', roll: 'Press the snow and roll — the ball grows', throw: 'Let go — throw!', ice: 'Swipe the mouse across the icicles',
    wishes: ['Happy New Year to you!', "Dress warm — don't forget your hat", 'May everything work out this year', 'Make a wish — it will come true!', 'More warmth, fewer deadlines', 'Have a cocoa and take a break', 'Call someone you love today', "Build a snowman — doctor's orders!", 'Tangerines: the best vitamin C', "You're doing great. Really."] }];
  const T_ = () => TT[/^(ru|uk|be)/i.test(document.documentElement.lang || '') ? 0 : 1];
  const img = n => { const i = new Image(); i.src = 'img/' + n + '.png'; return i; };
  const SLEIGH = img('sleigh'), DEER = [img('deer0'), img('deer1')];
  const FIRE = ['#ff4852', '#5aff80', '#669eff', '#ffd65a', '#ff8c32', '#e673ff', '#ffffff'];
  const rnd = (a, b) => a + Math.random() * (b - a);

  document.addEventListener('DOMContentLoaded', () => {
    const hero = document.querySelector('#demo .scene-stage') || document.getElementById('demo');
    if (!hero) return;
    if (getComputedStyle(hero).position === 'static') hero.style.position = 'relative';
    const cv = document.createElement('canvas');
    cv.style.cssText = 'position:absolute;inset:0;width:100%;height:100%;pointer-events:none;z-index:2';
    cv.setAttribute('aria-hidden', 'true');
    hero.appendChild(cv);
    hero.style.touchAction = 'pan-y';                  // a sideways swipe on a phone rolls the snowball, an up/down one still scrolls
    const g = cv.getContext('2d');
    let W = 0, H = 0, dpr = 1, t = 0;
    let colW = 4, drift = [];
    const shared = () => { const a = window.PLPGround && window.PLPGround(); if (a && a.length) drift = a; };
    function resize() {
      dpr = Math.min(2, devicePixelRatio || 1); const r = hero.getBoundingClientRect(); W = r.width; H = r.height;
      cv.width = Math.round(W * dpr); cv.height = Math.round(H * dpr);
      shared();
      icicles = [];
    }
    const winter = () => { const b = document.querySelector('#seasonSeg button[aria-pressed="true"]'); return !b || b.dataset.season === 'winter'; };
    const col = x => Math.max(0, Math.min(drift.length - 1, Math.floor(x / colW)));
    let floorY = 0;                                       // the snow field the snowmen stand on: just above the demo's control bar
    const floor = () => { const c = hero.querySelector('.controls'); if (c && c.offsetParent) { const a = c.getBoundingClientRect(), b = hero.getBoundingClientRect(); floorY = Math.min(H - 6, a.top - b.top - 6); } else floorY = H - 6; };
    const top = x => Math.min(H - (drift[col(x)] || 0), floorY);

    let balls = [], men = [], splats = [], sparks = [], gifts = [], hints = [], icicles = [];
    let sleigh = null, nextSleigh = 8, held = null, mouse = null, last = null, mv = { x: 0, y: 0 }, shownRoll = false;
    function hint(text, x, y, life) { if (hints.some(h => h.text === text)) return; hints.push({ text, x, y, age: 0, life: life || 3.5 }); }
    function spark(x, y, vx, vy, life, size, color, kind, grav) { if (sparks.length < 900) sparks.push({ x, y, vx, vy, age: 0, life, size, color, kind, grav, rot: rnd(0, 6), spin: rnd(-12, 12) }); }

    // ---------- pointer (the controls of the demo keep their clicks) ----------
    const isControl = e => e.target.closest && e.target.closest('button,a,input,select,label,.hero-copy,.controls,.hero-plans,.hint');
    const pos = e => { const r = hero.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top }; };
    hero.addEventListener('pointerdown', e => {
      if (!winter() || isControl(e)) return;
      const p = pos(e);
      const gi = gifts.findIndex(o => Math.abs(o.x - p.x) < 34 && Math.abs(o.y - p.y) < 34);
      if (gi >= 0) { openGift(gifts[gi]); gifts.splice(gi, 1); return; }
      if (sleigh && p.x > sleigh.x - 90 && p.x < sleigh.x + 300 && Math.abs(p.y - sleigh.y) < 60 && sleigh.dir > 0 || sleigh && p.x < sleigh.x + 90 && p.x > sleigh.x - 300 && Math.abs(p.y - sleigh.y) < 60 && sleigh.dir < 0) { dropGift(); return; }
      if (manAt(p)) return;                                                          // a double-click may follow
      const b = balls.find(o => Math.hypot(o.x - p.x, o.y - p.y) < o.r + 8);
      if (b) { held = b; b.vx = b.vy = 0; return; }
      if (p.y > top(p.x) - 40) {                                                       // scoop a snowball from the drift
        held = { x: p.x, y: top(p.x) - 8, r: 8, vx: 0, vy: 0, ang: 0, fly: 9 };
        balls.push(held); for (let k = -3; k <= 3; k++) { const c = col(p.x) + k; if (drift[c] > 2) drift[c] = Math.max(0, drift[c] - 3 * Math.exp(-k * k / 5)); }
        if (!shownRoll) { shownRoll = true; hint(T_().roll, p.x, p.y - 60, 3); }
        e.preventDefault(); return;
      }
      // anywhere else: a snowball flies there from below and splats on the glass
      balls.push({ x: W / 2 + rnd(-80, 80), y: H + 30, r: 10, vx: 0, vy: 0, ang: 0, fly: 0, to: p, t0: t });
    });
    hero.addEventListener('pointermove', e => {
      const p = pos(e), now = performance.now();
      if (last) { const dt = Math.max(8, now - last.t) / 1000; mv = { x: (p.x - last.x) / dt, y: (p.y - last.y) / dt }; }
      last = { x: p.x, y: p.y, t: now }; mouse = p;
      if (!winter()) return;
      if (held) {
        const g0 = top(p.x), dx = p.x - held.x; let ny = p.y;
        if (ny + held.r > g0 - 2) {                       // rolling on the snow: it grows and leaves a track
          ny = g0 - held.r * 0.9;
          const d = drift[col(p.x)];
          if (true) { held.r = Math.min(46, Math.sqrt(held.r * held.r + Math.abs(dx) * Math.max(6, Math.min(d, held.r)) * 0.6 / Math.PI)); for (let c = col(Math.min(p.x, held.x)); c <= col(Math.max(p.x, held.x)); c++) drift[c] = Math.max(0, drift[c] * 0.9); }
          held.ang += dx / held.r;
        }
        held.x = p.x; held.y = ny;
        if (held.r > 14) hint(T_().throw, p.x, ny - held.r - 40, 2);
      }
      // the icicles under the top edge fall when the mouse sweeps them fast
      const sp = Math.hypot(mv.x, mv.y);
      if (p.y < 70 && sp > 250) for (const c of icicles) if (!c.falling && c.len > 6 && Math.abs(c.x - p.x) < c.w + 10) { c.falling = true; c.y = c.len / 2; c.vy = 0; }
    });
    addEventListener('pointerup', () => { if (held) { held.vx = Math.max(-900, Math.min(900, mv.x * 0.5)); held.vy = Math.max(-900, Math.min(900, mv.y * 0.5)); held.fly = 0; held = null; } });
    hero.addEventListener('dblclick', e => {
      if (!winter() || isControl(e)) return;
      const p = pos(e), m = manAt(p);
      if (m) { crumble(m); return; }
      const young = balls.findIndex(b => b.r <= 8.5 && Math.hypot(b.x - p.x, b.y - p.y) < 20);
      if (young >= 0) balls.splice(young, 1);
      if (p.y > top(p.x) - 60) raise(p.x);
    });
    hero.addEventListener('pointerleave', () => { mouse = null; last = null; });

    function manAt(p) { return men.find(m => Math.abs(p.x - m.x) < m.r[0] + 6 && p.y < m.y + 6 && p.y > m.y - (m.r[0] + m.r[1] + m.r[2]) * 1.9); }
    function raise(x, delay) {
      const r0 = 24, m = { x: Math.max(40, Math.min(W - 40, x)), y: top(x) + 3, r: [r0, r0 * 0.75, r0 * 0.55], born: t + (delay || 0), look: 1, headless: false };
      men.push(m); if (men.length > 4) men.shift();
      setTimeout(() => hint(T_().man, m.x, m.y - 128, 5), ((delay || 0) + 0.9) * 1000);
    }
    function crumble(m) {
      const n = men.indexOf(m); if (n >= 0) men.splice(n, 1);
      for (let i = 0; i < 46; i++) spark(m.x + rnd(-24, 24), m.y - rnd(0, 110), rnd(-170, 170), -rnd(60, 280), rnd(0.6, 1.2), rnd(3, 7), '#fff', 5, 900);
      for (let c = col(m.x - 30); c <= col(m.x + 30); c++) drift[c] = Math.min(18, (drift[c] || 0) + 8);
    }
    function splat(x, y, vx, vy) {
      const r = rnd(28, 40), dir = Math.atan2(vy, vx), core = r * rnd(0.3, 0.38), lumps = [];
      for (let i = 0, n = 12 + Math.random() * 8 | 0; i < n; i++) { const a = rnd(0, 6.28), d = core * Math.sqrt(Math.random()); lumps.push([Math.cos(a) * d, Math.sin(a) * d, core * rnd(0.45, 0.8)]); }
      for (let i = 0, n = 16 + Math.random() * 10 | 0; i < n; i++) {
        const fwd = Math.random() < 0.75, a = (fwd ? rnd(-0.9, 0.9) : rnd(0, 6.28)) + (fwd ? dir : 0), d = core * (1.1 + rnd(0, 1.9));
        if (d < r * 0.92) lumps.push([Math.cos(a) * d, Math.sin(a) * d, Math.max(1.2, core * 0.6 * (1 - d / r) * rnd(0.45, 1.1))]);
      }
      splats.push({ x, y, r, lumps, age: 0, slide: 0 }); if (splats.length > 14) splats.shift();
      for (let i = 0; i < 10; i++) { const a = rnd(0, 6.28); spark(x, y, Math.cos(a) * rnd(60, 200), Math.sin(a) * rnd(60, 200) - 40, rnd(0.4, 0.8), rnd(2, 4), '#fff', 5, 700); }
    }
    function dropGift() {
      if (!sleigh || sleigh.dropped) return; sleigh.dropped = true;
      gifts.push({ x: sleigh.x, y: sleigh.y + 20, vy: -40, c: Math.random() * 3 | 0, landed: false, age: 0 });
    }
    function openGift(o) {
      for (let k = 0; k < 70; k++) { const a = rnd(0.15, 0.85) * Math.PI; spark(o.x, o.y, Math.cos(a) * rnd(80, 330), -Math.sin(a) * rnd(160, 520), rnd(1.6, 2.8), rnd(4, 7), FIRE[k % FIRE.length], 2, 420); }
      hints.push({ text: T_().wishes[Math.random() * T_().wishes.length | 0], x: o.x, y: o.y - 175, age: 0, life: 6, big: true });
      raise(o.x + (o.x < W / 2 ? 80 : -80), 0.6);
    }

    // ---------- the world ----------
    function step(dt) {
      t += dt;
      if (!icicles.length && W > 0) { let x = rnd(14, 40); while (x < W - 14) { const big = Math.random() < 0.25, max = big ? rnd(26, 42) : rnd(10, 22); icicles.push({ x, max, len: max * rnd(0.5, 0.85), w: big ? rnd(6, 9) : rnd(4, 6), falling: false, y: 0, vy: 0, rot: 0, regrow: 0 }); x += rnd(22, 60); } }
      for (const c of icicles) {
        if (c.falling) { c.vy += 1500 * dt; c.y += c.vy * dt; c.rot += dt * 1.4; if (c.y + c.len / 2 >= top(c.x)) { for (let i = 0; i < 10; i++) spark(c.x, top(c.x) - 2, rnd(-140, 140), -rnd(40, 240), rnd(0.4, 0.9), rnd(1.5, 3.5), '#d8f2ff', 3, 900); c.falling = false; c.len = 0; c.regrow = 20; } }
        else if (c.regrow > 0) c.regrow -= dt; else c.len = Math.min(c.max, c.len + dt * 2);
      }
      if (t > 4 && t < 4.1 && !shownRoll) hint(T_().ice, W * 0.3, 92, 4);
      // Santa every 20–30 s
      if (!sleigh) { nextSleigh -= dt; if (nextSleigh <= 0) { nextSleigh = rnd(20, 30); const dir = Math.random() < 0.5 ? 1 : -1; sleigh = { x: dir > 0 ? -320 : W + 320, y0: H * rnd(0.18, 0.3), y: 0, dir, age: 0, dropped: false }; } }
      else {
        sleigh.age += dt; sleigh.x += sleigh.dir * (W + 640) / 10 * dt; sleigh.y = sleigh.y0 + Math.sin(sleigh.age * 1.6) * 14;
        if (Math.random() < dt * 30) spark(sleigh.x - sleigh.dir * 40, sleigh.y + 18, -sleigh.dir * rnd(10, 50), rnd(-10, 40), rnd(0.8, 1.5), rnd(2, 4.5), Math.random() < 0.7 ? '#ffd866' : '#fff', 4, 40);
        if (!sleigh.dropped && sleigh.age > 2.6 && sleigh.x > W * 0.2 && sleigh.x < W * 0.8) dropGift();
        if (sleigh.dir > 0 ? sleigh.x > W + 360 : sleigh.x < -360) sleigh = null;
      }
      for (const o of gifts) { o.age += dt; if (!o.landed) { o.vy += 650 * dt; o.y += o.vy * dt; if (o.y >= top(o.x) - 20) { o.y = top(o.x) - 20; o.landed = true; o.age = 0; } } else o.y = top(o.x) - 20; }
      gifts = gifts.filter(o => !(o.landed && o.age > 60));
      for (const b of balls) {
        if (b === held) continue;
        if (b.to) {                                          // flying from below into the glass
          const u = (t - b.t0) / 0.32;
          if (u >= 1) { splat(b.to.x, b.to.y, b.to.x - b.x0x || 1, -1); b.dead = true; continue; }
          b.x0x = b.x0x ?? b.x; b.y0y = b.y0y ?? b.y;
          b.cx = b.x0x + (b.to.x - b.x0x) * u; b.cy = b.y0y + (b.to.y - b.y0y) * u - Math.sin(u * Math.PI) * 60; b.cr = 20 - 10 * u; continue;
        }
        b.fly += dt; b.vy += 1500 * dt; b.x += b.vx * dt; b.y += b.vy * dt;
        if (b.fly > 0.26 && b.fly < 0.31 && Math.hypot(b.vx, b.vy) > 260 && b.y < H * 0.75) { splat(b.x, b.y, b.vx, b.vy); b.dead = true; continue; }
        // a thrown ball knocks a snowman's head off, a second one the rest
        const m = men.find(m => Math.abs(b.x - m.x) < m.r[0] + b.r && b.y > m.y - (m.r[0] + m.r[1] + m.r[2]) * 1.9 && b.y < m.y && Math.hypot(b.vx, b.vy) > 300);
        if (m) { if (m.headless) crumble(m); else { m.headless = true; balls.push({ x: m.x, y: m.y - (m.r[0] * 1.6 + m.r[1] * 1.55 + m.r[2] * 0.75), r: m.r[2], vx: b.vx * 0.55, vy: -320, ang: 0, fly: 9, face: true }); } b.dead = true; continue; }
        const gy = top(b.x);
        if (b.y + b.r * 0.9 > gy) { b.y = gy - b.r * 0.9; b.vy = b.vy > 120 ? -b.vy * 0.18 : 0; b.vx *= Math.pow(0.18, dt); b.ang += b.vx * dt / b.r; }
        if (b.x < b.r) { b.x = b.r; b.vx = Math.abs(b.vx) * 0.3; } if (b.x > W - b.r) { b.x = W - b.r; b.vx = -Math.abs(b.vx) * 0.3; }
      }
      balls = balls.filter(b => !b.dead);
      for (const m of men) { const want = mouse ? (mouse.x < m.x - 8 ? -1 : mouse.x > m.x + 8 ? 1 : m.look) : 1; m.look += (want - m.look) * Math.min(1, dt * 5); }
      for (const s of splats) { s.age += dt; if (s.age > 2.5) s.slide += dt * Math.max(0, 5 - (s.age - 2.5) * 0.45); }
      splats = splats.filter(s => s.age < 14);
      for (const s of sparks) { s.age += dt; s.vy += s.grav * dt; const k = Math.pow(0.4, dt); s.vx *= k; s.vy *= k; if (s.kind === 2) s.vx += Math.sin(s.age * 7 + s.rot) * 40 * dt; s.x += s.vx * dt; s.y += s.vy * dt; s.rot += s.spin * dt; }
      sparks = sparks.filter(s => s.age < s.life && s.y < H + 20);
      for (const h of hints) h.age += dt; hints = hints.filter(h => h.age < h.life);
    }

    function ball(x, y, r, ang) {
      const gr = g.createRadialGradient(x - r * 0.35, y - r * 0.4, r * 0.1, x, y, r * 1.2);
      gr.addColorStop(0, '#fff'); gr.addColorStop(0.55, '#f2f6fc'); gr.addColorStop(1, '#c4d2e8');
      g.fillStyle = gr; g.beginPath(); g.arc(x, y, r, 0, 7); g.fill();
      g.fillStyle = 'rgba(90,120,160,.15)'; for (let k = 0; k < 6; k++) { const a = (ang || 0) + k * 2.4, rr = r * (0.3 + 0.55 * ((k * 0.618) % 1)); g.beginPath(); g.arc(x + Math.cos(a) * rr, y + Math.sin(a) * rr * 0.9, r * 0.09, 0, 7); g.fill(); }
    }
    function face(x, y, r, look, ang) {
      const R = (dx, dy) => [x + dx * Math.cos(ang) - dy * Math.sin(ang), y + dx * Math.sin(ang) + dy * Math.cos(ang)];
      const ex = r * 0.12 * look; g.fillStyle = '#1c1c20';
      for (const s of [-1, 1]) { const [px, py] = R(s * r * 0.3 + ex, -r * 0.15); g.beginPath(); g.arc(px, py, r * 0.1, 0, 7); g.fill(); }
      for (let k = 0; k < 5; k++) { const an = Math.PI * (0.2 + 0.6 * k / 4), [px, py] = R(ex * 0.7 + Math.cos(an) * r * 0.42, r * 0.05 + Math.sin(an) * r * 0.35); g.beginPath(); g.arc(px, py, r * 0.05, 0, 7); g.fill(); }
      const dir = look >= 0 ? 1 : -1, nl = r * 0.9 * Math.max(0.25, Math.abs(look));
      const a = R(ex, -r * 0.05), b = R(ex + dir * nl, r * 0.08), c = R(ex, r * 0.18);
      g.fillStyle = '#f0801e'; g.beginPath(); g.moveTo(...a); g.lineTo(...b); g.lineTo(...c); g.closePath(); g.fill();
    }
    function label(text, x0, y, a, size, big) {
      g.textAlign = 'center'; g.textBaseline = 'middle'; g.font = '700 ' + size.toFixed(1) + 'px Inter, system-ui, sans-serif';
      const w = g.measureText(text).width, x = Math.max(w / 2 + 14, Math.min(W - w / 2 - 14, x0));
      g.globalAlpha = a * 0.85; g.fillStyle = '#10182a'; g.beginPath(); g.roundRect ? g.roundRect(x - w / 2 - 12, y - size * 0.95, w + 24, size * 1.9, 14) : g.rect(x - w / 2 - 12, y - size * 0.95, w + 24, size * 1.9); g.fill();
      g.globalAlpha = a; g.fillStyle = big ? '#ffe6a8' : '#fff'; g.fillText(text, x, y); g.globalAlpha = 1;
    }
    function draw() {
      g.setTransform(dpr, 0, 0, dpr, 0, 0); g.clearRect(0, 0, W, H);
      if (!winter()) return;
      // icicles hanging from the top
      for (const c of icicles) {
        if (c.len < 1) continue;
        const y0 = c.falling ? c.y - c.len / 2 : 0;
        g.save(); g.translate(c.x, y0 + c.len / 2); g.rotate(c.falling ? c.rot : 0); g.translate(-c.x, -(y0 + c.len / 2));
        const gr = g.createLinearGradient(c.x - c.w / 2, 0, c.x + c.w / 2, 0); gr.addColorStop(0, 'rgba(185,215,245,.6)'); gr.addColorStop(0.35, 'rgba(242,250,255,.95)'); gr.addColorStop(0.6, 'rgba(205,230,255,.65)'); gr.addColorStop(1, 'rgba(140,184,230,.55)');
        g.fillStyle = gr; g.beginPath(); g.moveTo(c.x - c.w / 2, y0); g.lineTo(c.x + c.w / 2, y0); g.quadraticCurveTo(c.x + c.w * 0.25, y0 + c.len * 0.6, c.x + c.w * 0.03, y0 + c.len); g.quadraticCurveTo(c.x - c.w * 0.25, y0 + c.len * 0.6, c.x - c.w / 2, y0); g.fill(); g.restore();
      }
      // the sleigh with four reindeer, the lead one with a red nose
      if (sleigh && SLEIGH.naturalWidth && DEER[0].naturalWidth && DEER[1].naturalWidth) {
        const k = 1.15; g.save(); if (sleigh.dir < 0) { g.translate(sleigh.x * 2, 0); g.scale(-1, 1); }
        let lx = sleigh.x + 32 * k, ly = sleigh.y - 4 * k; g.strokeStyle = '#8c5a28'; g.lineWidth = 1.4;
        for (let i = 0; i < 4; i++) {
          const x = sleigh.x + (72 + i * 47) * k, y = sleigh.y - (8 + Math.sin(t * 9 + i * 1.1) * 3 + i * 2) * k, w = 58 * k, h = w * 0.75;
          g.beginPath(); g.moveTo(lx, ly); g.lineTo(x - w * 0.3, y + h * 0.04); g.stroke(); lx = x - w * 0.3; ly = y + h * 0.04;
          g.drawImage(DEER[((t * 9 | 0) + i) % 2], x - w / 2, y - h / 2, w, h);
          if (i === 3) { const nx = x + w * 0.402, ny = y - h * 0.197, gr = g.createRadialGradient(nx, ny, 0, nx, ny, 9); gr.addColorStop(0, 'rgba(255,96,80,1)'); gr.addColorStop(0.4, 'rgba(255,32,16,.55)'); gr.addColorStop(1, 'rgba(255,32,16,0)'); g.fillStyle = gr; g.beginPath(); g.arc(nx, ny, 9, 0, 7); g.fill(); }
        }
        const sw = 110 * k, sh = sw * 0.692; g.drawImage(SLEIGH, sleigh.x - sw / 2, sleigh.y - sh / 2, sw, sh); g.restore();
      }
      // gifts
      const GC = [['#d8262e', '#f4c542'], ['#1f8a4c', '#e8303a'], ['#2f6fd6', '#e9eef5']];
      for (const o of gifts) {
        const w = 40, bob = o.landed ? Math.max(0, Math.sin(o.age * 4)) * 2 : 0, [a, b] = GC[o.c];
        g.fillStyle = a; g.fillRect(o.x - w * 0.38, o.y - w * 0.15 - bob, w * 0.76, w * 0.6); g.fillRect(o.x - w * 0.43, o.y - w * 0.3 - bob, w * 0.86, w * 0.2);
        g.fillStyle = b; g.fillRect(o.x - w * 0.06, o.y - w * 0.3 - bob, w * 0.12, w * 0.75);
        g.strokeStyle = b; g.lineWidth = w * 0.08; g.beginPath(); g.ellipse(o.x - w * 0.12, o.y - w * 0.39 - bob, w * 0.12, w * 0.08, 0, 0, 7); g.stroke(); g.beginPath(); g.ellipse(o.x + w * 0.12, o.y - w * 0.39 - bob, w * 0.12, w * 0.08, 0, 0, 7); g.stroke();
      }
      // a waiting gift keeps a pulsing "click me" label over it
      for (const o of gifts) if (o.landed) {
        const pulse = 1 + Math.sin(t * 5) * 0.06, a = Math.min(1, o.age * 3), gr = g.createRadialGradient(o.x, o.y, 0, o.x, o.y, 42);
        gr.addColorStop(0, 'rgba(255,220,120,' + (0.35 + Math.sin(t * 5) * 0.12) * a + ')'); gr.addColorStop(1, 'rgba(255,220,120,0)'); g.fillStyle = gr; g.beginPath(); g.arc(o.x, o.y, 42, 0, 7); g.fill();
        label(T_().gift, o.x, o.y - 52, a, 16 * pulse, false);
      }
      // snowmen
      for (const m of men) {
        const grow = Math.max(0, Math.min(1, (t - m.born) / 0.8)); if (grow <= 0) continue;
        const s = 0.6 + 0.4 * grow, [r0, r1, r2] = m.r.map(r => r * s), c0 = m.y - r0 * 0.9, c1 = c0 - r0 * 0.78 - r1 * 0.75, c2 = c1 - r1 * 0.8 - r2 * 0.75;
        g.strokeStyle = '#5c3c24'; g.lineWidth = 3; g.lineCap = 'round';
        g.beginPath(); g.moveTo(m.x - r1 * 0.7, c1); g.lineTo(m.x - r1 * 1.9, c1 - r1 * 0.7); g.stroke();
        g.beginPath(); g.moveTo(m.x + r1 * 0.7, c1); g.lineTo(m.x + r1 * 1.8, c1 - r1 * (0.9 + Math.sin(t * 6) * 0.3)); g.stroke();
        ball(m.x, c0, r0, 1); ball(m.x, c1, r1, 2);
        g.fillStyle = '#1c1c20'; for (let i = 0; i < 3; i++) { g.beginPath(); g.arc(m.x, c1 - r1 * 0.45 + i * r1 * 0.4, r1 * 0.09, 0, 7); g.fill(); }
        g.fillStyle = '#ce2834'; g.fillRect(m.x - r2 * 1.05, c2 + r2 * 0.6, r2 * 2.1, r2 * 0.35);
        if (m.headless) continue;
        ball(m.x, c2, r2, 3); face(m.x, c2, r2, Math.max(-1, Math.min(1, m.look)), 0);
        g.fillStyle = '#2a5aaa'; g.beginPath(); g.roundRect ? g.roundRect(m.x - r2 * 0.8, c2 - r2 * 1.45, r2 * 1.6, r2 * 0.75, r2 * 0.35) : g.rect(m.x - r2 * 0.8, c2 - r2 * 1.45, r2 * 1.6, r2 * 0.75); g.fill();
        g.fillStyle = '#f6f9fd'; g.beginPath(); g.arc(m.x, c2 - r2 * 1.5, r2 * 0.22, 0, 7); g.fill();
      }
      for (const b of balls) { if (b.to) { if (b.cx !== undefined) ball(b.cx, b.cy, b.cr, 0); continue; } ball(b.x, b.y, b.r, b.ang); if (b.face) face(b.x, b.y, b.r, 1, b.ang || 0); }
      // splats on the glass
      for (const s of splats) {
        const fade = s.age > 10 ? Math.max(0, 1 - (s.age - 10) / 4) : 1, pop = Math.min(1, s.age * 9), k = 0.6 + 0.4 * pop;
        g.save(); g.globalAlpha = 0.97 * fade; g.translate(s.x, s.y + s.slide); g.scale(k, k);
        g.shadowColor = 'rgba(20,32,55,.45)'; g.shadowBlur = 4; g.shadowOffsetY = 1;
        const gr = g.createRadialGradient(-s.r * 0.15, -s.r * 0.15, 0, 0, 0, s.r); gr.addColorStop(0, '#fff'); gr.addColorStop(0.45, '#f4f7fc'); gr.addColorStop(1, '#dce4f2');
        g.fillStyle = gr; g.beginPath(); for (const [x, y, r] of s.lumps) { g.moveTo(x + r, y); g.arc(x, y, r, 0, 7); } g.fill(); g.restore();
      }
      // sparks: confetti, powder, ice, glitter
      for (const s of sparks) {
        const k = s.age / s.life; g.globalAlpha = s.kind === 2 ? (k > 0.8 ? (1 - k) * 5 : 1) : Math.pow(1 - k, 1.3);
        g.fillStyle = s.color;
        if (s.kind === 2) { g.save(); g.translate(s.x, s.y); g.rotate(s.rot * 0.3); g.fillRect(-s.size / 2, -s.size * 0.22 * Math.abs(Math.cos(s.rot)), s.size, s.size * 0.45 * Math.abs(Math.cos(s.rot)) + 0.5); g.restore(); }
        else { g.beginPath(); g.arc(s.x, s.y, s.size / 2, 0, 7); g.fill(); }
      }
      g.globalAlpha = 1;
      // hints and wishes
      g.textAlign = 'center'; g.textBaseline = 'middle';
      for (const h of hints) label(h.text, h.x, h.y - Math.min(1, h.age) * 18, Math.min(1, h.age * 3) * Math.min(1, (h.life - h.age) * 2), h.big ? 22 : 16, h.big);
      g.globalAlpha = 1;
    }
    let prev = 0;
    function frame(now) {
      const dt = Math.min(0.05, (now - (prev || now)) / 1000); prev = now;
      const r = hero.getBoundingClientRect();
      shared(); floor(); if (r.bottom > 0 && r.top < innerHeight && !document.hidden) { if (Math.abs(r.width - W) > 1 || Math.abs(r.height - H) > 1) resize(); step(dt); draw(); }
      requestAnimationFrame(frame);
    }
    resize(); floor(); addEventListener('resize', resize);
    if (location.hash === '#festdemo') {                   // a scripted check of every trick
      nextSleigh = 99; sleigh = { x: W * 0.2, y0: H * 0.22, y: 0, dir: 1, age: 0, dropped: false };
      setTimeout(() => { raise(W * 0.3); balls.push({ x: W * 0.55, y: top(W * 0.55) - 20, r: 22, vx: 0, vy: 0, ang: 0, fly: 9 }); splat(W * 0.7, H * 0.4, 1, 0.3); splat(W * 0.2, H * 0.3, -1, 0.5); icicles.forEach((c, i) => { if (i % 4 === 0) { c.falling = true; c.y = c.len / 2; c.vy = 0; } }); openGift({ x: W * 0.8, y: top(W * 0.8) - 15 }); }, 1500);
    }
    requestAnimationFrame(frame);
  });
})();
