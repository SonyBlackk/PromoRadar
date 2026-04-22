(() => {
  const currencyFormatter = new Intl.NumberFormat("pt-BR", {
    style: "currency",
    currency: "BRL"
  });

  function safeParse(input, fallback) {
    try {
      return JSON.parse(input);
    } catch {
      return fallback;
    }
  }

  function buildLabels(period, count, labels7d) {
    if (period === "7D" && labels7d.length === count) {
      return labels7d;
    }

    if (period === "1A") {
      const months = ["Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez"];
      return months.slice(0, count);
    }

    if (period === "Tudo") {
      return Array.from({ length: count }, (_, index) => `T${index + 1}`);
    }

    return Array.from({ length: count }, (_, index) => `${index + 1}`);
  }

  function initMainChart() {
    const canvas = document.getElementById("mainPriceChart");
    if (!canvas || !window.Chart) {
      return;
    }

    const seriesByPeriod = safeParse(canvas.dataset.series || "{}", {});
    const labels7d = safeParse(canvas.dataset.labels || "[]", []);

    const defaultPeriod = "7D";
    const defaultSeries = seriesByPeriod[defaultPeriod] || [];

    const context = canvas.getContext("2d");
    const gradient = context.createLinearGradient(0, 0, 0, 320);
    gradient.addColorStop(0, "rgba(91, 87, 243, 0.35)");
    gradient.addColorStop(1, "rgba(91, 87, 243, 0.02)");

    const chart = new Chart(context, {
      type: "line",
      data: {
        labels: buildLabels(defaultPeriod, defaultSeries.length, labels7d),
        datasets: [
          {
            data: defaultSeries,
            tension: 0.38,
            borderColor: "#5b57f3",
            borderWidth: 2.5,
            pointRadius: 4,
            pointHoverRadius: 6,
            pointBackgroundColor: "#ffffff",
            pointBorderColor: "#5b57f3",
            pointBorderWidth: 2,
            fill: true,
            backgroundColor: gradient
          }
        ]
      },
      options: {
        maintainAspectRatio: false,
        responsive: true,
        plugins: {
          legend: { display: false },
          tooltip: {
            backgroundColor: "#151a2f",
            titleFont: { family: "Plus Jakarta Sans" },
            bodyFont: { family: "Plus Jakarta Sans" },
            callbacks: {
              label(ctx) {
                return currencyFormatter.format(ctx.parsed.y);
              }
            }
          }
        },
        scales: {
          x: {
            grid: { display: false },
            ticks: {
              color: "#7c859b",
              font: { family: "Plus Jakarta Sans", weight: "500" }
            }
          },
          y: {
            beginAtZero: false,
            ticks: {
              color: "#7c859b",
              callback(value) {
                return currencyFormatter.format(value);
              },
              font: { family: "Plus Jakarta Sans", weight: "500" }
            },
            grid: {
              color: "rgba(148, 163, 184, 0.2)",
              drawBorder: false
            }
          }
        }
      }
    });

    const periodButtons = document.querySelectorAll(".period-btn");
    periodButtons.forEach((button) => {
      button.addEventListener("click", () => {
        const period = button.dataset.period;
        const selectedSeries = seriesByPeriod[period] || [];

        periodButtons.forEach((item) => item.classList.remove("is-active"));
        button.classList.add("is-active");

        chart.data.labels = buildLabels(period, selectedSeries.length, labels7d);
        chart.data.datasets[0].data = selectedSeries;
        chart.update();
      });
    });
  }

  function initSparklines() {
    const sparklineElements = document.querySelectorAll("canvas.sparkline");
    sparklineElements.forEach((canvas) => {
      const points = safeParse(canvas.dataset.points || "[]", []);
      if (!points.length || !window.Chart) {
        return;
      }

      const color = canvas.closest(".suggestion-card")?.querySelector(".suggestion-badge")?.classList.contains("attention")
        ? "#f97316"
        : "#10b981";

      const context = canvas.getContext("2d");
      const gradient = context.createLinearGradient(0, 0, 0, 64);
      gradient.addColorStop(0, `${color}40`);
      gradient.addColorStop(1, `${color}00`);

      new Chart(context, {
        type: "line",
        data: {
          labels: points.map((_, idx) => idx + 1),
          datasets: [
            {
              data: points,
              borderColor: color,
              backgroundColor: gradient,
              fill: true,
              borderWidth: 2,
              tension: 0.45,
              pointRadius: 0
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { display: false }, tooltip: { enabled: false } },
          scales: {
            x: { display: false },
            y: { display: false }
          }
        }
      });
    });
  }

  document.addEventListener("DOMContentLoaded", () => {
    initMainChart();
    initSparklines();
  });
})();
