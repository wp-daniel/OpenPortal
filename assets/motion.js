(() => {
  'use strict';

  const lib = window.OpenPortalMotion;
  if (!lib) return;
  const { gsap, ScrollTrigger } = lib;
  gsap.registerPlugin(ScrollTrigger);

  function travelAlong(route, placePacket) {
    const head = { distance: 0 };
    return gsap.to(head, {
      distance: route.getTotalLength(), duration: 1.05, ease: 'sine.inOut',
      onUpdate: () => placePacket(route.getPointAtLength(head.distance)),
    });
  }

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
    sequence = null;
    chapter = index;
    chapters.forEach((scene, i) => { scene.hidden = i !== index; });
    buttons.forEach((button, i) => button.setAttribute('aria-pressed', String(i === index)));
    client.querySelectorAll('.typing').forEach(line => line.classList.remove('typing'));
    if (client.hidden || !motion.matches) return;
    const scene = chapters[index];
    sequence = gsap.timeline({ paused: !onScreen || document.hidden }).timeScale(1.15);
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
    const advances = automatic && index < chapters.length - 1;
    const duration = sequence.duration() + (advances ? 2.4 : 0);
    sequence.fromTo(buttons[index], { '--step-progress': 0 }, { '--step-progress': 1, duration, ease: 'none' }, 0);
    if (advances) sequence.eventCallback('onComplete', () => showChapter(index + 1));
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
        animation.call(() => { caption.textContent = route.dataset.caption; })
          .set(packet, { opacity: 1 })
          .to(route, { opacity: 1, strokeWidth: 2.4, duration: .18 })
          .add(travelAlong(route, point => {
            packet.setAttribute('cx', point.x);
            packet.setAttribute('cy', point.y);
          }), '<')
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
    const trigger = host.querySelector('.hero-logo');
    const mark = document.getElementById('hero-mark');
    const outer = mark.querySelector('g[mask="url(#sL)"]');
    const inner = outer.querySelector('path');
    const outerArch = inner.nextElementSibling;
    const innerPath = inner.getAttribute('d');
    const outerPath = outerArch.getAttribute('d');
    const bar = mark.querySelector('g[mask="url(#sW)"]');
    const outerGaps = mark.querySelector('#sL > g');
    const shift = (el, x, y) => el.setAttribute('transform', `translate(${x} ${y})`);

    const standing = { bend: 0, lag: 0, dent: 0, squat: 0, bridge: 0, x: 0, y: 0, roll: 0 };
    const body = { ...standing };
    let reaction;

    function clearPose() {
      Object.assign(body, standing);
      mark.style.removeProperty('transform');
      inner.setAttribute('d', innerPath);
      outerArch.setAttribute('d', outerPath);
      for (const part of [outer, bar, outerGaps]) part.removeAttribute('transform');
    }

    function resetLogo() {
      reaction?.kill();
      if (host.contains(mark)) clearPose();
    }
    host.addEventListener('logo-game-start', resetLogo);

    function flexArch(path, radius, offset, dent) {
      const tangent = radius * .55228475;
      const crown = dent - radius;
      path.setAttribute('d', `M${-radius} 70V0 C${-radius} ${-tangent} ${offset - tangent} ${crown} ${offset} ${crown}
        C${offset + tangent} ${crown} ${radius} ${-tangent} ${radius} 0V70`);
    }

    function drawReaction() {
      const { bend, lag, dent, squat, bridge, x, y, roll } = body;
      // Deform the crowns above the weave and keep both stems aligned at the crossings.
      flexArch(outerArch, 76, bend, dent);
      flexArch(inner, 38, bend * .48 + lag, dent * .48);
      const stance = `translate(0 70) scale(${1 + squat * .005} ${1 - squat * .009}) translate(0 -70)`;
      outer.setAttribute('transform', stance);
      bar.setAttribute('transform', `${stance} translate(0 ${bridge})`);
      shift(outerGaps, 0, bridge);
      mark.style.transform = `translate(${x / 2.9}%, ${y / 2.32}%) rotate(${roll}deg)`;
    }

    function playReaction(hit) {
      const { x, y } = hit;
      const away = -Math.sign(x);
      const lateral = gsap.utils.clamp(-1, 1, x / 80);
      const timeline = gsap.timeline({ onUpdate: drawReaction, onComplete: clearPose });
      if (Math.abs(x) > 55 && y >= 10 && y < 44) {
        // A lower push puts the weight on the far foot, followed by a quick side step.
        timeline.to(body, { ...standing, bend: 14 * lateral, squat: 8, bridge: 4, duration: .12, ease: 'power2.out' })
          .to(body, { ...standing, bend: -19 * lateral, lag: 7 * lateral, x: -22 * lateral, y: -13, roll: -6 * lateral, duration: .21, ease: 'power2.out' })
          .to(body, { ...standing, bend: 6 * lateral, x: -19 * lateral, squat: 9, bridge: 5, duration: .2, ease: 'power2.in' });
      } else if (Math.abs(x) > 55 && y < 44) {
        // One shoulder recoils first. The inner arch catches up as the weight comes back.
        const bend = away * (23 + 12 * Math.abs(lateral));
        timeline.to(body, { ...standing, bend, lag: -away * 8, dent: 5, squat: 6, bridge: -3, duration: .13, ease: 'power3.out' })
          .to(body, { bend: -bend * .34, lag: away * 7, dent: 2, squat: -3, bridge: 3, duration: .2, ease: 'sine.inOut' })
          .to(body, { bend: bend * .12, lag: -away * 3, squat: 2, bridge: -1, duration: .17, ease: 'sine.inOut' });
      } else if (y < -32) {
        // The crown ducks into the arches with the weight planted on the feet.
        timeline.to(body, { ...standing, bend: -16 * lateral, dent: 16, squat: 15, bridge: 4, duration: .14, ease: 'power3.out' })
          .to(body, { bend: 7 * lateral, dent: 2, squat: -5, bridge: -3, duration: .25, ease: 'power2.out' }, '+=.05')
          .to(body, { bend: -3 * lateral, dent: 4, squat: 3, bridge: 1, duration: .14, ease: 'sine.inOut' });
      } else if (y > 46) {
        // A tap on a foot pulls that side free, then the body absorbs the landing.
        timeline.to(body, { ...standing, bend: -14 * lateral, squat: 5, bridge: -8, roll: -5 * lateral, duration: .12, ease: 'power2.out' })
          .to(body, { ...standing, bend: 7 * lateral, x: -18 * lateral, y: -24, roll: 8 * lateral, squat: -3, duration: .18, ease: 'power3.out' })
          .to(body, { ...standing, x: -12 * lateral, bend: 4 * lateral, squat: 9, bridge: 4, duration: .2, ease: 'power2.in' })
          .to(body, { y: -7, squat: -2, bridge: -2, duration: .13, ease: 'power2.out' });
      } else if (Math.abs(y - 27.3) < 13) {
        // The traversa slides through its crossings and sends a small ripple up the arches.
        timeline.to(body, { ...standing, bridge: 10, squat: 4, dent: 6, bend: -9 * lateral, duration: .1, ease: 'power3.out' })
          .to(body, { bridge: -7, squat: -4, dent: 0, bend: 5 * lateral, lag: -6 * lateral, duration: .16, ease: 'power2.out' })
          .to(body, { bridge: 3, squat: 2, bend: 0, lag: 3 * lateral, duration: .15, ease: 'sine.inOut' });
      } else {
        // Crouch, extend on takeoff, tuck at the apex, then settle through the feet.
        timeline.to(body, { ...standing, dent: 7, bend: 10 * lateral, squat: 12, bridge: 3, duration: .13, ease: 'power2.in' })
          .to(body, { ...standing, x: -13 * lateral, y: -35, squat: -7, bend: -15 * lateral, roll: -5 * lateral, bridge: -3, duration: .24, ease: 'power2.out' })
          .to(body, { ...standing, x: -15 * lateral, y: -23, dent: 4, bridge: 4, squat: 2, duration: .13, ease: 'sine.in' })
          .to(body, { ...standing, x: -15 * lateral, dent: 6, squat: 10, duration: .18, ease: 'power2.in' });
      }
      return timeline.to(body, { ...standing, duration: .25, ease: 'sine.out' });
    }

    const react = event => {
      if (event.defaultPrevented || !trigger.contains(event.target)) return;
      reaction?.kill();
      const bounds = trigger.getBoundingClientRect();
      const box = mark.viewBox.baseVal;
      const ratio = gsap.utils.clamp(0, 1);
      const hit = event.detail === 0 ? { x: 0, y: 0 } : {
        x: box.x + box.width * ratio((event.clientX - bounds.left) / bounds.width),
        y: box.y + box.height * ratio((event.clientY - bounds.top) / bounds.height),
      };
      reaction = playReaction(hit);
      if (event.pointerType === 'mouse') document.dispatchEvent(new Event('logo-replay'));
    };
    host.addEventListener('click', react);

    gsap.to('.reading-progress', { scaleX: 1, ease: 'none', scrollTrigger: { start: 0, end: 'max', scrub: true } });
    for (const title of document.querySelectorAll('[data-rise-title]')) {
      gsap.from(title.firstElementChild, {
        y: () => Math.min(88, title.offsetHeight * .9), opacity: .25, ease: 'power2.out',
        scrollTrigger: { trigger: title, start: 'top 98%', end: 'top 68%', scrub: true, invalidateOnRefresh: true },
      });
    }
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
      host.removeEventListener('click', react);
      host.removeEventListener('logo-game-start', resetLogo);
      resetLogo();
      series.forEach(line => line.restore());
      area.setAttribute('d', originalArea);
      preview.remove();
      guide.remove();
      days.forEach(label => label.classList.remove('current-day'));
    };
  });
})();
