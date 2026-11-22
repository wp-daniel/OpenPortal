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
  window.matchMedia('(max-width: 740px)').addEventListener('change', () => setMenu(false));

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

})();
