import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlarmClock, ArrowLeft, CheckCircle2, Cpu, History, Lock, Paperclip, Pencil, Send, Sparkles, Trash2, Wand2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { Avatar, Badge, Card, ColorDot, DefinitionList, ErrorState, FileList, FileUploader, Skeleton, Spinner, Toggle } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api, errorMessage } from '@/lib/api'
import { fmt, historyFieldLabel, priorityMeta, statusMeta } from '@/lib/format'
import { useCategories, useGroups } from '@/lib/queries'
import type { FileDto, TicketDetail as T, TicketPriority, TicketStatus } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useCan } from '@/store/auth'

export default function TicketDetail() {
  const { id } = useParams()
  const nav = useNavigate()
  const qc = useQueryClient()
  const confirm = useConfirm()
  const { can, user } = useCan()
  const q = useQuery({ queryKey: ['ticket', id], queryFn: () => api.get<T>(`/tickets/${id}`) })
  const groups = useGroups()
  const categories = useCategories()
  const [reply, setReply] = useState('')
  const [internal, setInternal] = useState(false)
  const [files, setFiles] = useState<FileDto[]>([])
  const [editing, setEditing] = useState(false)

  const refresh = (t?: T) => {
    if (t) qc.setQueryData(['ticket', id], t)
    else qc.invalidateQueries({ queryKey: ['ticket', id] })
    qc.invalidateQueries({ queryKey: ['tickets'] })
    qc.invalidateQueries({ queryKey: ['dashboard'] })
  }
  const status = useMutation({
    mutationFn: (v: { status: TicketStatus; note?: string }) => api.patch<T>(`/tickets/${id}/status`, v),
    onSuccess: (t) => { refresh(t); toast.success('Talep durumu güncellendi.') },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const assign = useMutation({
    mutationFn: (v: { groupId?: number | null; assigneeId?: string | null }) => api.patch<T>(`/tickets/${id}/assign`, v),
    onSuccess: (t) => { refresh(t); toast.success('Atama güncellendi.') },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const comment = useMutation({
    mutationFn: async () => {
      if (files.length) await api.post(`/tickets/${id}/attachments`, files.map((f) => f.id))
      if (reply.trim()) await api.post(`/tickets/${id}/comments`, { body: reply, isInternal: internal })
    },
    onSuccess: () => { setReply(''); setFiles([]); setInternal(false); refresh(); toast.success('Yanıtınız eklendi.') },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const update = useMutation({
    mutationFn: (v: { title: string; description: string; categoryId: number; priority: TicketPriority; type: string; assetId?: number | null }) => api.put<T>(`/tickets/${id}`, v),
    onSuccess: (t) => { refresh(t); setEditing(false); toast.success('Talep güncellendi.') },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const del = useMutation({
    mutationFn: () => api.del(`/tickets/${id}`),
    onSuccess: () => { toast.success('Talep silindi.'); qc.invalidateQueries({ queryKey: ['tickets'] }); nav('/tickets') },
  })

  const timeline = useMemo(() => {
    if (!q.data) return []
    return [
      ...q.data.comments.map((c) => ({ kind: 'comment' as const, at: c.createdAt, c })),
      ...q.data.history.filter((h) => h.field !== 'created').map((h) => ({ kind: 'history' as const, at: h.createdAt, h })),
    ].sort((a, b) => a.at.localeCompare(b.at))
  }, [q.data])

  if (q.isLoading) return <div className="grid gap-6 lg:grid-cols-3"><Skeleton className="h-96 lg:col-span-2" /><Skeleton className="h-96" /></div>
  if (q.error || !q.data) return <ErrorState error={q.error} onRetry={q.refetch} />
  const t = q.data
  const isRequester = t.requester.id === user?.id
  const changeStatus = async (s: TicketStatus) => {
    if (s === 'Resolved' && t.canManage) {
      const note = await confirm({ title: 'Talebi çözüldü olarak işaretle', message: 'Kullanıcıya iletilecek çözüm açıklamasını yazınız.', confirmText: 'Çözüldü', input: { label: 'Çözüm açıklaması', required: true } })
      if (note) status.mutate({ status: s, note })
      return
    }
    if (s === 'Closed' && !isRequester) {
      if (!(await confirm({ title: 'Talebi kapat', message: 'Kapatılan talebe yorum eklenemez ve yeniden açılamaz.', confirmText: 'Kapat' }))) return
    }
    status.mutate({ status: s })
  }
  const statusButtonLabel: Record<TicketStatus, string> = { Open: 'Açık yap', InProgress: isRequester && t.status === 'Resolved' ? 'Sorun devam ediyor' : 'İşleme al', Pending: 'Beklemeye al', Resolved: 'Çözüldü', Closed: isRequester ? 'Onayla ve kapat' : 'Kapat' }

  return (
    <div>
      <Link to="/tickets" className="mb-3 inline-flex items-center gap-1.5 text-sm muted hover:text-ink-900 dark:hover:text-white"><ArrowLeft className="h-4 w-4" /> Talepler</Link>
      <div className="mb-6 flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-sm font-semibold muted">#{t.id}</span>
            <Badge tone={statusMeta[t.status].tone} dot>{statusMeta[t.status].label}</Badge>
            <Badge tone={priorityMeta[t.priority].tone}>{priorityMeta[t.priority].label}</Badge>
            <Badge>{t.type === 'Incident' ? 'Arıza' : 'Hizmet talebi'}</Badge>
            {t.isOverdue && <Badge tone="red">SLA aşıldı</Badge>}
          </div>
          <h1 className="mt-2 text-2xl font-bold">{t.title}</h1>
          <p className="mt-1 text-sm muted">{t.requester.fullName} tarafından {fmt.dateTime(t.createdAt)} tarihinde açıldı</p>
        </div>
        <div className="flex flex-wrap gap-2">
          {t.allowedTransitions.map((s) => (
            <button key={s} disabled={status.isPending} onClick={() => changeStatus(s)}
              className={s === 'Resolved' || (s === 'Closed' && isRequester) ? 'btn-success' : s === 'InProgress' && !isRequester ? 'btn-primary' : 'btn-secondary'}>
              {s === 'Resolved' && <CheckCircle2 className="h-4 w-4" />}{statusButtonLabel[s]}
            </button>
          ))}
          {t.canEdit && <button className="btn-secondary" onClick={() => setEditing((v) => !v)}><Pencil className="h-4 w-4" /> Düzenle</button>}
          {can('tickets.delete') && (
            <button className="btn-ghost text-red-600" onClick={async () => (await confirm({ title: `#${t.id} talebini sil`, message: 'Talep, yorumları ve geçmişiyle birlikte kalıcı olarak silinecek.', confirmText: 'Kalıcı olarak sil', danger: true })) && del.mutate()} aria-label="Talebi sil"><Trash2 className="h-4 w-4" /></button>
          )}
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          {editing ? (
            <EditTicket t={t} categories={categories.data ?? []} saving={update.isPending} onCancel={() => setEditing(false)} onSave={(v) => update.mutate(v)} />
          ) : (
            <Card>
              <div className="flex items-center gap-3"><Avatar name={t.requester.fullName} size={36} /><div><div className="font-semibold text-ink-900 dark:text-white">{t.requester.fullName}</div><div className="text-xs muted">{t.requesterTitle} · {t.department?.name}</div></div></div>
              <p className="mt-4 whitespace-pre-wrap leading-7">{t.description}</p>
              {t.attachments.length > 0 && <div className="mt-4 border-t border-ink-100 pt-4 dark:border-white/[0.06]"><div className="mb-2 text-xs font-semibold muted">Ekler</div><FileList files={t.attachments} /></div>}
            </Card>
          )}

          <section aria-label="Talep geçmişi">
            <h2 className="mb-3 flex items-center gap-2 text-base font-semibold"><History className="h-4 w-4 text-ink-400" /> Etkinlik</h2>
            <ol className="relative space-y-4 before:absolute before:top-2 before:bottom-2 before:left-[15px] before:w-px before:bg-ink-200 dark:before:bg-white/10">
              {timeline.map((item) => item.kind === 'comment' ? (
                <li key={`c${item.c.id}`} className="relative flex gap-3">
                  <Avatar name={item.c.author.fullName} size={32} />
                  <div className={cn('card min-w-0 flex-1 p-4', item.c.isInternal && 'border-amber-300/70 bg-amber-50/60 dark:border-amber-400/20 dark:bg-amber-400/[0.05]')}>
                    <div className="flex flex-wrap items-center gap-2 text-xs">
                      <span className="font-semibold text-ink-900 dark:text-white">{item.c.author.fullName}</span>
                      {item.c.authorIsStaff && <Badge tone="brand">Destek</Badge>}
                      {item.c.isInternal && <Badge tone="amber"><Lock className="h-3 w-3" /> İç not</Badge>}
                      <span className="muted">{fmt.smart(item.c.createdAt)}</span>
                    </div>
                    <p className="mt-2 whitespace-pre-wrap text-sm leading-6">{item.c.body}</p>
                  </div>
                </li>
              ) : (
                <li key={`h${item.h.id}`} className="relative flex items-center gap-3 pl-1 text-xs">
                  <span className="relative z-10 grid h-6 w-6 place-items-center rounded-full bg-white ring-1 ring-ink-200 dark:bg-ink-900 dark:ring-white/10">
                    {item.h.field === 'rule' ? <Wand2 className="h-3 w-3 text-violet-500" /> : <Sparkles className="h-3 w-3 text-ink-400" />}
                  </span>
                  <span className="muted">
                    <b className="text-ink-700 dark:text-ink-200">{item.h.actor?.fullName ?? 'Sistem'}</b> · {historyFieldLabel[item.h.field] ?? item.h.field}
                    {item.h.oldValue && <> <s>{item.h.oldValue}</s> →</>} {item.h.newValue && <b className="text-ink-700 dark:text-ink-200">{item.h.newValue}</b>} · {fmt.smart(item.h.createdAt)}
                  </span>
                </li>
              ))}
              {timeline.length === 0 && <li className="pl-10 text-sm muted">Henüz yanıt yok.</li>}
            </ol>
          </section>

          {t.status !== 'Closed' ? (
            <Card title="Yanıt yaz">
              <textarea className="input" rows={4} value={reply} onChange={(e) => setReply(e.target.value)} placeholder={internal ? 'Yalnızca destek ekibinin göreceği iç not…' : 'Mesajınızı yazınız…'} aria-label="Yanıt" />
              <div className="mt-3"><FileUploader value={files} onChange={setFiles} multiple /></div>
              <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
                {can('tickets.manage') ? <Toggle checked={internal} onChange={setInternal} label={<span className="inline-flex items-center gap-1.5"><Lock className="h-3.5 w-3.5" /> İç not (kullanıcı görmez)</span>} /> : <span />}
                <button className="btn-primary" disabled={(!reply.trim() && files.length === 0) || comment.isPending} onClick={() => comment.mutate()}>{comment.isPending ? <Spinner /> : <Send className="h-4 w-4" />} Gönder</button>
              </div>
            </Card>
          ) : (
            <div className="card p-4 text-center text-sm muted">Bu talep kapatılmıştır. Yeni bir sorun için lütfen yeni talep açınız.</div>
          )}
        </div>

        <aside className="space-y-6">
          <Card title="Atama">
            {t.canManage ? (
              <div className="space-y-3">
                <label className="block"><span className="label">Destek grubu</span>
                  <select className="input" value={t.groupId ?? ''} disabled={assign.isPending} onChange={(e) => assign.mutate({ groupId: e.target.value ? Number(e.target.value) : null, assigneeId: t.assignee?.id ?? null })}>
                    <option value="">Grup yok</option>{groups.data?.filter((g) => g.isSupportGroup).map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
                  </select>
                </label>
                <label className="block"><span className="label">Atanan personel</span>
                  <select className="input" value={t.assignee?.id ?? ''} disabled={assign.isPending} onChange={(e) => assign.mutate({ groupId: t.groupId ?? null, assigneeId: e.target.value || null })}>
                    <option value="">Atanmamış</option>
                    {/* Atanan kişi seçili grubun üyesi değilse de (ör. "Üzerime al") listede görünmeli; aksi halde kutu yanlışlıkla "Atanmamış" gösterir */}
                    {[...(t.assignee ? [{ userId: t.assignee.id, fullName: t.assignee.fullName }] : []),
                      ...(groups.data?.find((g) => g.id === t.groupId)?.members ?? groups.data?.filter((g) => g.isSupportGroup).flatMap((g) => g.members) ?? [])]
                      .filter((m, i, a) => a.findIndex((x) => x.userId === m.userId) === i)
                      .map((m) => <option key={m.userId} value={m.userId}>{m.fullName}</option>)}
                  </select>
                </label>
                {t.assignee?.id !== user?.id && <button className="btn-secondary w-full" onClick={() => assign.mutate({ groupId: t.groupId ?? null, assigneeId: user!.id })}>Üzerime al</button>}
              </div>
            ) : (
              <DefinitionList items={[{ label: 'Grup', value: t.groupName ?? '—' }, { label: 'Atanan', value: t.assignee?.fullName ?? 'Henüz atanmadı' }]} />
            )}
          </Card>
          <Card title="Detaylar">
            <DefinitionList items={[
              { label: 'Kategori', value: <ColorDot color={t.categoryColor} label={t.categoryName} /> },
              { label: 'Departman', value: t.department?.name },
              { label: 'SLA hedefi', value: <span className={cn('inline-flex items-center gap-1', t.isOverdue && 'text-red-600 dark:text-red-400')}><AlarmClock className="h-3.5 w-3.5" /> {fmt.dateTime(t.dueAt)}</span> },
              { label: 'Çözüm', value: t.resolvedAt ? fmt.dateTime(t.resolvedAt) : '—' },
              { label: 'Son güncelleme', value: fmt.relative(t.updatedAt) },
              ...(t.appliedRule ? [{ label: 'Yönlendirme', value: <span className="inline-flex items-center gap-1 text-violet-600 dark:text-violet-300"><Wand2 className="h-3.5 w-3.5" /> {t.appliedRule}</span> }] : []),
              ...(t.requesterPhone ? [{ label: 'Telefon', value: <a href={`tel:${t.requesterPhone.replace(/\s/g, '')}`}>{t.requesterPhone}</a> }] : []),
            ]} />
          </Card>
          {t.asset && (
            <Card title="İlgili cihaz">
              <Link to={`/inventory/${t.asset.id}`} className="flex items-center gap-3 rounded-lg hover:text-brand-600">
                <span className="grid h-9 w-9 place-items-center rounded-lg bg-ink-100 dark:bg-white/[0.06]"><Cpu className="h-4 w-4" /></span>
                <span><span className="block font-medium">{t.asset.name}</span><span className="block text-xs muted">{t.asset.assetTag}</span></span>
              </Link>
            </Card>
          )}
          {t.attachments.length > 0 && <Card title={<span className="inline-flex items-center gap-2"><Paperclip className="h-4 w-4" /> Ekler ({t.attachments.length})</span>}><FileList files={t.attachments} /></Card>}
        </aside>
      </div>
    </div>
  )
}

function EditTicket({ t, categories, saving, onCancel, onSave }: { t: T; categories: { id: number; name: string }[]; saving: boolean; onCancel: () => void; onSave: (v: { title: string; description: string; categoryId: number; priority: TicketPriority; type: string; assetId?: number | null }) => void }) {
  const [v, setV] = useState({ title: t.title, description: t.description, categoryId: t.categoryId, priority: t.priority, type: t.type })
  return (
    <Card title="Talebi düzenle" footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={onCancel}>Vazgeç</button><button className="btn-primary" disabled={saving} onClick={() => onSave({ ...v, assetId: t.asset?.id ?? null })}>{saving && <Spinner />} Kaydet</button></div>}>
      <div className="space-y-3">
        <label className="block"><span className="label">Başlık</span><input className="input" value={v.title} onChange={(e) => setV({ ...v, title: e.target.value })} /></label>
        <div className="grid gap-3 sm:grid-cols-3">
          <label className="block"><span className="label">Kategori</span><select className="input" value={v.categoryId} onChange={(e) => setV({ ...v, categoryId: Number(e.target.value) })}>{categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
          <label className="block"><span className="label">Öncelik</span><select className="input" value={v.priority} onChange={(e) => setV({ ...v, priority: e.target.value as TicketPriority })}>{(['Low', 'Medium', 'High', 'Critical'] as const).map((p) => <option key={p} value={p}>{priorityMeta[p].label}</option>)}</select></label>
          <label className="block"><span className="label">Tür</span><select className="input" value={v.type} onChange={(e) => setV({ ...v, type: e.target.value as T['type'] })}><option value="Incident">Arıza</option><option value="Request">Hizmet talebi</option></select></label>
        </div>
        <label className="block"><span className="label">Açıklama</span><textarea className="input" rows={6} value={v.description} onChange={(e) => setV({ ...v, description: e.target.value })} /></label>
      </div>
    </Card>
  )
}
