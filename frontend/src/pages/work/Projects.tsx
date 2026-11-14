import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { Avatar, Badge, Drawer, EmptyState, Field, PageHeader, Progress, QueryState, Spinner, UserPicker } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { fmt, projectStatusMeta, toInputDate } from '@/lib/format'
import { useDepartments } from '@/lib/queries'
import { DepartmentOptions } from '@/components/DepartmentOptions'
import type { Project, ProjectStatus } from '@/lib/types'
import { useCan } from '@/store/auth'

function ProjectDrawer({ project, onClose }: { project: Project | null; onClose: () => void }) {
  const qc = useQueryClient()
  const departments = useDepartments()
  const [v, setV] = useState({ code: project?.code ?? '', name: project?.name ?? '', description: project?.description ?? '', departmentId: project?.department?.id?.toString() ?? '', status: project?.status ?? 'Planned' as ProjectStatus, startDate: toInputDate(project?.startDate), dueDate: toInputDate(project?.dueDate) })
  const [owner, setOwner] = useState(project ? { id: project.owner.id, fullName: project.owner.fullName } : null)
  const save = useMutation({
    mutationFn: () => { const b = { ...v, ownerId: owner?.id, departmentId: v.departmentId ? Number(v.departmentId) : null, startDate: v.startDate || null, dueDate: v.dueDate || null }; return project ? api.put(`/projects/${project.id}`, b) : api.post('/projects', b) },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['projects'] }); toast.success('Proje kaydedildi.'); onClose() },
    onError: (e) => toast.error(errorMessage(e)),
  })
  return (
    <Drawer open onClose={onClose} title={project ? project.name : 'Yeni proje'} footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" disabled={!owner || save.isPending} onClick={() => save.mutate()}>{save.isPending && <Spinner />} Kaydet</button></>}>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Kod" required><input className="input uppercase" value={v.code} onChange={(e) => setV({ ...v, code: e.target.value })} placeholder="PRJ-004" /></Field>
        <Field label="Durum"><select className="input" value={v.status} onChange={(e) => setV({ ...v, status: e.target.value as ProjectStatus })}>{(Object.keys(projectStatusMeta) as ProjectStatus[]).map((s) => <option key={s} value={s}>{projectStatusMeta[s].label}</option>)}</select></Field>
        <Field label="Proje adı" required className="sm:col-span-2"><input className="input" value={v.name} onChange={(e) => setV({ ...v, name: e.target.value })} /></Field>
        <Field label="Açıklama" className="sm:col-span-2"><textarea className="input" rows={3} value={v.description} onChange={(e) => setV({ ...v, description: e.target.value })} /></Field>
        <Field label="Sorumlu" required><UserPicker value={owner} onChange={setOwner} /></Field>
        <Field label="Departman"><select className="input" value={v.departmentId} onChange={(e) => setV({ ...v, departmentId: e.target.value })}><option value="">Seçilmedi</option><DepartmentOptions departments={departments.data} includeStores={false} /></select></Field>
        <Field label="Başlangıç"><input type="date" className="input" value={v.startDate} onChange={(e) => setV({ ...v, startDate: e.target.value })} /></Field>
        <Field label="Bitiş"><input type="date" className="input" value={v.dueDate} onChange={(e) => setV({ ...v, dueDate: e.target.value })} /></Field>
      </div>
    </Drawer>
  )
}

export default function Projects() {
  const { can } = useCan()
  const q = useQuery({ queryKey: ['projects'], queryFn: () => api.get<Project[]>('/projects') })
  const [edit, setEdit] = useState<Project | null | undefined>(undefined)
  return (
    <div>
      <PageHeader title="Projeler" description="Proje portföyü ve görev ilerlemesi." actions={can('projects.manage') && <button className="btn-primary" onClick={() => setEdit(null)}><Plus className="h-4 w-4" /> Proje</button>} />
      <QueryState query={q} empty={(d) => d.length === 0 && <div className="card"><EmptyState title="Proje yok" /></div>}>
        {(list) => (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {list.map((p) => {
              const pct = p.taskCount ? Math.round((p.doneCount / p.taskCount) * 100) : 0
              return (
                <div key={p.id} className="card flex flex-col p-5">
                  <div className="flex items-center justify-between"><span className="text-xs font-semibold muted">{p.code}</span><Badge tone={projectStatusMeta[p.status].tone} dot>{projectStatusMeta[p.status].label}</Badge></div>
                  <h3 className="mt-2 font-semibold text-ink-900 dark:text-white">{p.name}</h3>
                  <p className="mt-1 line-clamp-2 flex-1 text-sm muted">{p.description}</p>
                  <div className="mt-4 flex items-center justify-between text-xs"><span className="muted">{p.doneCount}/{p.taskCount} görev</span><b>%{pct}</b></div>
                  <Progress className="mt-1.5" value={pct} tone={pct === 100 ? 'green' : 'brand'} />
                  <div className="mt-4 flex items-center justify-between">
                    <span className="flex items-center gap-2 text-xs"><Avatar name={p.owner.fullName} size={22} />{p.owner.fullName}</span>
                    <span className="text-xs muted">{fmt.date(p.dueDate)}</span>
                  </div>
                  <div className="mt-4 flex gap-2 border-t border-ink-100 pt-3 dark:border-white/[0.06]">
                    <Link to="/tasks?scope=all" className="btn-secondary btn-sm flex-1">Görevler</Link>
                    {can('projects.manage') && <button className="btn-ghost btn-sm" onClick={() => setEdit(p)}>Düzenle</button>}
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </QueryState>
      {edit !== undefined && <ProjectDrawer project={edit} onClose={() => setEdit(undefined)} />}
    </div>
  )
}
