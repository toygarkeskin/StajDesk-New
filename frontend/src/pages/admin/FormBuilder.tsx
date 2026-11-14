import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Eye, GitBranch, Plus, Save, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { Badge, Card, Drawer, Field, PageHeader, QueryState, SearchInput, Spinner, Tabs, Toggle, UserPicker } from '@/components/ui'
import { api, ApiError, errorMessage } from '@/lib/api'
import { DynamicIcon } from '@/lib/icon'
import { useDepartments, useRoles } from '@/lib/queries'
import { DepartmentOptions } from '@/components/DepartmentOptions'
import type { ApproverType, DirectoryEntry, FieldType, FormDefinition, FormField, FormSummary, WorkflowStep } from '@/lib/types'
import { tint } from '@/lib/utils'
import { useCan } from '@/store/auth'
import { DynamicForm } from '../workflow/DynamicForm'

const fieldTypes: { value: FieldType; label: string }[] = [
  { value: 'text', label: 'Metin' }, { value: 'textarea', label: 'Uzun metin' }, { value: 'number', label: 'Sayı' }, { value: 'currency', label: 'Tutar (₺)' },
  { value: 'date', label: 'Tarih' }, { value: 'select', label: 'Seçim' }, { value: 'multiselect', label: 'Çoklu seçim' }, { value: 'checkbox', label: 'Onay kutusu' },
  { value: 'email', label: 'E-posta' }, { value: 'file', label: 'Dosya' }, { value: 'user', label: 'Kişi (çalışan seçimi)' }, { value: 'description', label: 'Açıklama metni' },
]
const approverTypes: { value: ApproverType; label: string }[] = [
  { value: 'Manager', label: 'Başvuranın yöneticisi' }, { value: 'DepartmentManager', label: 'Başvuranın departman yöneticisi' },
  { value: 'ResponsibleDepartment', label: 'Formun sorumlu birimi' }, { value: 'Role', label: 'Belirli bir rol' }, { value: 'User', label: 'Belirli bir kişi' },
  { value: 'FieldUser', label: 'Formda seçilen kişi' },
]

/** Belirli kişi adımı: kayıtlı kimliğin adını rehberden çözer, aranabilir seçici gösterir. */
function StepUserPicker({ userId, onChange }: { userId?: string | null; onChange: (id: string | null) => void }) {
  const [picked, setPicked] = useState<{ id: string; fullName: string } | null>(null)
  const known = useQuery({
    queryKey: ['directory-user', userId], enabled: !!userId && picked?.id !== userId,
    queryFn: () => api.get<DirectoryEntry>(`/directory/${userId}`), retry: false,
  })
  const value = userId ? (picked?.id === userId ? picked : known.data ? { id: known.data.id, fullName: known.data.fullName } : known.isError ? { id: userId, fullName: 'Bulunamayan / pasif kullanıcı' } : { id: userId, fullName: 'Yükleniyor…' }) : null
  return (
    <div className="w-72">
      <UserPicker value={value} invalid={!userId} placeholder="Onaylayıcı ara…" onChange={(u) => { setPicked(u); onChange(u?.id ?? null) }} />
    </div>
  )
}
const icons = ['FileText', 'Monitor', 'KeyRound', 'CalendarDays', 'Wallet', 'Banknote', 'Plane', 'Car', 'GraduationCap', 'Shirt', 'Truck', 'Package', 'Megaphone', 'Wrench', 'Lightbulb', 'Scale', 'Store', 'Gift', 'Rocket', 'ClipboardCheck']

function Editor({ formKey, onClose }: { formKey: string | null; onClose: () => void }) {
  const qc = useQueryClient()
  const { isAdmin, user } = useCan()
  const departments = useDepartments()
  const roles = useRoles()
  const existing = useQuery({ queryKey: ['form', formKey], queryFn: () => api.get<FormDefinition>(`/forms/${formKey}`, { edit: true }), enabled: !!formKey })
  const blank = { key: '', name: '', description: '', module: 'Diğer Uygulamalar', icon: 'FileText', color: '#d9143f', kind: 'standard', ownerDepartmentId: user?.department?.id?.toString() ?? '', isActive: true }
  const [meta, setMeta] = useState(blank)
  const [fields, setFields] = useState<FormField[]>([{ key: 'title', label: 'Başlık', type: 'text', required: true }])
  const [steps, setSteps] = useState<WorkflowStep[]>([{ name: 'Yönetici Onayı', approverType: 'Manager' }])
  const [tab, setTab] = useState<'fields' | 'steps' | 'preview'>('fields')
  // Mevcut formu yalnızca ilk yüklemede forma aktar (arka planda yeniden çekilse de kullanıcının düzenlemeleri ezilmez)
  const [loaded, setLoaded] = useState(false)
  const f = existing.data
  if (f && !loaded) {
    setLoaded(true)
    setMeta({ key: f.key, name: f.name, description: f.description ?? '', module: f.module, icon: f.icon, color: f.color, kind: f.kind, ownerDepartmentId: f.ownerDepartment?.id?.toString() ?? '', isActive: f.isActive })
    setFields(f.fields)
    setSteps(f.steps)
  }
  const save = useMutation({
    mutationFn: () => { const b = { ...meta, ownerDepartmentId: meta.ownerDepartmentId ? Number(meta.ownerDepartmentId) : null, fields, steps, allowedRoles: existing.data?.allowedRoles ?? [] }; return existing.data ? api.put(`/forms/${existing.data.id}`, b) : api.post('/forms', b) },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['forms-admin'] }); qc.invalidateQueries({ queryKey: ['form'] }); qc.invalidateQueries({ queryKey: ['apps'] }); toast.success('Form kaydedildi.'); onClose() },
    onError: (e) => toast.error(e instanceof ApiError && e.errors ? Object.values(e.errors).flat().join(' ') : errorMessage(e)),
  })
  const upd = (i: number, p: Partial<FormField>) => setFields(fields.map((f, n) => (n === i ? { ...f, ...p } : f)))
  const move = <T,>(arr: T[], i: number, d: number) => { const a = [...arr]; const [x] = a.splice(i, 1); a.splice(i + d, 0, x); return a }

  return (
    <Drawer open onClose={onClose} width="max-w-4xl" title={existing.data ? `${existing.data.name} formunu düzenle` : 'Yeni form / uygulama'}
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate()}>{save.isPending ? <Spinner /> : <Save className="h-4 w-4" />} Kaydet</button></>}>
      <div className="grid gap-4 sm:grid-cols-3">
        <Field label="Form adı" required><input className="input" value={meta.name} onChange={(e) => setMeta({ ...meta, name: e.target.value })} /></Field>
        <Field label="Anahtar (URL)" required help="ör. kirtasiye-talebi"><input className="input font-mono" disabled={!!existing.data} value={meta.key} onChange={(e) => setMeta({ ...meta, key: e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '-') })} /></Field>
        <Field label="Modül (departman grubu)" required><input className="input" value={meta.module} onChange={(e) => setMeta({ ...meta, module: e.target.value })} /></Field>
        <Field label="Açıklama" className="sm:col-span-3"><input className="input" value={meta.description} onChange={(e) => setMeta({ ...meta, description: e.target.value })} /></Field>
        <Field label="Sorumlu birim" help={isAdmin ? undefined : 'Yalnızca kendi departmanınız adına form oluşturabilirsiniz.'}><select className="input" value={meta.ownerDepartmentId} disabled={!isAdmin} onChange={(e) => setMeta({ ...meta, ownerDepartmentId: e.target.value })}><option value="">Yok</option><DepartmentOptions departments={departments.data} includeStores={false} /></select></Field>
        <Field label="İkon ve renk"><div className="flex gap-2"><select className="input" value={meta.icon} onChange={(e) => setMeta({ ...meta, icon: e.target.value })}>{[...new Set([meta.icon, ...icons])].map((i) => <option key={i}>{i}</option>)}</select><input type="color" className="h-9 w-12 shrink-0 rounded-lg border border-ink-200 dark:border-white/10" value={meta.color} onChange={(e) => setMeta({ ...meta, color: e.target.value })} /><span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg" style={{ background: tint(meta.color, 0.15), color: meta.color }}><DynamicIcon name={meta.icon} className="h-4 w-4" /></span></div></Field>
        <Field label="Tür" help="Özel tipler ayrı iş mantığı çalıştırır."><select className="input" value={meta.kind} disabled={!!existing.data} onChange={(e) => setMeta({ ...meta, kind: e.target.value })}><option value="standard">Standart</option><option value="leave">İzin (bakiye + iş günü)</option><option value="expense">Masraf (limit kontrolü)</option><option value="advance">Avans (limit kontrolü)</option><option value="revenue">Ciro (mağaza + mükerrer kontrolü)</option><option value="shift">Vardiya (izin çakışması + takas onayı)</option></select></Field>
        <div className="sm:col-span-3"><Toggle checked={meta.isActive} onChange={(x) => setMeta({ ...meta, isActive: x })} label="Yayında (Uygulama Merkezi'nde görünür)" /></div>
      </div>
      <Tabs className="mt-6 mb-4" value={tab} onChange={setTab} tabs={[{ value: 'fields', label: 'Alanlar', count: fields.length }, { value: 'steps', label: 'Onay adımları', count: steps.length }, { value: 'preview', label: <span className="inline-flex items-center gap-1.5"><Eye className="h-3.5 w-3.5" /> Önizleme</span> }]} />
      {tab === 'fields' && (
        <div className="space-y-3">
          {fields.map((f, i) => (
            <div key={i} className="rounded-xl border border-ink-200 p-3 dark:border-white/10">
              <div className="grid gap-2 sm:grid-cols-[1fr_160px_150px_auto]">
                <input className="input" value={f.label} onChange={(e) => upd(i, { label: e.target.value })} placeholder="Alan etiketi" aria-label="Etiket" />
                <input className="input font-mono text-xs" value={f.key} onChange={(e) => upd(i, { key: e.target.value.replace(/[^a-zA-Z0-9_]/g, '') })} placeholder="anahtar" aria-label="Anahtar" />
                <select className="input" value={f.type} onChange={(e) => upd(i, { type: e.target.value as FieldType })} aria-label="Tip">{fieldTypes.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}</select>
                <div className="flex items-center gap-1">
                  <button className="btn-ghost btn-icon" disabled={i === 0} onClick={() => setFields(move(fields, i, -1))} aria-label="Yukarı"><ArrowUp className="h-4 w-4" /></button>
                  <button className="btn-ghost btn-icon" disabled={i === fields.length - 1} onClick={() => setFields(move(fields, i, 1))} aria-label="Aşağı"><ArrowDown className="h-4 w-4" /></button>
                  <button className="btn-ghost btn-icon text-red-600" onClick={() => setFields(fields.filter((_, n) => n !== i))} aria-label="Sil"><Trash2 className="h-4 w-4" /></button>
                </div>
              </div>
              {f.type !== 'description' && (
                <div className="mt-2 flex flex-wrap items-center gap-4">
                  <Toggle checked={f.required} onChange={(x) => upd(i, { required: x })} label="Zorunlu" />
                  {(f.type === 'number' || f.type === 'currency') && <><input type="number" className="input h-8 w-28" placeholder="Min" value={f.min ?? ''} onChange={(e) => upd(i, { min: e.target.value === '' ? null : Number(e.target.value) })} /><input type="number" className="input h-8 w-28" placeholder="Max" value={f.max ?? ''} onChange={(e) => upd(i, { max: e.target.value === '' ? null : Number(e.target.value) })} /></>}
                  <input className="input h-8 flex-1" placeholder="Yardım metni (isteğe bağlı)" value={f.helpText ?? ''} onChange={(e) => upd(i, { helpText: e.target.value })} />
                </div>
              )}
              {(f.type === 'select' || f.type === 'multiselect') && <input className="input mt-2 h-8" placeholder="Seçenekler (virgülle ayırın)" value={f.options?.join(', ') ?? ''} onChange={(e) => upd(i, { options: e.target.value.split(',').map((o) => o.trim()).filter(Boolean) })} />}
            </div>
          ))}
          <button className="btn-secondary" onClick={() => setFields([...fields, { key: `alan${fields.length + 1}`, label: 'Yeni alan', type: 'text', required: false }])}><Plus className="h-4 w-4" /> Alan ekle</button>
        </div>
      )}
      {tab === 'steps' && (
        <div className="space-y-3">
          <p className="text-sm muted">Adımlar sırayla işler. Onaylayıcı başvuru sahibinin kendisiyse veya bulunamazsa adım otomatik geçilir; tüm adımlar onaylanınca başvuru sorumlu birime düşer.</p>
          {steps.map((s, i) => (
            <div key={i} className="flex flex-wrap items-center gap-2 rounded-xl border border-ink-200 p-3 dark:border-white/10">
              <span className="grid h-7 w-7 place-items-center rounded-full bg-ink-100 text-xs font-bold dark:bg-white/10">{i + 1}</span>
              <input className="input flex-1" value={s.name} onChange={(e) => setSteps(steps.map((x, n) => (n === i ? { ...x, name: e.target.value } : x)))} aria-label="Adım adı" />
              <select className="input w-64" value={s.approverType} onChange={(e) => setSteps(steps.map((x, n) => (n === i ? { ...x, approverType: e.target.value as ApproverType } : x)))} aria-label="Onaylayıcı">{approverTypes.map((a) => <option key={a.value} value={a.value}>{a.label}</option>)}</select>
              {s.approverType === 'Role' && <select className="input w-44" value={s.roleCode ?? ''} onChange={(e) => setSteps(steps.map((x, n) => (n === i ? { ...x, roleCode: e.target.value } : x)))} aria-label="Rol"><option value="">Rol seçin</option>{roles.data?.map((r) => <option key={r.code} value={r.code}>{r.name}</option>)}</select>}
              {s.approverType === 'User' && <StepUserPicker userId={s.userId} onChange={(id) => setSteps(steps.map((x, n) => (n === i ? { ...x, userId: id } : x)))} />}
              {s.approverType === 'FieldUser' && (
                <select className="input w-56" value={s.fieldKey ?? ''} aria-label="Kişi alanı" onChange={(e) => setSteps(steps.map((x, n) => (n === i ? { ...x, fieldKey: e.target.value } : x)))}>
                  <option value="">Kişi alanı seçin</option>
                  {fields.filter((f) => f.type === 'user').map((f) => <option key={f.key} value={f.key}>{f.label}</option>)}
                </select>
              )}
              <button className="btn-ghost btn-icon text-red-600" onClick={() => setSteps(steps.filter((_, n) => n !== i))} aria-label="Adımı sil"><Trash2 className="h-4 w-4" /></button>
            </div>
          ))}
          <button className="btn-secondary" onClick={() => setSteps([...steps, { name: 'Onay', approverType: 'ResponsibleDepartment' }])}><Plus className="h-4 w-4" /> Adım ekle</button>
        </div>
      )}
      {tab === 'preview' && <div className="rounded-xl border border-dashed border-ink-300 p-4 dark:border-white/15"><DynamicForm fields={fields} onSubmit={() => toast.info('Önizleme: gönderim yapılmadı.')} submitLabel="Önizleme gönder" /></div>}
    </Drawer>
  )
}

export default function FormBuilder() {
  const [sp, setSp] = useSearchParams()
  const q = useQuery({ queryKey: ['forms-admin'], queryFn: () => api.get<FormSummary[]>('/forms', { all: true }) })
  const [search, setSearch] = useState('')
  const editId = sp.get('edit')
  const editKey = editId === 'new' ? null : q.data?.find((f) => f.id === Number(editId))?.key ?? null
  const list = q.data?.filter((f) => !search || f.name.toLocaleLowerCase('tr-TR').includes(search.toLocaleLowerCase('tr-TR')) || f.module.toLocaleLowerCase('tr-TR').includes(search.toLocaleLowerCase('tr-TR')))
  return (
    <div>
      <PageHeader title="Form Tasarımcısı" description="Departman uygulamalarının alanlarını ve onay akışlarını yönetin. Değişiklikler anında Uygulama Merkezi'ne yansır." actions={<button className="btn-primary" onClick={() => setSp({ edit: 'new' })}><Plus className="h-4 w-4" /> Yeni form</button>} />
      <SearchInput className="mb-4 sm:w-80" value={search} onChange={setSearch} placeholder="Form veya modül ara…" />
      <QueryState query={q}>
        {() => (
          <Card bodyClass="">
            <div className="overflow-x-auto"><table className="w-full min-w-[760px] text-sm">
              <thead className="bg-ink-50/70 dark:bg-white/[0.02]"><tr><th className="th">Form</th><th className="th">Modül</th><th className="th">Sorumlu birim</th><th className="th">Tür</th><th className="th">Onay adımı</th><th className="th">Durum</th></tr></thead>
              <tbody>{list?.map((f) => (
                <tr key={f.id} className="tr cursor-pointer" onClick={() => setSp({ edit: String(f.id) })}>
                  <td className="td"><div className="flex items-center gap-3"><span className="grid h-8 w-8 place-items-center rounded-lg" style={{ background: tint(f.color, 0.12), color: f.color }}><DynamicIcon name={f.icon} className="h-4 w-4" /></span><div><div className="font-medium text-ink-900 dark:text-white">{f.name}</div><div className="font-mono text-[11px] muted">{f.key}</div></div></div></td>
                  <td className="td">{f.module}</td><td className="td max-w-56 truncate">{f.ownerDepartment?.name ?? '—'}</td>
                  <td className="td">{f.kind === 'standard' ? <Badge>Standart</Badge> : <Badge tone="violet">{f.kind}</Badge>}</td>
                  <td className="td"><span className="inline-flex items-center gap-1"><GitBranch className="h-3.5 w-3.5 text-ink-400" />{f.stepCount}</span></td>
                  <td className="td">{f.isActive ? <Badge tone="green">Yayında</Badge> : <Badge>Pasif</Badge>}</td>
                </tr>
              ))}</tbody>
            </table></div>
          </Card>
        )}
      </QueryState>
      {editId && (editId === 'new' || editKey) && <Editor key={editId} formKey={editKey} onClose={() => setSp({})} />}
    </div>
  )
}
