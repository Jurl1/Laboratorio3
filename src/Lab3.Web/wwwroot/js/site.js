// Lab3 — JS mínimo con fetch (PLAN.md §7). Sin dependencias además de Bootstrap.
(() => {
  "use strict";

  const token = document.querySelector('meta[name="request-verification-token"]')?.content ?? "";

  /** POST con el anti-forgery token en el header. */
  async function post(url, body) {
    const res = await fetch(url, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        "RequestVerificationToken": token,
        ...(body ? { "Content-Type": "application/json" } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
    });
    // Sesión vencida: el servidor redirige al login.
    if (res.redirected && new URL(res.url).pathname.startsWith("/cuenta/login")) {
      window.location.href = res.url;
      throw new Error("login");
    }
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return res.json();
  }

  // ---------- Likes ----------
  document.addEventListener("click", async (e) => {
    const btn = e.target.closest("[data-like-review]");
    if (!btn || btn.disabled) return;
    btn.disabled = true;
    try {
      const data = await post(`/api/reviews/${btn.dataset.likeReview}/like`);
      btn.querySelector("[data-like-count]").textContent = data.count;
      btn.classList.toggle("btn-danger", data.liked);
      btn.classList.toggle("btn-outline-danger", !data.liked);
      btn.setAttribute("aria-pressed", String(data.liked));
    } catch (err) {
      if (err.message !== "login") alert("No se pudo registrar el like. Probá de nuevo.");
    } finally {
      btn.disabled = false;
    }
  });

  // ---------- Bookmarks ----------
  document.querySelectorAll("select[data-bookmark]").forEach((select) => {
    let previous = select.value;
    const feedback = select.parentElement.querySelector("[data-bookmark-feedback]");
    select.addEventListener("change", async () => {
      select.disabled = true;
      try {
        await post("/api/bookmarks", {
          tipo: select.dataset.tipo,
          tmdbId: Number(select.dataset.tmdbId),
          status: select.value || null,
        });
        previous = select.value;
        if (feedback) feedback.textContent = select.value ? "Guardado ✓" : "Quitado de tus listas";
      } catch (err) {
        select.value = previous;
        if (err.message !== "login" && feedback) feedback.textContent = "No se pudo guardar.";
      } finally {
        select.disabled = false;
      }
    });
  });

  // ---------- Spoilers ----------
  document.addEventListener("click", (e) => {
    const btn = e.target.closest("[data-spoiler-toggle]");
    if (!btn) return;
    btn.closest("[data-spoiler]").querySelector("[data-spoiler-body]").classList.remove("d-none");
    btn.remove();
  });

  // ---------- Pestaña Videos: se carga solo al abrirla (cuota de YouTube) ----------
  document.querySelectorAll("[data-videos-url]").forEach((tab) => {
    let loaded = false;
    tab.addEventListener("shown.bs.tab", async () => {
      if (loaded) return;
      loaded = true;
      const target = document.querySelector("[data-videos-target]");
      try {
        const res = await fetch(tab.dataset.videosUrl, { credentials: "same-origin" });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        // HTML generado por el servidor (Razor ya escapó los textos).
        target.innerHTML = await res.text();
      } catch {
        loaded = false;
        target.innerHTML = '<p class="text-body-secondary">No se pudieron cargar los videos.</p>';
      }
    });
  });

  // ---------- Miniatura de YouTube → reproductor youtube-nocookie ----------
  document.addEventListener("click", (e) => {
    const btn = e.target.closest("[data-yt-embed]");
    if (!btn) return;
    const id = btn.dataset.ytEmbed;
    if (!/^[\w-]{6,20}$/.test(id)) return;
    const iframe = document.createElement("iframe");
    iframe.src = `https://www.youtube-nocookie.com/embed/${id}?autoplay=1`;
    iframe.title = btn.getAttribute("aria-label") ?? "Video de YouTube";
    iframe.allow = "accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture; fullscreen";
    iframe.referrerPolicy = "strict-origin-when-cross-origin";
    iframe.allowFullscreen = true;
    btn.replaceWith(iframe);
  });

  // ---------- Confirmación antes de borrar ----------
  document.addEventListener("submit", (e) => {
    const msg = e.target.dataset?.confirm;
    if (msg && !confirm(msg)) e.preventDefault();
  });

  // ---------- Abrir la pestaña indicada en la URL (#reviews) ----------
  if (location.hash) {
    const trigger = document.querySelector(`[data-bs-target="${CSS.escape(location.hash)}"]`);
    if (trigger && window.bootstrap) bootstrap.Tab.getOrCreateInstance(trigger).show();
  }
})();
