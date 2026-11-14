import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, ClipboardList, EyeOff, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { Badge, Drawer, EmptyState, Field, PageHeader, QueryState, Spinner, Toggle } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { fmt } from '@/lib/format'
import type { SurveyListItem, SurveyQuestion } from '@/lib/types'
import { useCan } from '@/store/auth'

function SurveyBuilder({ open, onClose }: { open: boolean; onClose: () => void }) {
  const qc = useQueryClient()
  const [v, setV] = useState({ title: '', description: '', isAnonymous: true, isActive: true, endsAt: '' })
  const [qs, setQs] = useState<SurveyQuestion[]>([{ id: 'q1', text: '', type: 'single', options: ['', ''], required: true }])
  const save = useMutation({
    mutationFn: () => api.post('/surveys', { ...v, endsAt: v.endsAt || null, questions: qs.map((q) => ({ ...q, options: q.options.filter((o) => o.trim()) })) }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['surveys'] }); toast.success('Anket yayınlandı.'); onClose() },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const upd = (i: number, p: Partial<SurveyQuestion>) => setQs(qs.map((q, n) => (n === i ? { ...q, ...p } : q)))
  return (
    <Drawer open={open} onClose={onClose} title="Yeni anket" width="max-w-2xl" footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate()}>{save.isPending && <Spinner />} Yayınla</button></>}>
      <div className="space-y-4">
        <Field label="Başlık" required><input className="input" value={v.title} onChange={(e) => setV({ ...v, title: e.target.value })} /></Field>
        <Field label="Açıklama"><textarea className="input" rows={2} value={v.description} onChange={(e) => setV({ ...v, description: e.target.value })} /></Field>
        <div className="flex flex-wrap gap-6"><Toggle checked={v.isAnonymous} onChange={(x) => setV({ ...v, isAnonymous: x })} label="Anonim" /><Field label="Bitiş tarihi"><input type="date" className="input" value={v.endsAt} onChange={(e) => setV({ ...v, endsAt: e.target.value })} /></Field></div>
        {qs.map((q, i) => (
          <div key={i} className="rounded-xl border border-ink-200 p-4 dark:border-white/10">
            <div className="flex gap-2">
              <input className="input" placeholder={`${i + 1}. soru`} value={q.text} onChange={(e) => upd(i, { text: e.target.value })} />
              <select className="input w-40" value={q.type} onChange={(e) => upd(i, { type: e.target.value as SurveyQuestion['type'] })}><option value="single">Tek seçim</option><option value="multi">Çoklu seçim</option><option value="rating">1-5 puan</option><option value="text">Metin</option></select>
              <button className="btn-ghost btn-icon" onClick={() => setQs(qs.filter((_, n) => n !== i))} aria-label="Soruyu sil"><Trash2 className="h-4 w-4" /></button>
            </div>
            {(q.type === 'single' || q.type === 'multi') && (
              <div className="mt-3 space-y-2 pl-4">
                {q.options.map((o, oi) => <input key={oi} className="input h-8" placeholder={`Seçenek ${oi + 1}`} value={o} onChange={(e) => upd(i, { options: q.options.map((x, n) => (n === oi ? e.target.value : x)) })} />)}
                <button className="btn-ghost btn-sm" onClick={() => upd(i, { options: [...q.options, ''] })}><Plus className="h-3.5 w-3.5" /> Seçenek</button>
              </div>
            )}
          </div>
        ))}
        <button className="btn-secondary" onClick={() => setQs([...qs, { id: `q${qs.length + 1}`, text: '', type: 'single', options: ['', ''], required: true }])}><Plus className="h-4 w-4" /> Soru ekle</button>
      </div>
    </Drawer>
  )
}

export default function Surveys() {
  const { can } = useCan()
  const [builder, setBuilder] = useState(false)
  const q = useQuery({ queryKey: ['surveys'], queryFn: () => api.get<SurveyListItem[]>('/surveys') })
  return (
    <div className="max-w-4xl">
      <PageHeader title="Anketler" description="Görüşleriniz şirket kararlarına yön verir." actions={can('surveys.manage') && <button className="btn-primary" onClick={() => setBuilder(true)}><Plus className="h-4 w-4" /> Anket oluştur</button>} />
      <QueryState query={q} empty={(d) => d.length === 0 && <div className="card"><EmptyState icon={<ClipboardList className="h-5 w-5" />} title="Aktif anket yok" /></div>}>
        {(list) => (
          <div className="space-y-3">
            {list.map((s) => (
              <Link key={s.id} to={`/surveys/${s.id}`} className="card flex items-center gap-4 p-5 transition hover:shadow-pop">
                <span className="grid h-10 w-10 place-items-center rounded-xl bg-violet-50 text-violet-600 dark:bg-violet-400/10 dark:text-violet-300"><ClipboardList className="h-5 w-5" /></span>
                <span className="min-w-0 flex-1">
                  <span className="flex flex-wrap items-center gap-2"><span className="font-semibold text-ink-900 dark:text-white">{s.title}</span>{s.isAnonymous && <Badge><EyeOff className="h-3 w-3" /> Anonim</Badge>}{!s.isActive && <Badge>Kapandı</Badge>}</span>
                  <span className="block text-sm muted">{s.description}</span>
                  <span className="mt-1 block text-xs muted">{s.questionCount} soru · {s.responseCount} yanıt{s.endsAt && ` · Son gün ${fmt.date(s.endsAt)}`}</span>
                </span>
                {s.hasResponded ? <Badge tone="green"><CheckCircle2 className="h-3 w-3" /> Yanıtladınız</Badge> : s.isActive && <span className="btn-primary btn-sm">Katıl</span>}
              </Link>
            ))}
          </div>
        )}
      </QueryState>
      {builder && <SurveyBuilder open={builder} onClose={() => setBuilder(false)} />}
    </div>
  )
}
