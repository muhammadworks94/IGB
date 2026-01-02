// Frontend Auth Service (for calling the JWT API)
// Storage model:
// - Access token kept in-memory (safer than localStorage)
// - Refresh token stored in HttpOnly cookie by the backend (/api/auth/login + /api/auth/refresh)
// - This file provides: login/register/refresh/logout + authFetch + session expiry warning

(function () {
  var accessToken = null;
  var expMs = 0;
  var warnTimer = null;
  var logoutTimer = null;

  function parseJwt(token) {
    try {
      var parts = token.split(".");
      if (parts.length !== 3) return null;
      var payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
      var json = decodeURIComponent(atob(payload).split("").map(function (c) {
        return "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2);
      }).join(""));
      return JSON.parse(json);
    } catch {
      return null;
    }
  }

  function setAccessToken(token) {
    accessToken = token || null;
    var payload = token ? parseJwt(token) : null;
    expMs = payload && payload.exp ? payload.exp * 1000 : 0;
    startSessionTimers();
  }

  function startSessionTimers() {
    if (warnTimer) clearTimeout(warnTimer);
    if (logoutTimer) clearTimeout(logoutTimer);
    if (!expMs) return;

    var now = Date.now();
    var warnAt = expMs - 60 * 1000; // 60s warning
    if (warnAt > now) {
      warnTimer = setTimeout(function () {
        try { window.igbToast?.({ title: "Session", message: "Your session will expire soon.", variant: "warning", delay: 5000 }); } catch { }
      }, warnAt - now);
    }

    if (expMs > now) {
      logoutTimer = setTimeout(function () {
        // token expired: clear in-memory token; caller can redirect to login
        setAccessToken(null);
        try { window.igbToast?.({ title: "Session", message: "Session expired. Please log in again.", variant: "danger", delay: 6000 }); } catch { }
      }, expMs - now);
    }
  }

  async function api(path, options) {
    var opts = options || {};
    opts.headers = opts.headers || {};
    opts.credentials = "include"; // send refresh cookie

    if (accessToken) {
      opts.headers["Authorization"] = "Bearer " + accessToken;
    }

    var res = await fetch(path, opts);
    return res;
  }

  async function authFetch(path, options) {
    var res = await api(path, options);
    if (res.status !== 401) return res;

    // Attempt one refresh then retry once
    var refreshed = await refresh();
    if (!refreshed) return res;
    return await api(path, options);
  }

  async function login(email, password, rememberMe) {
    var res = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify({ email: email, password: password, rememberMe: !!rememberMe })
    });

    if (!res.ok) {
      var err = await safeJson(res);
      return { ok: false, error: err?.error || "Login failed." };
    }

    var data = await res.json();
    setAccessToken(data.accessToken);
    return { ok: true, user: data };
  }

  async function register(payload) {
    var res = await fetch("/api/auth/register", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify(payload)
    });
    var data = await safeJson(res);
    if (!res.ok) return { ok: false, error: data?.error || "Registration failed.", data: data };
    return { ok: true, data: data };
  }

  async function refresh() {
    var res = await fetch("/api/auth/refresh", {
      method: "POST",
      credentials: "include"
    });
    if (!res.ok) {
      setAccessToken(null);
      return false;
    }
    var data = await res.json();
    setAccessToken(data.accessToken);
    return true;
  }

  async function logout() {
    // Best-effort: if token exists, call API logout. Always clear local token.
    try {
      await api("/api/auth/logout", { method: "POST" });
    } catch { /* ignore */ }
    setAccessToken(null);
    return true;
  }

  function hasRole(role) {
    if (!accessToken) return false;
    var payload = parseJwt(accessToken);
    var r = payload && (payload["role"] || payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]);
    return String(r || "").toLowerCase() === String(role || "").toLowerCase();
  }

  async function safeJson(res) {
    try { return await res.json(); } catch { return null; }
  }

  // Expose
  window.igbAuth = {
    login: login,
    register: register,
    refresh: refresh,
    logout: logout,
    authFetch: authFetch,
    setAccessToken: setAccessToken, // for testing
    hasRole: hasRole
  };
})();


