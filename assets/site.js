(() => {
  'use strict';

  function setupBrandMotion() {
    const mark = document.querySelector('.hero-art > svg');
    if (typeof mark.animate !== 'function') return;

    // The official SVG shares a mask between its concentric arches.
    // Only the outer arch touches its cutouts, so the inner arch can travel independently.
    const outer = mark.querySelector('g[mask="url(#sL)"]');
    const inner = outer.querySelector('path');
    const bar = mark.querySelector('g[mask="url(#sW)"]');
    const outerGaps = mark.querySelector('#sL > g');
    const barGaps = mark.querySelector('#sW > g');
    const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)');
    const poses = [
      { at: 0,    outer: [0, 0],     inner: [0, 0],   bar: [0, 0] },
      { at: 0.12, outer: [0, 0],     inner: [0, 0],   bar: [0, 0] },
      { at: 0.43, outer: [-19, -12], inner: [49, 9],  bar: [-7, 9] },
      { at: 0.73, outer: [14, 3],    inner: [-40, -9], bar: [12, -13] },
      { at: 1,    outer: [0, 0],     inner: [0, 0],   bar: [0, 0] },
    ];

    // Keep the alternating over/under gaps at the moving intersections.
    const tracks = [
      [outer, pose => pose.outer],
      [inner, pose => pose.inner],
      [bar, pose => pose.bar],
      [outerGaps, pose => [0, pose.bar[1] - pose.outer[1]]],
      [barGaps, pose => [pose.inner[0] - pose.bar[0], 0]],
    ];
    let animations = [];
    function stop() {
      for (const animation of animations) {
        animation.onfinish = null;
        animation.cancel();
      }
      outer.prepend(inner);
      animations = [];
    }
    function play() {
      stop();
      if (motionPreference.matches) return;
      outer.before(inner);
      animations = tracks.map(([piece, position]) => {
        const frames = poses.map(pose => {
          const [x, y] = position(pose);
          return {
            offset: pose.at,
            easing: 'cubic-bezier(.45, 0, .2, 1)',
            transform: `translate(${x}px, ${y}px)`,
          };
        });
        return piece.animate(frames, { duration: 3200 });
      });
      animations[0].onfinish = () => outer.prepend(inner);
    }

    function applyPreference() {
      if (motionPreference.matches) stop();
      else play();
    }
    motionPreference.addEventListener('change', applyPreference);
    applyPreference();
  }
  setupBrandMotion();

  const menu = document.querySelector('.menu-button');
  const navigation = document.querySelector('#navigation');
  function setMenu(open) {
    navigation.classList.toggle('open', open);
    menu.setAttribute('aria-expanded', String(open));
  }
  menu.addEventListener('click', () => setMenu(!navigation.classList.contains('open')));
  navigation.addEventListener('click', () => setMenu(false));
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && navigation.classList.contains('open')) {
      setMenu(false);
      menu.focus();
    }
  });
  document.addEventListener('click', event => {
    if (!event.target.closest('.header')) setMenu(false);
  });
  window.matchMedia('(max-width: 740px)').addEventListener('change', () => setMenu(false));

  for (const link of document.querySelectorAll('.languages a')) {
    link.addEventListener('click', () => {
      link.hash = window.location.hash;
    });
  }

  for (const card of document.querySelectorAll('[data-choices]')) {
    const buttons = [...card.querySelectorAll('button[aria-controls]')];
    for (const button of buttons) {
      button.addEventListener('click', () => {
        for (const option of buttons) {
          const selected = button === option;
          option.setAttribute('aria-pressed', String(selected));
          document.getElementById(option.getAttribute('aria-controls')).hidden = !selected;
        }
      });
    }
  }

  const demoApps = [...document.querySelectorAll('[data-demo-app]')];
  const detail = document.querySelector('.portal-detail');
  for (const app of demoApps) {
    app.addEventListener('click', () => {
      const open = app.getAttribute('aria-expanded') !== 'true';
      for (const item of demoApps) item.setAttribute('aria-expanded', 'false');
      app.setAttribute('aria-expanded', String(open));
      detail.hidden = !open;
      detail.textContent = app.querySelector('b').textContent + ' — ' + document.querySelector('.portal-demo').dataset.appNote;
    });
  }
})();
