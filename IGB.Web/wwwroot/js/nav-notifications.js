(() => {
  if (!document.getElementById("navNotificationsList")) return; // TopNav not present

  const tokenMeta = document.querySelector('meta[name="request-verification-token"]');
  const antiforgeryToken = tokenMeta?.getAttribute("content") ?? "";

  const indicator = document.getElementById("navNotificationIndicator");
  const badge = indicator?.querySelector(".badge");
  const list = document.getElementById("navNotificationsList");

  const setIndicator = (unread) => {
    const n = Number(unread ?? 0);
    if (!indicator) return;
    if (n > 0) {
      if (badge) badge.textContent = String(Math.min(n, 99));
      indicator.classList.remove("d-none");
    } else {
      if (badge) badge.textContent = "0";
      indicator.classList.add("d-none");
    }
  };

  const esc = (s) =>
    String(s ?? "").replace(/[&<>"'`=\/]/g, (c) => ({
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      '"': "&quot;",
      "'": "&#39;",
      "`": "&#x60;",
      "=": "&#x3D;",
      "/": "&#x2F;",
    })[c]);

  const renderList = (items) => {
    if (!list) return;
    const arr = Array.isArray(items) ? items : [];
    if (!arr.length) {
      list.innerHTML = '<div class="text-muted small px-2 py-2">No notifications yet.</div>';
      return;
    }
    list.innerHTML = arr
      .map(
        (n) => `
      <div class="px-2 py-2 border-bottom">
        <div class="d-flex justify-content-between align-items-start">
          <div class="fw-semibold small">${esc(n.title || "Notification")}</div>
          <div class="text-muted small ms-2">${esc(formatTime(n.createdAtUtc))}</div>
        </div>
        <div class="text-muted small">${esc(n.message || "")}</div>
      </div>`
      )
      .join("");
  };

  const formatTime = (createdAtUtc) => {
    if (!createdAtUtc) return "";
    const d = new Date(createdAtUtc);
    if (Number.isNaN(d.getTime())) return "";
    return d.toLocaleString();
  };

  const fetchSnapshot = async () => {
    try {
      const res = await fetch("/Notifications/My", { credentials: "same-origin" });
      if (!res.ok) return;
      const data = await res.json();
      setIndicator(data.unreadCount);
      renderList(data.items);
    } catch {
      // ignore
    }
  };

  const post = async (url) => {
    try {
      const res = await fetch(url, {
        method: "POST",
        credentials: "same-origin",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: antiforgeryToken,
        },
        body: "{}",
      });
      return res.ok;
    } catch {
      return false;
    }
  };

  document.addEventListener("DOMContentLoaded", () => {
    fetchSnapshot();

    const markRead = document.getElementById("navNotificationsMarkReadBtn");
    markRead?.addEventListener("click", async (e) => {
      e.preventDefault();
      if (await post("/Notifications/MarkAllRead")) await fetchSnapshot();
    });

    const clearBtn = document.getElementById("navNotificationsClearBtn");
    clearBtn?.addEventListener("click", async (e) => {
      e.preventDefault();
      if (await post("/Notifications/Clear")) await fetchSnapshot();
    });
  });

  // SignalR integration: if the page has already created a global connection, hook into it.
  // Otherwise, we rely on the existing layout SignalR script to still display toast notifications.
  window.igbNavNotifications = {
    onNotify: () => fetchSnapshot(),
  };
})();


