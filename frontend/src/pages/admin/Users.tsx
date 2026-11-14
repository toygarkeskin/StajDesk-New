import { zodResolver } from '@hookform/resolvers/zod'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, KeyRound, Plus, ShieldCheck, UserCheck, UserX } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { Badge, Drawer, EmptyState, Field, Modal, PageHeader, Pagination, PersonCell, QueryState, SearchInput, Spinner, Tabs, UserPicker } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api, ApiError, errorMessage } from '@/lib/api'
import { employmentLabel, fmt, roleLabel, toInputDate } from '@/lib/format'
import { useDepartments, useRoles, useTitles } from '@/lib/queries'
import { DepartmentOptions } from '@/components/DepartmentOptions'
import type { Paged, UserDetail, UserListItem } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useCan } from '@/store/auth'

const schema = z.object({
  firstName: z.string().trim().min(1, 'Ad zorunludur.'), lastName: z.string().trim().min(1, 'Soyad zorunludur.'),
  email: z.string().trim().email('Geçerli bir kurumsal e-posta giriniz.'), employeeNo: z.string().optional(),
  departmentId: z.string().min(1, 'Departman seçiniz.'), jobTitleId: z.string().optional(), phone: z.string().optional(),
  birthDate: z.string().optional(), hireDate: z.string().optional(), employmentType: z.enum(['FullTime', 'PartTime', 'Intern']), location: z.string().optional(),
})
type V = z.infer<typeof schema>

export function TempPasswordModal({ value, onClose }: { value: { name: string; password: string } | null; onClose: () => void }) {
  return (
    <Modal open={!!value} onClose={onClose} title="Geçici parola" size="sm" footer={<button className="btn-primary" onClick={onClose}>Anladım</button>}>
      <p className="text-sm"><b>{value?.name}</b> için geçici parola oluşturuldu. Bu parola <b>yalnızca bir kez</b> gösterilir; çalışana güvenli bir kanalla iletiniz. İlk girişte yeni parola belirlemesi istenecektir.</p>
      <div className="mt-4 flex items-center gap-2 rounded-lg border border-dashed border-brand-300 bg-brand-50 p-3 font-mono text-lg font-bold tracking-wider text-brand-700 dark:border-brand-400/30 dark:bg-brand-400/10 dark:text-brand-200">
        <span className="flex-1">{value?.password}</span>
        <button className="btn-ghost btn-sm" onClick={() => { navigator.clipboard.writeText(value?.password ?? ''); toast.success('Kopyalandı') }}><Copy className="h-4 w-4" /></button>
      </div>
    </Modal>
  )
}

function UserDrawer({ userId, onClose }: { userId: string | 'new'; onClose: () => void }) {
  const qc = useQueryClient()
  const confirm = useConfirm()
  const { can, user: me } = useCan()
  const isNew = userId === 'new'
  const detail = useQuery({ queryKey: ['user', userId], queryFn: () => api.get<UserDetail>(`/users/${userId}`), enabled: !isNew })
  const departments = useDepartments()
  const titles = useTitles()
  const roles = useRoles()
  const [tab, setTab] = useState<'info' | 'roles'>('info')
  const [manager, setManager] = useState<{ id: string; fullName: string } | null>(null)
  const [roleSel, setRoleSel] = useState<string[] | null>(null)
  const [temp, setTemp] = useState<{ name: string; password: string } | null>(null)
  const d = detail.data
  const { register, handleSubmit, setError, formState: { errors } } = useForm<V>({
    resolver: zodResolver(schema), mode: 'onTouched',
    values: d ? { firstName: d.firstName, lastName: d.lastName, email: d.email, employeeNo: d.employeeNo ?? '', departmentId: d.departmentId?.toString() ?? '', jobTitleId: d.jobTitleId?.toString() ?? '', phone: d.phone ?? '', birthDate: toInputDate(d.birthDate), hireDate: toInputDate(d.hireDate), employmentType: d.employmentType, location: d.location ?? '' }
      : { firstName: '', lastName: '', email: '', departmentId: '', employmentType: 'FullTime' },
  })
  const currentRoles = roleSel ?? d?.roles.map((r) => r.code) ?? ['EMPLOYEE']
  const managerValue = manager ?? (d?.manager ? { id: d.manager.id, fullName: d.manager.fullName } : null)
  const refresh = () => { qc.invalidateQueries({ queryKey: ['users'] }); qc.invalidateQueries({ queryKey: ['user', userId] }) }

  const save = useMutation({
    mutationFn: (v: V) => {
      const body = { ...v, departmentId: Number(v.departmentId), jobTitleId: v.jobTitleId ? Number(v.jobTitleId) : null, managerId: managerValue?.id ?? null, birthDate: v.birthDate || null, hireDate: v.hireDate || null, roleCodes: isNew ? currentRoles : undefined }
      return (isNew ? api.post<{ user: UserDetail; temporaryPassword: string }>('/users', body) : api.put<UserDetail>(`/users/${userId}`, body)) as Promise<unknown>
    },
    onSuccess: (r) => {
      refresh()
      if (isNew) { const res = r as { user: UserDetail; temporaryPassword: string }; setTemp({ name: res.user.fullName, password: res.temporaryPassword }) }
      else toast.success('Kullanıcı güncellendi.')
    },
    onError: (e) => { if (e instanceof ApiError && e.errors) Object.entries(e.errors).forEach(([k, m]) => setError(k as keyof V, { message: m[0] })); toast.error(errorMessage(e)) },
  })
  const saveRoles = useMutation({ mutationFn: () => api.put(`/users/${userId}/roles`, { roleCodes: currentRoles }), onSuccess: () => { refresh(); toast.success('Roller güncellendi.') }, onError: (e) => toast.error(errorMessage(e)) })
  const deactivate = useMutation({ mutationFn: (reason: string) => api.post(`/users/${userId}/deactivate`, { reason }), onSuccess: () => { refresh(); toast.success('Hesap devre dışı bırakıldı.') }, onError: (e) => toast.error(errorMessage(e)) })
  const activate = useMutation({ mutationFn: () => api.post(`/users/${userId}/activate`), onSuccess: () => { refresh(); toast.success('Hesap etkinleştirildi.') } })
  const reset = useMutation({ mutationFn: () => api.post<{ temporaryPassword: string }>(`/users/${userId}/reset-password`), onSuccess: (r) => setTemp({ name: d!.fullName, password: r.temporaryPassword }), onError: (e) => toast.error(errorMessage(e)) })
  const privileged = ['SUPER_ADMIN', 'ADMIN']

  return (
    <Drawer open onClose={onClose} width="max-w-2xl" title={isNew ? 'Yeni kullanıcı' : d?.fullName ?? '…'} subtitle={!isNew && d && <span className="flex items-center gap-2">{d.email} {d.isActive ? <Badge tone="green">Aktif</Badge> : <Badge tone="red">Pasif</Badge>}</span>}
      footer={tab === 'info'
        ? <><button className="btn-secondary" onClick={onClose}>Kapat</button><button className="btn-primary" form="user-form" disabled={save.isPending}>{save.isPending && <Spinner />} {isNew ? 'Hesabı oluştur' : 'Kaydet'}</button></>
        : <><button className="btn-secondary" onClick={onClose}>Kapat</button><button className="btn-primary" disabled={saveRoles.isPending} onClick={() => saveRoles.mutate()}>{saveRoles.isPending && <Spinner />} Rolleri kaydet</button></>}>
      {!isNew && <Tabs className="mb-5" value={tab} onChange={setTab} tabs={[{ value: 'info', label: 'Bilgiler' }, { value: 'roles', label: 'Roller ve yetki' }]} />}
      {!isNew && d && tab === 'info' && (
        <div className="mb-5 flex flex-wrap gap-2 rounded-xl bg-ink-50 p-3 dark:bg-white/[0.03]">
          <button className="btn-secondary btn-sm" onClick={async () => (await confirm({ title: 'Parolayı sıfırla', message: 'Yeni geçici parola oluşturulacak ve kullanıcının tüm oturumları kapatılacak.', confirmText: 'Sıfırla' })) && reset.mutate()}><KeyRound className="h-3.5 w-3.5" /> Parola sıfırla</button>
          {d.isActive
            ? <button className="btn-secondary btn-sm text-red-600" disabled={d.id === me?.id} onClick={async () => { const r = await confirm({ title: `${d.fullName} hesabını devre dışı bırak`, message: <>Kullanıcı sisteme giriş yapamayacak, açık oturumları kapatılacak. Zimmetli <b>{d.assignedAssetCount}</b> cihaz için IT'ye bildirim gider.</>, danger: true, confirmText: 'Devre dışı bırak', input: { label: 'Gerekçe', required: true, placeholder: 'ör. İşten ayrılış (istifa)' } }); if (r) deactivate.mutate(r) }}><UserX className="h-3.5 w-3.5" /> Devre dışı bırak</button>
            : <button className="btn-secondary btn-sm text-emerald-600" onClick={() => activate.mutate()}><UserCheck className="h-3.5 w-3.5" /> Etkinleştir</button>}
          <span className="ml-auto self-center text-xs muted">Son giriş: {fmt.relative(d.lastLoginAt)} · {d.assignedAssetCount} zimmet · {d.openTicketCount} açık talep</span>
          {!d.isActive && d.deactivationReason && <p className="w-full text-xs text-red-600">Pasif: {d.deactivationReason} ({fmt.date(d.deactivatedAt)})</p>}
        </div>
      )}
      {tab === 'info' ? (
        <form id="user-form" className="grid gap-4 sm:grid-cols-2" noValidate onSubmit={handleSubmit((v) => save.mutate(v))}>
          <Field label="Ad" required error={errors.firstName?.message}><input className={cn('input', errors.firstName && 'input-error')} {...register('firstName')} /></Field>
          <Field label="Soyad" required error={errors.lastName?.message}><input className={cn('input', errors.lastName && 'input-error')} {...register('lastName')} /></Field>
          <Field label="Kurumsal e-posta" required error={errors.email?.message}><input type="email" className={cn('input', errors.email && 'input-error')} {...register('email')} /></Field>
          <Field label="Personel no"><input className="input" {...register('employeeNo')} /></Field>
          <Field label="Departman / mağaza" required error={errors.departmentId?.message}><select className={cn('input', errors.departmentId && 'input-error')} {...register('departmentId')}><option value="">Seçiniz…</option><DepartmentOptions departments={departments.data} /></select></Field>
          <Field label="Unvan" help="Organizasyonel unvan, sistem rolünden bağımsızdır."><select className="input" {...register('jobTitleId')}><option value="">Seçilmedi</option>{titles.data?.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}</select></Field>
          <Field label="Yönetici" help="Onay akışlarında 'Yönetici Onayı' adımı bu kişiye gider."><UserPicker value={managerValue} onChange={setManager} /></Field>
          <Field label="Çalışma türü"><select className="input" {...register('employmentType')}>{Object.entries(employmentLabel).map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></Field>
          <Field label="Telefon"><input className="input" {...register('phone')} /></Field>
          <Field label="Lokasyon"><input className="input" {...register('location')} /></Field>
          <Field label="Doğum tarihi"><input type="date" className="input" {...register('birthDate')} /></Field>
          <Field label="İşe başlama"><input type="date" className="input" {...register('hireDate')} /></Field>
          {isNew && <p className="rounded-lg bg-sky-50 p-3 text-xs text-sky-800 sm:col-span-2 dark:bg-sky-400/10 dark:text-sky-200">Hesap oluşturulunca güvenli bir geçici parola üretilir ve yalnızca bir kez gösterilir. Kullanıcı ilk girişte kendi parolasını belirler.</p>}
        </form>
      ) : (
        <div className="space-y-2">
          <p className="mb-3 text-sm muted">Bir kullanıcıya birden fazla rol atanabilir; yetkiler rollerin birleşimidir. Admin/Super Admin rollerini yalnızca Super Admin atayabilir.</p>
          {roles.data?.map((r) => {
            const on = currentRoles.includes(r.code)
            const locked = r.code === 'EMPLOYEE' || (privileged.includes(r.code) && !can('roles.manage'))
            return (
              <label key={r.code} className={cn('flex cursor-pointer items-start gap-3 rounded-xl border p-3 transition', on ? 'border-brand-400 bg-brand-50/50 dark:border-brand-400/40 dark:bg-brand-400/[0.06]' : 'border-ink-200 dark:border-white/10', locked && 'cursor-not-allowed opacity-60')}>
                <input type="checkbox" className="mt-1 accent-brand-600" checked={on} disabled={locked} onChange={() => setRoleSel(on ? currentRoles.filter((x) => x !== r.code) : [...currentRoles, r.code])} />
                <span><span className="flex items-center gap-2 font-semibold text-ink-900 dark:text-white">{r.name}{privileged.includes(r.code) && <ShieldCheck className="h-3.5 w-3.5 text-brand-600" />}</span><span className="block text-xs muted">{r.description} · {r.permissions.length} yetki</span></span>
              </label>
            )
          })}
        </div>
      )}
      <TempPasswordModal value={temp} onClose={() => { setTemp(null); if (isNew) onClose() }} />
    </Drawer>
  )
}

export default function Users() {
  const [f, setF] = useState({ search: '', departmentId: '', role: '', isActive: 'true', page: 1 })
  const [open, setOpen] = useState<string | 'new' | null>(null)
  const departments = useDepartments()
  const params = { search: f.search || undefined, departmentId: f.departmentId || undefined, role: f.role || undefined, isActive: f.isActive || undefined, page: f.page, pageSize: 20 }
  const q = useQuery({ queryKey: ['users', params], queryFn: () => api.get<Paged<UserListItem>>('/users', params), placeholderData: keepPreviousData })
  return (
    <div>
      <PageHeader title="Kullanıcılar" description="Hesapları yalnızca IT/Admin oluşturur. Dışarıdan kayıt kapalıdır." actions={<button className="btn-primary" onClick={() => setOpen('new')}><Plus className="h-4 w-4" /> Kullanıcı oluştur</button>} />
      <div className="mb-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-5">
        <SearchInput className="lg:col-span-2" value={f.search} onChange={(v) => setF({ ...f, search: v, page: 1 })} placeholder="Ad, e-posta veya personel no…" />
        <select className="input" value={f.departmentId} onChange={(e) => setF({ ...f, departmentId: e.target.value, page: 1 })} aria-label="Departman"><option value="">Tüm departmanlar</option><DepartmentOptions departments={departments.data} /></select>
        <select className="input" value={f.role} onChange={(e) => setF({ ...f, role: e.target.value, page: 1 })} aria-label="Rol"><option value="">Tüm roller</option>{Object.entries(roleLabel).map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select>
        <select className="input" value={f.isActive} onChange={(e) => setF({ ...f, isActive: e.target.value, page: 1 })} aria-label="Durum"><option value="true">Aktif</option><option value="false">Pasif</option><option value="">Tümü</option></select>
      </div>
      <QueryState query={q} empty={(d) => d.items.length === 0 && <div className="card"><EmptyState title="Kullanıcı bulunamadı" /></div>}>
        {(d) => (
          <div className="card overflow-hidden">
            <div className="overflow-x-auto"><table className="w-full min-w-[860px] text-sm">
              <thead className="bg-ink-50/70 dark:bg-white/[0.02]"><tr><th className="th">Çalışan</th><th className="th">Departman / unvan</th><th className="th">Roller</th><th className="th">Durum</th><th className="th">Son giriş</th></tr></thead>
              <tbody>{d.items.map((u) => (
                <tr key={u.id} className="tr cursor-pointer" onClick={() => setOpen(u.id)}>
                  <td className="td"><PersonCell name={u.fullName} sub={`${u.email}${u.employeeNo ? ' · ' + u.employeeNo : ''}`} /></td>
                  <td className="td max-w-64"><div className="truncate">{u.department?.name}</div><div className="truncate text-xs muted">{u.jobTitle}</div></td>
                  <td className="td"><div className="flex flex-wrap gap-1">{u.roles.filter((r) => r !== 'EMPLOYEE').map((r) => <Badge key={r} tone={r.includes('ADMIN') ? 'brand' : 'violet'}>{roleLabel[r] ?? r}</Badge>)}{u.roles.length === 1 && <Badge>Çalışan</Badge>}</div></td>
                  <td className="td">{!u.isActive ? <Badge tone="red">Pasif</Badge> : u.mustChangePassword ? <Badge tone="amber">Parola bekliyor</Badge> : <Badge tone="green">Aktif</Badge>}</td>
                  <td className="td text-xs muted">{fmt.relative(u.lastLoginAt)}</td>
                </tr>
              ))}</tbody>
            </table></div>
            <Pagination page={d.page} totalPages={d.totalPages} total={d.totalCount} onPage={(p) => setF({ ...f, page: p })} />
          </div>
        )}
      </QueryState>
      {open && <UserDrawer userId={open} onClose={() => setOpen(null)} />}
    </div>
  )
}
