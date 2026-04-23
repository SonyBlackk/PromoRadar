(() => {
  const TABLE_CONTENT_ID = "myProductsTableContent";
  const FILTER_FORM_ID = "myProductsFiltersForm";
  const INLINE_NOTICE_ID = "myProductsInlineNotice";
  const PARTIAL_KEY = "partial";
  const PARTIAL_VALUE = "content";

  let requestSequence = 0;
  let noticeTimeoutId = null;

  function parseJson(value, fallback) {
    try {
      return JSON.parse(value);
    } catch {
      return fallback;
    }
  }

  function debounce(fn, delayMs) {
    let timerId = null;
    return (...args) => {
      if (timerId) {
        clearTimeout(timerId);
      }

      timerId = window.setTimeout(() => {
        fn(...args);
      }, delayMs);
    };
  }

  function getFiltersForm() {
    return document.getElementById(FILTER_FORM_ID);
  }

  function getTableContentContainer() {
    return document.getElementById(TABLE_CONTENT_ID);
  }

  function renderCharts(root = document) {
    if (!window.Chart) {
      return;
    }

    const trendCanvases = root.querySelectorAll("canvas.my-product-trend");
    trendCanvases.forEach((canvas) => {
      const points = parseJson(canvas.dataset.points || "[]", []);
      if (!Array.isArray(points) || points.length < 2) {
        return;
      }

      const color = canvas.dataset.color || "#10b981";
      const context = canvas.getContext("2d");
      if (!context) {
        return;
      }

      const gradient = context.createLinearGradient(0, 0, 0, 64);
      gradient.addColorStop(0, `${color}33`);
      gradient.addColorStop(1, `${color}00`);

      new Chart(context, {
        type: "line",
        data: {
          labels: points.map((_, index) => index + 1),
          datasets: [
            {
              data: points,
              borderColor: color,
              borderWidth: 2,
              pointRadius: points.map((_, index) => (index === points.length - 1 ? 3 : 0)),
              pointHoverRadius: 3,
              pointBackgroundColor: color,
              pointBorderColor: color,
              fill: true,
              backgroundColor: gradient,
              tension: 0.38
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: { display: false },
            tooltip: { enabled: false }
          },
          scales: {
            x: { display: false },
            y: { display: false }
          }
        }
      });
    });
  }

  function closeAllDetailMenus() {
    const openMenus = document.querySelectorAll(".my-products-row-menu[open], .my-products-notify-menu[open]");
    openMenus.forEach((menu) => {
      menu.removeAttribute("open");
    });
  }

  function bindOutsideClickToCloseMenus() {
    document.addEventListener("click", (event) => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) {
        return;
      }

      const insideMenu = target.closest(".my-products-row-menu, .my-products-notify-menu");
      if (!insideMenu) {
        closeAllDetailMenus();
      }
    });
  }

  function showInlineNotice(message) {
    const notice = document.getElementById(INLINE_NOTICE_ID);
    if (!notice) {
      return;
    }

    if (noticeTimeoutId) {
      clearTimeout(noticeTimeoutId);
      noticeTimeoutId = null;
    }

    notice.classList.remove("d-none");
    notice.innerHTML = `<i class="bi bi-check2-circle"></i><span>${message}</span>`;

    noticeTimeoutId = window.setTimeout(() => {
      notice.classList.add("d-none");
      notice.innerHTML = "";
      noticeTimeoutId = null;
    }, 3200);
  }

  function bindNotificationPanels(root = document) {
    const saveButtons = root.querySelectorAll(".js-save-notify-channels");
    saveButtons.forEach((button) => {
      if (button.dataset.bound === "true") {
        return;
      }

      button.dataset.bound = "true";
      button.addEventListener("click", () => {
        const productName = button.dataset.productName || "produto";
        const panel = button.closest(".my-products-notify-menu");
        if (panel) {
          panel.removeAttribute("open");
        }

        showInlineNotice(`Canais de notificação atualizados para ${productName}.`);
      });
    });
  }

  function buildFetchUrl({ form, resetPage = false, urlOverride = null }) {
    const baseUrl = form?.dataset.baseUrl || form?.action || window.location.href;

    const url = urlOverride ? new URL(urlOverride, window.location.origin) : new URL(baseUrl, window.location.origin);
    const params = new URLSearchParams(url.search);

    if (form) {
      const formData = new FormData(form);
      const formParams = new URLSearchParams();
      for (const [key, value] of formData.entries()) {
        const normalized = String(value ?? "").trim();
        if (normalized.length > 0) {
          formParams.set(key, normalized);
        }
      }

      for (const [key, value] of formParams.entries()) {
        params.set(key, value);
      }

      if (!formParams.has("search")) {
        params.delete("search");
      }
      if (!formParams.has("store")) {
        params.delete("store");
      }
      if (!formParams.has("category")) {
        params.delete("category");
      }
      if (!formParams.has("sort")) {
        params.delete("sort");
      }
    }

    if (resetPage) {
      params.set("page", "1");
    }

    params.set(PARTIAL_KEY, PARTIAL_VALUE);
    url.search = params.toString();
    return url;
  }

  function updateBrowserUrl(requestUrl) {
    const cleanUrl = new URL(requestUrl.toString());
    cleanUrl.searchParams.delete(PARTIAL_KEY);
    window.history.replaceState({}, "", `${cleanUrl.pathname}${cleanUrl.search}`);
  }

  async function fetchTableContent({ form, resetPage = false, urlOverride = null }) {
    const container = getTableContentContainer();
    if (!container || !form) {
      return;
    }

    const sequence = ++requestSequence;
    const requestUrl = buildFetchUrl({ form, resetPage, urlOverride });

    container.classList.add("is-loading");
    try {
      const response = await fetch(requestUrl.toString(), {
        headers: {
          "X-Requested-With": "XMLHttpRequest"
        }
      });

      if (!response.ok) {
        throw new Error(`Request failed with status ${response.status}`);
      }

      const html = await response.text();
      if (sequence !== requestSequence) {
        return;
      }

      container.innerHTML = html;
      updateBrowserUrl(requestUrl);

      renderCharts(container);
      bindNotificationPanels(container);
    } finally {
      container.classList.remove("is-loading");
    }
  }

  function bindFilters() {
    const form = getFiltersForm();
    if (!form) {
      return;
    }

    const searchInput = form.querySelector("input[name='search']");
    const selectInputs = form.querySelectorAll(".js-auto-submit-filter");

    const debouncedSearch = debounce(() => {
      fetchTableContent({ form, resetPage: true }).catch(() => form.submit());
    }, 320);

    if (searchInput) {
      searchInput.addEventListener("input", debouncedSearch);
      searchInput.addEventListener("keydown", (event) => {
        if (event.key === "Enter") {
          event.preventDefault();
          fetchTableContent({ form, resetPage: true }).catch(() => form.submit());
        }
      });
    }

    selectInputs.forEach((input) => {
      input.addEventListener("change", () => {
        fetchTableContent({ form, resetPage: true }).catch(() => form.submit());
      });
    });
  }

  function bindPaginationAjax() {
    const container = getTableContentContainer();
    const form = getFiltersForm();
    if (!container || !form) {
      return;
    }

    container.addEventListener("click", (event) => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) {
        return;
      }

      const pageLink = target.closest(".my-products-pagination a.my-products-page-btn");
      if (!pageLink) {
        return;
      }

      event.preventDefault();
      const href = pageLink.getAttribute("href");
      if (!href) {
        return;
      }

      fetchTableContent({ form, urlOverride: href }).catch(() => {
        window.location.href = href;
      });
    });
  }

  document.addEventListener("DOMContentLoaded", () => {
    renderCharts(document);
    bindNotificationPanels(document);
    bindOutsideClickToCloseMenus();
    bindFilters();
    bindPaginationAjax();
  });
})();
