(() => {
  'use strict';

  const trigger = document.querySelector('.hero-logo');
  const hero = trigger.closest('.hero');
  const copy = hero.querySelector('.hero-copy');
  const art = trigger.parentElement;
  const gsap = window.OpenPortalMotion?.gsap;
  const game = document.querySelector('.logo-game');
  const board = game.querySelector('.game-board');
  const field = board.querySelector('svg');
  const player = game.querySelector('#game-player');
  const ball = game.querySelector('#game-ball');
  const trail = game.querySelector('#game-trail');
  const aim = game.querySelector('#game-aim');
  const impact = game.querySelector('#game-impact');
  const score = game.querySelector('#game-score');
  const result = game.querySelector('.game-result');
  const finalScore = game.querySelector('#game-final-score');
  const retry = game.querySelector('#game-retry');
  const sound = game.querySelector('#game-sound');
  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  const original = trigger.querySelector('svg');
  const mascot = window.OpenPortalLogoPlayer(original);
  const logoHome = { x: -62.5, y: 257, width: 125, height: 100 };
  const pose = { side: 1, charge: 0, swing: 0, lean: 0, reveal: 0 };
  const left = 14;
  const right = 626;
  const ceiling = 20;
  const paddleY = 278;
  const swingWindow = 280;
  const clamp = (value, min, max) => Math.max(min, Math.min(max, value));
  const keys = new Set();
  let taps = [];
  let state = 'closed';
  let frame = 0;
  let lastTime = 0;
  let serveAt = 0;
  let swingAt = -Infinity;
  let contactAt = -Infinity;
  let impactAt = -Infinity;
  let impactX = 0;
  let impactY = 0;
  let strongHit = false;
  let courtVisible = true;
  let pausedAt = null;
  let points = 0;
  let returned = false;
  let paddleX, targetX, x, y, vx, vy;
  let audio, volume, musicTimer;
  let muted = false;
  let beat = 0;
  const melody = [72, 76, 79, 84, 79, 76, 74, 67, 69, 72, 76, 81, 76, 72, 71, 67];

  function tone(note, start, duration, level, type = 'square') {
    const oscillator = audio.createOscillator();
    const envelope = audio.createGain();
    oscillator.type = type;
    oscillator.frequency.value = 440 * 2 ** ((note - 69) / 12);
    envelope.gain.setValueAtTime(level, start);
    envelope.gain.exponentialRampToValueAtTime(.001, start + duration);
    oscillator.connect(envelope).connect(volume);
    oscillator.start(start);
    oscillator.stop(start + duration);
  }

  function stopMusic() {
    clearInterval(musicTimer);
    musicTimer = undefined;
    if (volume) volume.gain.value = 0;
  }

  function startMusic() {
    if (muted || document.hidden || musicTimer !== undefined || !window.AudioContext) return;
    audio ??= new AudioContext();
    if (!volume) {
      volume = audio.createGain();
      volume.connect(audio.destination);
    }
    audio.resume().catch(() => {});
    volume.gain.value = .35;
    musicTimer = setInterval(() => {
      const time = audio.currentTime;
      tone(melody[beat % melody.length], time, .105, .12);
      if (beat % 4 === 0) tone(beat % 16 < 8 ? 48 : 45, time, .19, .18, 'triangle');
      beat++;
    }, 135);
  }

  function cue(note) {
    if (musicTimer !== undefined) tone(note, audio.currentTime, .14, .15);
  }

  // Mirror out-of-bounds travel to preserve distance on a wall bounce.
  function reflectedX(position) {
    const span = right - left;
    const wrapped = ((position - left) % (2 * span) + 2 * span) % (2 * span);
    return left + (wrapped <= span ? wrapped : 2 * span - wrapped);
  }

  function draw(time) {
    player.setAttribute('transform', 'translate(' + paddleX + ' 0)');
    mascot.draw(pose);
    ball.setAttribute('x', x - 6);
    ball.setAttribute('y', y - 6);

    const incoming = vy > 0 && y < paddleY && time >= serveAt;
    const landing = reflectedX(x + vx * (paddleY - y) / vy);
    aim.setAttribute('d', incoming ? `M${landing - 7} ${paddleY}h14 M${landing} ${paddleY - 4}v8` : '');
    aim.style.opacity = incoming ? clamp((y - 70) / 170, .15, .68) : 0;
    const trailLength = strongHit && vy < 0 ? .095 : .055;
    trail.setAttribute('d', reduced.matches || time < serveAt ? '' :
      `M${x} ${y}L${clamp(x - vx * trailLength, left, right)} ${Math.max(ceiling, y - vy * trailLength)}`);
    trail.style.opacity = strongHit ? .5 : .22;

    const flash = clamp((time - impactAt) / 360, 0, 1);
    impact.setAttribute('cx', impactX);
    impact.setAttribute('cy', impactY);
    impact.setAttribute('r', 8 + 25 * flash);
    impact.style.opacity = reduced.matches ? 0 : (1 - flash) * .65;
  }

  function flashAt(time, atX, atY) {
    impactX = atX;
    impactY = atY;
    impactAt = time;
  }

  function strike() {
    if (state !== 'playing') return;
    swingAt = performance.now();
  }

  function finish() {
    state = 'over';
    keys.clear();
    stopMusic();
    pose.expression = 'miss';
    pose.swing = pose.charge = pose.lean = 0;
    mascot.draw(pose);
    trail.removeAttribute('d');
    finalScore.textContent = score.parentElement.textContent;
    result.hidden = false;
    retry.focus({ preventScroll: true });
  }

  function tick(time) {
    if (state !== 'playing' || pausedAt !== null) return;
    const dt = clamp((time - lastTime) / 1000, 0, .032);
    lastTime = time;
    const direction = Number(keys.has('ArrowRight')) - Number(keys.has('ArrowLeft'));
    targetX = clamp(targetX + direction * 530 * dt, left + 31, right - 31);
    const playerMotion = targetX - paddleX;
    paddleX += playerMotion * Math.min(1, dt * 26);

    if (time >= serveAt) {
      const previousX = x;
      const previousY = y;
      const previousVx = vx;
      const nextX = x + vx * dt;
      x = reflectedX(nextX);
      y += vy * dt;
      if (nextX < left || nextX > right) vx = -vx;
      if (y <= ceiling && vy < 0) {
        flashAt(time, reflectedX(previousX + previousVx * (ceiling - previousY) / vy), ceiling);
        y = 2 * ceiling - y;
        vy = -vy;
        if (returned) {
          score.value = ++points;
          returned = false;
          if (gsap && !reduced.matches) gsap.fromTo(score, { scale: 1.3 }, { scale: 1, duration: .3, overwrite: true });
          cue(84);
        }
        strongHit = false;
      }
      if (vy > 0 && previousY <= paddleY && y > paddleY) {
        const contactX = reflectedX(previousX + previousVx * (paddleY - previousY) / vy);
        const reach = mascot.reach;
        const side = contactX >= paddleX ? 1 : -1;
        const powered = time - swingAt <= swingWindow;
        // The stroke sweeps from the body to the racket. A timed swing adds reach and pace.
        const offset = (contactX - paddleX - side * reach * .72) / (powered ? 51 : 39);
        if (Math.abs(offset) <= 1) {
          const angle = clamp(offset * .68 + playerMotion / 240, -.9, .9);
          const speed = Math.min(525, 255 + points * 18 + (powered ? 68 : 0));
          x = contactX;
          y = paddleY - (y - paddleY);
          vx = Math.sin(angle) * speed;
          vy = -Math.cos(angle) * speed;
          returned = true;
          strongHit = powered;
          contactAt = time;
          swingAt = -Infinity;
          pose.side = side;
          flashAt(time, contactX, paddleY);
          cue(powered ? 91 : 79);
        }
      }
      if (y > 392) {
        finish();
        return;
      }
    }
    const incoming = vy > 0 && y < paddleY;
    const landing = reflectedX(x + vx * (paddleY - y) / vy);
    if (incoming && Math.abs(landing - paddleX) > 10) {
      const side = landing >= paddleX ? 1 : -1;
      pose.side += (side - pose.side) * (reduced.matches ? 1 : Math.min(1, dt * 16));
    }
    const swingAge = time - Math.max(swingAt, contactAt);
    pose.swing = reduced.matches || swingAge >= 310 ? 0 :
      Math.sin(Math.PI * (Math.max(0, swingAge) / 310) ** .58);
    pose.charge = !incoming || reduced.matches ? 0 : clamp((y - 135) / 145, 0, 1);
    pose.lean = reduced.matches ? 0 : clamp(playerMotion / 90, -1, 1);
    pose.gazeX = reduced.matches ? 0 : clamp((x - paddleX) / 100, -1, 1);
    pose.gazeY = reduced.matches ? 0 : clamp((y - 300) / 180, -1, 1);
    draw(time);
    frame = requestAnimationFrame(tick);
  }

  function startRound() {
    state = 'playing';
    result.hidden = true;
    score.value = points = 0;
    returned = false;
    strongHit = false;
    swingAt = contactAt = impactAt = -Infinity;
    pausedAt = null;
    Object.assign(pose, { side: 1, charge: 0, swing: 0, lean: 0, reveal: 1, expression: undefined });
    paddleX = targetX = x = 320;
    y = 68;
    vx = (Math.random() < .5 ? -1 : 1) * 105;
    vy = 190;
    lastTime = performance.now();
    serveAt = lastTime + 850;
    draw(lastTime);
    board.focus({ preventScroll: true });
    frame = requestAnimationFrame(tick);
    syncPlayback();
  }

  function setLogoBox({ x, y, width, height }) {
    original.setAttribute('x', x);
    original.setAttribute('y', y);
    original.setAttribute('width', width);
    original.setAttribute('height', height);
  }

  function fieldPoint(clientX, clientY, element = field) {
    return new DOMPoint(clientX, clientY).matrixTransform(element.getScreenCTM().inverse());
  }

  function landingLogoBox() {
    const bounds = trigger.getBoundingClientRect();
    const top = fieldPoint(bounds.left, bounds.top, player);
    const bottom = fieldPoint(bounds.right, bounds.bottom, player);
    return { x: top.x, y: top.y, width: bottom.x - top.x, height: bottom.y - top.y };
  }

  const gameDetails = game.querySelectorAll('.game-header, .game-footer, .game-wall, .game-line, .game-wing, #game-ball');
  let transition;

  function enter() {
    state = 'opening';
    art.dispatchEvent(new Event('logo-game-start'));
    game.hidden = false;
    result.hidden = true;
    copy.inert = true;
    art.inert = true;
    hero.setAttribute('aria-labelledby', 'game-title');
    player.setAttribute('transform', 'translate(320 0)');
    player.append(original);
    startMusic();
    board.focus({ preventScroll: true });
    Object.assign(pose, { side: 1, charge: 0, swing: 0, lean: 0, reveal: 0, expression: undefined });
    if (!gsap || reduced.matches) {
      copy.style.visibility = 'hidden';
      setLogoBox(logoHome);
      startRound();
      return;
    }
    const box = landingLogoBox();
    setLogoBox(box);
    const playPose = () => mascot.draw(pose);
    transition = gsap.timeline({ onComplete: startRound })
      .to(copy, { x: -copy.getBoundingClientRect().right - 32, autoAlpha: 0, duration: .5, ease: 'power2.inOut' }, 0)
      .to(box, { ...logoHome, duration: .68, ease: 'power3.inOut', onUpdate: () => setLogoBox(box) }, .06)
      .to(pose, { reveal: 1, duration: .36, ease: 'power2.inOut', onUpdate: playPose }, .38)
      .fromTo(gameDetails, { opacity: 0 }, { opacity: 1, duration: .3 }, .4);
  }

  function leave() {
    if (state === 'closed' || state === 'closing') return;
    state = 'closing';
    transition?.kill();
    cancelAnimationFrame(frame);
    keys.clear();
    stopMusic();
    result.hidden = true;
    trail.removeAttribute('d');
    aim.removeAttribute('d');
    impact.style.opacity = 0;
    const restore = () => {
      mascot.settle();
      trigger.append(original);
      original.setAttribute('width', '290');
      original.setAttribute('height', '232');
      original.removeAttribute('x');
      original.removeAttribute('y');
      copy.style.removeProperty('visibility');
      gsap?.set(copy, { clearProps: 'transform,opacity,visibility' });
      gsap?.set(gameDetails, { clearProps: 'opacity' });
      hero.setAttribute('aria-labelledby', 'hero-title');
      copy.inert = false;
      art.inert = false;
      game.hidden = true;
      trigger.focus({ preventScroll: true });
      state = 'closed';
    };
    if (!gsap || reduced.matches) {
      restore();
      return;
    }
    const box = { x: original.x.baseVal.value, y: original.y.baseVal.value,
      width: original.width.baseVal.value, height: original.height.baseVal.value };
    transition = gsap.timeline({ onComplete: restore })
      .to(gameDetails, { opacity: 0, duration: .22 }, 0)
      .to(box, { ...landingLogoBox(), duration: .77, ease: 'sine.inOut', onUpdate: () => setLogoBox(box) }, 0)
      .to(pose, { reveal: 0, duration: .46, ease: 'power2.inOut', onUpdate: () => mascot.draw(pose) }, 0)
      .to(copy, { x: 0, autoAlpha: 1, duration: .52, ease: 'power2.out' }, .17);
  }

  trigger.addEventListener('click', event => {
    const now = performance.now();
    taps = taps.filter(time => now - time <= 3000);
    taps.push(now);
    if (taps.length < 10 || state !== 'closed') return;
    taps = [];
    event.preventDefault();
    enter();
  }, { capture: true });
  board.addEventListener('pointermove', move);
  board.addEventListener('pointerdown', event => {
    if (state !== 'playing' || event.button !== 0 || !event.isPrimary) return;
    board.focus({ preventScroll: true });
    move(event);
    board.setPointerCapture(event.pointerId);
    strike();
  });
  function move(event) {
    if (state !== 'playing' || !event.isPrimary) return;
    targetX = fieldPoint(event.clientX, event.clientY).x;
  }
  game.addEventListener('keydown', event => {
    if (event.key === 'Escape') {
      event.preventDefault();
      leave();
      return;
    }
    if (event.target === board && event.code === 'Space') {
      event.preventDefault();
      if (!event.repeat) strike();
      return;
    }
    if (state !== 'playing' || !['ArrowLeft', 'ArrowRight'].includes(event.key)) return;
    event.preventDefault();
    keys.add(event.key);
  });
  window.addEventListener('keyup', event => keys.delete(event.key));
  window.addEventListener('blur', () => keys.clear());
  function syncPlayback() {
    if (state !== 'playing') return;
    if (!courtVisible || document.hidden) {
      if (pausedAt !== null) return;
      pausedAt = performance.now();
      cancelAnimationFrame(frame);
      keys.clear();
      stopMusic();
      return;
    }
    if (pausedAt !== null) {
      lastTime = performance.now();
      serveAt += lastTime - pausedAt;
      pausedAt = null;
      frame = requestAnimationFrame(tick);
    }
    startMusic();
  }
  new IntersectionObserver(([entry]) => {
    courtVisible = entry.isIntersecting;
    syncPlayback();
  }).observe(board);
  document.addEventListener('visibilitychange', () => {
    if (state === 'opening' && document.hidden) stopMusic();
    syncPlayback();
  });
  retry.addEventListener('click', () => { if (state === 'over') startRound(); });
  game.querySelector('#game-back').addEventListener('click', leave);
  game.querySelector('#game-exit').addEventListener('click', leave);
  sound.addEventListener('click', () => {
    muted = !muted;
    sound.setAttribute('aria-pressed', String(!muted));
    if (muted) stopMusic();
    else if (state === 'opening') startMusic();
    else syncPlayback();
  });
})();
