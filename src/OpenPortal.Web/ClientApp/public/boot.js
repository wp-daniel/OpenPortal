// Runs before first paint, from <head>, so a reload never flashes the wrong theme or language. A file rather
// than an inline script so the Content-Security-Policy can stay "script-src 'self'".
try {
  // The saved (or OS) theme.
  var t = localStorage.getItem('openportal-theme')
  if (t !== 'light' && t !== 'dark') t = matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
  document.documentElement.classList.toggle('dark', t === 'dark')
} catch {
  // Storage can be unavailable (private mode, blocked site data); the defaults apply.
}

try {
  // <html lang> from the saved language; the app corrects it once the offered languages are known.
  var l = localStorage.getItem('openportal-lang')
  if (l && /^[A-Za-z-]{2,10}$/.test(l)) document.documentElement.lang = l
} catch {
  // Storage can be unavailable (private mode, blocked site data); the defaults apply.
}
