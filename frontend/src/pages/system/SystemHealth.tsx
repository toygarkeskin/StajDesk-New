import { useQuery } from '@tanstack/react-query'
import { CheckCircle2, Cpu, Database, ExternalLink, FileText, MessageSquareShare, RefreshCw, Server, XCircle, Zap } from 'lucide-react'
import { Badge, PageHeader, QueryState, Skeleton } from '@/components/ui'
import { api } from '@/lib/api'
import { fmt } from '@/lib/format'
import type { SystemHealth as H } from '@/lib/types'
import { cn } from '@/lib/utils'

const icon: Record<string, typeof Database> = { PostgreSQL: Database, Redis: Zap, RabbitMQ: MessageSquareShare, Worker: Cpu, Seq: FileText }

export default function SystemHealth() {
  const q = useQuery({ queryKey: ['system-health'], queryFn: () => api.get<H>('/system/health'), refetchInterval: 15000 })
  return (
    <div>
      <PageHeader title="Sistem Durumu" description="Altyapı bileşenlerinin anlık sağlık durumu (15 sn'de bir yenilenir)."
        actions={<><a className="btn-secondary" href="/swagger" target="_blank" rel="noreferrer"><ExternalLink className="h-4 w-4" /> Swagger</a><button className="btn-secondary" onClick={() => q.refetch()} disabled={q.isFetching}><RefreshCw className={cn('h-4 w-4', q.isFetching && 'animate-spin')} /> Yenile</button></>} />
      <QueryState query={q} skeleton={<div className="grid gap-4 md:grid-cols-3">{[0, 1, 2, 3, 4].map((i) => <Skeleton key={i} className="h-32" />)}</div>}>
        {(h) => (
          <div className="space-y-6">
            <div className={cn('card flex items-center gap-4 p-5', h.healthy ? 'border-emerald-300/60 dark:border-emerald-400/20' : 'border-amber-300/70 dark:border-amber-400/20')}>
              {h.healthy ? <CheckCircle2 className="h-8 w-8 text-emerald-500" /> : <XCircle className="h-8 w-8 text-amber-500" />}
              <div><div className="text-lg font-semibold text-ink-900 dark:text-white">{h.healthy ? 'Tüm sistemler çalışıyor' : 'Bazı bileşenlerde sorun var'}</div><div className="text-sm muted">Son kontrol {fmt.time(h.checkedAt)} · Outbox: {h.outboxPending} bekleyen, {h.outboxFailed} başarısız olay</div></div>
            </div>
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {h.components.map((c) => {
                const I = icon[c.name] ?? Server
                return (
                  <div key={c.name} className="card p-5">
                    <div className="flex items-start justify-between">
                      <div className="flex items-center gap-3"><span className={cn('grid h-10 w-10 place-items-center rounded-xl', c.healthy ? 'bg-emerald-50 text-emerald-600 dark:bg-emerald-400/10 dark:text-emerald-300' : 'bg-red-50 text-red-600 dark:bg-red-400/10 dark:text-red-300')}><I className="h-5 w-5" /></span><div><div className="font-semibold text-ink-900 dark:text-white">{c.name}</div><div className="text-xs muted">{c.role}</div></div></div>
                      <Badge tone={c.healthy ? 'green' : 'red'} dot>{c.healthy ? 'Sağlıklı' : 'Sorunlu'}</Badge>
                    </div>
                    <p className="mt-4 text-sm">{c.detail}</p>
                    <div className="mt-2 text-xs muted">Yanıt süresi: {c.latencyMs} ms</div>
                  </div>
                )
              })}
            </div>
            <div className="card p-5 text-sm muted">
              <b className="text-ink-800 dark:text-ink-100">Yönetim arayüzleri (yerel):</b> RabbitMQ <code className="kbd">http://localhost:15673</code> · Seq <code className="kbd">http://localhost:5342</code> · Sağlık uçları <code className="kbd">/health/live</code>, <code className="kbd">/health/ready</code>
            </div>
          </div>
        )}
      </QueryState>
    </div>
  )
}
