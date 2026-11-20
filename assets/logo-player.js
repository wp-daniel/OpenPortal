(() => {
  'use strict';

  const neutralBarY = 27.3;
  const gripX = 104;
  const gripY = 39;
  const reach = 45;
  const ink = 'var(--brand-ink)';

  function createPlayer(mark) {
    const body = mark.querySelector('g[mask="url(#sL)"]');
    const bar = mark.querySelector('g[mask="url(#sW)"]');
    const racket = bar.querySelector('path');
    const weaveGaps = mark.querySelectorAll('#sL > g, #sW > g');
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const element = (name, attributes) => {
      const node = document.createElementNS(mark.namespaceURI, name);
      for (const [attribute, value] of Object.entries(attributes)) node.setAttribute(attribute, value);
      return node;
    };

    const arms = element('g', { 'aria-hidden': 'true', fill: 'none', stroke: ink, 'stroke-linecap': 'round' });
    const left = element('path', { 'stroke-width': 12 });
    const right = element('path', { 'stroke-width': 12 });
    arms.append(left, right);

    const face = element('g', { 'aria-hidden': 'true', fill: ink });
    const eyes = [-1, 1].map(() => element('ellipse', { rx: 4.2, ry: 5.7 }));
    const mouth = element('path', { fill: 'none', stroke: ink, 'stroke-width': 3.5, 'stroke-linecap': 'round' });
    face.append(...eyes, mouth);
    const grip = element('circle', {
      'aria-hidden': 'true', r: 8.5, fill: 'var(--page)', stroke: ink, 'stroke-width': 4,
    });

    function draw({ side = 1, charge = 0, swing = 0, lean = 0, reveal = 1,
      gazeX = side * .55, gazeY = -1, blink = 0, expression } = {}) {
      if (!arms.parentNode) {
        bar.before(arms);
        body.after(face);
        bar.after(grip);
      }
      if (reduced.matches) charge = swing = lean = 0;

      const crouch = reveal * (charge * .1 - swing * .035);
      const leanAngle = reveal * (4 * lean - 3 * side * charge + 5 * side * swing);
      const bodyTransform = `translate(0 70) skewX(${leanAngle}) scale(${1 + crouch * .28} ${1 - crouch}) translate(0 -70)`;
      body.setAttribute('transform', bodyTransform);
      face.setAttribute('transform', bodyTransform);

      // The original line travels out of the weave and rotates around its new grip.
      const handX = reveal * side * (gripX + 7 * charge - 7 * swing);
      const handY = neutralBarY + reveal * (gripY - neutralBarY + 11 * (1 - Math.abs(side)) + 9 * charge - 9 * swing);
      const angle = reveal * (90 + side * (12 + 25 * charge - 74 * swing));
      racket.setAttribute('transform',
        `translate(${handX} ${handY}) rotate(${angle}) scale(${1 - .26 * reveal}) translate(${-43 * reveal} ${-neutralBarY})`);
      // Fade the cuts back in once the bar is within the original mask bounds.
      const weave = Math.max(0, 1 - reveal / .1);
      mark.toggleAttribute('data-racket-free', weave === 0);
      if (weave > 0) {
        for (const gaps of weaveGaps) gaps.setAttribute('opacity', weave);
      }
      grip.setAttribute('cx', handX);
      grip.setAttribute('cy', handY);
      grip.setAttribute('opacity', reveal);

      const shoulderY = 10 + 60 * crouch;
      const shoulderShift = (shoulderY - 70) * Math.tan(leanAngle * Math.PI / 180);
      for (const [direction, arm] of [[-1, left], [1, right]]) {
        const shoulderX = direction * 75 * (1 + crouch * .28) + shoulderShift;
        const elbowX = direction * (94 + 10 * charge + 8 * swing);
        // Both hands touch the same pivot briefly when the racket crosses the body.
        arm.setAttribute('opacity', reveal * Math.min(1, Math.max(0, .5 + direction * side * 1.4)));
        arm.setAttribute('d', `M${shoulderX} ${shoulderY} Q${elbowX} ${shoulderY + 10} ${handX} ${handY}`);
      }

      face.setAttribute('opacity', reveal);
      eyes.forEach((eye, index) => {
        eye.setAttribute('cx', (index ? 13 : -13) + gazeX * 4);
        eye.setAttribute('cy', 1 + gazeY * 3);
        eye.setAttribute('ry', Math.max(1.1, 5.7 - blink * 4.6 - charge * 1.8));
      });
      mouth.setAttribute('d', expression === 'miss'
        ? 'M-4 19a4 4 0 1 0 8 0a4 4 0 1 0-8 0'
        : `M-7 17q7 ${4 + swing * 9 - charge * 3} 14 0`);
    }

    function settle() {
      arms.remove();
      face.remove();
      grip.remove();
      body.removeAttribute('transform');
      racket.removeAttribute('transform');
      mark.removeAttribute('data-racket-free');
      for (const gaps of weaveGaps) gaps.removeAttribute('opacity');
    }

    return { draw, settle, reach };
  }

  window.OpenPortalLogoPlayer = createPlayer;
})();
