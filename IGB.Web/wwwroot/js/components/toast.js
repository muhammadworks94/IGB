// Minimal toast helper using Bootstrap toasts.
// Layout already contains #toastContainer.
(function () {
  function ensureContainer() {
    return document.getElementById("toastContainer");
  }

  function escapeHtml(s) {
    return String(s ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll("\"", "&quot;")
      .replaceAll("'", "&#039;");
  }

  window.igbToast = function (opts) {
    var o = opts || {};
    var title = escapeHtml(o.title || "Notification");
    var message = escapeHtml(o.message || "");
    var variant = o.variant || "primary"; // primary|success|danger|warning|info
    var delay = typeof o.delay === "number" ? o.delay : 5000;

    var container = ensureContainer();
    if (!container || typeof bootstrap === "undefined") return;

    var toastId = "toast_" + Math.random().toString(36).substring(2);
    var html = `
      <div id="${toastId}" class="toast align-items-center text-bg-${variant} border-0 mb-2" role="alert" aria-live="assertive" aria-atomic="true">
        <div class="d-flex">
          <div class="toast-body">
            <strong>${title}:</strong> ${message}
          </div>
          <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
        </div>
      </div>`;

    container.insertAdjacentHTML("beforeend", html);
    var el = document.getElementById(toastId);
    var toast = new bootstrap.Toast(el, { delay: delay });
    toast.show();
    el.addEventListener("hidden.bs.toast", function () { try { el.remove(); } catch { } });
  };
})();


