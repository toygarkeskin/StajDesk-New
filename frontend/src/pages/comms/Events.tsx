import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, MapPin, Users } from 'lucide-react'
import { toast } from 'sonner'
import { Badge, EmptyState, PageHeader, QueryState } from '@/components/ui'
import { api } from '@/lib/api'
import { fmt } from '@/lib/format'
import type { CompanyEvent } from '@/lib/types'
import { cn } from '@/lib/utils'

export default function Events() {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['events'], queryFn: () => api.get<CompanyEvent[]>('/events') })
  const attend = useMutation({
    mutationFn: (id: number) => api.post<{ attending: boolean }>(`/events/${id}/attend`),
    onSuccess: (r) => { qc.invalidateQueries({ queryKey: ['events'] }); toast.success(r.attending ? 'Katılımınız kaydedildi.' : 'Katılımınız iptal edildi.') },
  })
  return (
    <div className="max-w-5xl">
      <PageHeader title="Etkinlikler" description="Kurum içi toplantılar, eğitimler ve sosyal etkinlikler." />
      <QueryState query={q} empty={(d) => d.length === 0 && <div className="card"><EmptyState title="Yaklaşan etkinlik yok" /></div>}>
        {(list) => (
          <div className="grid gap-4 md:grid-cols-2">
            {list.map((e) => {
              const d = new Date(e.startsAt)
              return (
                <div key={e.id} className="card flex gap-4 p-5">
                  <div className="grid h-16 w-16 shrink-0 place-items-center rounded-2xl bg-brand-50 text-center dark:bg-brand-400/10">
                    <div><div className="text-[11px] font-semibold uppercase text-brand-600 dark:text-brand-300">{d.toLocaleDateString('tr-TR', { month: 'short' })}</div><div className="font-display text-2xl leading-none font-bold text-brand-700 dark:text-brand-100">{d.getDate()}</div></div>
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2"><Badge tone="violet">{e.category}</Badge>{e.isPublic && <Badge tone="cyan">Genel sitede</Badge>}</div>
                    <h3 className="mt-1.5 font-semibold text-ink-900 dark:text-white">{e.title}</h3>
                    <p className="mt-0.5 text-sm muted">{e.description}</p>
                    <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs muted"><span>{fmt.weekday(e.startsAt)} {fmt.time(e.startsAt)}{e.endsAt && `–${fmt.time(e.endsAt)}`}</span>{e.location && <span className="inline-flex items-center gap-1"><MapPin className="h-3 w-3" />{e.location}</span>}<span className="inline-flex items-center gap-1"><Users className="h-3 w-3" />{e.attendeeCount} katılımcı</span></div>
                    <button className={cn('mt-3', e.isAttending ? 'btn-success btn-sm' : 'btn-secondary btn-sm')} disabled={attend.isPending} onClick={() => attend.mutate(e.id)}>{e.isAttending ? <><Check className="h-3.5 w-3.5" /> Katılıyorum</> : 'Katılacağım'}</button>
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </QueryState>
    </div>
  )
}
