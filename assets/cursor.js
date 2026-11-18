(() => {
  'use strict';
  const root = document.documentElement;
  const enabled = matchMedia('(hover: hover) and (pointer: fine) and (prefers-reduced-motion: no-preference)');
  const cursor = document.createElement('div');
  cursor.className = 'magnetic-cursor';
  cursor.setAttribute('aria-hidden', 'true');
  cursor.innerHTML = '<i class="cursor-ring"></i><i class="cursor-dot"></i>';
  document.body.append(cursor);
  const [ring, dot] = cursor.children;
  const point = { x: 0, y: 0 };
  const shape = { x: 0, y: 0, w: 36, h: 36, radius: 18 };
  let frame = 0;
  let active = false;
  let pressed = false;
  let hovered = null;

  function paint() {
    frame = 0;
    const hit = document.elementFromPoint(point.x, point.y);
    if (!hit || hit.closest('textarea, input:not([type=radio]), [contenteditable=true]')) {
      hide();
      return;
    }
    const control = hit.closest('a[href], button:enabled, .appearance label');
    if (hovered !== control) hovered?.removeAttribute('data-hover-light');
    const diameter = pressed ? 29 : 36;
    let goal = { x: point.x, y: point.y, w: diameter, h: diameter, radius: diameter / 2 };
    if (control) {
      const box = control.getBoundingClientRect();
      if (control.matches('.footer-cta a')) {
        control.setAttribute('data-hover-light', '');
        control.style.setProperty('--pointer-x', (point.x - box.left) + 'px');
        control.style.setProperty('--pointer-y', (point.y - box.top) + 'px');
      }
      // A wide grid tab or full-width link is a hit area, not a cursor shape.
      if (box.width <= 112 && box.height <= 48) {
        const gap = 2;
        goal = { x: box.left + box.width / 2, y: box.top + box.height / 2,
          w: box.width + gap * 2, h: box.height + gap * 2,
          radius: Math.min((parseFloat(getComputedStyle(control).borderTopLeftRadius) || 3) + gap, box.height / 2 + gap) };
      }
    } else if (hovered) {
      // Release at the pointer instead of dragging a control-sized outline across the page.
      Object.assign(shape, goal);
    }
    hovered = control;
    let moving = false;
    for (const key of Object.keys(shape)) {
      const delta = goal[key] - shape[key];
      if (Math.abs(delta) < .15) shape[key] = goal[key];
      else { shape[key] += delta * .2; moving = true; }
    }
    ring.style.transform = `translate3d(${shape.x - shape.w / 2}px, ${shape.y - shape.h / 2}px, 0)`;
    ring.style.width = shape.w + 'px';
    ring.style.height = shape.h + 'px';
    ring.style.borderRadius = shape.radius + 'px';
    ring.classList.toggle('is-hovered', Boolean(control));
    if (control) {
      ring.style.setProperty('--light-x', (point.x - shape.x + shape.w / 2) + 'px');
      ring.style.setProperty('--light-y', (point.y - shape.y + shape.h / 2) + 'px');
    }
    if (moving) frame = requestAnimationFrame(paint);
  }
  function schedule() { if (!frame) frame = requestAnimationFrame(paint); }

  function hide() {
    active = false;
    pressed = false;
    hovered?.removeAttribute('data-hover-light');
    hovered = null;
    ring.classList.remove('is-hovered');
    root.classList.remove('custom-cursor');
    cancelAnimationFrame(frame);
    frame = 0;
  }

  function track() {
    if (!active) return;
    schedule();
  }

  function move(event) {
    if (!enabled.matches || event.pointerType !== 'mouse') { hide(); return; }
    point.x = event.clientX;
    point.y = event.clientY;
    if (!active) {
      Object.assign(shape, { x: point.x, y: point.y, w: 36, h: 36, radius: 18 });
      active = true;
      root.classList.add('custom-cursor');
    }
    dot.style.transform = `translate3d(${point.x - 2.5}px, ${point.y - 2.5}px, 0)`;
    track();
  }
  window.addEventListener('pointermove', move, { passive: true });
  document.addEventListener('pointerover', track, { passive: true });
  document.addEventListener('pointerout', track, { passive: true });
  window.addEventListener('pointerdown', event => { pressed = true; move(event); }, { passive: true });
  window.addEventListener('pointerup', () => { pressed = false; if (active) schedule(); });
  document.addEventListener('scroll', track, { capture: true, passive: true });
  window.addEventListener('resize', track);
  window.addEventListener('keydown', hide, true);
  window.addEventListener('blur', hide);
  window.addEventListener('pointercancel', hide);
  root.addEventListener('pointerleave', hide);
  document.addEventListener('visibilitychange', hide);
  enabled.addEventListener('change', hide);
})();
