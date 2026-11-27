(() => {
  'use strict';
  const story = document.querySelector('#access');
  const lib = window.OpenPortalMotion;
  if (!story || !lib) return;

  const { gsap } = lib;
  const header = document.querySelector('.header');
  const sticky = story.querySelector('.access-sticky');
  const heading = story.querySelector('.access-heading');
  const shots = [...story.querySelectorAll('.access-shot')];
  const cameras = [...story.querySelectorAll('.access-camera')];
  const outlines = [...story.querySelectorAll('.access-focus')];
  const copies = [...story.querySelectorAll('.access-copy')];

  gsap.matchMedia().add({
    motion: '(min-height: 600px) and (prefers-reduced-motion: no-preference)',
    compact: '(max-width: 740px)',
  }, ({ conditions }) => {
    if (!conditions.motion) return;
    const { compact } = conditions;
    const aspect = compact ? 544 / 440 : 1240 / 690;
    // Account status, the Support row and the permission tree, in screenshot coordinates.
    const regions = compact ? [
      { left: .061, top: .714, width: .69, height: .121, zoom: 1.29 },
      { left: .061, top: .79, width: .48, height: .109, zoom: 1.45 },
      { left: .071, top: .614, width: .64, height: .344, zoom: 1.37 },
    ] : [
      { left: .114, top: .546, width: .493, height: .081, zoom: 1.57 },
      { left: .092, top: .612, width: .382, height: .074, zoom: 1.85 },
      { left: .344, top: .51, width: .51, height: .32, zoom: 1.65 },
    ];
    let headerHeight;
    let distance;
    let entryY;
    let width;
    let height;
    let imageWidth;
    let imageHeight;
    let details;

    function positionCamera({ left, top, width: regionWidth, height: regionHeight, zoom }) {
      const scale = Math.min(zoom, width * .93 / (imageWidth * regionWidth), height * .87 / (imageHeight * regionHeight));
      const x = width / 2 - imageWidth * scale * (left + regionWidth / 2);
      const y = height / 2 - imageHeight * scale * (top + regionHeight / 2);
      return {
        scale,
        x: gsap.utils.clamp(width - imageWidth * scale, 0, x),
        y: gsap.utils.clamp(height - imageHeight * scale, 0, y),
      };
    }

    function measure() {
      headerHeight = header.offsetHeight;
      story.style.setProperty('--access-header', headerHeight + 'px');
      const viewportHeight = sticky.offsetHeight + headerHeight;
      distance = compact
        ? gsap.utils.clamp(1150, 1700, viewportHeight * 2.15)
        : gsap.utils.clamp(1400, 2420, viewportHeight * 2.55);
      story.style.setProperty('--access-distance', distance + 'px');

      const shot = shots[0];
      width = shot.clientWidth;
      height = shot.clientHeight;
      imageWidth = Math.max(width, height * aspect);
      imageHeight = imageWidth / aspect;
      story.style.setProperty('--access-image-width', imageWidth + 'px');
      story.style.setProperty('--access-image-height', imageHeight + 'px');
      details = regions.map(positionCamera);
      entryY = Math.max(0, heading.offsetTop + heading.offsetHeight + (compact ? 24 : 38) - shot.offsetTop);
    }

    story.classList.add('is-immersive');
    measure();
    const wide = { x: () => (width - imageWidth) / 2, y: 0, scale: 1 };
    gsap.set(shots.slice(1), { autoAlpha: 0 });
    gsap.set(copies, { opacity: 0, y: 16 });
    outlines.forEach((outline, i) => {
      const { left, top, width, height } = regions[i];
      gsap.set(outline, { left: `${left * 100}%`, top: `${top * 100}%`, width: `${width * 100}%`, height: `${height * 100}%` });
    });

    const timeline = gsap.timeline({
      defaults: { ease: 'sine.inOut' },
      scrollTrigger: {
        id: 'access-passage',
        trigger: story,
        start: () => 'top ' + headerHeight + 'px',
        end: () => '+=' + distance,
        scrub: .28,
        invalidateOnRefresh: true,
        onRefreshInit: measure,
      },
    });

    function focusOn(index, start, duration) {
      timeline.to(cameras[index], {
        scale: () => details[index].scale,
        x: () => details[index].x,
        y: () => details[index].y,
        duration,
        ease: 'power2.inOut',
      }, start)
        .to(outlines[index], { opacity: 1, duration: duration * .31 }, start + duration * .58);
    }

    function changeView(from, to, start) {
      const close = start + .022;
      const cut = close + .049;
      const full = 'inset(0% 0% 0% 0% round 14px)';
      const narrow = 'inset(42% 14% 42% 14% round 9px)';
      timeline.to(copies[from], { opacity: 0, y: -10, duration: .044 }, start)
        .to(outlines[from], { opacity: 0, duration: .038 }, start)
        .to(cameras[from], { ...wide, duration: .105, ease: 'power2.inOut' }, start)
        .fromTo(shots[from], { clipPath: full }, {
          clipPath: narrow, duration: .049, ease: 'power2.in', immediateRender: false,
        }, close)
        .set(shots[from], { autoAlpha: 0 }, cut)
        .set(shots[to], { autoAlpha: 1, clipPath: narrow }, cut)
        .to(shots[to], { clipPath: full, duration: .054, ease: 'power2.out' }, cut)
        .set(shots[to], { clipPath: 'none' }, cut + .054)
        .to(copies[to], { opacity: 1, y: 0, duration: .052 }, cut + .004);
    }

    timeline.set(cameras, wide, 0)
      .fromTo(shots[0], {
        y: () => entryY, scale: compact ? .91 : .79,
        transformPerspective: 1500, rotationX: compact ? 4 : 9, rotationY: compact ? 0 : -5,
      }, { y: 0, scale: 1, rotationX: 0, rotationY: 0, duration: .194, ease: 'power2.inOut' }, 0)
      .to(heading, { opacity: 0, y: -52, scale: .94, duration: .091, ease: 'sine.inOut' }, .006)
      .to(copies[0], { opacity: 1, y: 0, duration: .064 }, .136);

    focusOn(0, .174, .134);
    changeView(0, 1, .354);
    focusOn(1, .423, .141);
    changeView(1, 2, .61);
    focusOn(2, .69, .148);

    timeline.to(outlines[2], { opacity: 0, duration: .043 }, .889)
      .to(cameras[2], { ...wide, duration: .111, ease: 'power2.inOut' }, .889)
      .to(shots[2], { y: 8, scale: .97, duration: .111 }, .889);

    return () => {
      story.classList.remove('is-immersive');
      for (const name of ['header', 'distance', 'image-width', 'image-height']) {
        story.style.removeProperty('--access-' + name);
      }
    };
  });
})();
