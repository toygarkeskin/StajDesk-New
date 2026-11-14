import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CalendarDays, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Avatar, Badge, Drawer, Field, PageHeader, QueryState, Segmented, Spinner, UserPicker } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api, errorMessage } from '@/lib/api'
import { fmt, priorityMeta, taskStatusMeta, toInputDate } from '@/lib/format'
import type { Paged, Project, TicketPriority, WorkTask, WorkTaskStatus } from '@/lib/types'
import { cn } from '@/lib/utils'

const cols: WorkTaskStatus[] = ['Todo', 'InProgress', 'Review', 'Done']

function TaskDrawer({ task, open, onClose }: { task?: WorkTask | null; open: boolean; onClose: () => void }) {
  const qc = useQueryClient()
  const confirm = useConfirm()
  const projects = useQuery({ queryKey: ['projects'], queryFn: () => api.get<Project[]>('/projects') })
  const [v, setV] = useState({ title: task?.title ?? '', description: task?.description ?? '', projectId: task?.projectId?.toString() ?? '', status: task?.status ?? 'Todo' as WorkTaskStatus, priority: task?.priority ?? 'Medium' as TicketPriority, dueDate: toInputDate(task?.dueDate) })
  const [assignee, setAssignee] = useState(task?.assignee ? { id: task.assignee.id, fullName: task.assignee.fullName } : null)
  const save = useMutation({
    mutationFn: () => { const body = { ...v, projectId: v.projectId ? Number(v.projectId) : null, dueDate: v.dueDate || null, assigneeId: assignee?.id ?? null }; return task ? api.put(`/tasks/${task.id}`, body) : api.post('/tasks', body) },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['tasks'] }); qc.invalidateQueries({ queryKey: ['projects'] }); toast.success('Görev kaydedildi.'); onClose() },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const del = useMutation({ mutationFn: () => api.del(`/tasks/${task!.id}`), onSuccess: () => { qc.invalidateQueries({ queryKey: ['tasks'] }); onClose() }, onError: (e) => toast.error(errorMessage(e)) })
  return (
    <Drawer open={open} onClose={onClose} title={task ? 'Görevi düzenle' : 'Yeni görev'}
      footer={<>{task && <button className="btn-ghost mr-auto text-red-600" onClick={async () => (await confirm({ title: 'Görevi sil', message: task.title, danger: true, confirmText: 'Sil' })) && del.mutate()}><Trash2 className="h-4 w-4" /> Sil</button>}<button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" disabled={!v.title.trim() || save.isPending} onClick={() => save.mutate()}>{save.isPending && <Spinner />} Kaydet</button></>}>
      <div className="space-y-4">
        <Field label="Başlık" required><input className="input" value={v.title} onChange={(e) => setV({ ...v, title: e.target.value })} /></Field>
        <Field label="Açıklama"><textarea className="input" rows={4} value={v.description} onChange={(e) => setV({ ...v, description: e.target.value })} /></Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Proje"><select className="input" value={v.projectId} onChange={(e) => setV({ ...v, projectId: e.target.value })}><option value="">Kişisel görev</option>{projects.data?.map((p) => <option key={p.id} value={p.id}>{p.code} · {p.name}</option>)}</select></Field>
          <Field label="Sorumlu"><UserPicker value={assignee} onChange={setAssignee} /></Field>
          <Field label="Durum"><select className="input" value={v.status} onChange={(e) => setV({ ...v, status: e.target.value as WorkTaskStatus })}>{cols.map((c) => <option key={c} value={c}>{taskStatusMeta[c].label}</option>)}</select></Field>
          <Field label="Öncelik"><select className="input" value={v.priority} onChange={(e) => setV({ ...v, priority: e.target.value as TicketPriority })}>{(['Low', 'Medium', 'High', 'Critical'] as const).map((p) => <option key={p} value={p}>{priorityMeta[p].label}</option>)}</select></Field>
          <Field label="Bitiş tarihi"><input type="date" className="input" value={v.dueDate} onChange={(e) => setV({ ...v, dueDate: e.target.value })} /></Field>
        </div>
      </div>
    </Drawer>
  )
}

export default function Tasks() {
  const qc = useQueryClient()
  const [scope, setScope] = useState<'mine' | 'created' | 'all'>('mine')
  const q = useQuery({ queryKey: ['tasks', scope], queryFn: () => api.get<Paged<WorkTask>>('/tasks', { scope, pageSize: 200 }) })
  const [edit, setEdit] = useState<WorkTask | null | undefined>(undefined)
  const [dragId, setDragId] = useState<number | null>(null)
  const [over, setOver] = useState<WorkTaskStatus | null>(null)
  const move = useMutation({
    mutationFn: ({ id, status }: { id: number; status: WorkTaskStatus }) => api.patch(`/tasks/${id}/status`, { status }),
    onMutate: async ({ id, status }) => {
      const key = ['tasks', scope]
      const prev = qc.getQueryData<Paged<WorkTask>>(key)
      if (prev) qc.setQueryData(key, { ...prev, items: prev.items.map((t) => (t.id === id ? { ...t, status } : t)) })
      return { prev, key }
    },
    onError: (e, _, ctx) => { if (ctx?.prev) qc.setQueryData(ctx.key, ctx.prev); toast.error(errorMessage(e)) },
    onSettled: () => qc.invalidateQueries({ queryKey: ['tasks'] }),
  })
  return (
    <div>
      <PageHeader title="Görevler" description="Kartları sürükleyerek durumunu değiştirin."
        actions={<><Segmented value={scope} onChange={setScope} options={[{ value: 'mine', label: 'Bana atanan' }, { value: 'created', label: 'Oluşturduğum' }, { value: 'all', label: 'Tümü' }]} /><button className="btn-primary" onClick={() => setEdit(null)}><Plus className="h-4 w-4" /> Görev</button></>} />
      <QueryState query={q}>
        {(d) => (
          <div className="grid gap-4 overflow-x-auto pb-2 md:grid-cols-4">
            {cols.map((c) => {
              const items = d.items.filter((t) => t.status === c)
              return (
                <div key={c} onDragOver={(e) => { e.preventDefault(); setOver(c) }} onDragLeave={() => setOver(null)}
                  onDrop={() => { if (dragId) move.mutate({ id: dragId, status: c }); setDragId(null); setOver(null) }}
                  className={cn('min-w-64 rounded-xl bg-ink-100/60 p-2 transition dark:bg-white/[0.02]', over === c && 'ring-2 ring-brand-400/60')}>
                  <div className="flex items-center justify-between px-2 py-1.5"><Badge tone={taskStatusMeta[c].tone} dot>{taskStatusMeta[c].label}</Badge><span className="text-xs font-semibold muted">{items.length}</span></div>
                  <div className="mt-1 min-h-20 space-y-2">
                    {items.map((t) => (
                      <div key={t.id} draggable onDragStart={() => setDragId(t.id)} onClick={() => setEdit(t)} role="button" tabIndex={0} onKeyDown={(e) => e.key === 'Enter' && setEdit(t)}
                        className={cn('card cursor-grab p-3 active:cursor-grabbing', dragId === t.id && 'opacity-50')}>
                        <div className="flex items-start justify-between gap-2"><span className={cn('text-[13px] font-semibold text-ink-900 dark:text-white', t.status === 'Done' && 'line-through opacity-60')}>{t.title}</span><Badge tone={priorityMeta[t.priority].tone}>{priorityMeta[t.priority].label}</Badge></div>
                        {t.projectName && <div className="mt-1 text-[11px] muted">{t.projectName}</div>}
                        <div className="mt-2 flex items-center justify-between">
                          <span className={cn('inline-flex items-center gap-1 text-[11px]', t.isOverdue ? 'font-semibold text-red-600 dark:text-red-400' : 'muted')}><CalendarDays className="h-3 w-3" />{t.dueDate ? fmt.date(t.dueDate) : 'Tarihsiz'}</span>
                          {t.assignee && <Avatar name={t.assignee.fullName} size={22} />}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </QueryState>
      {edit !== undefined && <TaskDrawer task={edit} open onClose={() => setEdit(undefined)} />}
    </div>
  )
}
