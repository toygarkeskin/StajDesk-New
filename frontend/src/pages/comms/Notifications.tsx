import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, Bell, CheckCheck, CheckCircle2, ClipboardCheck, Info, LifeBuoy, MessageSquare, UserPlus } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { EmptyState, PageHeader, QueryState, Tabs } from '@/components/ui'
import { api } from '@/lib/api'
import { fmt } from '@/lib/format'
import { usePollInterval } from '@/lib/realtime'
import type { NotificationItem } from '@/lib/types'
import { cn } from '@/lib/utils'

const icons: Record<string, { icon: typeof Info; cls: string }> = {
  ticket: { icon: LifeBuoy, cls: 'bg-sky-50 text-sky-600 dark:bg-sky-400/10 dark:text-sky-300' },
  approval: { icon: ClipboardCheck, cls: 'bg-amber-50 text-amber-600 dark:bg-amber-400/10 dark:text-amber-300' },
  success: { icon: CheckCircle2, cls: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-400/10 dark:text-emerald-300' },
  warning: { icon: AlertTriangle, cls: 'bg-red-50 text-red-600 dark:bg-red-400/10 dark:text-red-300' },
  message: { icon: MessageSquare, cls: 'bg-violet-50 text-violet-600 dark:bg-violet-400/10 dark:text-violet-300' },
  account: { icon: UserPlus, cls: 'bg-brand-50 text-brand-600 dark:bg-brand-400/10 dark:text-brand-300' },
  info: { icon: Info, cls: 'bg-ink-100 text-ink-600 dark:bg-white/[0.06] dark:text-ink-300' },
}

export default function Notifications() {
  const [tab, setTab] = useState<'all' | 'unread'>('all')
  const qc = useQueryClient()
  const nav = useNavigate()
  const poll = usePollInterval(20000)
  const q = useQuery({ queryKey: ['notifications', tab], queryFn: () => api.get<NotificationItem[]>('/notifications', { unreadOnly: tab === 'unread', take: 100 }), refetchInterval: poll })
  const refresh = () => { qc.invalidateQueries({ queryKey: ['notifications'] }); qc.invalidateQueries({ queryKey: ['notif-count'] }) }
  const readAll = useMutation({ mutationFn: () => api.post('/notifications/read-all'), onSuccess: refresh })
  const open = async (n: NotificationItem) => { if (!n.isRead) { await api.post(`/notifications/${n.id}/read`); refresh() } if (n.link) nav(n.link) }
  return (
    <div className="max-w-3xl">
      <PageHeader title="Bildirim Merkezi" description="Talep, onay, hesap ve iletişim bildirimleriniz. Bildirimler arka plan servisi (Worker) tarafından oluşturulur." actions={<button className="btn-secondary" onClick={() => readAll.mutate()}><CheckCheck className="h-4 w-4" /> Tümünü okundu yap</button>} />
      <Tabs className="mb-4" value={tab} onChange={setTab} tabs={[{ value: 'all', label: 'Tümü' }, { value: 'unread', label: 'Okunmamış' }]} />
      <QueryState query={q} empty={(d) => d.length === 0 && <div className="card"><EmptyState icon={<Bell className="h-5 w-5" />} title={tab === 'unread' ? 'Okunmamış bildiriminiz yok' : 'Bildiriminiz yok'} /></div>}>
        {(list) => (
          <div className="card divide-y divide-ink-100 dark:divide-white/[0.05]">
            {list.map((n) => {
              const m = icons[n.kind] ?? icons.info
              return (
                <button key={n.id} onClick={() => open(n)} className={cn('flex w-full gap-4 px-5 py-4 text-left hover:bg-ink-50/70 dark:hover:bg-white/[0.02]', !n.isRead && 'bg-brand-50/40 dark:bg-brand-400/[0.04]')}>
                  <span className={cn('grid h-9 w-9 shrink-0 place-items-center rounded-xl', m.cls)}><m.icon className="h-4 w-4" /></span>
                  <span className="min-w-0 flex-1">
                    <span className="flex items-center justify-between gap-3"><span className={cn('text-sm', n.isRead ? 'font-medium' : 'font-semibold text-ink-900 dark:text-white')}>{n.title}</span><span className="shrink-0 text-xs muted">{fmt.relative(n.createdAt)}</span></span>
                    <span className="mt-0.5 block text-sm muted">{n.message}</span>
                  </span>
                  {!n.isRead && <span className="mt-2 h-2 w-2 shrink-0 rounded-full bg-brand-500" />}
                </button>
              )
            })}
          </div>
        )}
      </QueryState>
    </div>
  )
}
