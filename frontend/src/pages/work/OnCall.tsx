import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Phone, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Avatar, Badge, EmptyState, Field, Modal, PageHeader, QueryState, Spinner, UserPicker } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api, errorMessage } from '@/lib/api'
import { fmt } from '@/lib/format'
import type { OnCall as O } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useCan } from '@/store/auth'

export default function OnCall() {
  const { can } = useCan()
  const qc = useQueryClient()
  const confirm = useConfirm()
  const q = useQuery({ queryKey: ['on-call'], queryFn: () => api.get<O[]>('/on-call') })
  const [open, setOpen] = useState(false)
  const [who, setWho] = useState<{ id: string; fullName: string } | null>(null)
  const [v, setV] = useState({ team: 'BT Destek', startsAt: '', endsAt: '', note: '' })
  const add = useMutation({
    mutationFn: () => api.post('/on-call', { ...v, userId: who!.id, startsAt: new Date(v.startsAt).toISOString(), endsAt: new Date(v.endsAt).toISOString() }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['on-call'] }); setOpen(false); toast.success('Nöbet eklendi.') },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const del = useMutation({ mutationFn: (id: number) => api.del(`/on-call/${id}`), onSuccess: () => qc.invalidateQueries({ queryKey: ['on-call'] }) })
  const teams = [...new Set(q.data?.map((x) => x.team) ?? [])]
  return (
    <div className="max-w-5xl">
      <PageHeader title="Nöbetçi Kişi" description="Mesai dışı acil durumlarda ulaşabileceğiniz nöbetçi personel." actions={can('oncall.manage') && <button className="btn-primary" onClick={() => setOpen(true)}><Plus className="h-4 w-4" /> Nöbet ekle</button>} />
      <QueryState query={q} empty={(d) => d.length === 0 && <div className="card"><EmptyState title="Tanımlı nöbet yok" /></div>}>
        {(list) => (
          <div className="space-y-6">
            <div className="grid gap-4 md:grid-cols-3">
              {list.filter((x) => x.isCurrent).map((o) => (
                <div key={o.id} className="card border-emerald-300/60 p-5 dark:border-emerald-400/20">
                  <Badge tone="green" dot>Şu an nöbette · {o.team}</Badge>
                  <div className="mt-3 flex items-center gap-3"><Avatar name={o.user.fullName} size={44} /><div><div className="font-semibold text-ink-900 dark:text-white">{o.user.fullName}</div><div className="text-xs muted">{o.jobTitle}</div></div></div>
                  {o.phone && <a href={`tel:${o.phone.replace(/\s/g, '')}`} className="btn-success mt-4 w-full"><Phone className="h-4 w-4" /> {o.phone}</a>}
                  <div className="mt-2 text-center text-xs muted">{fmt.dateTime(o.startsAt)} – {fmt.dateTime(o.endsAt)}</div>
                </div>
              ))}
            </div>
            {teams.map((t) => (
              <section key={t}>
                <h2 className="mb-2 text-xs font-semibold uppercase tracking-wider muted">{t}</h2>
                <div className="card divide-y divide-ink-100 dark:divide-white/[0.05]">
                  {list.filter((x) => x.team === t).map((o) => (
                    <div key={o.id} className={cn('flex items-center gap-3 px-5 py-3', o.isCurrent && 'bg-emerald-50/50 dark:bg-emerald-400/[0.04]')}>
                      <Avatar name={o.user.fullName} size={30} />
                      <div className="min-w-0 flex-1"><div className="text-sm font-medium text-ink-900 dark:text-white">{o.user.fullName}</div><div className="text-xs muted">{fmt.dateTime(o.startsAt)} – {fmt.dateTime(o.endsAt)}{o.note && ` · ${o.note}`}</div></div>
                      {o.isCurrent && <Badge tone="green">Aktif</Badge>}
                      {can('oncall.manage') && <button className="btn-ghost btn-icon text-red-600" aria-label="Sil" onClick={async () => (await confirm({ title: 'Nöbeti sil', message: `${o.user.fullName} · ${t}`, danger: true, confirmText: 'Sil' })) && del.mutate(o.id)}><Trash2 className="h-4 w-4" /></button>}
                    </div>
                  ))}
                </div>
              </section>
            ))}
          </div>
        )}
      </QueryState>
      <Modal open={open} onClose={() => setOpen(false)} title="Nöbet ekle" footer={<><button className="btn-secondary" onClick={() => setOpen(false)}>Vazgeç</button><button className="btn-primary" disabled={!who || !v.startsAt || !v.endsAt || add.isPending} onClick={() => add.mutate()}>{add.isPending && <Spinner />} Ekle</button></>}>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Personel" required className="sm:col-span-2"><UserPicker value={who} onChange={setWho} /></Field>
          <Field label="Ekip" required className="sm:col-span-2"><input className="input" list="teams" value={v.team} onChange={(e) => setV({ ...v, team: e.target.value })} /><datalist id="teams">{teams.map((t) => <option key={t} value={t} />)}</datalist></Field>
          <Field label="Başlangıç" required><input type="datetime-local" className="input" value={v.startsAt} onChange={(e) => setV({ ...v, startsAt: e.target.value })} /></Field>
          <Field label="Bitiş" required><input type="datetime-local" className="input" value={v.endsAt} onChange={(e) => setV({ ...v, endsAt: e.target.value })} /></Field>
          <Field label="Not" className="sm:col-span-2"><input className="input" value={v.note} onChange={(e) => setV({ ...v, note: e.target.value })} /></Field>
        </div>
      </Modal>
    </div>
  )
}
