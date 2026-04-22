document.addEventListener("DOMContentLoaded", () => {
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
