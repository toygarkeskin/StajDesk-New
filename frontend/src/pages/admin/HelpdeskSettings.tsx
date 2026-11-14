import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { FlaskConical, Plus, Trash2, Wand2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Badge, Card, ColorDot, Drawer, Field, PageHeader, QueryState, Spinner, Tabs, Toggle, UserPicker } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api, errorMessage } from '@/lib/api'
import { priorityMeta } from '@/lib/format'
import { useCategories, useDepartments, useGroups } from '@/lib/queries'
import { DepartmentOptions } from '@/components/DepartmentOptions'
import type { Category, RoutingRule, TicketPriority } from '@/lib/types'

function CategoryDrawer({ c, onClose }: { c: Category | null; onClose: () => void }) {
  const qc = useQueryClient()
  const groups = useGroups()
  const [v, setV] = useState({ name: c?.name ?? '', description: c?.description ?? '', color: c?.color ?? '#e11d48', isActive: c?.isActive ?? true, defaultGroupId: c?.defaultGroupId?.toString() ?? '' })
  const save = useMutation({
    mutationFn: () => { const b = { ...v, defaultGroupId: v.defaultGroupId ? Number(v.defaultGroupId) : null }; return c ? api.put(`/ticket-categories/${c.id}`, b) : api.post('/ticket-categories', b) },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['categories'] }); toast.success('Kategori kaydedildi.'); onClose() }, onError: (e) => toast.error(errorMessage(e)),
  })
  return (
    <Drawer open onClose={onClose} title={c?.name ?? 'Yeni kategori'} footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate()}>{save.isPending && <Spinner />} Kaydet</button></>}>
      <div className="space-y-4">
        <Field label="Ad" required><input className="input" value={v.name} onChange={(e) => setV({ ...v, name: e.target.value })} /></Field>
        <Field label="Açıklama"><input className="input" value={v.description} onChange={(e) => setV({ ...v, description: e.target.value })} /></Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Renk"><div className="flex gap-2"><input type="color" className="h-9 w-12 rounded-lg border border-ink-200 dark:border-white/10" value={v.color} onChange={(e) => setV({ ...v, color: e.target.value })} /><input className="input font-mono" value={v.color} onChange={(e) => setV({ ...v, color: e.target.value })} /></div></Field>
          <Field label="Varsayılan destek grubu"><select className="input" value={v.defaultGroupId} onChange={(e) => setV({ ...v, defaultGroupId: e.target.value })}><option value="">Yok</option>{groups.data?.filter((g) => g.isSupportGroup).map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}</select></Field>
        </div>
        <Toggle checked={v.isActive} onChange={(x) => setV({ ...v, isActive: x })} label="Aktif (talep formunda görünür)" />
      </div>
    </Drawer>
  )
}

function RuleDrawer({ r, onClose }: { r: RoutingRule | null; onClose: () => void }) {
  const qc = useQueryClient()
  const categories = useCategories()
  const groups = useGroups()
  const departments = useDepartments()
  const [v, setV] = useState({ name: r?.name ?? '', sortOrder: r?.sortOrder ?? 100, isActive: r?.isActive ?? true, categoryId: r?.categoryId?.toString() ?? '', departmentId: r?.departmentId?.toString() ?? '', keyword: r?.keyword ?? '', minPriority: r?.minPriority ?? '', assignGroupId: r?.assignGroupId?.toString() ?? '', setPriority: r?.setPriority ?? '' })
  const [user, setUser] = useState(r?.assignUserId ? { id: r.assignUserId, fullName: r.assignUserName ?? '' } : null)
  const n = (x: string) => (x ? Number(x) : null)
  const save = useMutation({
    mutationFn: () => { const b = { ...v, categoryId: n(v.categoryId), departmentId: n(v.departmentId), assignGroupId: n(v.assignGroupId), assignUserId: user?.id ?? null, minPriority: v.minPriority || null, setPriority: v.setPriority || null, keyword: v.keyword || null }; return r ? api.put(`/routing-rules/${r.id}`, b) : api.post('/routing-rules', b) },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['rules'] }); toast.success('Kural kaydedildi.'); onClose() }, onError: (e) => toast.error(errorMessage(e)),
  })
  return (
    <Drawer open onClose={onClose} title={r?.name ?? 'Yeni yönlendirme kuralı'} subtitle="Sıra numarası küçük olan kural önce değerlendirilir; ilk eşleşen uygulanır." width="max-w-2xl"
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate()}>{save.isPending && <Spinner />} Kaydet</button></>}>
      <div className="space-y-5">
        <div className="grid gap-4 sm:grid-cols-[1fr_120px]"><Field label="Kural adı" required><input className="input" value={v.name} onChange={(e) => setV({ ...v, name: e.target.value })} /></Field><Field label="Sıra"><input type="number" className="input" value={v.sortOrder} onChange={(e) => setV({ ...v, sortOrder: Number(e.target.value) })} /></Field></div>
        <fieldset className="rounded-xl border border-ink-200 p-4 dark:border-white/10"><legend className="px-1 text-xs font-semibold uppercase tracking-wider muted">Koşullar (boş = her şey)</legend>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Kategori"><select className="input" value={v.categoryId} onChange={(e) => setV({ ...v, categoryId: e.target.value })}><option value="">Herhangi</option>{categories.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select></Field>
            <Field label="Talep sahibinin departmanı"><select className="input" value={v.departmentId} onChange={(e) => setV({ ...v, departmentId: e.target.value })}><option value="">Herhangi</option><DepartmentOptions departments={departments.data} /></select></Field>
            <Field label="Başlık/açıklamada geçen kelime"><input className="input" value={v.keyword} onChange={(e) => setV({ ...v, keyword: e.target.value })} placeholder="ör. VPN" /></Field>
            <Field label="En az öncelik"><select className="input" value={v.minPriority} onChange={(e) => setV({ ...v, minPriority: e.target.value as TicketPriority })}><option value="">Herhangi</option>{(['Low', 'Medium', 'High', 'Critical'] as const).map((p) => <option key={p} value={p}>{priorityMeta[p].label}</option>)}</select></Field>
          </div>
        </fieldset>
        <fieldset className="rounded-xl border border-ink-200 p-4 dark:border-white/10"><legend className="px-1 text-xs font-semibold uppercase tracking-wider muted">Eylemler</legend>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Gruba ata"><select className="input" value={v.assignGroupId} onChange={(e) => setV({ ...v, assignGroupId: e.target.value })}><option value="">—</option>{groups.data?.filter((g) => g.isSupportGroup).map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}</select></Field>
            <Field label="Kişiye ata"><UserPicker value={user} onChange={setUser} /></Field>
            <Field label="Önceliği ayarla"><select className="input" value={v.setPriority} onChange={(e) => setV({ ...v, setPriority: e.target.value as TicketPriority })}><option value="">Değiştirme</option>{(['Low', 'Medium', 'High', 'Critical'] as const).map((p) => <option key={p} value={p}>{priorityMeta[p].label}</option>)}</select></Field>
            <div className="pt-7"><Toggle checked={v.isActive} onChange={(x) => setV({ ...v, isActive: x })} label="Aktif" /></div>
          </div>
        </fieldset>
      </div>
    </Drawer>
  )
}

export default function HelpdeskSettings() {
  const qc = useQueryClient()
  const confirm = useConfirm()
  const [tab, setTab] = useState<'categories' | 'rules'>('categories')
  const categories = useCategories()
  const rules = useQuery({ queryKey: ['rules'], queryFn: () => api.get<RoutingRule[]>('/routing-rules') })
  const [cat, setCat] = useState<Category | null | undefined>(undefined)
  const [rule, setRule] = useState<RoutingRule | null | undefined>(undefined)
  const [test, setTest] = useState({ categoryId: '', title: '', priority: 'Medium' as TicketPriority })
  const [testResult, setTestResult] = useState<RoutingRule | null | 'none'>(null)
  const runTest = useMutation({ mutationFn: () => api.post<RoutingRule | null>('/routing-rules/test', { categoryId: Number(test.categoryId), title: test.title, description: test.title, priority: test.priority }), onSuccess: (r) => setTestResult(r ?? 'none') })
  const delCat = useMutation({ mutationFn: (id: number) => api.del(`/ticket-categories/${id}`), onSuccess: () => qc.invalidateQueries({ queryKey: ['categories'] }), onError: (e) => toast.error(errorMessage(e)) })
  const delRule = useMutation({ mutationFn: (id: number) => api.del(`/routing-rules/${id}`), onSuccess: () => qc.invalidateQueries({ queryKey: ['rules'] }) })

  return (
    <div>
      <PageHeader title="Kategoriler & Yönlendirme Kuralları" description="GLPI iş kuralları yaklaşımı: yeni talepler koşullara göre otomatik olarak gruba, kişiye ve önceliğe yönlendirilir."
        actions={<button className="btn-primary" onClick={() => (tab === 'categories' ? setCat(null) : setRule(null))}><Plus className="h-4 w-4" /> {tab === 'categories' ? 'Kategori' : 'Kural'}</button>} />
      <Tabs className="mb-4" value={tab} onChange={setTab} tabs={[{ value: 'categories', label: 'Kategoriler' }, { value: 'rules', label: 'Yönlendirme kuralları' }]} />
      {tab === 'categories' ? (
        <QueryState query={categories}>
          {(list) => (
            <div className="card overflow-hidden"><table className="w-full text-sm">
              <thead className="bg-ink-50/70 dark:bg-white/[0.02]"><tr><th className="th">Kategori</th><th className="th">Varsayılan grup</th><th className="th">Talep (açık)</th><th className="th">Durum</th><th className="th" /></tr></thead>
              <tbody>{list.map((c) => (
                <tr key={c.id} className="tr cursor-pointer" onClick={() => setCat(c)}>
                  <td className="td"><ColorDot color={c.color} label={c.name} /><div className="text-xs muted">{c.description}</div></td><td className="td">{c.defaultGroupName ?? '—'}</td><td className="td">{c.ticketCount} ({c.openTicketCount})</td>
                  <td className="td">{c.isActive ? <Badge tone="green">Aktif</Badge> : <Badge>Pasif</Badge>}</td>
                  <td className="td text-right">{c.ticketCount === 0 && <button className="btn-ghost btn-icon text-red-600" aria-label="Sil" onClick={async (e) => { e.stopPropagation(); if (await confirm({ title: 'Kategoriyi sil', message: c.name, danger: true, confirmText: 'Sil' })) delCat.mutate(c.id) }}><Trash2 className="h-4 w-4" /></button>}</td>
                </tr>
              ))}</tbody>
            </table></div>
          )}
        </QueryState>
      ) : (
        <div className="grid gap-6 xl:grid-cols-3">
          <div className="xl:col-span-2">
            <QueryState query={rules}>
              {(list) => (
                <div className="space-y-3">
                  {list.map((r) => (
                    <button key={r.id} onClick={() => setRule(r)} className="card flex w-full items-start gap-4 p-4 text-left hover:shadow-pop">
                      <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-violet-50 text-xs font-bold text-violet-600 dark:bg-violet-400/10 dark:text-violet-300">{r.sortOrder}</span>
                      <span className="min-w-0 flex-1">
                        <span className="flex items-center gap-2"><span className="font-semibold text-ink-900 dark:text-white">{r.name}</span>{!r.isActive && <Badge>Pasif</Badge>}<span className="ml-auto text-xs muted">{r.matchCount} eşleşme</span></span>
                        <span className="mt-1.5 flex flex-wrap gap-1.5 text-xs">
                          <span className="muted">EĞER</span>{r.categoryName && <Badge>{r.categoryName}</Badge>}{r.departmentName && <Badge>{r.departmentName}</Badge>}{r.keyword && <Badge>“{r.keyword}”</Badge>}{r.minPriority && <Badge>≥ {priorityMeta[r.minPriority].label}</Badge>}
                          <span className="muted">→</span>{r.assignGroupName && <Badge tone="brand">{r.assignGroupName}</Badge>}{r.assignUserName && <Badge tone="brand">{r.assignUserName}</Badge>}{r.setPriority && <Badge tone="amber">Öncelik: {priorityMeta[r.setPriority].label}</Badge>}
                        </span>
                      </span>
                      <span role="button" tabIndex={0} className="btn-ghost btn-icon text-red-600" aria-label="Sil" onClick={async (e) => { e.stopPropagation(); if (await confirm({ title: 'Kuralı sil', message: r.name, danger: true, confirmText: 'Sil' })) delRule.mutate(r.id) }}><Trash2 className="h-4 w-4" /></span>
                    </button>
                  ))}
                </div>
              )}
            </QueryState>
          </div>
          <Card title={<span className="inline-flex items-center gap-2"><FlaskConical className="h-4 w-4" /> Kural test aracı</span>}>
            <div className="space-y-3">
              <Field label="Kategori"><select className="input" value={test.categoryId} onChange={(e) => setTest({ ...test, categoryId: e.target.value })}><option value="">Seçiniz</option>{categories.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select></Field>
              <Field label="Örnek başlık"><input className="input" value={test.title} onChange={(e) => setTest({ ...test, title: e.target.value })} placeholder="ör. Mağaza kasası açılmıyor" /></Field>
              <Field label="Öncelik"><select className="input" value={test.priority} onChange={(e) => setTest({ ...test, priority: e.target.value as TicketPriority })}>{(['Low', 'Medium', 'High', 'Critical'] as const).map((p) => <option key={p} value={p}>{priorityMeta[p].label}</option>)}</select></Field>
              <button className="btn-secondary w-full" disabled={!test.categoryId || runTest.isPending} onClick={() => runTest.mutate()}><Wand2 className="h-4 w-4" /> Test et</button>
              {testResult === 'none' && <p className="rounded-lg bg-ink-50 p-3 text-sm dark:bg-white/[0.03]">Eşleşen kural yok; talep kategorinin varsayılan grubuna gider.</p>}
              {testResult && testResult !== 'none' && <p className="rounded-lg bg-violet-50 p-3 text-sm text-violet-800 dark:bg-violet-400/10 dark:text-violet-200">Eşleşen kural: <b>{testResult.name}</b></p>}
            </div>
          </Card>
        </div>
      )}
      {cat !== undefined && <CategoryDrawer c={cat} onClose={() => setCat(undefined)} />}
      {rule !== undefined && <RuleDrawer r={rule} onClose={() => setRule(undefined)} />}
    </div>
  )
}
