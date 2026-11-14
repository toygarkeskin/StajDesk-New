import { zodResolver } from '@hookform/resolvers/zod'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeySquare, Laptop, Plus } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { z } from 'zod'
import { Badge, Drawer, EmptyState, Field, PageHeader, Pagination, PersonCell, Progress, QueryState, SearchInput, Spinner, Tabs } from '@/components/ui'
import { api, ApiError, errorMessage } from '@/lib/api'
import { assetStatusMeta, assetTypeLabel, fmt } from '@/lib/format'
import { useDepartments } from '@/lib/queries'
import { DepartmentOptions } from '@/components/DepartmentOptions'
import type { Asset, AssetStatus, AssetType, License, Paged } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useCan } from '@/store/auth'

const assetSchema = z.object({
  name: z.string().trim().min(1, 'Varlık adı zorunludur.').max(150),
  assetTag: z.string().trim().regex(/^[A-Za-z0-9-]{3,40}$/, 'Envanter no harf, rakam ve tire içermelidir.'),
  type: z.string(), status: z.string(),
  manufacturer: z.string().optional(), model: z.string().optional(), serialNumber: z.string().optional(),
  departmentId: z.string().optional(), location: z.string().optional(),
  purchaseDate: z.string().optional(), warrantyEndsAt: z.string().optional(),
  purchaseCost: z.number({ message: 'Geçerli bir tutar giriniz.' }).min(0).optional().nullable(), notes: z.string().optional(),
})
type AssetForm = z.infer<typeof assetSchema>

export function AssetDrawer({ asset, open, onClose }: { asset?: Asset | null; open: boolean; onClose: () => void }) {
  const qc = useQueryClient()
  const departments = useDepartments()
  const { register, handleSubmit, setError, formState: { errors } } = useForm<AssetForm>({
    resolver: zodResolver(assetSchema), mode: 'onTouched',
    values: asset ? {
      name: asset.name, assetTag: asset.assetTag, type: asset.type, status: asset.status, manufacturer: asset.manufacturer ?? '', model: asset.model ?? '', serialNumber: asset.serialNumber ?? '',
      departmentId: asset.department?.id?.toString() ?? '', location: asset.location ?? '', purchaseDate: asset.purchaseDate ?? '', warrantyEndsAt: asset.warrantyEndsAt ?? '', purchaseCost: asset.purchaseCost ?? null, notes: asset.notes ?? '',
    } : { name: '', assetTag: '', type: 'Laptop', status: 'InStock', purchaseCost: null },
  })
  const save = useMutation({
    mutationFn: (v: AssetForm) => {
      const body = { ...v, departmentId: v.departmentId ? Number(v.departmentId) : null, purchaseDate: v.purchaseDate || null, warrantyEndsAt: v.warrantyEndsAt || null }
      return asset ? api.put<Asset>(`/assets/${asset.id}`, body) : api.post<Asset>('/assets', body)
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['assets'] }); qc.invalidateQueries({ queryKey: ['asset'] }); toast.success(asset ? 'Varlık güncellendi.' : 'Varlık eklendi.'); onClose() },
    onError: (e) => { if (e instanceof ApiError && e.errors) Object.entries(e.errors).forEach(([k, m]) => setError(k as keyof AssetForm, { message: m[0] })); toast.error(errorMessage(e)) },
  })
  return (
    <Drawer open={open} onClose={onClose} title={asset ? `${asset.assetTag} düzenle` : 'Yeni varlık'} subtitle="Donanım envanter kaydı"
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" form="asset-form" disabled={save.isPending}>{save.isPending && <Spinner />} Kaydet</button></>}>
      <form id="asset-form" className="grid gap-4 sm:grid-cols-2" onSubmit={handleSubmit((v) => save.mutate(v))} noValidate>
        <Field label="Varlık adı" required error={errors.name?.message} className="sm:col-span-2"><input className={cn('input', errors.name && 'input-error')} {...register('name')} /></Field>
        <Field label="Envanter no" required error={errors.assetTag?.message}><input className={cn('input uppercase', errors.assetTag && 'input-error')} placeholder="DP-LT-0001" {...register('assetTag')} /></Field>
        <Field label="Tür"><select className="input" {...register('type')}>{(Object.keys(assetTypeLabel) as AssetType[]).map((t) => <option key={t} value={t}>{assetTypeLabel[t]}</option>)}</select></Field>
        <Field label="Durum" help={asset?.assignedTo ? 'Zimmetli varlıkta durum için önce iade alınız.' : undefined}><select className="input" {...register('status')}>{(Object.keys(assetStatusMeta) as AssetStatus[]).map((t) => <option key={t} value={t}>{assetStatusMeta[t].label}</option>)}</select></Field>
        <Field label="Üretici"><input className="input" {...register('manufacturer')} /></Field>
        <Field label="Model"><input className="input" {...register('model')} /></Field>
        <Field label="Seri no"><input className="input" {...register('serialNumber')} /></Field>
        <Field label="Departman / mağaza"><select className="input" {...register('departmentId')}><option value="">Seçilmedi</option><DepartmentOptions departments={departments.data} /></select></Field>
        <Field label="Konum"><input className="input" placeholder="ör. Genel Müdürlük 3. Kat" {...register('location')} /></Field>
        <Field label="Satın alma tarihi"><input type="date" className="input" {...register('purchaseDate')} /></Field>
        <Field label="Garanti bitişi" error={errors.warrantyEndsAt?.message}><input type="date" className="input" {...register('warrantyEndsAt')} /></Field>
        <Field label="Maliyet (₺)" error={errors.purchaseCost?.message}><input type="number" step="0.01" className="input" {...register('purchaseCost', { setValueAs: (v) => (v === '' || v == null ? null : Number(v)) })} /></Field>
        <Field label="Notlar" className="sm:col-span-2"><textarea className="input" rows={3} {...register('notes')} /></Field>
      </form>
    </Drawer>
  )
}

export default function Inventory() {
  const { can } = useCan()
  const manage = can('inventory.manage')
  const viewAll = can('inventory.view')
  const [tab, setTab] = useState<'assets' | 'licenses'>('assets')
  const [filters, setFilters] = useState({ search: '', type: '', status: '', departmentId: '', warrantyExpiring: false, page: 1 })
  const [drawer, setDrawer] = useState(false)
  const nav = useNavigate()
  const departments = useDepartments()
  const params = { ...filters, type: filters.type || undefined, status: filters.status || undefined, departmentId: filters.departmentId || undefined, warrantyExpiring: filters.warrantyExpiring || undefined, mine: !viewAll, pageSize: 20 }
  const q = useQuery({ queryKey: ['assets', params], queryFn: () => api.get<Paged<Asset>>('/assets', params), placeholderData: keepPreviousData, enabled: tab === 'assets' })
  const licenses = useQuery({ queryKey: ['licenses'], queryFn: () => api.get<License[]>('/licenses'), enabled: tab === 'licenses' && viewAll })
  const set = (patch: Partial<typeof filters>) => setFilters((f) => ({ ...f, page: 1, ...patch }))

  return (
    <div>
      <PageHeader title={viewAll ? 'Envanter' : 'Zimmetimdeki cihazlar'} description={viewAll ? 'Donanım ve yazılım varlıkları, zimmet/iade geçmişi ve garanti takibi.' : 'Adınıza zimmetli cihazlar. Arıza için cihaz seçerek talep açabilirsiniz.'}
        actions={manage && tab === 'assets' && <button className="btn-primary" onClick={() => setDrawer(true)}><Plus className="h-4 w-4" /> Varlık ekle</button>} />
      {viewAll && <Tabs className="mb-4" value={tab} onChange={setTab} tabs={[{ value: 'assets', label: <span className="inline-flex items-center gap-2"><Laptop className="h-4 w-4" /> Donanım</span> }, { value: 'licenses', label: <span className="inline-flex items-center gap-2"><KeySquare className="h-4 w-4" /> Yazılım lisansları</span> }]} />}

      {tab === 'assets' ? (
        <>
          {viewAll && (
            <div className="mb-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-6">
              <SearchInput className="lg:col-span-2" value={filters.search} onChange={(v) => set({ search: v })} placeholder="Ad, envanter no, seri no, kişi…" />
              <select className="input" value={filters.type} onChange={(e) => set({ type: e.target.value })} aria-label="Tür"><option value="">Tüm türler</option>{(Object.keys(assetTypeLabel) as AssetType[]).map((t) => <option key={t} value={t}>{assetTypeLabel[t]}</option>)}</select>
              <select className="input" value={filters.status} onChange={(e) => set({ status: e.target.value })} aria-label="Durum"><option value="">Tüm durumlar</option>{(Object.keys(assetStatusMeta) as AssetStatus[]).map((t) => <option key={t} value={t}>{assetStatusMeta[t].label}</option>)}</select>
              <select className="input" value={filters.departmentId} onChange={(e) => set({ departmentId: e.target.value })} aria-label="Departman"><option value="">Tüm birimler</option><DepartmentOptions departments={departments.data} /></select>
              <label className="flex items-center gap-2 text-sm"><input type="checkbox" className="accent-brand-600" checked={filters.warrantyExpiring} onChange={(e) => set({ warrantyExpiring: e.target.checked })} /> Garantisi 90 gün içinde bitenler</label>
            </div>
          )}
          <QueryState query={q} empty={(d) => d.items.length === 0 && <div className="card"><EmptyState title={viewAll ? 'Varlık bulunamadı' : 'Adınıza zimmetli cihaz bulunmuyor'} /></div>}>
            {(d) => (
              <div className="card overflow-hidden">
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[920px] text-sm">
                    <thead className="bg-ink-50/70 dark:bg-white/[0.02]"><tr><th className="th">Varlık</th><th className="th">Tür</th><th className="th">Durum</th><th className="th">Zimmetli</th><th className="th">Birim / konum</th><th className="th">Garanti</th><th className="th text-right">Maliyet</th></tr></thead>
                    <tbody>
                      {d.items.map((a) => {
                        const expired = a.warrantyEndsAt && new Date(a.warrantyEndsAt) < new Date()
                        return (
                          <tr key={a.id} className="tr cursor-pointer" onClick={() => nav(`/inventory/${a.id}`)}>
                            <td className="td"><div className="font-medium text-ink-900 dark:text-white">{a.name}</div><div className="text-xs muted">{a.assetTag} · {a.serialNumber}</div></td>
                            <td className="td">{assetTypeLabel[a.type]}</td>
                            <td className="td"><Badge tone={assetStatusMeta[a.status].tone} dot>{assetStatusMeta[a.status].label}</Badge></td>
                            <td className="td">{a.assignedTo ? <PersonCell name={a.assignedTo.fullName} /> : <span className="muted">—</span>}</td>
                            <td className="td max-w-56"><div className="truncate">{a.department?.name ?? '—'}</div><div className="truncate text-xs muted">{a.location}</div></td>
                            <td className={cn('td text-xs', expired ? 'text-red-600 dark:text-red-400' : 'muted')}>{fmt.date(a.warrantyEndsAt)}</td>
                            <td className="td text-right">{fmt.money(a.purchaseCost)}</td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
                <Pagination page={d.page} totalPages={d.totalPages} total={d.totalCount} onPage={(p) => setFilters((f) => ({ ...f, page: p }))} />
              </div>
            )}
          </QueryState>
        </>
      ) : (
        <QueryState query={licenses}>
          {(list) => (
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {list.map((l) => (
                <div key={l.id} className="card p-5">
                  <div className="flex items-start justify-between gap-2"><div><div className="font-semibold text-ink-900 dark:text-white">{l.name}</div><div className="text-xs muted">{l.vendor} · {l.version}</div></div>{l.isExpired ? <Badge tone="red">Süresi doldu</Badge> : <Badge tone="green">Aktif</Badge>}</div>
                  <div className="mt-4 flex items-end justify-between text-sm"><span className="muted">Kullanım</span><b className="text-ink-900 dark:text-white">{l.used} / {l.seats}</b></div>
                  <Progress className="mt-1.5" value={(l.used / l.seats) * 100} tone={l.used / l.seats > 0.9 ? 'amber' : 'brand'} />
                  <div className="mt-3 flex justify-between text-xs muted"><span>Bitiş: {fmt.date(l.expiresAt)}</span><span>{fmt.money(l.cost)}</span></div>
                  {l.notes && <p className="mt-2 text-xs text-amber-600 dark:text-amber-400">{l.notes}</p>}
                </div>
              ))}
            </div>
          )}
        </QueryState>
      )}
      <AssetDrawer open={drawer} onClose={() => setDrawer(false)} />
    </div>
  )
}
