(() => {
  'use strict';

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
  window.matchMedia('(max-width: 880px)').addEventListener('change', () => setMenu(false));

  for (const link of document.querySelectorAll('.languages a')) {
    if (window.location.protocol === 'file:') link.pathname += 'index.html';
    link.addEventListener('click', () => {
      link.hash = window.location.hash;
    });
  }

  for (const card of document.querySelectorAll('[data-choices]')) {
    const buttons = [...card.querySelectorAll('button[aria-controls]')];
    for (const button of buttons) {
      button.addEventListener('click', () => {
        if (button.getAttribute('aria-pressed') === 'true') return;
        for (const option of buttons) {
          const selected = button === option;
          option.setAttribute('aria-pressed', String(selected));
          document.getElementById(option.getAttribute('aria-controls')).hidden = !selected;
        }
        if (card.classList.contains('database')) {
          card.dataset.provider = button.getAttribute('aria-controls');
          card.dispatchEvent(new Event('providerchange'));
        }
        if (card.classList.contains('integration')) {
          const net = button.getAttribute('aria-controls') === 'net';
          card.querySelector('#net-visual').hidden = !net;
          card.querySelector('#oidc-visual').hidden = net;
          card.dispatchEvent(new Event('integrationchange'));
        }
      });
    }
  }

  const copyButton = document.querySelector('.quickstart-copy-button');
  if (navigator.clipboard?.writeText) {
    const command = document.getElementById('quickstart-code');
    const copyAction = copyButton.closest('.quickstart-copy-action');
    const feedback = copyAction.querySelector('.quickstart-feedback');
    const feedbackLabel = feedback.querySelector('span');
    const status = document.querySelector('.quickstart-status');
    const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
    let feedbackAnimation;
    let copyAttempt = 0;
    copyAction.hidden = false;

    function showCopyFeedback(copied) {
      if (feedbackAnimation) {
        feedbackAnimation.onfinish = null;
        feedbackAnimation.cancel();
      }
      const message = copied ? status.dataset.copied : status.dataset.copyError;
      status.textContent = message;
      feedbackLabel.textContent = message;
      feedback.toggleAttribute('data-error', !copied);
      feedback.hidden = false;

      const button = copyButton.getBoundingClientRect();
      const center = button.left + button.width / 2;
      const edge = feedback.offsetWidth / 2 + 12;
      const transform = (x, y, tilt = 0, scale = 1) => {
        const drift = Math.min(innerWidth - center - edge, Math.max(edge - center, x));
        return `translate(${drift}px, ${y}px) rotate(${tilt}deg) scale(${scale})`;
      };
      const direction = Math.random() < .5 ? -1 : 1;
      const offset = direction * (14 + Math.random() * 9);
      const keyframes = reducedMotion.matches ? [
        { transform: transform(0, 0) }, { transform: transform(0, 0) },
      ] : [
        { opacity: .85, transform: transform(offset * .45, 0, -direction * 7, .84), easing: 'cubic-bezier(.2, .8, .3, 1.12)' },
        { opacity: 1, transform: transform(offset, -9, direction * 4, 1.04), offset: .2 },
        { opacity: 1, transform: transform(offset, -11), offset: .76 },
        { opacity: 0, transform: transform(offset + direction * 6, -24, direction * 3, .97) },
      ];
      feedbackAnimation = feedback.animate(keyframes, { duration: copied ? 1500 : 3400, fill: 'both' });
      feedbackAnimation.onfinish = () => {
        feedback.hidden = true;
        feedbackAnimation.cancel();
        feedbackAnimation = null;
      };
    }

    copyButton.addEventListener('click', async () => {
      const attempt = ++copyAttempt;
      status.textContent = '';
      let copied = true;
      try {
        await navigator.clipboard.writeText(command.textContent);
      } catch {
        copied = false;
      }
      if (attempt === copyAttempt) showCopyFeedback(copied);
    });
  }
})();
