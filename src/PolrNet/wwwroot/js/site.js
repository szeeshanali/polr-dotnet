// Small enhancements; every page works without JavaScript too.

// Copy buttons: <button data-copy="#input-id">
document.querySelectorAll('[data-copy]').forEach(button => {
  button.addEventListener('click', async () => {
    const input = document.querySelector(button.dataset.copy);
    try {
      await navigator.clipboard.writeText(input.value);
    } catch {
      input.select(); // clipboard API needs HTTPS; let the user copy by hand
      return;
    }
    button.textContent = 'Copied!';
  });
});

// QR codes: <div data-qr="text">, drawn by qrcode.min.js when the page includes it.
if (window.QRCode) {
  document.querySelectorAll('[data-qr]').forEach(el => new QRCode(el, { text: el.dataset.qr, width: 128, height: 128 }));
}

// Custom ending availability check on the shorten form.
document.getElementById('check-ending')?.addEventListener('click', async () => {
  const ending = document.getElementById('CustomEnding').value.trim();
  const status = document.getElementById('ending-status');
  if (!ending) return;

  const response = await fetch('/api/v2/link_avail_check?link_ending=' + encodeURIComponent(ending));
  const result = await response.text();
  status.textContent = { available: 'Available!', unavailable: 'Already taken.', invalid: 'Only letters, numbers, - and _ are allowed.' }[result];
  status.className = 'form-text ' + (result === 'available' ? 'text-success' : 'text-danger');
});
