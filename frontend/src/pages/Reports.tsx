import { useQuery } from '@tanstack/react-query'
import { AlarmClock, CheckCircle2, Download, Gauge, Inbox, Timer } from 'lucide-react'
import { useState } from 'react'
import { BarList, ColumnChart, DonutChart, TrendChart } from '@/components/charts'
import { Badge, Card, PageHeader, PersonCell, Progress, QueryState, Skeleton, StatCard, Tabs } from '@/components/ui'
import { api } from '@/lib/api'
import { assetTypeLabel, fmt } from '@/lib/format'
import { useCategories, useDepartments } from '@/lib/queries'
import { DepartmentOptions } from '@/components/DepartmentOptions'
import type { AssetType, InventoryReport, PeopleReport, TicketReport, WorkflowReport } from '@/lib/types'
import { useCan } from '@/store/auth'

const today = new Date()
const iso = (d: Date) => d.toISOString().slice(0, 10)

export default function Reports() {
  const { can } = useCan()
  const tabs = [
    ...(can('reports.view', 'reports.view_department', 'tickets.view_all') ? [{ value: 'tickets', label: 'Destek talepleri' }] : []),
    ...(can('reports.view', 'reports.view_department', 'submissions.view_all') ? [{ value: 'workflows', label: 'İş akışları' }] : []),
    ...(can('inventory.view', 'reports.view') ? [{ value: 'inventory', label: 'Envanter' }] : []),
    ...(can('reports.view', 'users.manage', 'leave.manage') ? [{ value: 'people', label: 'İnsan kaynakları' }] : []),
  ]
  const [tab, setTab] = useState(tabs[0]?.value ?? 'tickets')
  const [f, setF] = useState({ from: iso(new Date(today.getTime() - 29 * 864e5)), to: iso(today), departmentId: '', categoryId: '', status: '' })
  const departments = useDepartments()
  const categories = useCategories()
  const params = { from: f.from, to: f.to, departmentId: f.departmentId || undefined, categoryId: f.categoryId || undefined, status: f.status || undefined }

  return (
    <div>
      <PageHeader title="Raporlar" description="Gerçek zamanlı API verisiyle oluşturulan raporlar. Kapsam yetkinize göre belirlenir." />
      <Tabs className="mb-4" value={tab} onChange={setTab} tabs={tabs} />
      {(tab === 'tickets' || tab === 'workflows') && (
        <div className="card mb-5 grid gap-3 p-4 sm:grid-cols-2 lg:grid-cols-6">
          <label><span className="label">Başlangıç</span><input type="date" className="input" value={f.from} onChange={(e) => setF({ ...f, from: e.target.value })} /></label>
          <label><span className="label">Bitiş</span><input type="date" className="input" value={f.to} onChange={(e) => setF({ ...f, to: e.target.value })} /></label>
          <label className="lg:col-span-2"><span className="label">Departman</span><select className="input" value={f.departmentId} onChange={(e) => setF({ ...f, departmentId: e.target.value })}><option value="">Tümü</option><DepartmentOptions departments={departments.data} /></select></label>
          {tab === 'tickets' && <label><span className="label">Kategori</span><select className="input" value={f.categoryId} onChange={(e) => setF({ ...f, categoryId: e.target.value })}><option value="">Tümü</option>{categories.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>}
          {tab === 'tickets' && <label><span className="label">Durum</span><select className="input" value={f.status} onChange={(e) => setF({ ...f, status: e.target.value })}><option value="">Tümü</option><option value="Open">Açık</option><option value="InProgress">İşlemde</option><option value="Pending">Beklemede</option><option value="Resolved">Çözüldü</option><option value="Closed">Kapandı</option></select></label>}
        </div>
      )}
      {tab === 'tickets' && <TicketsReport params={params} />}
      {tab === 'workflows' && <WorkflowsReport params={params} />}
      {tab === 'inventory' && <InventoryReportView />}
      {tab === 'people' && <PeopleReportView />}
    </div>
  )
}

const loading = <div className="grid gap-4 md:grid-cols-4">{[0, 1, 2, 3].map((i) => <Skeleton key={i} className="h-24" />)}<Skeleton className="h-72 md:col-span-4" /></div>

function TicketsReport({ params }: { params: Record<string, string | undefined> }) {
  const q = useQuery({ queryKey: ['report-tickets', params], queryFn: () => api.get<TicketReport>('/reports/tickets', params) })
  return (
    <QueryState query={q} skeleton={loading}>
      {(r) => (
        <div className="space-y-6">
          <div className="flex items-center justify-between"><Badge tone="violet">{r.scope}</Badge><button className="btn-secondary btn-sm" onClick={() => api.download(`/reports/tickets/export?${new URLSearchParams(Object.entries(params).filter(([, v]) => v) as [string, string][])}`, 'talep-raporu.csv')}><Download className="h-3.5 w-3.5" /> CSV dışa aktar</button></div>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
            <StatCard label="Toplam talep" value={fmt.number(r.total)} icon={<Inbox className="h-4 w-4" />} />
            <StatCard label="Açık" value={fmt.number(r.open)} icon={<Timer className="h-4 w-4" />} tone="blue" />
            <StatCard label="Çözülen" value={fmt.number(r.resolved)} icon={<CheckCircle2 className="h-4 w-4" />} tone="green" />
            <StatCard label="SLA aşımı (açık)" value={fmt.number(r.overdue)} icon={<AlarmClock className="h-4 w-4" />} tone="red" />
            <StatCard label="SLA uyumu" value={`%${r.slaCompliance}`} hint={`Ort. çözüm ${fmt.hours(r.avgResolutionHours)}`} icon={<Gauge className="h-4 w-4" />} tone="amber" />
          </div>
          <Card title="Günlük oluşturulan / çözülen"><TrendChart data={r.trend} height={260} /></Card>
          <div className="grid gap-6 lg:grid-cols-3">
            <Card title="Kategori"><DonutChart data={r.byCategory} height={160} /></Card>
            <Card title="Öncelik"><ColumnChart data={r.byPriority.map((p) => ({ ...p, color: ({ Low: '#94a3b8', Medium: '#0ea5e9', High: '#f59e0b', Critical: '#ef4444' } as Record<string, string>)[p.key] }))} height={200} /></Card>
            <Card title="Açık taleplerin yaşı"><BarList data={r.aging} color="#f59e0b" /></Card>
          </div>
          <div className="grid gap-6 lg:grid-cols-2">
            <Card title="Departmana göre (ilk 15)"><BarList data={r.byDepartment} /></Card>
            <Card title="Destek grubuna göre"><BarList data={r.byGroup} color="#6366f1" /></Card>
          </div>
          <Card title="Destek personeli performansı" bodyClass="">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[640px] text-sm">
                <thead className="bg-ink-50/70 dark:bg-white/[0.02]"><tr><th className="th">Personel</th><th className="th">Atanan</th><th className="th">Çözülen</th><th className="th">Ort. çözüm</th><th className="th w-56">SLA uyumu</th></tr></thead>
                <tbody>{r.agents.map((a) => (
                  <tr key={a.id} className="tr"><td className="td"><PersonCell name={a.fullName} /></td><td className="td">{a.assigned}</td><td className="td">{a.resolved}</td><td className="td">{fmt.hours(a.avgResolutionHours)}</td>
                    <td className="td"><div className="flex items-center gap-2"><Progress value={a.slaCompliance} tone={a.slaCompliance >= 85 ? 'green' : 'amber'} /><span className="w-12 text-xs font-semibold">%{a.slaCompliance}</span></div></td></tr>
                ))}</tbody>
              </table>
            </div>
          </Card>
        </div>
      )}
    </QueryState>
  )
}

function WorkflowsReport({ params }: { params: Record<string, string | undefined> }) {
  const q = useQuery({ queryKey: ['report-workflows', params], queryFn: () => api.get<WorkflowReport>('/reports/workflows', { from: params.from, to: params.to, departmentId: params.departmentId }) })
  return (
    <QueryState query={q} skeleton={loading}>
      {(r) => (
        <div className="space-y-6">
          <Badge tone="violet">{r.scope}</Badge>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
            <StatCard label="Toplam başvuru" value={r.total} /><StatCard label="Onay bekleyen" value={r.pending} tone="amber" /><StatCard label="Onaylanan" value={r.approved} tone="blue" />
            <StatCard label="Tamamlanan" value={r.completed} tone="green" /><StatCard label="Ort. karar süresi" value={fmt.hours(r.avgDecisionHours)} hint={`${r.rejected} ret`} tone="red" />
          </div>
          <Card title="Günlük başvuru / sonuçlanan"><TrendChart data={r.trend} labels={{ created: 'Başvuru', resolved: 'Sonuçlanan' }} /></Card>
          <div className="grid gap-6 lg:grid-cols-2">
            <Card title="Modüllere göre"><BarList data={r.byModule} color="#6366f1" /></Card>
            <Card title="Departmanlara göre"><BarList data={r.byDepartment} /></Card>
          </div>
          <Card title="Form bazında" bodyClass="">
            <div className="overflow-x-auto"><table className="w-full min-w-[720px] text-sm">
              <thead className="bg-ink-50/70 dark:bg-white/[0.02]"><tr><th className="th">Form</th><th className="th">Modül</th><th className="th">Toplam</th><th className="th">Bekleyen</th><th className="th">Onay</th><th className="th">Ret</th><th className="th">Tamam</th><th className="th">Ort. karar</th></tr></thead>
              <tbody>{r.byForm.map((x) => <tr key={x.key} className="tr"><td className="td font-medium">{x.name}</td><td className="td muted">{x.module}</td><td className="td font-semibold">{x.total}</td><td className="td">{x.pending}</td><td className="td">{x.approved}</td><td className="td">{x.rejected}</td><td className="td">{x.completed}</td><td className="td">{fmt.hours(x.avgDecisionHours)}</td></tr>)}</tbody>
            </table></div>
          </Card>
        </div>
      )}
    </QueryState>
  )
}

function InventoryReportView() {
  const q = useQuery({ queryKey: ['report-inventory'], queryFn: () => api.get<InventoryReport>('/reports/inventory') })
  return (
    <QueryState query={q} skeleton={loading}>
      {(r) => (
        <div className="space-y-6">
          <div className="grid gap-4 sm:grid-cols-3"><StatCard label="Toplam varlık" value={r.total} /><StatCard label="Envanter değeri (hurda hariç)" value={fmt.money(r.totalValue)} tone="green" /><StatCard label="90 gün içinde garanti bitişi" value={r.warrantyExpiring.length} tone="amber" /></div>
          <div className="grid gap-6 lg:grid-cols-3">
            <Card title="Türe göre"><DonutChart data={r.byType.map((t) => ({ ...t, label: assetTypeLabel[t.key as AssetType] ?? t.label }))} height={150} /></Card>
            <Card title="Birimlere göre" className="lg:col-span-2"><BarList data={r.byDepartment} /></Card>
          </div>
          <Card title="Lisans kullanımı" bodyClass="">
            <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">{r.licenses.map((l) => <li key={l.name} className="flex items-center gap-4 px-5 py-3 text-sm"><span className="w-56 truncate font-medium">{l.name}</span><Progress value={(l.used / l.seats) * 100} tone={l.used / l.seats > 0.9 ? 'amber' : 'brand'} /><span className="w-20 text-right text-xs font-semibold">{l.used}/{l.seats}</span><span className="w-28 text-right text-xs muted">{fmt.date(l.expiresAt)}</span></li>)}</ul>
          </Card>
        </div>
      )}
    </QueryState>
  )
}

function PeopleReportView() {
  const q = useQuery({ queryKey: ['report-people'], queryFn: () => api.get<PeopleReport>('/reports/people') })
  return (
    <QueryState query={q} skeleton={loading}>
      {(r) => (
        <div className="space-y-6">
          <div className="grid gap-4 sm:grid-cols-4"><StatCard label="Aktif çalışan" value={r.active} /><StatCard label="Pasif hesap" value={r.inactive} tone="gray" /><StatCard label="Son 90 günde işe giren" value={r.newHires90} tone="green" /><StatCard label="İlk parolasını belirlemeyen" value={r.mustChangePassword} tone="amber" /></div>
          <div className="grid gap-6 lg:grid-cols-3">
            <Card title="Organizasyon"><DonutChart data={r.byDepartmentType} height={150} /></Card>
            <Card title="Çalışma türü"><DonutChart data={r.byEmploymentType} height={150} /></Card>
            <Card title="En kalabalık birimler"><BarList data={r.byDepartment.slice(0, 8)} /></Card>
          </div>
        </div>
      )}
    </QueryState>
  )
}
