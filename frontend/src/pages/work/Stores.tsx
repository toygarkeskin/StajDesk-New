import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Phone, Store as StoreIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Badge, Drawer, EmptyState, Field, PageHeader, Pagination, PersonCell, QueryState, SearchInput, Spinner, StatCard, UserPicker } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { fmt, storeStatusMeta } from '@/lib/format'
import type { Paged, StoreItem, StoreStatus } from '@/lib/types'
import { useCan } from '@/store/auth'

export default function Stores() {
  const { can } = useCan()
  const qc = useQueryClient()
  const [f, setF] = useState({ search: '', city: '', status: '', page: 1 })
  const params = { search: f.search || undefined, city: f.city || undefined, status: f.status || undefined, page: f.page, pageSize: 20 }
  const q = useQuery({ queryKey: ['stores', params], queryFn: () => api.get<Paged<StoreItem>>('/stores', params), placeholderData: keepPreviousData })
  const cities = useQuery({ queryKey: ['store-cities'], queryFn: () => api.get<string[]>('/stores/cities') })
  const [edit, setEdit] = useState<StoreItem | null>(null)
  const [v, setV] = useState<{ code: string; name: string; city: string; phone: string; address: string; status: StoreStatus; areaSqm: string }>({ code: '', name: '', city: '', phone: '', address: '', status: 'Open', areaSqm: '' })
  const [manager, setManager] = useState<{ id: string; fullName: string } | null>(null)
  const openEdit = (s: StoreItem) => { setEdit(s); setV({ code: s.code, name: s.name, city: s.city ?? '', phone: s.phone ?? '', address: s.address ?? '', status: s.status, areaSqm: s.areaSqm?.toString() ?? '' }); setManager(s.manager ? { id: s.manager.id, fullName: s.manager.fullName } : null) }
  const save = useMutation({
    mutationFn: () => api.put(`/stores/${edit!.id}`, { ...v, areaSqm: v.areaSqm ? Number(v.areaSqm) : null, managerId: manager?.id ?? null }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['stores'] }); setEdit(null); toast.success('Mağaza güncellendi.') },
    onError: (e) => toast.error(errorMessage(e)),
  })
  return (
    <div>
      <PageHeader title="Mağaza Yönetimi" description="Mağaza listesi, yöneticiler ve durum bilgileri (organizasyon verisinden)." />
      <div className="mb-5 grid gap-4 sm:grid-cols-3">
        <StatCard label="Toplam mağaza" value={fmt.number(q.data?.totalCount)} icon={<StoreIcon className="h-4 w-4" />} />
        <StatCard label="Şehir" value={cities.data?.length ?? '—'} tone="violet" />
        <StatCard label="Filtre" value={f.city || 'Tümü'} tone="cyan" />
      </div>
      <div className="mb-4 flex flex-col gap-2 sm:flex-row">
        <SearchInput className="sm:w-80" value={f.search} onChange={(x) => setF({ ...f, search: x, page: 1 })} placeholder="Mağaza adı veya kodu…" />
        <select className="input sm:w-52" value={f.city} onChange={(e) => setF({ ...f, city: e.target.value, page: 1 })} aria-label="Şehir"><option value="">Tüm şehirler</option>{cities.data?.map((c) => <option key={c}>{c}</option>)}</select>
        <select className="input sm:w-44" value={f.status} onChange={(e) => setF({ ...f, status: e.target.value, page: 1 })} aria-label="Durum"><option value="">Tüm durumlar</option>{(Object.keys(storeStatusMeta) as StoreStatus[]).map((s) => <option key={s} value={s}>{storeStatusMeta[s].label}</option>)}</select>
      </div>
      <QueryState query={q} empty={(d) => d.items.length === 0 && <div className="card"><EmptyState title="Mağaza bulunamadı" /></div>}>
        {(d) => (
          <div className="card overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[820px] text-sm">
                <thead className="bg-ink-50/70 dark:bg-white/[0.02]"><tr><th className="th">Kod</th><th className="th">Mağaza</th><th className="th">Şehir</th><th className="th">Yönetici</th><th className="th">Personel</th><th className="th">Alan</th><th className="th">Durum</th><th className="th" /></tr></thead>
                <tbody>
                  {d.items.map((s) => (
                    <tr key={s.id} className="tr">
                      <td className="td font-semibold muted">{s.code}</td>
                      <td className="td"><div className="font-medium text-ink-900 dark:text-white">{s.name}</div>{s.phone && <div className="inline-flex items-center gap-1 text-xs muted"><Phone className="h-3 w-3" />{s.phone}</div>}</td>
                      <td className="td">{s.city ?? '—'}</td>
                      <td className="td">{s.manager ? <PersonCell name={s.manager.fullName} /> : <span className="muted">—</span>}</td>
                      <td className="td">{s.staffCount}</td>
                      <td className="td">{s.areaSqm ? `${fmt.number(s.areaSqm)} m²` : '—'}</td>
                      <td className="td"><Badge tone={storeStatusMeta[s.status].tone} dot>{storeStatusMeta[s.status].label}</Badge></td>
                      <td className="td text-right">{can('stores.manage') && <button className="btn-ghost btn-sm" onClick={() => openEdit(s)}>Düzenle</button>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={d.page} totalPages={d.totalPages} total={d.totalCount} onPage={(p) => setF({ ...f, page: p })} />
          </div>
        )}
      </QueryState>
      <Drawer open={!!edit} onClose={() => setEdit(null)} title={edit?.name ?? ''} footer={<><button className="btn-secondary" onClick={() => setEdit(null)}>Vazgeç</button><button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate()}>{save.isPending && <Spinner />} Kaydet</button></>}>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Kod"><input className="input" value={v.code} onChange={(e) => setV({ ...v, code: e.target.value })} /></Field>
          <Field label="Durum"><select className="input" value={v.status} onChange={(e) => setV({ ...v, status: e.target.value as StoreStatus })}>{(Object.keys(storeStatusMeta) as StoreStatus[]).map((s) => <option key={s} value={s}>{storeStatusMeta[s].label}</option>)}</select></Field>
          <Field label="Mağaza adı" className="sm:col-span-2"><input className="input" value={v.name} onChange={(e) => setV({ ...v, name: e.target.value })} /></Field>
          <Field label="Şehir"><input className="input" value={v.city} onChange={(e) => setV({ ...v, city: e.target.value })} /></Field>
          <Field label="Alan (m²)"><input type="number" className="input" value={v.areaSqm} onChange={(e) => setV({ ...v, areaSqm: e.target.value })} /></Field>
          <Field label="Telefon"><input className="input" value={v.phone} onChange={(e) => setV({ ...v, phone: e.target.value })} /></Field>
          <Field label="Yönetici"><UserPicker value={manager} onChange={setManager} /></Field>
          <Field label="Adres" className="sm:col-span-2"><textarea className="input" rows={2} value={v.address} onChange={(e) => setV({ ...v, address: e.target.value })} /></Field>
        </div>
      </Drawer>
    </div>
  )
}
