import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Award, Heart, Medal, Sparkles, Trophy } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Avatar, Badge, EmptyState, Field, Modal, PageHeader, QueryState, Spinner, UserPicker } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { fmt } from '@/lib/format'
import type { Achievement, AchievementKind } from '@/lib/types'
import { useCan } from '@/store/auth'

const kindMeta: Record<AchievementKind, { label: string; icon: typeof Trophy; tone: 'amber' | 'brand' | 'violet' }> = {
  Award: { label: 'Ödül', icon: Trophy, tone: 'amber' }, Kudos: { label: 'Takdir', icon: Sparkles, tone: 'brand' }, Milestone: { label: 'Kıdem', icon: Medal, tone: 'violet' },
}

export default function Achievements() {
  const qc = useQueryClient()
  const { can } = useCan()
  const q = useQuery({ queryKey: ['achievements'], queryFn: () => api.get<Achievement[]>('/achievements', { take: 60 }) })
  const [open, setOpen] = useState(false)
  const [who, setWho] = useState<{ id: string; fullName: string } | null>(null)
  const [v, setV] = useState({ title: '', description: '', kind: 'Kudos' as AchievementKind })
  const [liked, setLiked] = useState<number[]>([])
  const give = useMutation({
    mutationFn: () => api.post('/achievements', { userId: who!.id, ...v }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['achievements'] }); setOpen(false); setWho(null); setV({ title: '', description: '', kind: 'Kudos' }); toast.success('Takdiriniz iletildi 🎉') },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const like = useMutation({ mutationFn: (id: number) => api.post(`/achievements/${id}/like`), onSuccess: (_, id) => { setLiked((l) => [...l, id]); qc.invalidateQueries({ queryKey: ['achievements'] }) } })
  return (
    <div className="max-w-5xl">
      <PageHeader title="Çalışan Başarıları" description="Ödüller, kıdem rozetleri ve çalışma arkadaşlarınızdan takdirler." actions={<button className="btn-primary" onClick={() => setOpen(true)}><Award className="h-4 w-4" /> Takdir gönder</button>} />
      <QueryState query={q} empty={(d) => d.length === 0 && <div className="card"><EmptyState title="Henüz kayıt yok" description="İlk takdiri siz gönderin!" /></div>}>
        {(list) => (
          <div className="grid gap-4 md:grid-cols-2">
            {list.map((a) => {
              const m = kindMeta[a.kind]
              return (
                <div key={a.id} className="card p-5">
                  <div className="flex items-start gap-3">
                    <Avatar name={a.user.fullName} size={44} />
                    <div className="min-w-0 flex-1"><div className="font-semibold text-ink-900 dark:text-white">{a.user.fullName}</div><div className="text-xs muted">{a.userTitle}</div></div>
                    <Badge tone={m.tone}><m.icon className="h-3 w-3" /> {m.label}</Badge>
                  </div>
                  <div className="mt-4 rounded-xl bg-ink-50 p-4 dark:bg-white/[0.03]"><div className="font-semibold text-ink-900 dark:text-white">{a.title}</div>{a.description && <p className="mt-1 text-sm muted">{a.description}</p>}</div>
                  <div className="mt-3 flex items-center justify-between text-xs muted">
                    <span>{a.givenBy.fullName} · {fmt.relative(a.createdAt)}</span>
                    <button className="btn-ghost btn-sm" disabled={liked.includes(a.id)} onClick={() => like.mutate(a.id)}><Heart className={liked.includes(a.id) ? 'h-3.5 w-3.5 fill-brand-500 text-brand-500' : 'h-3.5 w-3.5'} /> {a.likes}</button>
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </QueryState>
      <Modal open={open} onClose={() => setOpen(false)} title="Takdir gönder" footer={<><button className="btn-secondary" onClick={() => setOpen(false)}>Vazgeç</button><button className="btn-primary" disabled={!who || !v.title.trim() || give.isPending} onClick={() => give.mutate()}>{give.isPending && <Spinner />} Gönder</button></>}>
        <div className="space-y-4">
          <Field label="Kime?" required><UserPicker value={who} onChange={setWho} /></Field>
          {can('content.manage') && <Field label="Tür"><select className="input" value={v.kind} onChange={(e) => setV({ ...v, kind: e.target.value as AchievementKind })}><option value="Kudos">Takdir</option><option value="Award">Ödül</option><option value="Milestone">Kıdem</option></select></Field>}
          <Field label="Başlık" required><input className="input" value={v.title} onChange={(e) => setV({ ...v, title: e.target.value })} placeholder="ör. Kampanya haftasındaki desteğin için teşekkürler!" /></Field>
          <Field label="Mesaj"><textarea className="input" rows={3} value={v.description} onChange={(e) => setV({ ...v, description: e.target.value })} /></Field>
        </div>
      </Modal>
    </div>
  )
}
