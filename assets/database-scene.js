// The same cross sections form both the bound pages and the three storage platters.
// Project into a 2D canvas so the illustration also works without a WebGL context.
(() => {
  'use strict';
  const lib = window.OpenPortalMotion;
  const card = document.querySelector('.database');
  if (!lib || !card) return;
  const stage = card.querySelector('.db-model');
  const marks = [...stage.querySelectorAll('img')];
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d');
  if (!ctx) return;

  const camera = new lib.OrthographicCamera(-1, 1, 1, -1, .1, 80);
  camera.position.set(0, 8, 10);
  camera.lookAt(0, .1, 0);
  camera.updateMatrixWorld();
  const plate = new lib.Group();
  const point = new lib.Vector3();
  const corners = 80;
  const layers = 12;
  // Four sheets become the recessed collar, thick body, and two rim edges of each drum.
  const drumSections = [
    { top: 0, depth: .18, radius: 1.55 },
    { top: .36, depth: .36, radius: 1.80 },
    { top: .41, depth: .05, radius: 1.84 },
    { top: .46, depth: .05, radius: 1.82 },
  ];
  const motion = matchMedia('(prefers-reduced-motion: no-preference)');
  const state = { mix: 0, opening: 0 };
  let width, height, dark, material;

  function smooth(a, b, value) {
    const t = Math.min(1, Math.max(0, (value - a) / (b - a)));
    return t * t * (3 - 2 * t);
  }

  // Match each ray on the clipped paper outline to the same ray on the platter.
  const rim = Array.from({ length: corners }, (_, i) => {
    const angle = i * Math.PI * 2 / corners;
    const x = Math.cos(angle), z = Math.sin(angle);
    const radius = Math.min(
      ((Math.abs(x) / 2.12) ** 12 + (Math.abs(z) / 1.57) ** 12) ** (-1 / 12),
      x > z ? 3.09 / (x - z) : Infinity,
    );
    return { x, z, paper: radius };
  });

  function project(x, y, z, onPlate = true) {
    point.set(x, y, z);
    if (onPlate) point.applyMatrix4(plate.matrixWorld);
    point.project(camera);
    return [(1 + point.x) * width / 2, (1 - point.y) * height / 2];
  }

  function path(points, close = false) {
    ctx.beginPath();
    ctx.moveTo(...points[0]);
    for (let i = 1; i < points.length; i++) ctx.lineTo(...points[i]);
    if (close) ctx.closePath();
  }

  function gradient(x1, y1, x2, y2, stops) {
    const fill = ctx.createLinearGradient(x1, y1, x2, y2);
    for (const [offset, color] of stops) fill.addColorStop(offset, color);
    return fill;
  }

  function engraving(image, opacity) {
    if (opacity <= 0) return;
    const origin = project(0, .005, 0);
    const right = project(1, .005, 0);
    const lower = project(0, .005, 1);
    // Compensate for the camera's viewing angle to keep the print legible.
    const h = 2.36, w = h * .74 * image.naturalWidth / image.naturalHeight;
    ctx.save();
    ctx.globalAlpha = opacity;
    ctx.transform(right[0] - origin[0], right[1] - origin[1],
      lower[0] - origin[0], lower[1] - origin[1], ...origin);
    ctx.drawImage(image, -w / 2, -h / 2, w, h);
    ctx.restore();
  }

  function drawLayer(i) {
    const level = i / (layers - 1);
    const t = smooth(.18 * level, .80 + .20 * level, state.mix);
    const twist = Math.sin(t * Math.PI);
    const lift = Math.sin(Math.PI * state.mix);
    const paperY = -.44 + .052 * i;
    const section = drumSections[i % drumSections.length];
    const drum = Math.floor(i / drumSections.length);
    const platterY = -.92 + drum * .66 + section.top;
    const y = paperY * (1 - t) + platterY * t;
    plate.position.set(twist * (level - .5) * 1.82,
      y + lift * (level - .4) * 1.35 + state.opening * level * .8, 0);
    plate.rotation.set(0, -.34 * (1 - t) + twist * (.28 + level * .98) + state.opening * level * .48, 0);
    plate.updateMatrixWorld();

    const depth = .048 + (section.depth - .048) * t;
    const outline = [], bottom = [];
    for (const { x, z, paper } of rim) {
      const r = paper + (section.radius - paper) * t;
      outline.push(project(r * x, 0, r * z));
      bottom.push(project(r * x, -depth, r * z));
    }

    // A soft falloff around the rim gives the matte surface depth.
    const top = i === layers - 1;
    const left = Math.min(...outline.map(p => p[0]));
    const right = Math.max(...outline.map(p => p[0]));
    const midY = outline[0][1];
    const side = gradient(left, midY, right, midY, [[0, material.wall], [1, material.shade]]);
    path(bottom, true);
    ctx.fillStyle = side;
    ctx.fill();
    // Fill the visible rim as a single strip to avoid hairline seams.
    const front = [], back = [];
    const first = outline.findIndex(p => p[0] === right);
    for (let j = 0; j < corners; j++) {
      const n = (first + j) % corners;
      front.push(outline[n]);
      back.push(bottom[n]);
      if (outline[n][0] === left) break;
    }
    path(front.concat(back.reverse()), true);
    ctx.fill();
    path(outline, true);
    ctx.fillStyle = top ? material.face : side;
    ctx.fill();
    ctx.strokeStyle = material.edge;
    ctx.lineWidth = top ? 1.1 : .75;
    ctx.stroke();

    if (top) {
      const inset = rim.map(({ x, z, paper }) => {
        const r = (paper + (1.82 - paper) * t) * .91;
        return project(r * x, .003, r * z);
      });
      path(inset, true);
      ctx.lineWidth = .8;
      ctx.strokeStyle = material.edge;
      ctx.stroke();
      engraving(marks[0], 1 - smooth(.22, .50, t));
      engraving(marks[1], smooth(.48, .82, t));
    }
  }

  function drawRoutes() {
    // One local connection fans into three incoming routes to the same store.
    for (const side of [-1, 1]) {
      for (let i = 0; i < 3; i++) {
        const amount = i === 0 ? 1 : smooth(.22 + i * .07, .9, state.mix);
        if (amount === 0) continue;
        const z = .46 + (i - 1) * .92;
        const points = [[5.7, z + .8], [3.8 + i * .15, z + .8], [2.88 + i * .15, z], [.8, z]];
        ctx.globalAlpha = amount;
        path(points.map(([x, depth]) => project(x * side, -.78 - .32 * state.mix, depth, false)));
        ctx.lineWidth = 1;
        ctx.strokeStyle = gradient(side < 0 ? 0 : width, 0, width / 2, 0, dark
          ? [[0,'#77777700'],[.42,'#515155'],[1,'#8d8d91']]
          : [[0,'#90909000'],[.42,'#b4b4b3'],[1,'#777778']]);
        ctx.stroke();
        const travel = Math.max(state.opening, Math.sin(Math.PI * state.mix));
        if (travel > .01) {
          ctx.save();
          ctx.globalAlpha *= travel;
          ctx.strokeStyle = dark ? '#eeeeef' : '#404044';
          ctx.setLineDash([width * .055, width]);
          ctx.lineDashOffset = -width * (.46 * state.mix + .33 * (1 - state.opening) + .03 * i);
          ctx.stroke();
          ctx.restore();
        }
      }
    }
    ctx.globalAlpha = 1;
  }

  function render() {
    if (!width || !height) return;
    // Leave headroom while the taller stack fans open between the two resting poses.
    camera.zoom = 1 - .12 * Math.sin(Math.PI * state.mix);
    camera.updateProjectionMatrix();
    const provider = state.mix >= .54 ? 'postgres' : 'sqlite';
    if (stage.parentElement.dataset.displayed !== provider) stage.parentElement.dataset.displayed = provider;
    ctx.clearRect(0, 0, width, height);
    drawRoutes();
    const center = project(0, -.80 - .32 * state.mix, 0, false);
    ctx.save();
    ctx.translate(...center);
    ctx.scale(height * .56, height * .16);
    const shadow = ctx.createRadialGradient(0, 0, 0, 0, 0, 1);
    shadow.addColorStop(0, dark ? '#000b' : '#0004');
    shadow.addColorStop(1, '#0000');
    ctx.fillStyle = shadow;
    ctx.fillRect(-1, -1, 2, 2);
    ctx.restore();
    for (let i = 0; i < layers; i++) drawLayer(i);
  }

  function resize() {
    width = stage.clientWidth;
    height = stage.clientHeight;
    if (!width || !height) return;
    const dpr = Math.min(devicePixelRatio || 1, 3);
    canvas.width = Math.round(width * dpr);
    canvas.height = Math.round(height * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    const half = Math.max(2.17, 2.95 * height / width);
    camera.top = half;
    camera.bottom = -half;
    camera.right = half * width / height;
    camera.left = -camera.right;
    render();
  }

  function select() {
    const target = card.dataset.provider === 'postgres' ? 1 : 0;
    lib.gsap.killTweensOf(state);
    if (!motion.matches) {
      state.mix = target;
      state.opening = 0;
      render();
      return;
    }
    lib.gsap.to(state, {
      mix: target, opening: 0, duration: Math.max(.35, 1.4 * Math.abs(target - state.mix)),
      ease: 'sine.inOut', onUpdate: render,
    });
  }

  function setTheme() {
    dark = document.documentElement.dataset.theme === 'dark';
    const styles = getComputedStyle(stage);
    material = Object.fromEntries(['face', 'wall', 'shade', 'edge'].map(name =>
      [name, styles.getPropertyValue('--db-' + name).trim()]));
    render();
  }

  Promise.all(marks.map(img => img.decode())).then(() => {
    state.mix = card.dataset.provider === 'postgres' ? 1 : 0;
    stage.append(canvas);
    setTheme();
    resize();
    stage.classList.add('rendered');
    new ResizeObserver(resize).observe(stage);
    new MutationObserver(setTheme).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
    card.addEventListener('providerchange', select);
    motion.addEventListener('change', select);
    lib.ScrollTrigger.create({
      trigger: stage, start: 'top 90%', once: true,
      onEnter() {
        if (motion.matches) lib.gsap.fromTo(state, { opening: 1 },
          { opening: 0, duration: 1.15, ease: 'power3.out', onUpdate: render });
      },
    });
  }).catch(() => { /* The selected original mark remains visible if canvas artwork is unavailable. */ });
})();
