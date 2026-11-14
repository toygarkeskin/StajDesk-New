import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { AlarmClock, Columns3, Download, MessageSquare, Paperclip, Plus, Rows3 } from 'lucide-react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Avatar, Badge, ColorDot, EmptyState, PageHeader, Pagination, QueryState, SearchInput, Segmented, Tabs } from '@/components/ui'
import { api } from '@/lib/api'
import { fmt, priorityMeta, statusMeta } from '@/lib/format'
import { useCategories, useDepartments, useGroups } from '@/lib/queries'
import { DepartmentOptions } from '@/components/DepartmentOptions'
import type { Paged, TicketListItem, TicketStatus } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useCan } from '@/store/auth'

const statuses: TicketStatus[] = ['Open', 'InProgress', 'Pending', 'Resolved', 'Closed']

export default function Tickets() {
  const [sp, setSp] = useSearchParams()
  const nav = useNavigate()
  const { can } = useCan()
  const staff = can('tickets.manage', 'tickets.view_all', 'tickets.view_department')
  const view = (sp.get('view') ?? 'list') as 'list' | 'board'
  const scope = sp.get('scope') ?? (staff ? 'active' : 'mine')
  const page = Number(sp.get('page') ?? 1)
  const set = (k: string, v?: string | null) => {
    const n = new URLSearchParams(sp)
    if (v) n.set(k, v); else n.delete(k)
    if (k !== 'page') n.delete('page')
    setSp(n, { replace: true })
  }
  const params = {
    scope, page, pageSize: view === 'board' ? 120 : 15, sort: sp.get('sort') ?? 'newest', search: sp.get('q') ?? undefined,
    status: sp.get('status') ?? undefined, priority: sp.get('priority') ?? undefined, categoryId: sp.get('categoryId') ?? undefined,
    groupId: sp.get('groupId') ?? undefined, departmentId: sp.get('departmentId') ?? undefined,
  }
  const q = useQuery({ queryKey: ['tickets', params], queryFn: () => api.get<Paged<TicketListItem>>('/tickets', params), placeholderData: keepPreviousData })
  const categories = useCategories()
  const groups = useGroups()
  const departments = useDepartments()

  const scopes = staff
    ? [{ value: 'active', label: 'Açık talepler' }, { value: 'assigned', label: 'Bana atanan' }, { value: 'mygroups', label: 'Gruplarım' }, { value: 'unassigned', label: 'Atanmamış' }, { value: 'overdue', label: 'SLA aşımı' }, { value: 'mine', label: 'Açtıklarım' }, { value: 'all', label: 'Tümü' }]
    : [{ value: 'mine', label: 'Taleplerim' }, { value: 'all', label: 'Tümü' }]

  const exportCsv = () => api.download(`/reports/tickets/export?${new URLSearchParams(Object.entries({ categoryId: params.categoryId, departmentId: params.departmentId, status: params.status }).filter(([, v]) => v) as [string, string][])}`, 'talepler.csv')

  return (
    <div>
      <PageHeader title="Destek talepleri" description={staff ? 'Yetki kapsamınızdaki tüm talepler. Filtreleyin, atayın ve takip edin.' : 'Açtığınız talepler ve durumları.'}
        actions={<>
          {can('reports.view', 'reports.view_department', 'tickets.view_all') && <button className="btn-secondary" onClick={exportCsv}><Download className="h-4 w-4" /> CSV</button>}
          <Segmented value={view} onChange={(v) => set('view', v === 'list' ? null : v)} options={[{ value: 'list', label: 'Liste', icon: <Rows3 className="h-3.5 w-3.5" /> }, { value: 'board', label: 'Pano', icon: <Columns3 className="h-3.5 w-3.5" /> }]} />
          <Link to="/tickets/new" className="btn-primary"><Plus className="h-4 w-4" /> Yeni talep</Link>
        </>} />

      <Tabs value={scope} onChange={(v) => set('scope', v)} tabs={scopes} />

      <div className="mt-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-6">
        <SearchInput className="lg:col-span-2" value={sp.get('q') ?? ''} onChange={(v) => set('q', v)} placeholder="Başlık, açıklama veya #numara…" />
        <select className="input" value={params.status ?? ''} onChange={(e) => set('status', e.target.value)} aria-label="Durum">
          <option value="">Tüm durumlar</option>{statuses.map((s) => <option key={s} value={s}>{statusMeta[s].label}</option>)}
        </select>
        <select className="input" value={params.priority ?? ''} onChange={(e) => set('priority', e.target.value)} aria-label="Öncelik">
          <option value="">Tüm öncelikler</option>{(['Critical', 'High', 'Medium', 'Low'] as const).map((p) => <option key={p} value={p}>{priorityMeta[p].label}</option>)}
        </select>
        <select className="input" value={params.categoryId ?? ''} onChange={(e) => set('categoryId', e.target.value)} aria-label="Kategori">
          <option value="">Tüm kategoriler</option>{categories.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        {staff ? (
          <select className="input" value={params.groupId ?? ''} onChange={(e) => set('groupId', e.target.value)} aria-label="Grup">
            <option value="">Tüm gruplar</option>{groups.data?.filter((g) => g.isSupportGroup).map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
          </select>
        ) : (
          <select className="input" value={params.sort} onChange={(e) => set('sort', e.target.value)} aria-label="Sıralama">
            <option value="newest">En yeni</option><option value="updated">Son güncellenen</option><option value="oldest">En eski</option>
          </select>
        )}
      </div>
      {staff && (
        <div className="mt-2 grid gap-2 sm:grid-cols-2 lg:grid-cols-6">
          <select className="input lg:col-span-2" value={params.departmentId ?? ''} onChange={(e) => set('departmentId', e.target.value)} aria-label="Departman">
            <option value="">Tüm departmanlar</option><DepartmentOptions departments={departments.data} />
          </select>
          <select className="input" value={params.sort} onChange={(e) => set('sort', e.target.value)} aria-label="Sıralama">
            <option value="newest">En yeni</option><option value="updated">Son güncellenen</option><option value="priority">Öncelik</option><option value="due">SLA hedefi</option><option value="oldest">En eski</option>
          </select>
        </div>
      )}

      <div className="mt-4">
        <QueryState query={q} empty={(d) => d.items.length === 0 && (
          <div className="card"><EmptyState title="Talep bulunamadı" description="Filtreleri değiştirmeyi deneyin veya yeni bir talep oluşturun." action={<Link to="/tickets/new" className="btn-primary">Yeni talep</Link>} /></div>
        )}>
          {(d) => view === 'board' ? <Board items={d.items} onOpen={(id) => nav(`/tickets/${id}`)} /> : (
            <div className="card overflow-hidden">
              <div className="overflow-x-auto">
                <table className="w-full min-w-[900px] text-sm">
                  <thead className="bg-ink-50/70 dark:bg-white/[0.02]"><tr>
                    <th className="th w-16">No</th><th className="th">Talep</th><th className="th">Durum</th><th className="th">Öncelik</th>
                    <th className="th">Talep sahibi</th><th className="th">Atanan</th><th className="th">SLA</th><th className="th">Güncelleme</th>
                  </tr></thead>
                  <tbody>
                    {d.items.map((t) => (
                      <tr key={t.id} className="tr cursor-pointer" onClick={() => nav(`/tickets/${t.id}`)}>
                        <td className="td font-semibold muted">#{t.id}</td>
                        <td className="td max-w-80">
                          <Link to={`/tickets/${t.id}`} className="block truncate font-medium text-ink-900 hover:text-brand-600 dark:text-white" onClick={(e) => e.stopPropagation()}>{t.title}</Link>
                          <div className="mt-0.5 flex items-center gap-3 text-xs muted">
                            <ColorDot color={t.categoryColor} label={t.categoryName} />
                            {t.commentCount > 0 && <span className="inline-flex items-center gap-1"><MessageSquare className="h-3 w-3" />{t.commentCount}</span>}
                            {t.attachmentCount > 0 && <span className="inline-flex items-center gap-1"><Paperclip className="h-3 w-3" />{t.attachmentCount}</span>}
                          </div>
                        </td>
                        <td className="td"><Badge tone={statusMeta[t.status].tone} dot>{statusMeta[t.status].label}</Badge></td>
                        <td className="td"><Badge tone={priorityMeta[t.priority].tone}>{priorityMeta[t.priority].label}</Badge></td>
                        <td className="td"><div className="truncate font-medium">{t.requester.fullName}</div><div className="max-w-44 truncate text-xs muted">{t.departmentName}</div></td>
                        <td className="td">{t.assignee ? <span className="flex items-center gap-2"><Avatar name={t.assignee.fullName} size={22} /><span className="truncate">{t.assignee.fullName}</span></span> : <span className="text-xs muted">{t.groupName ?? 'Atanmamış'}</span>}</td>
                        <td className="td">{t.status === 'Resolved' || t.status === 'Closed' ? <span className="text-xs muted">—</span> : <span className={cn('inline-flex items-center gap-1 text-xs font-medium', t.isOverdue ? 'text-red-600 dark:text-red-400' : 'muted')}><AlarmClock className="h-3.5 w-3.5" />{fmt.relative(t.dueAt)}</span>}</td>
                        <td className="td text-xs muted">{fmt.relative(t.updatedAt)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Pagination page={d.page} totalPages={d.totalPages} total={d.totalCount} onPage={(p) => set('page', String(p))} />
            </div>
          )}
        </QueryState>
      </div>
    </div>
  )
}

function Board({ items, onOpen }: { items: TicketListItem[]; onOpen: (id: number) => void }) {
  return (
    <div className="grid gap-4 overflow-x-auto pb-2 md:grid-cols-5">
      {statuses.map((s) => {
        const col = items.filter((t) => t.status === s)
        return (
          <div key={s} className="min-w-64 rounded-xl bg-ink-100/60 p-2 dark:bg-white/[0.02]">
            <div className="flex items-center justify-between px-2 py-1.5">
              <Badge tone={statusMeta[s].tone} dot>{statusMeta[s].label}</Badge><span className="text-xs font-semibold muted">{col.length}</span>
            </div>
            <div className="mt-1 space-y-2">
              {col.map((t) => (
                <button key={t.id} onClick={() => onOpen(t.id)} className="card w-full p-3 text-left transition hover:-translate-y-0.5 hover:shadow-pop">
                  <div className="flex items-center justify-between text-[11px] muted"><span>#{t.id}</span><Badge tone={priorityMeta[t.priority].tone}>{priorityMeta[t.priority].label}</Badge></div>
                  <div className="mt-1.5 line-clamp-2 text-[13px] font-semibold text-ink-900 dark:text-white">{t.title}</div>
                  <div className="mt-2 flex items-center justify-between">
                    <ColorDot color={t.categoryColor} label={t.categoryName} />
                    {t.assignee ? <Avatar name={t.assignee.fullName} size={22} /> : <span className="text-[11px] muted">—</span>}
                  </div>
                  {t.isOverdue && <div className="mt-2 text-[11px] font-semibold text-red-600 dark:text-red-400">SLA aşıldı</div>}
                </button>
              ))}
              {col.length === 0 && <div className="px-2 py-6 text-center text-xs muted">Talep yok</div>}
            </div>
          </div>
        )
      })}
    </div>
  )
}
