import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, CheckCircle2, Send, Star } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { BarList } from '@/components/charts'
import { Card, ErrorState, Skeleton, Spinner, Tabs } from '@/components/ui'
import { api, ApiError, errorMessage } from '@/lib/api'
import type { Survey, SurveyResults } from '@/lib/types'
import { cn } from '@/lib/utils'

export default function SurveyPage() {
  const { id } = useParams()
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['survey', id], queryFn: () => api.get<Survey>(`/surveys/${id}`) })
  const [tab, setTab] = useState<'answer' | 'results'>('answer')
  const results = useQuery({ queryKey: ['survey-results', id], queryFn: () => api.get<SurveyResults>(`/surveys/${id}/results`), enabled: tab === 'results' })
  const [answers, setAnswers] = useState<Record<string, unknown>>({})
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const submit = useMutation({
    mutationFn: () => api.post(`/surveys/${id}/responses`, { answers }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['survey', id] }); qc.invalidateQueries({ queryKey: ['surveys'] }); toast.success('Yanıtınız kaydedildi. Teşekkür ederiz!') },
    onError: (e) => { if (e instanceof ApiError && e.errors) setErrors(e.errors); toast.error(errorMessage(e)) },
  })
  if (q.isLoading) return <Skeleton className="mx-auto h-96 max-w-3xl" />
  if (q.error || !q.data) return <ErrorState error={q.error} onRetry={q.refetch} />
  const s = q.data
  return (
    <div className="mx-auto max-w-3xl">
      <Link to="/surveys" className="mb-3 inline-flex items-center gap-1.5 text-sm muted hover:text-ink-900 dark:hover:text-white"><ArrowLeft className="h-4 w-4" /> Anketler</Link>
      <h1 className="text-2xl font-bold">{s.title}</h1>
      <p className="mt-1 muted">{s.description}{s.isAnonymous && ' · Yanıtlarınız anonim olarak kaydedilir.'}</p>
      {s.canManage && <Tabs className="mt-4" value={tab} onChange={setTab} tabs={[{ value: 'answer', label: 'Anket' }, { value: 'results', label: 'Sonuçlar' }]} />}
      {tab === 'results' ? (
        <div className="mt-5 space-y-4">
          {results.data && <p className="text-sm muted">{results.data.responseCount} yanıt</p>}
          {results.data?.questions.map((r) => (
            <Card key={r.id} title={r.text}>
              {r.type === 'text' ? <ul className="space-y-2 text-sm">{r.textAnswers.map((t, i) => <li key={i} className="rounded-lg bg-ink-50 px-3 py-2 dark:bg-white/[0.03]">{t}</li>)}{r.textAnswers.length === 0 && <li className="muted">Yanıt yok</li>}</ul>
                : <>{r.average != null && <div className="mb-3 font-display text-2xl font-bold">{r.average} <span className="text-sm font-normal muted">/ 5 ortalama</span></div>}<BarList data={Object.entries(r.counts).map(([k, v]) => ({ key: k, label: r.type === 'rating' ? `${k} puan` : k, value: v }))} /></>}
            </Card>
          ))}
        </div>
      ) : s.hasResponded ? (
        <div className="card mt-6 p-8 text-center"><CheckCircle2 className="mx-auto h-10 w-10 text-emerald-500" /><h2 className="mt-3 text-lg font-semibold">Bu ankete yanıt verdiniz</h2><p className="mt-1 text-sm muted">Katılımınız için teşekkür ederiz.</p></div>
      ) : !s.isActive ? (
        <div className="card mt-6 p-8 text-center muted">Bu anket yanıta kapanmıştır.</div>
      ) : (
        <div className="mt-6 space-y-4">
          {s.questions.map((qq, i) => (
            <Card key={qq.id} title={<span>{i + 1}. {qq.text}{qq.required && <span className="text-brand-600"> *</span>}</span>}>
              {qq.type === 'rating' && (
                <div className="flex gap-2" role="radiogroup">{[1, 2, 3, 4, 5].map((n) => (
                  <button key={n} type="button" aria-label={`${n} puan`} onClick={() => setAnswers({ ...answers, [qq.id]: n })} className={cn('grid h-11 w-11 place-items-center rounded-xl border transition', (answers[qq.id] as number) >= n ? 'border-amber-400 bg-amber-50 text-amber-500 dark:bg-amber-400/10' : 'border-ink-200 text-ink-300 dark:border-white/10')}><Star className={cn('h-5 w-5', (answers[qq.id] as number) >= n && 'fill-amber-400')} /></button>
                ))}</div>
              )}
              {qq.type === 'single' && <div className="space-y-2">{qq.options.map((o) => <label key={o} className="flex cursor-pointer items-center gap-2.5 text-sm"><input type="radio" name={qq.id} className="accent-brand-600" checked={answers[qq.id] === o} onChange={() => setAnswers({ ...answers, [qq.id]: o })} /> {o}</label>)}</div>}
              {qq.type === 'multi' && <div className="space-y-2">{qq.options.map((o) => { const sel = (answers[qq.id] as string[] | undefined) ?? []; return <label key={o} className="flex cursor-pointer items-center gap-2.5 text-sm"><input type="checkbox" className="accent-brand-600" checked={sel.includes(o)} onChange={() => setAnswers({ ...answers, [qq.id]: sel.includes(o) ? sel.filter((x) => x !== o) : [...sel, o] })} /> {o}</label> })}</div>}
              {qq.type === 'text' && <textarea className="input" rows={3} value={(answers[qq.id] as string) ?? ''} onChange={(e) => setAnswers({ ...answers, [qq.id]: e.target.value })} />}
              {errors[qq.id] && <p className="error-text">{errors[qq.id][0]}</p>}
            </Card>
          ))}
          <div className="flex justify-end"><button className="btn-primary" disabled={submit.isPending} onClick={() => { setErrors({}); submit.mutate() }}>{submit.isPending ? <Spinner /> : <Send className="h-4 w-4" />} Yanıtları gönder</button></div>
        </div>
      )}
    </div>
  )
}
