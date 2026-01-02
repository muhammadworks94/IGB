// Layout helpers (kept small and dependency-free)
// - Sidebar toggle fallback: NobleUI's template.js usually handles this, but this ensures it always works.

(function () {
  function ready(fn) {
    if (document.readyState === "loading") {
      document.addEventListener("DOMContentLoaded", fn);
    } else {
      fn();
    }
  }

  ready(function () {
    var toggler = document.querySelector(".sidebar-toggler");
    if (!toggler) return;

    toggler.addEventListener("click", function (e) {
      e.preventDefault();

      // Prefer NobleUI default folded class if present in their CSS
      document.body.classList.toggle("sidebar-folded");

      // Also toggle an explicit class on sidebar for custom CSS hooks if needed
      var sidebar = document.querySelector(".sidebar");
      if (sidebar) sidebar.classList.toggle("is-collapsed");
    });
  });
})();


