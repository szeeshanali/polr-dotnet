// Draws the clicks-per-day chart on the stats page from the JSON embedded in the page.
const days = JSON.parse(document.getElementById('day-data').textContent);
const brand = getComputedStyle(document.documentElement).getPropertyValue('--brand').trim() || '#4f46e5';

new Chart(document.getElementById('day-chart'), {
  type: 'line',
  data: {
    labels: days.map(d => d.X),
    datasets: [{
      label: 'Clicks', data: days.map(d => d.Y), tension: 0.3, fill: true, pointRadius: 2,
      borderColor: brand, backgroundColor: brand + '22', pointBackgroundColor: brand,
    }],
  },
  options: {
    plugins: { legend: { display: false } },
    scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
  },
});
