import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { toast } from 'sonner'
import { create } from 'zustand'
import { useAuth } from '@/store/auth'

export interface RealtimeNotification {
  title: string
  message: string
  kind: string
  link?: string | null
}

/** Bağlantı durumu: bağlıyken periyodik sorgular seyrekleşir, koptuğunda sık sorguya geri dönülür. */
export const useRealtimeStatus = create<{ connected: boolean; set: (c: boolean) => void }>((set) => ({
  connected: false,
  set: (connected) => set({ connected }),
}))

/** Bağlıyken 2 dk, değilken verilen aralıkta yenile (gerçek zamanlı kanal düşerse veri yine güncel kalır). */
export function usePollInterval(fallbackMs: number) {
  const connected = useRealtimeStatus((s) => s.connected)
  return connected ? 120_000 : fallbackMs
}

/**
 * SignalR hub'ına bağlanır ve sunucudan gelen bildirimlerde ilgili sorguları anında yeniler.
 * Token, her (yeniden) bağlanmada auth deposundan okunur; böylece token yenilense de bağlantı sürer.
 */
export function useRealtime(onNotify?: (n: RealtimeNotification) => void) {
  const qc = useQueryClient()
  const userId = useAuth((s) => s.user?.id)
  const setConnected = useRealtimeStatus((s) => s.set)

  useEffect(() => {
    if (!userId) return
    let stopped = false
    const conn: HubConnection = new HubConnectionBuilder()
      .withUrl('/hubs/notifications', { accessTokenFactory: () => useAuth.getState().accessToken ?? '' })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000, 60000])
      // Bağlantı hataları aşağıda ele alınır (yeniden deneme + periyodik sorguya geri dönüş); istemcinin kendi
      // konsol günlüğü yalnızca kritik durumlar için açık, aksi halde normal sayfa geçişleri konsolu hatayla doldurur.
      .configureLogging(LogLevel.Critical)
      .build()

    conn.on('notification', (n: RealtimeNotification) => {
      qc.invalidateQueries({ queryKey: ['notif-count'] })
      qc.invalidateQueries({ queryKey: ['notifications'] })
      if (n.kind === 'approval') qc.invalidateQueries({ queryKey: ['approval-count'] })
      if (n.kind === 'message') {
        qc.invalidateQueries({ queryKey: ['msg-count'] })
        qc.invalidateQueries({ queryKey: ['conversations'] })
        qc.invalidateQueries({ queryKey: ['thread'] })
      }
      // Bildirimin ilgili olduğu kayıt açıksa (talep / başvuru detayı) anında yenilenir
      const ticket = n.link?.match(/^\/tickets\/(\d+)/)?.[1]
      if (n.link?.startsWith('/tickets')) qc.invalidateQueries({ queryKey: ['tickets'] })
      if (ticket) qc.invalidateQueries({ queryKey: ['ticket', ticket] })
      const submission = n.link?.match(/^\/submissions\/(\d+)/)?.[1]
      if (submission) qc.invalidateQueries({ queryKey: ['submission', submission] })
      if (n.link === '/approvals' || submission) qc.invalidateQueries({ queryKey: ['submissions'] })
      onNotify?.(n)
    })
    conn.onreconnecting(() => setConnected(false))
    conn.onreconnected(() => {
      setConnected(true)
      // Kopukken kaçan bildirimleri yakala
      qc.invalidateQueries({ queryKey: ['notif-count'] })
      qc.invalidateQueries({ queryKey: ['msg-count'] })
    })
    conn.onclose(() => setConnected(false))

    const start = async (attempt = 0) => {
      try {
        await conn.start()
        if (!stopped) setConnected(true)
      } catch {
        // Hub'a ulaşılamazsa periyodik sorgu devam eder; artan aralıklarla tekrar denenir.
        if (!stopped && conn.state === HubConnectionState.Disconnected)
          setTimeout(() => !stopped && start(attempt + 1), Math.min(60_000, 3000 * (attempt + 1)))
      }
    }
    // Bir sonraki tikte başlat: bileşen hemen sökülürse (StrictMode çift çalıştırma, hızlı sayfa geçişi)
    // bağlantı hiç açılmaz; "pazarlık sırasında durduruldu" hatası oluşmaz.
    const timer = setTimeout(() => start(), 0)

    return () => {
      stopped = true
      clearTimeout(timer)
      setConnected(false)
      if (conn.state !== HubConnectionState.Disconnected) conn.stop().catch(() => {})
    }
  }, [userId, qc, setConnected, onNotify])
}

const toastKind: Record<string, 'success' | 'warning' | 'info'> = { success: 'success', warning: 'warning' }

/** Varsayılan bildirim gösterimi: mesajlar sayfasındayken mesaj bildirimini tekrar göstermez. */
export function showRealtimeToast(n: RealtimeNotification, navigate: (to: string) => void, pathname: string) {
  if (n.kind === 'message' && pathname.startsWith('/messages')) return
  const fn = toast[toastKind[n.kind] ?? 'info']
  fn(n.title, {
    description: n.message,
    action: n.link ? { label: 'Aç', onClick: () => navigate(n.link!) } : undefined,
  })
}
