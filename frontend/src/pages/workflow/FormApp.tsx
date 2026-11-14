import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, CalendarRange, CircleDollarSign, GitBranch, Repeat, Settings2, Store, TrendingUp } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { BarList, MoneyTrendChart } from '@/components/charts'
import { Avatar, Badge, Card, EmptyState, ErrorState, Progress, Skeleton, StatCard } from '@/components/ui'
import { api, ApiError, errorMessage } from '@/lib/api'
import { fmt, submissionMeta } from '@/lib/format'
import { revenueBreakdown } from '@/lib/formSchema'
import { DynamicIcon } from '@/lib/icon'
import type { FormDefinition, LeaveCalendarEntry, Paged, RevenueSummary, ShiftChangeEntry, SubmissionDetail, SubmissionListItem } from '@/lib/types'
import { cn, tint } from '@/lib/utils'
import { DynamicForm } from './DynamicForm'

const approverLabel: Record<string, string> = { Manager: 'Yöneticiniz', DepartmentManager: 'Departman yöneticisi', Role: 'Rol', User: 'Belirli kişi', ResponsibleDepartment: 'Sorumlu birim', FieldUser: 'Formda seçtiğiniz kişi (boşsa atlanır)' }

/** Ciro formu: nakit + kart toplamı ve ortalama sepet önizlemesi (sunucu aynı kuralı uygular). */
function RevenuePreview({ v }: { v: Record<string, unknown> }) {
  const { hasSplit, sum, mismatch, averageBasket } = revenueBreakdown(v)
  if (!hasSplit && averageBasket == null) return null
  return (
    <div role="status" className={cn('flex flex-wrap gap-x-6 gap-y-1 rounded-lg p-3 text-sm', mismatch ? 'bg-amber-50 text-amber-800 dark:bg-amber-400/10 dark:text-amber-200' : 'bg-ink-50 dark:bg-white/[0.03]')}>
      {hasSplit && <span>Nakit + kart: <b>{fmt.money(sum)}</b>{mismatch && ' — toplam ciro ile eşleşmiyor'}</span>}
      {averageBasket != null && <span>Ortalama sepet: <b>{fmt.money(averageBasket)}</b></span>}
    </div>
  )
}

function RevenuePanel({ data }: { data: RevenueSummary }) {
  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-4">
        <StatCard label="Onaylı ciro (30 gün)" value={fmt.money(data.total)} icon={<CircleDollarSign className="h-5 w-5" />} />
        <StatCard label="Bildirim" value={fmt.number(data.reports)} tone="blue" />
        <StatCard label="Ortalama sepet" value={fmt.money(data.averageBasket)} tone="green" />
        <StatCard label="Kontrol bekleyen" value={fmt.number(data.pendingCount)} tone="amber" />
      </div>
      <Card title={<span className="inline-flex items-center gap-2"><TrendingUp className="h-4 w-4" /> Günlük ciro {data.companyWide ? '(tüm mağazalar)' : '(bildirimleriniz)'}</span>}>
        {data.reports === 0 ? <EmptyState title="Son 30 günde onaylı ciro bildirimi yok" description="Bildirimler Mağaza Muhasebesi onayından sonra burada görünür." /> : <MoneyTrendChart data={data.byDay} />}
      </Card>
    </div>
  )
}

/** İş günü hesabı (hafta sonu hariç; resmî tatiller sunucuda ayrıca düşülür) */
function weekdays(a?: unknown, b?: unknown) {
  if (typeof a !== 'string' || typeof b !== 'string' || !a || !b) return 0
  const s = new Date(a), e = new Date(b)
  if (e < s) return 0
  let n = 0
  for (const d = new Date(s); d <= e; d.setDate(d.getDate() + 1)) if (d.getDay() !== 0 && d.getDay() !== 6) n++
  return n
}

export default function FormApp() {
  const { key } = useParams()
  const nav = useNavigate()
  const qc = useQueryClient()
  const form = useQuery({ queryKey: ['form', key], queryFn: () => api.get<FormDefinition>(`/forms/${key}`) })
  const mine = useQuery({ queryKey: ['submissions', 'mine', key], queryFn: () => api.get<Paged<SubmissionListItem>>('/submissions', { scope: 'mine', formKey: key, pageSize: 8 }) })
  const calendar = useQuery({ queryKey: ['leave-calendar'], queryFn: () => api.get<LeaveCalendarEntry[]>('/leave/calendar'), enabled: form.data?.kind === 'leave' })
  const revenue = useQuery({ queryKey: ['revenue-summary'], queryFn: () => api.get<RevenueSummary>('/revenue/summary', { days: 30 }), enabled: form.data?.kind === 'revenue' })
  const shifts = useQuery({ queryKey: ['shifts'], queryFn: () => api.get<ShiftChangeEntry[]>('/shifts'), enabled: form.data?.kind === 'shift' })
  const [serverErrors, setServerErrors] = useState<Record<string, string[]> | null>(null)
  const submit = useMutation({
    mutationFn: (data: Record<string, unknown>) => api.send<SubmissionDetail>('post', `/forms/${key}/submissions`, { data }),
    onSuccess: ({ data, message }) => {
      toast.success(message ?? 'Başvurunuz oluşturuldu.')
      qc.invalidateQueries({ queryKey: ['submissions'] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
      qc.invalidateQueries({ queryKey: ['revenue-summary'] })
      qc.invalidateQueries({ queryKey: ['shifts'] })
      nav(`/submissions/${data.id}`)
    },
    onError: (e) => { setServerErrors(e instanceof ApiError ? e.errors ?? null : null); toast.error(errorMessage(e)) },
  })

  if (form.isLoading) return <div className="grid gap-6 lg:grid-cols-3"><Skeleton className="h-[480px] lg:col-span-2" /><Skeleton className="h-80" /></div>
  if (form.error || !form.data) return <ErrorState error={form.error} onRetry={form.refetch} />
  const f = form.data
  const ctx = f.context

  return (
    <div>
      <Link to="/apps" className="mb-3 inline-flex items-center gap-1.5 text-sm muted hover:text-ink-900 dark:hover:text-white"><ArrowLeft className="h-4 w-4" /> Uygulama Merkezi</Link>
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-4">
          <span className="grid h-12 w-12 place-items-center rounded-2xl" style={{ background: tint(f.color, 0.12), color: f.color }}><DynamicIcon name={f.icon} className="h-6 w-6" /></span>
          <div>
            <div className="text-xs font-semibold uppercase tracking-wider muted">{f.module}</div>
            <h1 className="text-2xl font-bold">{f.name}</h1>
            <p className="text-sm muted">{f.description}</p>
          </div>
        </div>
        {f.canManage && <Link to={`/admin/forms?edit=${f.id}`} className="btn-secondary"><Settings2 className="h-4 w-4" /> Formu düzenle</Link>}
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          {f.kind === 'leave' && ctx && (
            <div className="grid gap-4 sm:grid-cols-3">
              <div className="card p-4"><div className="text-xs muted">Kullanılabilir yıllık izin</div><div className="mt-1 font-display text-2xl font-bold text-emerald-600 dark:text-emerald-400">{fmt.number(ctx.leaveRemaining, 1)} gün</div><Progress className="mt-2" tone="green" value={ctx.leaveEntitled ? ((ctx.leaveRemaining ?? 0) / ctx.leaveEntitled) * 100 : 0} /></div>
              <div className="card p-4"><div className="text-xs muted">Hak edilen (devreden dahil)</div><div className="mt-1 font-display text-2xl font-bold text-ink-900 dark:text-white">{fmt.number(ctx.leaveEntitled, 1)}</div></div>
              <div className="card p-4"><div className="text-xs muted">Kullanılan / onay bekleyen</div><div className="mt-1 font-display text-2xl font-bold text-ink-900 dark:text-white">{fmt.number(ctx.leaveUsed, 1)} / {fmt.number(ctx.leavePending, 1)}</div></div>
            </div>
          )}
          {(f.kind === 'expense' || f.kind === 'advance') && ctx?.limit != null && (
            <div className="card flex items-center gap-3 p-4"><CircleDollarSign className="h-5 w-5 text-brand-600" /><span className="text-sm">{ctx.limitLabel}: <b className="text-ink-900 dark:text-white">{fmt.money(ctx.limit)}</b>. Bu tutarı aşan başvurular sistem tarafından kabul edilmez.</span></div>
          )}
          {f.kind === 'revenue' && (revenue.isLoading ? <Skeleton className="h-72" /> : revenue.data && <RevenuePanel data={revenue.data} />)}
          {f.kind === 'shift' && (
            <div className="card flex items-start gap-3 p-4 text-sm"><Repeat className="mt-0.5 h-5 w-5 shrink-0 text-brand-600" />
              <span>Takas yapacağınız kişiyi seçerseniz talep önce <b>onun onayına</b>, ardından yöneticinize gider. İzinli olduğunuz günler ve aynı güne ikinci talep sistem tarafından engellenir.</span>
            </div>
          )}
          <Card title="Başvuru formu">
            <DynamicForm fields={f.fields} serverErrors={serverErrors} submitting={submit.isPending} onSubmit={(d) => { setServerErrors(null); submit.mutate(d) }}
              extra={f.kind === 'leave' ? (v) => {
                const n = weekdays(v.startDate, v.endDate)
                return n > 0 ? <div className="rounded-lg bg-ink-50 p-3 text-sm dark:bg-white/[0.03]">Tahmini süre: <b>{n} iş günü</b> <span className="muted">(resmî tatiller sunucuda ayrıca düşülür)</span></div> : null
              } : f.kind === 'revenue' ? (v) => <RevenuePreview v={v} /> : undefined} />
          </Card>
        </div>

        <aside className="space-y-6">
          <Card title={<span className="inline-flex items-center gap-2"><GitBranch className="h-4 w-4" /> Onay akışı</span>}>
            {f.steps.length === 0 ? <p className="text-sm muted">Bu form onay gerektirmez.</p> : (
              <ol className="space-y-3">
                {f.steps.map((s, i) => (
                  <li key={i} className="flex gap-3">
                    <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-ink-100 text-xs font-bold dark:bg-white/10">{i + 1}</span>
                    <span><span className="block text-sm font-medium text-ink-900 dark:text-white">{s.name}</span><span className="block text-xs muted">{approverLabel[s.approverType]}{s.roleCode ? ` · ${s.roleCode}` : ''}</span></span>
                  </li>
                ))}
              </ol>
            )}
            {f.ownerDepartment && <p className="mt-4 border-t border-ink-100 pt-3 text-xs muted dark:border-white/[0.06]">Sorumlu birim: <b className="text-ink-700 dark:text-ink-200">{f.ownerDepartment.name}</b></p>}
          </Card>
          {f.kind === 'leave' && (
            <Card title={<span className="inline-flex items-center gap-2"><CalendarRange className="h-4 w-4" /> Ekip izin takvimi</span>} bodyClass="">
              {calendar.data?.length === 0 && <EmptyState title="Yaklaşan izin yok" />}
              <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">
                {calendar.data?.slice(0, 8).map((l) => (
                  <li key={l.submissionId} className="flex items-center gap-3 px-5 py-2.5">
                    <Avatar name={l.user.fullName} size={28} />
                    <span className="min-w-0 flex-1"><span className="block truncate text-sm font-medium text-ink-900 dark:text-white">{l.user.fullName}</span><span className="block text-xs muted">{fmt.date(l.start)} – {fmt.date(l.end)} · {l.days} gün</span></span>
                    <Badge tone={submissionMeta[l.status].tone}>{l.status === 'Pending' ? 'Bekliyor' : 'Onaylı'}</Badge>
                  </li>
                ))}
              </ul>
            </Card>
          )}
          {f.kind === 'revenue' && revenue.data && revenue.data.byStore.length > 0 && (
            <Card title={<span className="inline-flex items-center gap-2"><Store className="h-4 w-4" /> En yüksek cirolu mağazalar</span>}>
              <BarList color="#6366f1" data={revenue.data.byStore.map((s) => ({ key: s.store, label: s.store, value: s.amount }))} />
              <p className="mt-3 text-xs muted">Son 30 gün, onaylı bildirimler.</p>
            </Card>
          )}
          {f.kind === 'shift' && (
            <Card title={<span className="inline-flex items-center gap-2"><Repeat className="h-4 w-4" /> Ekip vardiya değişiklikleri</span>} bodyClass="">
              {shifts.data?.length === 0 && <EmptyState title="Önümüzdeki 30 günde değişiklik yok" />}
              <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">
                {shifts.data?.slice(0, 10).map((s) => (
                  <li key={s.submissionId} className="flex items-center gap-3 px-5 py-2.5">
                    <Avatar name={s.user.fullName} size={28} />
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium text-ink-900 dark:text-white">{s.user.fullName}</span>
                      <span className="block truncate text-xs muted">{fmt.date(s.date)} · {s.from} → {s.to}{s.swapWith && ` · ${s.swapWith}`}</span>
                    </span>
                    <Badge tone={submissionMeta[s.status].tone}>{s.status === 'Pending' ? 'Bekliyor' : 'Onaylı'}</Badge>
                  </li>
                ))}
              </ul>
            </Card>
          )}
          <Card title="Bu formdaki başvurularım" bodyClass="">
            {mine.data?.items.length === 0 && <EmptyState title="Henüz başvurunuz yok" />}
            <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">
              {mine.data?.items.map((s) => (
                <li key={s.id}><Link to={`/submissions/${s.id}`} className="block px-5 py-3 hover:bg-ink-50/70 dark:hover:bg-white/[0.02]">
                  <div className="flex items-center justify-between gap-2"><span className="text-xs font-semibold muted">{s.number}</span><Badge tone={submissionMeta[s.status].tone}>{submissionMeta[s.status].label}</Badge></div>
                  <div className="mt-1 truncate text-sm text-ink-900 dark:text-white">{s.summary}</div>
                  <div className="text-xs muted">{fmt.relative(s.createdAt)}{s.currentStepName && ` · ${s.currentStepName}`}</div>
                </Link></li>
              ))}
            </ul>
          </Card>
        </aside>
      </div>
    </div>
  )
}
