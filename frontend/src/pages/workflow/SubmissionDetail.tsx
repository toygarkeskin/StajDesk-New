import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Ban, Check, CheckCheck, Circle, Clock, MessageSquare, SkipForward, X } from 'lucide-react'
import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { Avatar, Badge, Card, DefinitionList, ErrorState, FileList, Skeleton, Spinner } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api, errorMessage } from '@/lib/api'
import { fmt, submissionMeta } from '@/lib/format'
import { DynamicIcon } from '@/lib/icon'
import type { SubmissionDetail as S } from '@/lib/types'
import { cn, tint } from '@/lib/utils'

const actionText: Record<string, string> = { Submitted: 'başvuruyu oluşturdu', Approved: 'onayladı', Rejected: 'reddetti', Commented: 'açıklama ekledi', Completed: 'tamamladı', Cancelled: 'iptal etti', Skipped: 'Adım otomatik geçildi' }

export default function SubmissionDetail() {
  const { id } = useParams()
  const nav = useNavigate()
  const qc = useQueryClient()
  const confirm = useConfirm()
  const q = useQuery({ queryKey: ['submission', id], queryFn: () => api.get<S>(`/submissions/${id}`) })
  const [comment, setComment] = useState('')
  const act = useMutation({
    mutationFn: ({ action, comment }: { action: 'approve' | 'reject' | 'complete' | 'cancel' | 'comments'; comment?: string }) => api.send<S>('post', `/submissions/${id}/${action}`, action === 'cancel' ? undefined : { comment }),
    onSuccess: ({ data, message }) => {
      qc.setQueryData(['submission', id], data)
      qc.invalidateQueries({ queryKey: ['submissions'] })
      qc.invalidateQueries({ queryKey: ['approval-count'] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
      setComment('')
      toast.success(message ?? 'İşlem tamamlandı.')
    },
    onError: (e) => toast.error(errorMessage(e)),
  })

  if (q.isLoading) return <div className="grid gap-6 lg:grid-cols-3"><Skeleton className="h-96 lg:col-span-2" /><Skeleton className="h-96" /></div>
  if (q.error || !q.data) return <ErrorState error={q.error} onRetry={q.refetch} />
  const s = q.data

  const approve = async () => {
    const c = await confirm({ title: 'Başvuruyu onayla', message: `${s.number} numaralı ${s.form.name} başvurusunu onaylıyorsunuz.`, confirmText: 'Onayla', input: { label: 'Açıklama (isteğe bağlı)' } })
    if (c) act.mutate({ action: 'approve', comment: c === 'ok' ? undefined : c })
  }
  const reject = async () => {
    const c = await confirm({ title: 'Başvuruyu reddet', message: 'Ret gerekçesi başvuru sahibine iletilecektir.', confirmText: 'Reddet', danger: true, input: { label: 'Ret gerekçesi', required: true } })
    if (c) act.mutate({ action: 'reject', comment: c })
  }

  return (
    <div>
      <button onClick={() => (window.history.length > 1 ? nav(-1) : nav('/requests'))} className="mb-3 inline-flex items-center gap-1.5 text-sm muted hover:text-ink-900 dark:hover:text-white"><ArrowLeft className="h-4 w-4" /> Geri</button>
      <div className="mb-6 flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex items-center gap-4">
          <span className="grid h-12 w-12 place-items-center rounded-2xl" style={{ background: tint(s.form.color, 0.12), color: s.form.color }}><DynamicIcon name={s.form.icon} className="h-6 w-6" /></span>
          <div>
            <div className="flex flex-wrap items-center gap-2"><span className="text-sm font-semibold muted">{s.number}</span><Badge tone={submissionMeta[s.status].tone} dot>{submissionMeta[s.status].label}</Badge></div>
            <h1 className="text-2xl font-bold">{s.form.name}</h1>
            <p className="text-sm muted">{s.summary}</p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          {s.canApprove && <><button className="btn-success" disabled={act.isPending} onClick={approve}><Check className="h-4 w-4" /> Onayla</button><button className="btn-danger" disabled={act.isPending} onClick={reject}><X className="h-4 w-4" /> Reddet</button></>}
          {s.canComplete && <button className="btn-primary" disabled={act.isPending} onClick={async () => { const c = await confirm({ title: 'Başvuruyu tamamla', message: 'İşlemin yapıldığını onaylıyor musunuz? Başvuru sahibine bildirim gönderilir.', confirmText: 'Tamamlandı', input: { label: 'Açıklama (isteğe bağlı)' } }); if (c) act.mutate({ action: 'complete', comment: c === 'ok' ? undefined : c }) }}><CheckCheck className="h-4 w-4" /> Tamamlandı olarak işaretle</button>}
          {s.canCancel && <button className="btn-secondary" disabled={act.isPending} onClick={async () => (await confirm({ title: 'Başvuruyu iptal et', message: 'Başvurunuz iptal edilecek ve onaylayıcılara bildirilecek.', confirmText: 'İptal et', danger: true })) && act.mutate({ action: 'cancel' })}><Ban className="h-4 w-4" /> İptal et</button>}
        </div>
      </div>

      {/* Onay adımları */}
      <div className="card mb-6 overflow-x-auto p-5">
        <ol className="flex min-w-max items-start gap-0">
          <li className="flex items-start">
            <div className="flex w-40 flex-col items-center text-center"><span className="grid h-8 w-8 place-items-center rounded-full bg-emerald-500 text-white"><Check className="h-4 w-4" /></span><span className="mt-2 text-xs font-semibold text-ink-900 dark:text-white">Başvuru</span><span className="text-[11px] muted">{fmt.smart(s.createdAt)}</span></div>
          </li>
          {s.steps.map((st) => (
            <li key={st.index} className="flex items-start">
              <div className={cn('mt-4 h-0.5 w-12', st.state === 'approved' || st.state === 'skipped' ? 'bg-emerald-400' : 'bg-ink-200 dark:bg-white/10')} />
              <div className="flex w-40 flex-col items-center text-center">
                <span className={cn('grid h-8 w-8 place-items-center rounded-full',
                  st.state === 'approved' && 'bg-emerald-500 text-white', st.state === 'rejected' && 'bg-red-500 text-white', st.state === 'skipped' && 'bg-ink-300 text-white dark:bg-ink-600',
                  st.state === 'current' && 'bg-amber-500 text-white ring-4 ring-amber-500/20', (st.state === 'waiting' || st.state === 'cancelled') && 'bg-ink-100 text-ink-400 dark:bg-white/10')}>
                  {st.state === 'approved' ? <Check className="h-4 w-4" /> : st.state === 'rejected' ? <X className="h-4 w-4" /> : st.state === 'skipped' ? <SkipForward className="h-4 w-4" /> : st.state === 'current' ? <Clock className="h-4 w-4" /> : <Circle className="h-3 w-3" />}
                </span>
                <span className="mt-2 text-xs font-semibold text-ink-900 dark:text-white">{st.name}</span>
                <span className="line-clamp-2 text-[11px] muted">{st.approvers.map((a) => a.fullName).join(', ') || (st.state === 'skipped' ? 'Otomatik geçildi' : '—')}</span>
              </div>
            </li>
          ))}
          {(s.status === 'Approved' || s.status === 'Completed') && (
            <li className="flex items-start">
              <div className={cn('mt-4 h-0.5 w-12', s.status === 'Completed' ? 'bg-emerald-400' : 'bg-ink-200 dark:bg-white/10')} />
              <div className="flex w-40 flex-col items-center text-center"><span className={cn('grid h-8 w-8 place-items-center rounded-full', s.status === 'Completed' ? 'bg-emerald-500 text-white' : 'bg-sky-500 text-white')}><CheckCheck className="h-4 w-4" /></span><span className="mt-2 text-xs font-semibold text-ink-900 dark:text-white">{s.status === 'Completed' ? 'Tamamlandı' : 'Uygulamada'}</span><span className="text-[11px] muted">{s.assignee?.fullName ?? ''}</span></div>
            </li>
          )}
        </ol>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <Card title="Başvuru bilgileri">
            <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2">
              {s.fields.map((f) => (
                <div key={f.key} className={cn(f.type === 'textarea' && 'sm:col-span-2')}>
                  <dt className="text-xs muted">{f.label}</dt>
                  <dd className="mt-0.5 whitespace-pre-wrap text-sm font-medium text-ink-900 dark:text-white">{f.display}</dd>
                </div>
              ))}
            </dl>
            {s.files.length > 0 && <div className="mt-5 border-t border-ink-100 pt-4 dark:border-white/[0.06]"><div className="mb-2 text-xs font-semibold muted">Ekler</div><FileList files={s.files} /></div>}
          </Card>
          <Card title="İşlem geçmişi">
            <ol className="space-y-4">
              {s.actions.map((a) => (
                <li key={a.id} className="flex gap-3">
                  {a.actor ? <Avatar name={a.actor.fullName} size={30} /> : <span className="grid h-[30px] w-[30px] place-items-center rounded-full bg-ink-100 dark:bg-white/10"><SkipForward className="h-3.5 w-3.5 text-ink-400" /></span>}
                  <div className="min-w-0 flex-1">
                    <div className="text-sm"><b className="text-ink-900 dark:text-white">{a.actor?.fullName ?? 'Sistem'}</b> <span className="muted">{actionText[a.action]}</span>{a.stepName && <span className="muted"> · {a.stepName}</span>}</div>
                    {a.comment && <p className="mt-1 rounded-lg bg-ink-50 px-3 py-2 text-sm dark:bg-white/[0.03]">{a.comment}</p>}
                    <div className="mt-0.5 text-[11px] text-ink-400">{fmt.dateTime(a.createdAt)}</div>
                  </div>
                </li>
              ))}
            </ol>
            {s.status !== 'Cancelled' && (
              <div className="mt-5 flex gap-2 border-t border-ink-100 pt-4 dark:border-white/[0.06]">
                <input className="input" value={comment} onChange={(e) => setComment(e.target.value)} placeholder="Açıklama ekleyin…" aria-label="Açıklama" />
                <button className="btn-secondary" disabled={!comment.trim() || act.isPending} onClick={() => act.mutate({ action: 'comments', comment })}>{act.isPending ? <Spinner /> : <MessageSquare className="h-4 w-4" />} Ekle</button>
              </div>
            )}
          </Card>
        </div>
        <aside className="space-y-6">
          <Card title="Başvuran">
            <div className="flex items-center gap-3"><Avatar name={s.submitter.fullName} size={40} /><div><div className="font-semibold text-ink-900 dark:text-white">{s.submitter.fullName}</div><div className="text-xs muted">{s.submitterTitle}</div></div></div>
            <div className="mt-4"><DefinitionList items={[
              { label: 'Departman', value: s.department?.name }, { label: 'Modül', value: s.form.module }, { label: 'Sorumlu birim', value: s.form.ownerDepartment?.name },
              { label: 'Oluşturma', value: fmt.dateTime(s.createdAt) }, { label: 'Son güncelleme', value: fmt.relative(s.updatedAt) },
              ...(s.amount != null ? [{ label: 'Tutar', value: fmt.money(s.amount) }] : []), ...(s.completedAt ? [{ label: 'Tamamlanma', value: fmt.dateTime(s.completedAt) }] : []),
            ]} /></div>
          </Card>
        </aside>
      </div>
    </div>
  )
}
