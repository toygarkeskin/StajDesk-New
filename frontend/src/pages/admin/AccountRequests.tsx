import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Plus, X } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Badge, EmptyState, Field, Modal, PageHeader, PersonCell, QueryState, Spinner, Tabs, UserPicker } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api, ApiError, errorMessage } from '@/lib/api'
import { employmentLabel, fmt } from '@/lib/format'
import { useDepartments, useTitles } from '@/lib/queries'
import { DepartmentOptions } from '@/components/DepartmentOptions'
import type { AccountRequest, AccountRequestStatus, EmploymentType, UserDetail } from '@/lib/types'
import { useCan } from '@/store/auth'
import { TempPasswordModal } from './Users'

const statusTone: Record<AccountRequestStatus, 'amber' | 'green' | 'red'> = { Pending: 'amber', Approved: 'green', Rejected: 'red' }
const statusLabel: Record<AccountRequestStatus, string> = { Pending: 'Bekliyor', Approved: 'Onaylandı', Rejected: 'Reddedildi' }

export default function AccountRequests() {
  const { can } = useCan()
  const qc = useQueryClient()
  const confirm = useConfirm()
  const [status, setStatus] = useState<AccountRequestStatus | ''>('Pending')
  const q = useQuery({ queryKey: ['account-requests', status], queryFn: () => api.get<AccountRequest[]>('/account-requests', { status: status || undefined }) })
  const [open, setOpen] = useState(false)
  const [temp, setTemp] = useState<{ name: string; password: string } | null>(null)
  const departments = useDepartments()
  const titles = useTitles()
  const empty = { firstName: '', lastName: '', email: '', employeeNo: '', departmentId: '', jobTitleId: '', startDate: '', employmentType: 'FullTime' as EmploymentType, phone: '', note: '' }
  const [v, setV] = useState(empty)
  const [manager, setManager] = useState<{ id: string; fullName: string } | null>(null)
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const refresh = () => qc.invalidateQueries({ queryKey: ['account-requests'] })
  const create = useMutation({
    mutationFn: () => api.post('/account-requests', { ...v, departmentId: Number(v.departmentId) || 0, jobTitleId: v.jobTitleId ? Number(v.jobTitleId) : null, managerId: manager?.id ?? null, startDate: v.startDate || null }),
    onSuccess: () => { refresh(); setOpen(false); setV(empty); setManager(null); toast.success('Hesap talebi IT ekibine iletildi.') },
    onError: (e) => { if (e instanceof ApiError && e.errors) setErrors(e.errors); toast.error(errorMessage(e)) },
  })
  const approve = useMutation({ mutationFn: (id: number) => api.post<{ user: UserDetail; temporaryPassword: string }>(`/account-requests/${id}/approve`), onSuccess: (r) => { refresh(); setTemp({ name: r.user.fullName, password: r.temporaryPassword }) }, onError: (e) => toast.error(errorMessage(e)) })
  const reject = useMutation({ mutationFn: ({ id, reason }: { id: number; reason: string }) => api.post(`/account-requests/${id}/reject`, { reason }), onSuccess: () => { refresh(); toast.success('Talep reddedildi.') } })
  const err = (k: string) => errors[k]?.[0]

  return (
    <div>
      <PageHeader title="Hesap Talepleri" description="İK yeni personel için talep oluşturur; IT/Admin onaylayarak hesabı açar."
        actions={can('accounts.request') && <button className="btn-primary" onClick={() => setOpen(true)}><Plus className="h-4 w-4" /> Hesap talebi oluştur</button>} />
      <Tabs className="mb-4" value={status} onChange={setStatus} tabs={[{ value: 'Pending', label: 'Bekleyen' }, { value: 'Approved', label: 'Onaylanan' }, { value: 'Rejected', label: 'Reddedilen' }, { value: '', label: 'Tümü' }]} />
      <QueryState query={q} empty={(d) => d.length === 0 && <div className="card"><EmptyState title="Talep yok" /></div>}>
        {(list) => (
          <div className="card divide-y divide-ink-100 dark:divide-white/[0.05]">
            {list.map((r) => (
              <div key={r.id} className="flex flex-col gap-3 p-5 lg:flex-row lg:items-center">
                <div className="flex-1"><PersonCell name={`${r.firstName} ${r.lastName}`} sub={r.email} /></div>
                <div className="flex-1 text-sm"><div>{r.department.name}</div><div className="text-xs muted">{r.jobTitle ?? 'Unvan belirtilmedi'} · {employmentLabel[r.employmentType]} · Başlangıç {fmt.date(r.startDate)}</div></div>
                <div className="flex-1 text-xs muted">Talep eden: {r.requestedBy.fullName} · {fmt.relative(r.createdAt)}{r.note && <div className="mt-1 text-ink-600 dark:text-ink-300">“{r.note}”</div>}{r.rejectReason && <div className="mt-1 text-red-600">Ret: {r.rejectReason}</div>}</div>
                <div className="flex items-center gap-2">
                  <Badge tone={statusTone[r.status]}>{statusLabel[r.status]}</Badge>
                  {r.status === 'Pending' && can('accounts.approve') && <>
                    <button className="btn-success btn-sm" disabled={approve.isPending} onClick={async () => (await confirm({ title: 'Hesabı oluştur', message: `${r.firstName} ${r.lastName} için hesap oluşturulacak ve geçici parola üretilecek.`, confirmText: 'Onayla ve oluştur' })) && approve.mutate(r.id)}><Check className="h-3.5 w-3.5" /> Onayla</button>
                    <button className="btn-secondary btn-sm" onClick={async () => { const reason = await confirm({ title: 'Talebi reddet', message: 'Gerekçe İK\'ya iletilecek.', danger: true, confirmText: 'Reddet', input: { label: 'Gerekçe', required: true } }); if (reason) reject.mutate({ id: r.id, reason }) }}><X className="h-3.5 w-3.5" /> Reddet</button>
                  </>}
                </div>
              </div>
            ))}
          </div>
        )}
      </QueryState>
      <Modal open={open} onClose={() => setOpen(false)} title="Yeni personel için hesap talebi" size="lg" footer={<><button className="btn-secondary" onClick={() => setOpen(false)}>Vazgeç</button><button className="btn-primary" disabled={create.isPending} onClick={() => { setErrors({}); create.mutate() }}>{create.isPending && <Spinner />} Talebi gönder</button></>}>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Ad" required error={err('firstName')}><input className="input" value={v.firstName} onChange={(e) => setV({ ...v, firstName: e.target.value })} /></Field>
          <Field label="Soyad" required error={err('lastName')}><input className="input" value={v.lastName} onChange={(e) => setV({ ...v, lastName: e.target.value })} /></Field>
          <Field label="Kurumsal e-posta" required error={err('email')}><input type="email" className="input" value={v.email} onChange={(e) => setV({ ...v, email: e.target.value })} /></Field>
          <Field label="Personel no"><input className="input" value={v.employeeNo} onChange={(e) => setV({ ...v, employeeNo: e.target.value })} /></Field>
          <Field label="Departman / mağaza" required error={err('departmentId')}><select className="input" value={v.departmentId} onChange={(e) => setV({ ...v, departmentId: e.target.value })}><option value="">Seçiniz…</option><DepartmentOptions departments={departments.data} /></select></Field>
          <Field label="Unvan"><select className="input" value={v.jobTitleId} onChange={(e) => setV({ ...v, jobTitleId: e.target.value })}><option value="">Seçilmedi</option>{titles.data?.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}</select></Field>
          <Field label="Yönetici"><UserPicker value={manager} onChange={setManager} /></Field>
          <Field label="İşe başlama" required error={err('startDate')}><input type="date" className="input" value={v.startDate} onChange={(e) => setV({ ...v, startDate: e.target.value })} /></Field>
          <Field label="Çalışma türü"><select className="input" value={v.employmentType} onChange={(e) => setV({ ...v, employmentType: e.target.value as EmploymentType })}>{Object.entries(employmentLabel).map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></Field>
          <Field label="Telefon"><input className="input" value={v.phone} onChange={(e) => setV({ ...v, phone: e.target.value })} /></Field>
          <Field label="Not" className="sm:col-span-2"><textarea className="input" rows={2} value={v.note} onChange={(e) => setV({ ...v, note: e.target.value })} /></Field>
        </div>
      </Modal>
      <TempPasswordModal value={temp} onClose={() => setTemp(null)} />
    </div>
  )
}
