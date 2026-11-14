import { Toaster } from 'sonner'
import { useUi } from '@/store/ui'

/** Temaya uyan bildirim (toast) kapsayıcısı */
export function ThemedToaster() {
  const theme = useUi((s) => s.theme)
  return <Toaster theme={theme} position="top-right" richColors closeButton toastOptions={{ style: { fontFamily: 'Inter, sans-serif' } }} />
}
