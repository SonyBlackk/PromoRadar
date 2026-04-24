const THEME_STORAGE_KEY = "promoradar-theme";
const LIGHT_THEME = "light";
const DARK_THEME = "dark";

function getSavedTheme() {
  try {
    const savedTheme = localStorage.getItem(THEME_STORAGE_KEY);
    return savedTheme === DARK_THEME || savedTheme === LIGHT_THEME
      ? savedTheme
      : LIGHT_THEME;
  } catch {
    return LIGHT_THEME;
  }
}

function saveTheme(theme) {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, theme);
  } catch {
    // Ignore storage failures (private mode, blocked storage, etc.)
  }
}

function updateThemeToggleButtons(theme) {
  const isDark = theme === DARK_THEME;
  const buttons = document.querySelectorAll("[data-theme-toggle]");

  buttons.forEach((button) => {
    const icon = button.querySelector("i");
    const nextModeLabel = isDark ? "Ativar modo claro" : "Ativar modo escuro";

    button.setAttribute("aria-label", nextModeLabel);
    button.setAttribute("title", nextModeLabel);
    button.setAttribute("aria-pressed", String(isDark));
    button.classList.toggle("theme-toggle-active", isDark);

    if (!icon) {
      return;
    }

    icon.classList.remove("bi-sun", "bi-moon-stars");
    icon.classList.add(isDark ? "bi-sun" : "bi-moon-stars");
  });
}

function applyTheme(theme) {
  const normalizedTheme = theme === DARK_THEME ? DARK_THEME : LIGHT_THEME;
  document.documentElement.setAttribute("data-theme", normalizedTheme);
  updateThemeToggleButtons(normalizedTheme);
}

function toggleTheme() {
  const activeTheme =
    document.documentElement.getAttribute("data-theme") === DARK_THEME
      ? DARK_THEME
      : LIGHT_THEME;
  const nextTheme = activeTheme === DARK_THEME ? LIGHT_THEME : DARK_THEME;

  applyTheme(nextTheme);
  saveTheme(nextTheme);
}

applyTheme(getSavedTheme());

document.addEventListener("DOMContentLoaded", () => {
  const themeButtons = document.querySelectorAll("[data-theme-toggle]");
  themeButtons.forEach((button) => {
    button.addEventListener("click", toggleTheme);
  });

  const searchInput = document.querySelector(".topbar-search input");
  if (!searchInput) {
    return;
  }

  searchInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
    }
  });
});
