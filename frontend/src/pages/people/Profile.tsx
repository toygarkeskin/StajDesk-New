import { useMutation, useQuery } from '@tanstack/react-query'
import { Save } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { Avatar, Badge, Card, DefinitionList, Field, PageHeader, Spinner, Tabs } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { employmentLabel, fmt } from '@/lib/format'
import type { Asset, LeaveBalance, Me, Paged } from '@/lib/types'
import { useAuth } from '@/store/auth'
import { PasswordForm } from '../auth/SetPassword'

export default function Profile() {
  const user = useAuth((s) => s.user)!
  const setUser = useAuth((s) => s.setUser)
  const [sp, setSp] = useSearchParams()
  const tab = (sp.get('tab') ?? 'profile') as 'profile' | 'security'
  const [v, setV] = useState({ phone: user.phone ?? '', location: user.location ?? '', bio: user.bio ?? '' })
  const leave = useQuery({ queryKey: ['leave-balance'], queryFn: () => api.get<LeaveBalance>('/leave/balance') })
  const assets = useQuery({ queryKey: ['my-assets'], queryFn: () => api.get<Paged<Asset>>('/assets', { mine: true, pageSize: 50 }) })
  const save = useMutation({ mutationFn: () => api.put<Me>('/auth/profile', v), onSuccess: (me) => { setUser(me); toast.success('Profiliniz güncellendi.') }, onError: (e) => toast.error(errorMessage(e)) })

  return (
    <div className="max-w-5xl">
      <PageHeader title="Profilim" />
      <Tabs className="mb-6" value={tab} onChange={(t) => setSp(t === 'profile' ? {} : { tab: t })} tabs={[{ value: 'profile', label: 'Profil' }, { value: 'security', label: 'Parola ve güvenlik' }]} />
      {tab === 'profile' ? (
        <div className="grid gap-6 lg:grid-cols-3">
          <Card className="lg:row-span-2">
            <div className="flex flex-col items-center text-center">
              <Avatar name={user.fullName} size={88} />
              <h2 className="mt-3 text-lg font-bold">{user.fullName}</h2>
              <p className="text-sm muted">{user.jobTitle}</p>
              <div className="mt-3 flex flex-wrap justify-center gap-1">{user.roles.map((r) => <Badge key={r.code} tone={r.code === 'EMPLOYEE' ? 'gray' : 'brand'}>{r.name}</Badge>)}</div>
            </div>
            <div className="mt-6"><DefinitionList items={[
              { label: 'Personel no', value: user.employeeNo }, { label: 'Departman', value: user.department?.name }, { label: 'Yönetici', value: user.manager?.fullName ?? '—' },
              { label: 'E-posta', value: user.email }, { label: 'Çalışma türü', value: employmentLabel[user.employmentType] }, { label: 'İşe başlama', value: fmt.date(user.hireDate) },
            ]} /></div>
            <p className="mt-4 text-xs muted">Organizasyon bilgilerinizdeki değişiklikler için İK'ya Personel Kartı başvurusu yapabilirsiniz.</p>
          </Card>
          <Card title="İletişim bilgilerim" className="lg:col-span-2" footer={<div className="flex justify-end"><button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate()}>{save.isPending ? <Spinner /> : <Save className="h-4 w-4" />} Kaydet</button></div>}>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="İş telefonu"><input className="input" value={v.phone} onChange={(e) => setV({ ...v, phone: e.target.value })} /></Field>
              <Field label="Lokasyon"><input className="input" value={v.location} onChange={(e) => setV({ ...v, location: e.target.value })} /></Field>
              <Field label="Hakkımda" className="sm:col-span-2" help={`${v.bio.length}/500`}><textarea className="input" rows={3} maxLength={500} value={v.bio} onChange={(e) => setV({ ...v, bio: e.target.value })} /></Field>
            </div>
          </Card>
          <div className="grid gap-6 sm:grid-cols-2 lg:col-span-2">
            <Card title="İzin bakiyem">
              {leave.data && <div className="space-y-1 text-sm"><div className="font-display text-3xl font-bold text-emerald-600 dark:text-emerald-400">{fmt.number(leave.data.remaining, 1)} gün</div><div className="muted">Hak ediş {fmt.number(leave.data.entitled)} + devreden {fmt.number(leave.data.carriedOver)} · kullanılan {fmt.number(leave.data.used, 1)}</div></div>}
            </Card>
            <Card title={`Zimmetimdeki cihazlar (${assets.data?.totalCount ?? 0})`} bodyClass="">
              <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">{assets.data?.items.map((a) => <li key={a.id} className="flex justify-between px-5 py-2.5 text-sm"><span className="truncate">{a.name}</span><span className="text-xs muted">{a.assetTag}</span></li>)}</ul>
            </Card>
          </div>
        </div>
      ) : (
        <Card title="Parolayı değiştir" className="max-w-lg">
          <PasswordForm />
          <p className="mt-4 text-xs muted">Parolanızı değiştirdiğinizde diğer cihazlardaki oturumlarınız güvenlik amacıyla kapatılır.</p>
        </Card>
      )}
    </div>
  )
}
