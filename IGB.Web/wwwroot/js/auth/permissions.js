// Frontend permissions helper
// - Loads permissions from /api/rbac/me (cookie-auth)
// - Hides elements with data-permission="perm.key" unless allowed

(function () {
  var perms = new Set();
  var loaded = false;

  async function load() {
    try {
      var res = await fetch("/api/rbac/me", { credentials: "include" });
      if (!res.ok) throw new Error("not ok");
      var data = await res.json();
      perms = new Set((data.permissions || []).map(function (p) { return String(p).toLowerCase(); }));
      if (data.isAdmin) perms.add("*");
      loaded = true;
      applyDom();
      return data;
    } catch {
      loaded = true;
      applyDom();
      return null;
    }
  }

  function has(permissionKey) {
    if (perms.has("*")) return true;
    return perms.has(String(permissionKey || "").toLowerCase());
  }

  function applyDom(root) {
    var r = root || document;
    r.querySelectorAll("[data-permission]").forEach(function (el) {
      var key = el.getAttribute("data-permission");
      var show = has(key);
      el.classList.toggle("d-none", !show);
      el.setAttribute("aria-hidden", show ? "false" : "true");
    });
  }

  // Auto-load on DOM ready
  document.addEventListener("DOMContentLoaded", function () {
    load();
  });

  window.igbPerm = { load: load, has: has, applyDom: applyDom, isLoaded: function () { return loaded; } };
})();


