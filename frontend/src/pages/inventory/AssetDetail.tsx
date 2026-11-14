import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowDownToLine, ArrowLeft, ArrowUpFromLine, LifeBuoy, Pencil, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { Avatar, Badge, Card, DefinitionList, EmptyState, ErrorState, Field, Modal, Skeleton, Spinner, UserPicker } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api, errorMessage } from '@/lib/api'
import { assetStatusMeta, assetTypeLabel, fmt, statusMeta } from '@/lib/format'
import type { AssetDetail as A, AssetStatus } from '@/lib/types'
import { useCan } from '@/store/auth'
import { AssetDrawer } from './Inventory'

export default function AssetDetail() {
  const { id } = useParams()
  const nav = useNavigate()
  const qc = useQueryClient()
  const confirm = useConfirm()
  const { can } = useCan()
  const manage = can('inventory.manage')
  const q = useQuery({ queryKey: ['asset', id], queryFn: () => api.get<A>(`/assets/${id}`) })
  const [edit, setEdit] = useState(false)
  const [checkout, setCheckout] = useState(false)
  const [checkin, setCheckin] = useState(false)
  const [who, setWho] = useState<{ id: string; fullName: string } | null>(null)
  const [note, setNote] = useState('')
  const [condition, setCondition] = useState('Sağlam')
  const [newStatus, setNewStatus] = useState<AssetStatus>('InStock')
  const done = (msg: string) => { qc.invalidateQueries({ queryKey: ['asset', id] }); qc.invalidateQueries({ queryKey: ['assets'] }); toast.success(msg); setCheckout(false); setCheckin(false); setNote(''); setWho(null) }
  const doCheckout = useMutation({ mutationFn: () => api.post(`/assets/${id}/checkout`, { userId: who!.id, note }), onSuccess: () => done('Zimmet kaydı oluşturuldu.'), onError: (e) => toast.error(errorMessage(e)) })
  const doCheckin = useMutation({ mutationFn: () => api.post(`/assets/${id}/checkin`, { condition, note, newStatus }), onSuccess: () => done('İade alındı.'), onError: (e) => toast.error(errorMessage(e)) })
  const del = useMutation({ mutationFn: () => api.del(`/assets/${id}`), onSuccess: () => { toast.success('Varlık silindi.'); nav('/inventory') }, onError: (e) => toast.error(errorMessage(e)) })

  if (q.isLoading) return <Skeleton className="h-96" />
  if (q.error || !q.data) return <ErrorState error={q.error} onRetry={q.refetch} />
  const { asset: a, assignments, software, tickets } = q.data

  return (
    <div>
      <Link to="/inventory" className="mb-3 inline-flex items-center gap-1.5 text-sm muted hover:text-ink-900 dark:hover:text-white"><ArrowLeft className="h-4 w-4" /> Envanter</Link>
      <div className="mb-6 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="flex items-center gap-2"><span className="text-sm font-semibold muted">{a.assetTag}</span><Badge tone={assetStatusMeta[a.status].tone} dot>{assetStatusMeta[a.status].label}</Badge></div>
          <h1 className="mt-1 text-2xl font-bold">{a.name}</h1>
          <p className="text-sm muted">{assetTypeLabel[a.type]} · {a.manufacturer} {a.model}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Link to="/tickets/new" className="btn-secondary"><LifeBuoy className="h-4 w-4" /> Arıza bildir</Link>
          {manage && (a.assignedTo
            ? <button className="btn-primary" onClick={() => setCheckin(true)}><ArrowDownToLine className="h-4 w-4" /> İade al</button>
            : <button className="btn-primary" disabled={a.status === 'Retired' || a.status === 'InRepair'} onClick={() => setCheckout(true)}><ArrowUpFromLine className="h-4 w-4" /> Zimmetle</button>)}
          {manage && <button className="btn-secondary" onClick={() => setEdit(true)}><Pencil className="h-4 w-4" /> Düzenle</button>}
          {can('inventory.manage') && !a.assignedTo && <button className="btn-ghost text-red-600" aria-label="Sil" onClick={async () => (await confirm({ title: `${a.assetTag} silinsin mi?`, message: 'Varlık kaydı ve zimmet geçmişi kalıcı olarak silinecek.', danger: true, confirmText: 'Kalıcı olarak sil' })) && del.mutate()}><Trash2 className="h-4 w-4" /></button>}
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <Card title="Zimmet ve iade geçmişi" bodyClass="">
            {assignments.length === 0 ? <EmptyState title="Bu varlık hiç zimmetlenmemiş" /> : (
              <ol className="divide-y divide-ink-100 dark:divide-white/[0.05]">
                {assignments.map((x) => (
                  <li key={x.id} className="flex items-start gap-3 px-5 py-4">
                    <Avatar name={x.user.fullName} size={34} />
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2"><b className="text-ink-900 dark:text-white">{x.user.fullName}</b>{x.returnedAt ? <Badge>İade edildi</Badge> : <Badge tone="green">Aktif zimmet</Badge>}</div>
                      <div className="mt-1 text-xs muted">Teslim: {fmt.dateTime(x.assignedAt)} · {x.assignedBy.fullName}</div>
                      {x.returnedAt && <div className="text-xs muted">İade: {fmt.dateTime(x.returnedAt)} · {x.returnedBy?.fullName} · Durum: {x.returnCondition ?? '—'}</div>}
                      {x.note && <p className="mt-1 text-xs">{x.note}</p>}
                    </div>
                  </li>
                ))}
              </ol>
            )}
          </Card>
          <Card title="İlgili talepler" bodyClass="">
            {tickets.length === 0 ? <EmptyState title="Bu cihazla ilgili talep yok" /> : (
              <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">
                {tickets.map((t) => <li key={t.id}><Link to={`/tickets/${t.id}`} className="flex items-center gap-3 px-5 py-3 hover:bg-ink-50/70 dark:hover:bg-white/[0.02]"><span className="text-xs font-semibold muted">#{t.id}</span><span className="flex-1 truncate">{t.title}</span><Badge tone={statusMeta[t.status].tone}>{statusMeta[t.status].label}</Badge></Link></li>)}
              </ul>
            )}
          </Card>
        </div>
        <aside className="space-y-6">
          <Card title="Bilgiler">
            <DefinitionList items={[
              { label: 'Zimmetli', value: a.assignedTo?.fullName ?? '—' }, { label: 'Birim', value: a.department?.name }, { label: 'Konum', value: a.location },
              { label: 'Seri no', value: a.serialNumber }, { label: 'Satın alma', value: fmt.date(a.purchaseDate) }, { label: 'Garanti bitişi', value: fmt.date(a.warrantyEndsAt) },
              { label: 'Maliyet', value: fmt.money(a.purchaseCost) }, { label: 'Açık talep', value: a.openTicketCount },
            ]} />
            {a.notes && <p className="mt-3 rounded-lg bg-amber-50 p-3 text-xs text-amber-800 dark:bg-amber-400/10 dark:text-amber-200">{a.notes}</p>}
          </Card>
          <Card title="Kurulu yazılımlar" bodyClass="">
            {software.length === 0 ? <EmptyState title="Kayıtlı yazılım yok" /> : (
              <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">{software.map((s) => <li key={s.id} className="flex justify-between px-5 py-2.5 text-sm"><span>{s.name}</span><span className="text-xs muted">{s.version}</span></li>)}</ul>
            )}
          </Card>
        </aside>
      </div>

      <AssetDrawer open={edit} asset={a} onClose={() => setEdit(false)} />
      <Modal open={checkout} onClose={() => setCheckout(false)} title="Zimmetle (teslim)" footer={<><button className="btn-secondary" onClick={() => setCheckout(false)}>Vazgeç</button><button className="btn-primary" disabled={!who || doCheckout.isPending} onClick={() => doCheckout.mutate()}>{doCheckout.isPending && <Spinner />} Zimmetle</button></>}>
        <div className="space-y-4">
          <Field label="Teslim alacak çalışan" required><UserPicker value={who} onChange={setWho} /></Field>
          <Field label="Not"><textarea className="input" rows={3} value={note} onChange={(e) => setNote(e.target.value)} placeholder="ör. Şarj aleti ve çanta ile teslim edildi." /></Field>
        </div>
      </Modal>
      <Modal open={checkin} onClose={() => setCheckin(false)} title="İade al" footer={<><button className="btn-secondary" onClick={() => setCheckin(false)}>Vazgeç</button><button className="btn-primary" disabled={doCheckin.isPending} onClick={() => doCheckin.mutate()}>{doCheckin.isPending && <Spinner />} İadeyi kaydet</button></>}>
        <div className="space-y-4">
          <p className="text-sm"><b>{a.assignedTo?.fullName}</b> üzerindeki zimmet kapatılacak.</p>
          <Field label="Cihaz durumu"><input className="input" value={condition} onChange={(e) => setCondition(e.target.value)} /></Field>
          <Field label="Yeni durum"><select className="input" value={newStatus} onChange={(e) => setNewStatus(e.target.value as AssetStatus)}><option value="InStock">Stoğa al</option><option value="InRepair">Servise gönder</option><option value="Retired">Hurdaya ayır</option></select></Field>
          <Field label="Not"><textarea className="input" rows={2} value={note} onChange={(e) => setNote(e.target.value)} /></Field>
        </div>
      </Modal>
    </div>
  )
}
