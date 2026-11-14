import { useQuery } from '@tanstack/react-query'
import { AlarmClock, AppWindow, Boxes, Cake, CalendarDays, ClipboardCheck, FileStack, Inbox, LifeBuoy, ListChecks, Megaphone, Phone, Pin, Plus, ShieldAlert, Trophy, UserPlus, Users, Wallet } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { DonutChart, TrendChart } from '@/components/charts'
import { Avatar, Badge, Card, EmptyState, ErrorState, Progress, Skeleton, StatCard } from '@/components/ui'
import { api } from '@/lib/api'
import { fmt, priorityMeta, statusMeta, taskStatusMeta } from '@/lib/format'
import type { Dashboard as D } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useAuth } from '@/store/auth'

function greeting() {
  const h = new Date().getHours()
  return h < 6 ? 'İyi geceler' : h < 12 ? 'Günaydın' : h < 18 ? 'İyi günler' : 'İyi akşamlar'
}

export default function Dashboard() {
  const user = useAuth((s) => s.user)!
  const nav = useNavigate()
  const q = useQuery({ queryKey: ['dashboard'], queryFn: () => api.get<D>('/dashboard'), refetchInterval: 60_000 })

  if (q.isLoading)
    return (
      <div className="space-y-6">
        <Skeleton className="h-28 rounded-2xl" />
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">{[0, 1, 2, 3].map((i) => <Skeleton key={i} className="h-24" />)}</div>
        <div className="grid gap-6 xl:grid-cols-3"><Skeleton className="h-80 xl:col-span-2" /><Skeleton className="h-80" /></div>
      </div>
    )
  if (q.error || !q.data) return <ErrorState error={q.error} onRetry={q.refetch} />
  const d = q.data
  const leaveTotal = d.leave.entitled + d.leave.carriedOver

  return (
    <div className="space-y-6">
      {/* Karşılama ve profil şeridi */}
      <section className="card relative overflow-hidden p-6">
        <div className="absolute -top-24 -right-16 h-64 w-64 rounded-full bg-brand-500/10 blur-3xl dark:bg-brand-500/15" />
        <div className="relative flex flex-col gap-5 md:flex-row md:items-center md:justify-between">
          <div className="flex items-center gap-4">
            <Avatar name={d.profile.fullName} size={56} />
            <div>
              <p className="text-sm muted">{greeting()},</p>
              <h1 className="text-2xl font-bold">{user.firstName} {user.lastName}</h1>
              <p className="mt-0.5 text-sm muted">{d.profile.jobTitle} · {d.profile.department}{d.profile.manager && <> · Yönetici: {d.profile.manager}</>}</p>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <Link to="/tickets/new" className="btn-primary"><Plus className="h-4 w-4" /> Destek talebi</Link>
            <Link to="/apps/leave" className="btn-secondary"><CalendarDays className="h-4 w-4" /> İzin talebi</Link>
            <Link to="/apps" className="btn-secondary"><AppWindow className="h-4 w-4" /> Uygulamalar</Link>
          </div>
        </div>
      </section>

      {/* Kişisel özet */}
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <div className="card p-4">
          <div className="flex items-start justify-between">
            <div><div className="text-xs font-medium muted">Yıllık izin bakiyesi</div><div className="mt-1.5 font-display text-2xl font-bold text-ink-900 dark:text-white">{fmt.number(d.leave.remaining, 1)} <span className="text-sm font-medium muted">/ {fmt.number(leaveTotal, 1)} gün</span></div></div>
            <div className="grid h-9 w-9 place-items-center rounded-lg bg-emerald-50 text-emerald-700 dark:bg-emerald-400/10 dark:text-emerald-300"><Wallet className="h-4 w-4" /></div>
          </div>
          <Progress className="mt-3" tone="green" value={leaveTotal ? (d.leave.remaining / leaveTotal) * 100 : 0} />
          <div className="mt-2 text-xs muted">Kullanılan {fmt.number(d.leave.used, 1)} · Onay bekleyen {fmt.number(d.leave.pending, 1)}</div>
        </div>
        <StatCard label="Açık taleplerim" value={d.myOpenTickets} hint={d.myTicketsAwaitingConfirmation > 0 ? `${d.myTicketsAwaitingConfirmation} talep onayınızı bekliyor` : 'Destek talepleriniz'} icon={<LifeBuoy className="h-4 w-4" />} tone="blue" onClick={() => nav('/tickets?scope=mine')} />
        <StatCard label="Onayımı bekleyenler" value={d.approvalsPending} hint={d.assignedToComplete > 0 ? `${d.assignedToComplete} başvuru tamamlanmayı bekliyor` : 'Form ve iş akışı onayları'} icon={<ClipboardCheck className="h-4 w-4" />} tone={d.approvalsPending > 0 ? 'amber' : 'gray'} onClick={() => nav('/approvals')} />
        <StatCard label="Bekleyen başvurularım" value={d.myPendingRequests} hint="İzin, masraf, talep formları" icon={<FileStack className="h-4 w-4" />} tone="violet" onClick={() => nav('/requests')} />
      </div>

      {/* Yetkiye göre operasyonel kartlar */}
      {d.support && (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
          <StatCard label="Kuyruktaki açık talepler" value={d.support.queueOpen} icon={<Inbox className="h-4 w-4" />} onClick={() => nav('/tickets?scope=active')} />
          <StatCard label="Atanmamış" value={d.support.unassigned} icon={<Users className="h-4 w-4" />} tone="amber" onClick={() => nav('/tickets?scope=unassigned')} />
          <StatCard label="SLA aşımı" value={d.support.overdue} icon={<AlarmClock className="h-4 w-4" />} tone="red" onClick={() => nav('/tickets?scope=overdue')} />
          <StatCard label="Bana atanan" value={d.support.assignedToMe} icon={<LifeBuoy className="h-4 w-4" />} tone="violet" onClick={() => nav('/tickets?scope=assigned')} />
          <StatCard label="Gruplarımın talepleri" value={d.support.myGroups} icon={<Users className="h-4 w-4" />} tone="cyan" onClick={() => nav('/tickets?scope=mygroups')} />
        </div>
      )}

      <div className="grid gap-6 xl:grid-cols-3">
        <div className="space-y-6 xl:col-span-2">
          {d.ticketStats && (
            <Card title="Talep eğilimi (son 14 gün)" actions={<span className="text-xs muted">{d.ticketStats.scope}</span>}>
              <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
                <div><div className="text-xs muted">SLA uyumu</div><div className="font-display text-xl font-bold text-ink-900 dark:text-white">%{d.ticketStats.slaCompliance}</div></div>
                <div><div className="text-xs muted">Ort. çözüm süresi</div><div className="font-display text-xl font-bold text-ink-900 dark:text-white">{fmt.hours(d.ticketStats.avgResolutionHours)}</div></div>
                <div className="flex items-center gap-2 text-xs"><span className="h-2 w-2 rounded-full bg-brand-500" /> Oluşturulan</div>
                <div className="flex items-center gap-2 text-xs"><span className="h-2 w-2 rounded-full bg-emerald-500" /> Çözülen</div>
              </div>
              <TrendChart data={d.ticketStats.trend} height={230} />
            </Card>
          )}

          <Card title="Son talepler" actions={<Link to="/tickets" className="text-xs font-semibold text-brand-600 dark:text-brand-400">Tümü →</Link>} bodyClass="">
            {d.recentTickets.length === 0 ? (
              <EmptyState title="Henüz talebiniz yok" description="Bir sorun yaşadığınızda destek ekibine talep açabilirsiniz." action={<Link className="btn-primary" to="/tickets/new">Talep aç</Link>} />
            ) : (
              <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">
                {d.recentTickets.map((t) => (
                  <li key={t.id}>
                    <Link to={`/tickets/${t.id}`} className="flex items-center gap-3 px-5 py-3 hover:bg-ink-50/70 dark:hover:bg-white/[0.02]">
                      <span className="w-12 shrink-0 text-xs font-semibold muted">#{t.id}</span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate font-medium text-ink-900 dark:text-white">{t.title}</span>
                        <span className="block truncate text-xs muted">{t.requester.fullName} · {t.categoryName} · {fmt.relative(t.updatedAt)}</span>
                      </span>
                      {t.isOverdue && <Badge tone="red">SLA</Badge>}
                      <Badge tone={priorityMeta[t.priority].tone}>{priorityMeta[t.priority].label}</Badge>
                      <Badge tone={statusMeta[t.status].tone} dot>{statusMeta[t.status].label}</Badge>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </Card>

          <div className="grid gap-6 lg:grid-cols-2">
            <Card title="Görevlerim" actions={<Link to="/tasks" className="text-xs font-semibold text-brand-600 dark:text-brand-400">Panoya git →</Link>} bodyClass="">
              {d.tasks.length === 0 ? <EmptyState icon={<ListChecks className="h-5 w-5" />} title="Açık göreviniz yok" /> : (
                <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">
                  {d.tasks.map((t) => (
                    <li key={t.id} className="flex items-center gap-3 px-5 py-3">
                      <span className={cn('h-2 w-2 shrink-0 rounded-full', t.isOverdue ? 'bg-red-500' : 'bg-ink-300 dark:bg-ink-600')} />
                      <span className="min-w-0 flex-1"><span className="block truncate text-sm font-medium text-ink-900 dark:text-white">{t.title}</span><span className="block text-xs muted">{t.projectName ?? 'Kişisel'} · {t.dueDate ? fmt.date(t.dueDate) : 'Tarihsiz'}</span></span>
                      <Badge tone={taskStatusMeta[t.status].tone}>{taskStatusMeta[t.status].label}</Badge>
                    </li>
                  ))}
                </ul>
              )}
            </Card>
            <Card title="Duyurular" actions={<Link to="/announcements" className="text-xs font-semibold text-brand-600 dark:text-brand-400">Tümü →</Link>} bodyClass="">
              {d.announcements.length === 0 ? <EmptyState icon={<Megaphone className="h-5 w-5" />} title="Duyuru yok" /> : (
                <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">
                  {d.announcements.map((a) => (
                    <li key={a.id} className="px-5 py-3">
                      <div className="flex items-center gap-2">{a.isPinned && <Pin className="h-3.5 w-3.5 text-brand-500" />}<span className="truncate text-sm font-semibold text-ink-900 dark:text-white">{a.title}</span></div>
                      <p className="mt-0.5 line-clamp-2 text-xs muted">{a.summary}</p>
                      <div className="mt-1 text-[11px] text-ink-400">{a.kind === 'News' ? 'Haber' : 'Duyuru'} · {fmt.relative(a.publishAt)}</div>
                    </li>
                  ))}
                </ul>
              )}
            </Card>
          </div>
        </div>

        <div className="space-y-6">
          {d.ticketStats && (
            <Card title="Durum dağılımı"><DonutChart data={d.ticketStats.byStatus.map((s) => ({ ...s, color: ({ Open: '#0ea5e9', InProgress: '#8b5cf6', Pending: '#f59e0b', Resolved: '#10b981', Closed: '#94a3b8' } as Record<string, string>)[s.key] }))} height={150} /></Card>
          )}
          {d.team && (
            <Card title="Ekibim">
              <div className="grid grid-cols-3 gap-3 text-center">
                <div><div className="font-display text-xl font-bold text-ink-900 dark:text-white">{d.team.teamSize}</div><div className="text-xs muted">Bağlı çalışan</div></div>
                <div><div className="font-display text-xl font-bold text-ink-900 dark:text-white">{d.team.onLeaveToday}</div><div className="text-xs muted">Bugün izinde</div></div>
                <div><div className="font-display text-xl font-bold text-ink-900 dark:text-white">{d.team.pendingApprovals}</div><div className="text-xs muted">Onay bekleyen</div></div>
              </div>
              {d.team.onLeave.length > 0 && <div className="mt-4 flex -space-x-2">{d.team.onLeave.map((u) => <Avatar key={u.id} name={u.fullName} size={28} />)}</div>}
            </Card>
          )}
          {d.admin && (
            <Card title="Hesap yönetimi">
              <div className="space-y-2.5 text-sm">
                <Link to="/admin/account-requests" className="flex items-center justify-between rounded-lg px-1 hover:text-brand-600"><span className="flex items-center gap-2"><UserPlus className="h-4 w-4 text-ink-400" /> Bekleyen hesap talepleri</span><Badge tone={d.admin.pendingAccountRequests ? 'amber' : 'gray'}>{d.admin.pendingAccountRequests}</Badge></Link>
                <div className="flex items-center justify-between px-1"><span className="flex items-center gap-2"><ShieldAlert className="h-4 w-4 text-ink-400" /> İlk parolasını belirlemeyen</span><b>{d.admin.mustChangePassword}</b></div>
                <div className="flex items-center justify-between px-1"><span className="flex items-center gap-2"><Users className="h-4 w-4 text-ink-400" /> Aktif / pasif kullanıcı</span><b>{d.admin.activeUsers} / {d.admin.inactiveUsers}</b></div>
              </div>
            </Card>
          )}
          {d.inventory && (
            <Card title="Envanter özeti" actions={<Link to="/inventory" className="text-xs font-semibold text-brand-600 dark:text-brand-400">Envanter →</Link>}>
              <div className="grid grid-cols-2 gap-3 text-sm">
                {[['Toplam', d.inventory.total], ['Kullanımda', d.inventory.inUse], ['Stokta', d.inventory.inStock], ['Serviste', d.inventory.inRepair]].map(([l, v]) => (
                  <div key={l} className="rounded-lg bg-ink-50 p-3 dark:bg-white/[0.03]"><div className="text-xs muted">{l}</div><div className="font-display text-lg font-bold text-ink-900 dark:text-white">{v}</div></div>
                ))}
              </div>
              <div className="mt-3 space-y-1 text-xs">
                <div className="flex justify-between"><span className="muted">90 gün içinde garantisi bitecek</span><b className="text-amber-600 dark:text-amber-400">{d.inventory.warrantyExpiring}</b></div>
                <div className="flex justify-between"><span className="muted">60 gün içinde süresi dolacak lisans</span><b className="text-amber-600 dark:text-amber-400">{d.inventory.licensesExpiring}</b></div>
              </div>
            </Card>
          )}
          <Card title="Yaklaşan etkinlikler" actions={<Link to="/events" className="text-xs font-semibold text-brand-600 dark:text-brand-400">Tümü →</Link>} bodyClass="">
            {d.events.length === 0 ? <EmptyState icon={<CalendarDays className="h-5 w-5" />} title="Yaklaşan etkinlik yok" /> : (
              <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">
                {d.events.map((e) => (
                  <li key={e.id} className="flex gap-3 px-5 py-3">
                    <div className="grid w-11 shrink-0 place-items-center rounded-lg bg-brand-50 py-1 text-center dark:bg-brand-400/10">
                      <div className="text-[10px] font-semibold uppercase text-brand-600 dark:text-brand-300">{fmt.dayMonth(e.startsAt).split(' ')[1]?.slice(0, 3)}</div>
                      <div className="font-display text-lg leading-none font-bold text-brand-700 dark:text-brand-200">{new Date(e.startsAt).getDate()}</div>
                    </div>
                    <div className="min-w-0"><div className="truncate text-sm font-semibold text-ink-900 dark:text-white">{e.title}</div><div className="truncate text-xs muted">{fmt.time(e.startsAt)} · {e.location}</div></div>
                  </li>
                ))}
              </ul>
            )}
          </Card>
          <Card title="Doğum günleri" bodyClass="">
            {d.birthdays.length === 0 ? <EmptyState icon={<Cake className="h-5 w-5" />} title="Bu hafta doğum günü yok" /> : (
              <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">
                {d.birthdays.slice(0, 6).map((b) => (
                  <li key={b.id} className="flex items-center gap-3 px-5 py-2.5">
                    <Avatar name={b.fullName} size={30} />
                    <div className="min-w-0 flex-1"><div className="truncate text-sm font-medium text-ink-900 dark:text-white">{b.fullName}</div><div className="truncate text-xs muted">{b.department}</div></div>
                    <Badge tone={b.isToday ? 'brand' : 'gray'}>{b.isToday ? 'Bugün 🎂' : `${b.day}.${String(b.month).padStart(2, '0')}`}</Badge>
                  </li>
                ))}
              </ul>
            )}
          </Card>
          <Card title="Çalışan başarıları" actions={<Link to="/achievements" className="text-xs font-semibold text-brand-600 dark:text-brand-400">Tümü →</Link>} bodyClass="">
            {d.achievements.length === 0 ? <EmptyState icon={<Trophy className="h-5 w-5" />} title="Henüz kayıt yok" /> : (
              <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">
                {d.achievements.map((a) => (
                  <li key={a.id} className="flex items-center gap-3 px-5 py-2.5">
                    <Trophy className="h-4 w-4 shrink-0 text-amber-500" />
                    <div className="min-w-0"><div className="truncate text-sm font-medium text-ink-900 dark:text-white">{a.user.fullName}</div><div className="truncate text-xs muted">{a.title}</div></div>
                  </li>
                ))}
              </ul>
            )}
          </Card>
          {d.onCall.length > 0 && (
            <Card title="Şu an nöbetçi" bodyClass="">
              <ul className="divide-y divide-ink-100 dark:divide-white/[0.05]">
                {d.onCall.map((o) => (
                  <li key={o.id} className="flex items-center gap-3 px-5 py-2.5">
                    <Avatar name={o.user.fullName} size={30} />
                    <div className="min-w-0 flex-1"><div className="truncate text-sm font-medium text-ink-900 dark:text-white">{o.user.fullName}</div><div className="text-xs muted">{o.team}</div></div>
                    {o.phone && <a href={`tel:${o.phone.replace(/\s/g, '')}`} className="btn-ghost btn-sm"><Phone className="h-3.5 w-3.5" /> Ara</a>}
                  </li>
                ))}
              </ul>
            </Card>
          )}
          {d.inventory === undefined || d.inventory === null ? (
            <Link to="/inventory" className="card flex items-center gap-3 p-4 hover:border-brand-300/60"><Boxes className="h-5 w-5 text-brand-600" /><span className="text-sm font-medium">Zimmetimdeki cihazlar</span></Link>
          ) : null}
        </div>
      </div>
    </div>
  )
}
