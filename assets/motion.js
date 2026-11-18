(() => {
  'use strict';

  const lib = window.OpenPortalMotion;
  if (!lib) return;
  const { gsap, ScrollTrigger } = lib;
  gsap.registerPlugin(ScrollTrigger);

  const integration = document.querySelector('.integration');
  const client = integration.querySelector('#net-visual');
  const chapters = [...client.querySelectorAll('.client-scene')];
  const buttons = [...client.querySelectorAll('.client-nav button')];
  const motion = matchMedia('(prefers-reduced-motion: no-preference)');
  let sequence;
  let chapter = 0;
  let onScreen = false;
  let automatic = true;

  function showChapter(index) {
    sequence?.revert();
    chapter = index;
    chapters.forEach((scene, i) => { scene.hidden = i !== index; });
    buttons.forEach((button, i) => button.setAttribute('aria-pressed', String(i === index)));
    client.querySelectorAll('.typing').forEach(line => line.classList.remove('typing'));
    if (client.hidden || !motion.matches) return;
    const scene = chapters[index];
    sequence = gsap.timeline({ paused: !onScreen || document.hidden });
    sequence.from(scene.querySelector('.client-art'), { opacity: 0, y: 8, duration: .34 })
      .from(scene.querySelectorAll('h4,p'), { y: 6, opacity: 0, stagger: .06, duration: .32 }, 0);
    if (index === 0) {
      for (const line of scene.querySelectorAll('.source-line')) {
        const content = line.firstElementChild;
        const count = content.textContent.length;
        sequence.fromTo(content, { width: '0ch' }, {
          width: count + 'ch', duration: count * .031, ease: 'steps(' + count + ')',
          onStart: () => line.classList.add('typing'),
          onComplete: () => line.classList.remove('typing'),
        }, '+=.10');
      }
      sequence.fromTo(scene.querySelector('.manifest-route em'), { left: '0%', opacity: 0 },
        { opacity: 1, duration: .14 }).to(scene.querySelector('.manifest-route em'),
        { left: '100%', duration: .85, ease: 'power2.inOut' });
    } else if (index === 1) {
      const [waiting, approved] = scene.querySelectorAll('.review-status span');
      sequence.from(scene.querySelector('.review-sheet'), { rotation: 3, y: 12, duration: .55 }, 0)
        .set(waiting, { autoAlpha: 1 }, 0)
        .fromTo(approved, { autoAlpha: 0, scale: 1.13 }, { autoAlpha: 1, scale: 1, duration: .35 }, 1.8)
        .to(waiting, { autoAlpha: 0, duration: .16 }, '<');
    } else if (index === 2) {
      const source = scene.querySelector('.secret-source b');
      const target = scene.querySelector('.secret-target b');
      // The administrator copies the one-time secret into application configuration.
      sequence.from(source, { clipPath: 'inset(0 100% 0 0)', duration: .55 }, .3)
        .fromTo(target, { autoAlpha: 0, y: -12 }, { autoAlpha: 1, y: 0, duration: .48 }, 1.75);
    } else {
      for (const path of scene.querySelectorAll('.grant-tree path')) {
        const length = path.getTotalLength();
        sequence.fromTo(path, { strokeDasharray: length, strokeDashoffset: length },
          { strokeDashoffset: 0, duration: .55, ease: 'power2.out' }, '<.2');
      }
      sequence.from(scene.querySelectorAll('.grant-tree g:not(:first-of-type)'),
        { opacity: 0, stagger: .2, duration: .35 }, .6);
    }
    if (automatic && index < chapters.length - 1) {
      sequence.call(() => showChapter(index + 1), [], '+=2.4');
    }
  }

  buttons.forEach((button, index) => button.addEventListener('click', () => {
    automatic = false;
    showChapter(index);
  }));
  function syncPlayback() { sequence?.paused(!onScreen || document.hidden || client.hidden); }
  new IntersectionObserver(([entry]) => {
    onScreen = entry.isIntersecting;
    syncPlayback();
  }, { threshold: .25 }).observe(integration.querySelector('.integration-stage'));
  document.addEventListener('visibilitychange', syncPlayback);
  motion.addEventListener('change', () => showChapter(chapter));
  integration.addEventListener('integrationchange', () => {
    automatic = true;
    showChapter(0);
  });
  showChapter(0);

  gsap.matchMedia().add('(prefers-reduced-motion: no-preference)', () => {
    let animation;
    const map = integration.querySelector('.oidc-map');
    const caption = map.querySelector('.oidc-caption');
    const complete = caption.textContent;
    function playFlow() {
      animation?.revert();
      caption.textContent = complete;
      if (!client.hidden) return;
      const packet = map.querySelector('.oidc-packet');
      const endpoints = integration.querySelectorAll('.oidc-endpoints p');
      animation = gsap.timeline();
      animation.from(map.querySelectorAll('.map-node'), { opacity: 0, y: 5, duration: .35, stagger: .09 });
      for (const [i, route] of [...map.querySelectorAll('.oidc-routes path')].entries()) {
        const distance = route.getTotalLength();
        const head = { progress: 0 };
        animation.call(() => { caption.textContent = route.dataset.caption; })
          .set(packet, { opacity: 1 })
          .to(route, { opacity: 1, strokeWidth: 2.4, duration: .18 })
          .to(head, {
            progress: 1, duration: 1.05, ease: 'sine.inOut', onUpdate() {
              const point = route.getPointAtLength(head.progress * distance);
              packet.setAttribute('cx', point.x);
              packet.setAttribute('cy', point.y);
            },
          }, '<')
          .to(endpoints[i < 3 ? 0 : 1], { borderTopColor: 'currentColor', duration: .25 }, '<')
          .to(packet, { opacity: 0, duration: .2 }, '+=.26')
          .to(route, { opacity: .42, strokeWidth: 1.4, duration: .2 }, '<');
      }
      animation.call(() => { caption.textContent = complete; });
    }
    integration.addEventListener('integrationchange', playFlow);
    playFlow();
    return () => {
      integration.removeEventListener('integrationchange', playFlow);
      caption.textContent = complete;
      animation?.revert();
    };
  });

  // Reveal each measured cubic exactly, including its partially drawn final segment.
  function growingSeries(path) {
    const original = path.getAttribute('d');
    const coords = original.match(/-?\d+(?:\.\d+)?/g).map(Number);
    const segments = (coords.length - 2) / 6;
    const baseline = coords[1];
    function split(a, b, t) { return a + (b - a) * t; }
    return {
      draw(progress, area) {
        const position = progress * segments;
        const full = Math.floor(position);
        let d = 'M' + coords.slice(0, 2);
        let endX = coords[0];
        for (let i = 0; i < full; i++) {
          const start = 2 + i * 6;
          d += 'C' + coords.slice(start, start + 6);
          endX = coords[start + 4];
        }
        if (full < segments) {
          const start = full * 6;
          const t = position - full;
          const partial = Array(6);
          for (let axis = 0; axis < 2; axis++) {
            const k = start + axis;
            const a = split(coords[k], coords[k + 2], t);
            const b = split(coords[k + 2], coords[k + 4], t);
            const c = split(coords[k + 4], coords[k + 6], t);
            partial[axis] = a;
            partial[axis + 2] = split(a, b, t);
            partial[axis + 4] = split(partial[axis + 2], split(b, c, t), t);
          }
          d += 'C' + partial;
          endX = partial[4];
        }
        path.setAttribute('d', d);
        if (area) area.setAttribute('d', d + 'V' + baseline + 'H' + coords[0] + 'Z');
      },
      restore() { path.setAttribute('d', original); },
    };
  }

  gsap.matchMedia().add('(prefers-reduced-motion: no-preference)', () => {
    const host = document.querySelector('.hero-art');
    const mark = host.querySelector('svg');
    const outer = mark.querySelector('g[mask="url(#sL)"]');
    const inner = outer.querySelector('path');
    const bar = mark.querySelector('g[mask="url(#sW)"]');
    const outerGaps = mark.querySelector('#sL > g');
    const barGaps = mark.querySelector('#sW > g');
    const pose = { ox: 0, oy: 0, ix: 0, iy: 0, bx: 0, by: 0 };
    const shift = (el, x, y) => el.setAttribute('transform', `translate(${x} ${y})`);
    const intro = gsap.timeline({
      onStart: () => outer.before(inner),
      onUpdate() {
        shift(outer, pose.ox, pose.oy);
        shift(inner, pose.ix, pose.iy);
        shift(bar, pose.bx, pose.by);
        shift(outerGaps, 0, pose.by - pose.oy);
        shift(barGaps, pose.ix - pose.bx, 0);
      },
      onComplete: () => outer.prepend(inner),
      defaults: { ease: 'power2.inOut' },
    });
    intro.to(pose, { ox: -19, oy: -12, ix: 49, iy: 9, bx: -7, by: 9, duration: .99 }, .38)
      .to(pose, { ox: 14, oy: 3, ix: -40, iy: -9, bx: 12, by: -13, duration: .96 })
      .to(pose, { ox: 0, oy: 0, ix: 0, iy: 0, bx: 0, by: 0, duration: .87 });
    const restart = event => {
      if (!mark.contains(event.target)) return;
      outer.before(inner);
      intro.restart();
    };
    host.addEventListener('click', restart);

    const titleLines = document.querySelectorAll('.hero-line');
    gsap.set(titleLines, { overflow: 'clip' });
    gsap.from('.hero-line > span', { yPercent: 135, stagger: .13, duration: 1, ease: 'power3.out',
      onComplete: () => gsap.set(titleLines, { clearProps: 'overflow' }) });
    gsap.from('.hero-actions, .lead', { opacity: 0, duration: .8, delay: .15 });
    gsap.to('.reading-progress', { scaleX: 1, ease: 'none', scrollTrigger: { start: 0, end: 'max', scrub: true } });
    for (const section of document.querySelectorAll('[data-reveal]:not(.chart-motion)')) {
      gsap.from(section, { y: 22, opacity: .45, ease: 'none',
        scrollTrigger: { trigger: section, start: 'top 96%', end: 'top 71%', scrub: true } });
    }
    const paths = [...document.querySelectorAll('.chart-traces path[class]')];
    const series = paths.map(growingSeries);
    const area = document.querySelector('.chart-traces path[fill]');
    const originalArea = area.getAttribute('d');
    const traces = area.parentNode;
    const preview = traces.cloneNode(true);
    preview.querySelector('path[fill]').remove();
    preview.setAttribute('opacity', '.18');
    traces.before(preview);
    const guide = document.createElementNS(traces.namespaceURI, 'line');
    guide.setAttribute('class', 'chart-guide');
    guide.setAttribute('x1', '0');
    guide.setAttribute('x2', '0');
    guide.setAttribute('y1', '8');
    guide.setAttribute('y2', '230');
    traces.before(guide);
    const days = [...document.querySelectorAll('.chart-x span')];
    const state = { progress: 0 };
    const draw = () => {
      series.forEach((line, i) => line.draw(state.progress, i === 0 ? area : null));
      guide.setAttribute('transform', 'translate(' + (32 + 641 * state.progress) + ' 0)');
      const reading = state.progress > 0 && state.progress < 1;
      guide.style.visibility = reading ? 'visible' : 'hidden';
      const day = Math.round(state.progress * (days.length - 1));
      days.forEach((label, i) => label.classList.toggle('current-day', reading && i === day));
    };
    draw();
    // Briefly settle on the activity peak on the 13th and the failed logins on the 15th.
    gsap.timeline({ onUpdate: draw, defaults: { ease: 'sine.inOut' },
      scrollTrigger: { trigger: '.chart-motion', start: 'top 82%', end: 'bottom 47%', scrub: .5 } })
      .to(state, { progress: 2 / 6, duration: 1 })
      .to(state, { progress: 4 / 6, duration: 1.1 }, '+=.16')
      .to(state, { progress: 1, duration: .85 }, '+=.12');

    return () => {
      host.removeEventListener('click', restart);
      outer.prepend(inner);
      for (const part of [outer, inner, bar, outerGaps, barGaps]) part.removeAttribute('transform');
      series.forEach(line => line.restore());
      area.setAttribute('d', originalArea);
      preview.remove();
      guide.remove();
      days.forEach(label => label.classList.remove('current-day'));
    };
  });
})();
