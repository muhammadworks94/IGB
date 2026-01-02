(function () {
  const nav = document.querySelector('.igb-public-nav');
  if (!nav) return;

  let ticking = false;
  function onScroll() {
    if (ticking) return;
    ticking = true;
    window.requestAnimationFrame(() => {
      const y = window.scrollY || document.documentElement.scrollTop || 0;
      nav.classList.toggle('igb-public-nav--shadow', y > 4);
      nav.classList.toggle('igb-public-nav--shrink', y > 20);
      ticking = false;
    });
  }

  onScroll();
  window.addEventListener('scroll', onScroll, { passive: true });

  // Desktop hover open for mega dropdown (keeps mobile click behavior)
  const prefersHover = window.matchMedia && window.matchMedia('(hover: hover)').matches;
  if (prefersHover) {
    const dd = nav.querySelector('.igb-mega.dropdown');
    if (dd) {
      dd.addEventListener('mouseenter', () => {
        const toggle = dd.querySelector('[data-bs-toggle="dropdown"]');
        if (!toggle) return;
        // If Bootstrap dropdown API exists, use it. Otherwise, fallback to adding show.
        try {
          if (window.bootstrap && bootstrap.Dropdown) {
            bootstrap.Dropdown.getOrCreateInstance(toggle).show();
          } else {
            dd.classList.add('show');
            dd.querySelector('.dropdown-menu')?.classList.add('show');
          }
        } catch { /* ignore */ }
      });
      dd.addEventListener('mouseleave', () => {
        const toggle = dd.querySelector('[data-bs-toggle="dropdown"]');
        if (!toggle) return;
        try {
          if (window.bootstrap && bootstrap.Dropdown) {
            bootstrap.Dropdown.getOrCreateInstance(toggle).hide();
          } else {
            dd.classList.remove('show');
            dd.querySelector('.dropdown-menu')?.classList.remove('show');
          }
        } catch { /* ignore */ }
      });
    }
  }
})();


