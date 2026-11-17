(() => {
  'use strict';

  function setupBrandMotion() {
    const mark = document.querySelector('.hero-art > svg');
    const label = document.querySelector('.hero-art').dataset.replayLabel;
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
      mark.setAttribute('role', motionPreference.matches ? 'img' : 'button');
      mark.setAttribute('aria-label', motionPreference.matches ? 'OpenPortal' : label);
      if (motionPreference.matches) mark.removeAttribute('tabindex');
      else mark.setAttribute('tabindex', '0');
      if (motionPreference.matches) stop();
      else play();
    }
    mark.addEventListener('click', play);
    mark.addEventListener('keydown', event => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        play();
      }
    });
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

  const scenes = [...document.querySelectorAll('[data-scene]')];
  const steps = [...document.querySelectorAll('.steps li')];
  const apps = [...document.querySelectorAll('[data-app]')];
  const tour = document.querySelector('#tour');
  const address = document.querySelector('#address');
  let current = 0;
  let selectedApp = apps[0];
  let timer = null;
  let paused = false;

  function showScene(index, focus = false) {
    current = index;
    const hosts = ['portal.example.test/sign-in', 'portal.example.test/home', selectedApp.dataset.host + '.example.test'];
    address.textContent = hosts[index];
    document.querySelector('#session-title').textContent = selectedApp.querySelector('b').textContent;
    scenes.forEach((scene, i) => {
      scene.hidden = i !== index;
      if (i === index) steps[i].setAttribute('aria-current', 'step');
      else steps[i].removeAttribute('aria-current');
    });
    if (focus) scenes[index].querySelector('h3').focus({ preventScroll: true });
  }

  function updateTour() {
    let label = tour.dataset.play;
    if (timer !== null) label = tour.dataset.pause;
    else if (current === scenes.length - 1) label = tour.dataset.replay;
    else if (paused || current > 0) label = tour.dataset.resume;
    document.querySelector('#tour-label').textContent = label;
    document.querySelector('#tour-icon use').setAttribute('href', timer === null ? '#i-play' : '#i-pause');
  }

  function stopTour() {
    window.clearTimeout(timer);
    timer = null;
    updateTour();
  }

  function manualScene(index) {
    paused = false;
    stopTour();
    showScene(index, true);
    updateTour();
  }
  document.querySelector('#sign-in').addEventListener('click', () => manualScene(1));
  for (const app of apps) {
    app.addEventListener('click', () => {
      selectedApp = app;
      manualScene(2);
    });
  }
  document.querySelector('#switch-app').addEventListener('click', () => manualScene(1));

  function scheduleAdvance() {
    timer = window.setTimeout(() => {
      showScene(current + 1);
      if (current === scenes.length - 1) stopTour();
      else scheduleAdvance();
    }, 3200);
    updateTour();
  }

  tour.addEventListener('click', () => {
    if (timer !== null) {
      paused = true;
      stopTour();
      return;
    }
    if (current === scenes.length - 1) {
      selectedApp = apps[0];
      showScene(0);
    }
    paused = false;
    scheduleAdvance();
  });

  function pauseIfPlaying() {
    if (timer !== null) {
      paused = true;
      stopTour();
    }
  }
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) pauseIfPlaying();
  });
  if ('IntersectionObserver' in window) {
    const observer = new IntersectionObserver(entries => {
      if (!entries[0].isIntersecting) pauseIfPlaying();
    });
    observer.observe(document.querySelector('#demo'));
  }
})();
