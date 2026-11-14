import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { Badge, Drawer, Field, PageHeader, PersonCell, QueryState, SearchInput, Spinner, Tabs, Toggle, UserPicker } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api, errorMessage } from '@/lib/api'
import { useDepartments, useTitles } from '@/lib/queries'
import type { Department, DepartmentType } from '@/lib/types'
import { useCan } from '@/store/auth'

const typeLabel: Record<DepartmentType, string> = { HeadOffice: 'Genel Müdürlük', Store: 'Mağaza', Region: 'Bölge' }

function DeptDrawer({ dept, onClose }: { dept: Department | null; onClose: () => void }) {
  const qc = useQueryClient()
  const [v, setV] = useState({ name: dept?.name ?? '', code: dept?.code ?? '', type: dept?.type ?? 'HeadOffice' as DepartmentType, isActive: dept?.isActive ?? true })
  const [manager, setManager] = useState(dept?.manager ?? null)
  const save = useMutation({
    mutationFn: () => { const b = { ...v, managerId: manager?.id ?? null, parentId: null }; return dept ? api.put(`/org/departments/${dept.id}`, b) : api.post('/org/departments', b) },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['departments'] }); toast.success('Departman kaydedildi.'); onClose() },
    onError: (e) => toast.error(errorMessage(e)),
  })
  return (
    <Drawer open onClose={onClose} title={dept?.name ?? 'Yeni departman'} footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" disabled={save.isPending || !v.name.trim()} onClick={() => save.mutate()}>{save.isPending && <Spinner />} Kaydet</button></>}>
      <div className="space-y-4">
        <Field label="Ad" required><input className="input" value={v.name} onChange={(e) => setV({ ...v, name: e.target.value })} /></Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Kod"><input className="input" value={v.code} onChange={(e) => setV({ ...v, code: e.target.value })} /></Field>
          <Field label="Tür"><select className="input" value={v.type} onChange={(e) => setV({ ...v, type: e.target.value as DepartmentType })}>{Object.entries(typeLabel).map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></Field>
        </div>
        <Field label="Departman yöneticisi" help="'Departman yöneticisi' ve 'Sorumlu birim' onay adımları bu kişiye yönlendirilir."><UserPicker value={manager} onChange={setManager} /></Field>
        <Toggle checked={v.isActive} onChange={(x) => setV({ ...v, isActive: x })} label="Aktif" />
      </div>
    </Drawer>
  )
}

export default function Organization() {
  const { can } = useCan()
  const manage = can('org.manage')
  const qc = useQueryClient()
  const confirm = useConfirm()
  const [tab, setTab] = useState<'departments' | 'titles'>('departments')
  const [type, setType] = useState<DepartmentType | ''>('HeadOffice')
  const [search, setSearch] = useState('')
  const [edit, setEdit] = useState<Department | null | undefined>(undefined)
  const [newTitle, setNewTitle] = useState('')
  const departments = useDepartments()
  const titles = useTitles()
  const filtered = useMemo(() => departments.data?.filter((d) => (!type || d.type === type) && d.name.toLocaleLowerCase('tr-TR').includes(search.toLocaleLowerCase('tr-TR'))) ?? [], [departments.data, type, search])
  const addTitle = useMutation({ mutationFn: () => api.post('/org/titles', { name: newTitle }), onSuccess: () => { qc.invalidateQueries({ queryKey: ['titles'] }); setNewTitle(''); toast.success('Unvan eklendi.') }, onError: (e) => toast.error(errorMessage(e)) })
  const delTitle = useMutation({ mutationFn: (id: number) => api.del(`/org/titles/${id}`), onSuccess: () => qc.invalidateQueries({ queryKey: ['titles'] }), onError: (e) => toast.error(errorMessage(e)) })

  return (
    <div>
      <PageHeader title="Organizasyon" description="Departman, mağaza ve unvan yönetimi. Organizasyonel unvan, sistem rolünden ayrıdır." actions={manage && tab === 'departments' && <button className="btn-primary" onClick={() => setEdit(null)}><Plus className="h-4 w-4" /> Departman</button>} />
      <Tabs className="mb-4" value={tab} onChange={setTab} tabs={[{ value: 'departments', label: 'Departmanlar', count: departments.data?.length }, { value: 'titles', label: 'Unvanlar', count: titles.data?.length }]} />
      {tab === 'departments' ? (
        <>
          <div className="mb-4 flex flex-col gap-2 sm:flex-row">
            <SearchInput className="sm:w-80" value={search} onChange={setSearch} placeholder="Departman ara…" />
            <select className="input sm:w-52" value={type} onChange={(e) => setType(e.target.value as DepartmentType)} aria-label="Tür"><option value="">Tüm türler</option>{Object.entries(typeLabel).map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select>
          </div>
          <QueryState query={departments}>
            {() => (
              <div className="card overflow-hidden"><div className="overflow-x-auto"><table className="w-full min-w-[720px] text-sm">
                <thead className="bg-ink-50/70 dark:bg-white/[0.02]"><tr><th className="th">Kod</th><th className="th">Ad</th><th className="th">Tür</th><th className="th">Yönetici</th><th className="th">Çalışan</th><th className="th">Durum</th></tr></thead>
                <tbody>{filtered.slice(0, 300).map((d) => (
                  <tr key={d.id} className={manage ? 'tr cursor-pointer' : 'tr'} onClick={() => manage && setEdit(d)}>
                    <td className="td text-xs font-semibold muted">{d.code}</td><td className="td font-medium text-ink-900 dark:text-white">{d.name}</td><td className="td"><Badge>{typeLabel[d.type]}</Badge></td>
                    <td className="td">{d.manager ? <PersonCell name={d.manager.fullName} /> : <span className="muted">—</span>}</td><td className="td">{d.userCount}</td><td className="td">{d.isActive ? <Badge tone="green">Aktif</Badge> : <Badge>Pasif</Badge>}</td>
                  </tr>
                ))}</tbody>
              </table></div></div>
            )}
          </QueryState>
        </>
      ) : (
        <div className="max-w-2xl">
          {manage && <div className="mb-4 flex gap-2"><input className="input" value={newTitle} onChange={(e) => setNewTitle(e.target.value)} placeholder="Yeni unvan" /><button className="btn-primary" disabled={!newTitle.trim() || addTitle.isPending} onClick={() => addTitle.mutate()}><Plus className="h-4 w-4" /> Ekle</button></div>}
          <QueryState query={titles}>
            {(list) => (
              <div className="card divide-y divide-ink-100 dark:divide-white/[0.05]">
                {list.map((t) => (
                  <div key={t.id} className="flex items-center justify-between px-5 py-2.5 text-sm"><span>{t.name}</span><span className="flex items-center gap-3"><span className="text-xs muted">{t.userCount} çalışan</span>{manage && t.userCount === 0 && <button className="btn-ghost btn-icon text-red-600" aria-label="Sil" onClick={async () => (await confirm({ title: 'Unvanı sil', message: t.name, danger: true, confirmText: 'Sil' })) && delTitle.mutate(t.id)}><Trash2 className="h-4 w-4" /></button>}</span></div>
                ))}
              </div>
            )}
          </QueryState>
        </div>
      )}
      {edit !== undefined && <DeptDrawer dept={edit} onClose={() => setEdit(undefined)} />}
    </div>
  )
}
