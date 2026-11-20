(() => {
  'use strict';

  const loupe = document.querySelector('.loupe');
  const layer = loupe.parentElement;
  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  const openTitle = loupe.title;
  const isDocked = () => loupe.hasAttribute('data-docked');
  const margin = 12;
  const peek = 44;
  const reveal = 20;
  const axes = ['x', 'y', 'size'];
  const position = { x: 0, y: 0, size: 0 };
  const rest = { ...position };
  const velocity = { ...position };
  const clamp = (value, min, max) => Math.max(min, Math.min(max, value));
  let viewWidth, viewHeight, cornerX, cornerY, fullSize, dockSize;
  let drag = null;
  let suppressClick = false;
  let placing = false;
  let hovering = false;
  let frame = 0;
  let lastFrame = 0;

  function dockPoint(revealed = hovering || loupe.matches(':focus-visible')) {
    return { x: cornerX + dockSize / 2 - peek - (revealed ? reveal : 0),
      y: cornerY - dockSize / 2, size: dockSize };
  }

  function activePoint(x, y) {
    const insetX = Math.min(fullSize / 2 + margin, viewWidth / 2);
    const insetY = Math.min(fullSize / 2 + margin, viewHeight / 2);
    return { x: clamp(x, insetX, viewWidth - insetX), y: clamp(y, insetY, viewHeight - insetY),
      size: fullSize };
  }

  function render() {
    loupe.style.translate = `${position.x - fullSize / 2}px ${position.y - fullSize / 2}px`;
    loupe.style.scale = position.size / fullSize;
    loupe.style.setProperty('--loupe-lean', `${clamp(velocity.y / 240, -2.5, 2.5)}px`);
  }

  function stop() {
    cancelAnimationFrame(frame);
    frame = lastFrame = 0;
    for (const axis of axes) velocity[axis] = 0;
  }

  function settle() {
    stop();
    Object.assign(position, rest);
    if (isDocked()) loupe.setAttribute('data-idle', '');
    render();
  }

  function animate(now) {
    const dt = lastFrame ? Math.min((now - lastFrame) / 1000, .05) : 1 / 60;
    lastFrame = now;
    const tracking = Boolean(drag?.moved);
    const rate = tracking ? 25 : 20;
    const damping = Math.exp(-rate * dt);

    for (const axis of axes) {
      if (tracking && axis !== 'size') {
        position[axis] = rest[axis];
        continue;
      }
      // Critically damped spring solved over the elapsed time, even if a frame is late.
      const distance = position[axis] - rest[axis];
      const slope = velocity[axis] + rate * distance;
      position[axis] = rest[axis] + (distance + slope * dt) * damping;
      velocity[axis] = (velocity[axis] - rate * slope * dt) * damping;
    }

    if (axes.every(axis => Math.abs(position[axis] - rest[axis]) < .2 && Math.abs(velocity[axis]) < 3)) {
      settle();
    } else {
      render();
      frame = requestAnimationFrame(animate);
    }
  }

  function moveTo(point) {
    Object.assign(rest, point);
    if (reduced.matches) settle();
    else if (!frame) frame = requestAnimationFrame(animate);
  }

  function measure() {
    const style = getComputedStyle(layer);
    viewWidth = layer.clientWidth;
    viewHeight = layer.clientHeight;
    cornerX = viewWidth - parseFloat(style.paddingRight);
    cornerY = viewHeight - parseFloat(style.paddingBottom);
    dockSize = parseFloat(style.getPropertyValue('--loupe-dock-size'));
    fullSize = loupe.offsetWidth;
  }

  function setPlacing(value) {
    placing = value;
    loupe.toggleAttribute('data-placing', value);
  }

  function setOpen() {
    hovering = false;
    loupe.removeAttribute('data-docked');
    loupe.removeAttribute('data-idle');
    loupe.setAttribute('aria-pressed', 'true');
    loupe.title = loupe.dataset.dockTitle;
  }

  function dock() {
    release(null, false);
    hovering = false;
    setPlacing(false);
    loupe.setAttribute('data-docked', '');
    loupe.setAttribute('aria-pressed', 'false');
    loupe.title = openTitle;
    moveTo(dockPoint());
  }

  function setHover(value) {
    if (hovering === value) return;
    hovering = value;
    if (isDocked() && !drag) moveTo(dockPoint());
  }

  function inDockArea(event) {
    return event.clientX >= cornerX - peek - reveal && event.clientX <= cornerX
      && Math.abs(event.clientY - (cornerY - dockSize / 2)) <= dockSize / 2;
  }

  loupe.addEventListener('pointerenter', event => {
    if (event.pointerType === 'mouse' && isDocked() && inDockArea(event)) setHover(true);
  });
  window.addEventListener('pointermove', event => {
    if (hovering && !inDockArea(event)) setHover(false);
  }, { passive: true });
  document.documentElement.addEventListener('pointerleave', () => setHover(false));
  loupe.addEventListener('focus', () => {
    if (isDocked() && !drag && loupe.matches(':focus-visible')) moveTo(dockPoint(true));
  });
  loupe.addEventListener('blur', () => {
    if (isDocked() && !drag) moveTo(dockPoint(hovering));
  });

  loupe.addEventListener('pointerdown', event => {
    if (event.button !== 0 || !event.isPrimary || drag) return;
    stop();
    const { clientX, clientY, pointerId, timeStamp } = event;
    drag = { pointerId, offsetX: clientX - position.x, offsetY: clientY - position.y,
      startX: clientX, startY: clientY, time: timeStamp, x: position.x, y: position.y,
      velocityX: 0, velocityY: 0, moved: false };
    suppressClick = false;
    loupe.setPointerCapture(pointerId);
    loupe.dataset.dragging = '';
  });

  loupe.addEventListener('pointermove', event => {
    if (event.pointerId !== drag?.pointerId) return;
    const { clientX, clientY, timeStamp } = event;
    if (!drag.moved) {
      if (Math.hypot(clientX - drag.startX, clientY - drag.startY) < 4) return;
      drag.moved = true;
      setPlacing(false);
      if (isDocked()) setOpen();
    }
    const x = clamp(clientX - drag.offsetX, 0, viewWidth);
    const y = clamp(clientY - drag.offsetY, 0, viewHeight);
    const elapsed = Math.max((timeStamp - drag.time) / 1000, .008);
    drag.velocityX = clamp(drag.velocityX * .35 + (x - drag.x) * .65 / elapsed, -950, 950);
    drag.velocityY = clamp(drag.velocityY * .35 + (y - drag.y) * .65 / elapsed, -950, 950);
    Object.assign(drag, { x, y, time: timeStamp });
    moveTo({ x, y, size: fullSize });
  });

  function release(event, fling) {
    if (!drag || (event && event.pointerId !== drag.pointerId)) return;
    const { pointerId, moved, time, x, y, velocityX, velocityY } = drag;
    drag = null;
    delete loupe.dataset.dragging;
    suppressClick = moved;
    if (loupe.hasPointerCapture(pointerId)) loupe.releasePointerCapture(pointerId);
    if (!moved) {
      moveTo(isDocked() ? dockPoint() : rest);
      return;
    }
    if (fling && event.timeStamp - time < 90 && !reduced.matches) {
      velocity.x = velocityX;
      velocity.y = velocityY;
    }
    moveTo(activePoint(x + velocity.x * .11, y + velocity.y * .11));
  }

  loupe.addEventListener('pointerup', event => release(event, true));
  loupe.addEventListener('pointercancel', event => release(event, false));
  loupe.addEventListener('lostpointercapture', event => release(event, false));
  window.addEventListener('blur', () => {
    setHover(false);
    release(null, false);
  });

  loupe.addEventListener('click', event => {
    if (event.detail && suppressClick) return;
    if (isDocked()) {
      setOpen();
      setPlacing(true);
      moveTo(activePoint(cornerX - fullSize / 2 - margin, cornerY - fullSize / 2));
    } else dock();
  });

  document.addEventListener('click', event => {
    if (isDocked() || loupe.contains(event.target)) return;
    if (placing && event.detail && !event.target.closest('a, button, input, label, select, textarea, [contenteditable]')) {
      setPlacing(false);
      moveTo(activePoint(event.clientX, event.clientY));
    } else dock();
  });

  const directions = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] };
  document.addEventListener('keydown', event => {
    if (isDocked()) return;
    if (event.key === 'Escape') {
      dock();
    } else if (event.target === loupe && directions[event.key]) {
      event.preventDefault();
      setPlacing(false);
      const [x, y] = directions[event.key];
      const step = event.shiftKey ? 12 : 36;
      moveTo(activePoint(rest.x + x * step, rest.y + y * step));
    }
  });

  document.addEventListener('focusin', event => {
    const target = event.target;
    if (!isDocked() && target !== loupe && !drag && target.matches(':focus-visible')) dock();
  });

  window.addEventListener('resize', () => {
    measure();
    moveTo(isDocked() ? dockPoint() : activePoint(rest.x, rest.y));
  });
  reduced.addEventListener('change', () => {
    if (reduced.matches) settle();
  });

  // Some engines accept the syntax but cannot render SVG displacement on a backdrop.
  const chromium = navigator.userAgentData?.brands.some(({ brand }) => brand === 'Chromium')
    || /\b(?:Chrome|HeadlessChrome)\/\d+/.test(navigator.userAgent);
  if (chromium && CSS.supports('backdrop-filter', 'url(#loupe-refraction)')) {
    loupe.dataset.refraction = '';
  }

  loupe.hidden = false;
  measure();
  Object.assign(rest, dockPoint());
  settle();
})();
