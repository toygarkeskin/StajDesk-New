import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Download, FileText, Lock, Plus, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { Badge, EmptyState, Field, FileUploader, Modal, PageHeader, QueryState, SearchInput, Spinner } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api, errorMessage } from '@/lib/api'
import { fmt } from '@/lib/format'
import { useDepartments } from '@/lib/queries'
import { DepartmentOptions } from '@/components/DepartmentOptions'
import type { DocumentItem, FileDto } from '@/lib/types'
import { useCan } from '@/store/auth'

export default function Documents() {
  const { can } = useCan()
  const qc = useQueryClient()
  const confirm = useConfirm()
  const [search, setSearch] = useState('')
  const q = useQuery({ queryKey: ['documents', search], queryFn: () => api.get<DocumentItem[]>('/documents', { search: search || undefined }) })
  const groups = useMemo(() => Object.entries((q.data ?? []).reduce<Record<string, DocumentItem[]>>((acc, d) => ((acc[d.category] ??= []).push(d), acc), {})), [q.data])
  const [open, setOpen] = useState(false)
  const departments = useDepartments()
  const [v, setV] = useState({ title: '', description: '', category: 'Genel', departmentId: '' })
  const [file, setFile] = useState<FileDto[]>([])
  const add = useMutation({
    mutationFn: () => api.post('/documents', { ...v, fileId: file[0].id, departmentId: v.departmentId ? Number(v.departmentId) : null }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['documents'] }); setOpen(false); setFile([]); setV({ title: '', description: '', category: 'Genel', departmentId: '' }); toast.success('Doküman eklendi.') },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const del = useMutation({ mutationFn: (id: number) => api.del(`/documents/${id}`), onSuccess: () => qc.invalidateQueries({ queryKey: ['documents'] }) })
  return (
    <div className="max-w-5xl">
      <PageHeader title="Dokümanlar" description="Politika, yönetmelik ve şablonlar. Departmana özel dokümanlar yalnızca o birime görünür." actions={can('documents.manage') && <button className="btn-primary" onClick={() => setOpen(true)}><Plus className="h-4 w-4" /> Doküman ekle</button>} />
      <SearchInput className="mb-5 sm:w-96" value={search} onChange={setSearch} placeholder="Doküman ara…" />
      <QueryState query={q} empty={(d) => d.length === 0 && <div className="card"><EmptyState title="Doküman bulunamadı" /></div>}>
        {() => (
          <div className="space-y-6">
            {groups.map(([cat, docs]) => (
              <section key={cat}>
                <h2 className="mb-2 text-xs font-semibold uppercase tracking-wider muted">{cat}</h2>
                <div className="card divide-y divide-ink-100 dark:divide-white/[0.05]">
                  {docs.map((d) => (
                    <div key={d.id} className="flex items-center gap-4 px-5 py-3.5">
                      <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-sky-50 text-sky-600 dark:bg-sky-400/10 dark:text-sky-300"><FileText className="h-5 w-5" /></span>
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-2"><span className="truncate font-medium text-ink-900 dark:text-white">{d.title}</span>{d.department && <Badge tone="violet"><Lock className="h-3 w-3" /> {d.department.name}</Badge>}</div>
                        <div className="truncate text-xs muted">{d.description} · {fmt.bytes(d.file.size)} · {d.downloads} indirme · {fmt.date(d.createdAt)}</div>
                      </div>
                      <button className="btn-secondary btn-sm" onClick={() => api.download(`/files/${d.file.id}`, d.file.fileName).then(() => qc.invalidateQueries({ queryKey: ['documents'] }))}><Download className="h-3.5 w-3.5" /> İndir</button>
                      {can('documents.manage') && <button className="btn-ghost btn-icon text-red-600" aria-label="Sil" onClick={async () => (await confirm({ title: 'Dokümanı kaldır', message: d.title, danger: true, confirmText: 'Kaldır' })) && del.mutate(d.id)}><Trash2 className="h-4 w-4" /></button>}
                    </div>
                  ))}
                </div>
              </section>
            ))}
          </div>
        )}
      </QueryState>
      <Modal open={open} onClose={() => setOpen(false)} title="Doküman ekle" footer={<><button className="btn-secondary" onClick={() => setOpen(false)}>Vazgeç</button><button className="btn-primary" disabled={!v.title.trim() || !file.length || add.isPending} onClick={() => add.mutate()}>{add.isPending && <Spinner />} Ekle</button></>}>
        <div className="space-y-4">
          <Field label="Başlık" required><input className="input" value={v.title} onChange={(e) => setV({ ...v, title: e.target.value })} /></Field>
          <Field label="Açıklama"><input className="input" value={v.description} onChange={(e) => setV({ ...v, description: e.target.value })} /></Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Kategori"><input className="input" list="doc-cats" value={v.category} onChange={(e) => setV({ ...v, category: e.target.value })} /><datalist id="doc-cats">{groups.map(([c]) => <option key={c} value={c} />)}</datalist></Field>
            <Field label="Görünürlük"><select className="input" value={v.departmentId} onChange={(e) => setV({ ...v, departmentId: e.target.value })}><option value="">Tüm çalışanlar</option><DepartmentOptions departments={departments.data} includeStores={false} /></select></Field>
          </div>
          <Field label="Dosya" required><FileUploader value={file} onChange={setFile} /></Field>
        </div>
      </Modal>
    </div>
  )
}
