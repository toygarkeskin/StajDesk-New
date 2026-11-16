// Kayıtlı tema tercihini sayfa çizilmeden önce uygular (varsayılan: koyu).
try {
  if (localStorage.getItem('stajdesk-theme') === 'light') document.documentElement.classList.remove('dark')
} catch {
  // Depolama kapalıysa varsayılan tema kalır
}
