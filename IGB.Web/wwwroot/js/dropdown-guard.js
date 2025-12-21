(() => {
  const hasBootstrap = () => typeof window.bootstrap !== "undefined" && !!window.bootstrap.Dropdown;

  const getToggles = () =>
    Array.from(document.querySelectorAll('[data-bs-toggle="dropdown"]'));

  const isShown = (toggle) => toggle?.getAttribute("aria-expanded") === "true";

  const getInstance = (toggle) => {
    if (!hasBootstrap()) return null;
    return window.bootstrap.Dropdown.getOrCreateInstance(toggle, { autoClose: true });
  };

  const hideAll = (exceptToggle) => {
    for (const t of getToggles()) {
      if (exceptToggle && t === exceptToggle) continue;
      const inst = getInstance(t);
      if (inst && isShown(t)) {
        try {
          inst.hide();
        } catch {
          // ignore
        }
      }
    }
  };

  document.addEventListener(
    "click",
    (e) => {
      const toggle = e.target?.closest?.('[data-bs-toggle="dropdown"]');
      if (!toggle) {
        // click outside: close open dropdowns
        hideAll();
        return;
      }

      if (!hasBootstrap()) return;

      // Prevent other document-level handlers (template.js) from interfering.
      e.preventDefault();
      e.stopPropagation();
      if (typeof e.stopImmediatePropagation === "function") e.stopImmediatePropagation();

      // Keep only one dropdown open at a time
      hideAll(toggle);

      const inst = getInstance(toggle);
      if (!inst) return;
      try {
        inst.toggle();
      } catch {
        // ignore
      }
    },
    true // capture phase: runs before bubble handlers
  );

  document.addEventListener(
    "keydown",
    (e) => {
      if (e.key === "Escape") hideAll();
    },
    true
  );
})();


