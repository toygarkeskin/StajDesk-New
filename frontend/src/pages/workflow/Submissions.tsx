import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Badge, EmptyState, PageHeader, Pagination, PersonCell, QueryState, SearchInput, Tabs } from '@/components/ui'
import { api } from '@/lib/api'
import { fmt, submissionMeta } from '@/lib/format'
import { DynamicIcon } from '@/lib/icon'
import type { Paged, SubmissionListItem, SubmissionStatus } from '@/lib/types'
import { tint } from '@/lib/utils'
import { useCan } from '@/store/auth'

export default function Submissions({ mode }: { mode: 'requests' | 'approvals' }) {
  const nav = useNavigate()
  const { can } = useCan()
  const [scope, setScope] = useState(mode === 'requests' ? 'mine' : 'approvals')
  const [status, setStatus] = useState<SubmissionStatus | ''>('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const params = { scope, status: status || undefined, search: search || undefined, page, pageSize: 15 }
  const q = useQuery({ queryKey: ['submissions', params], queryFn: () => api.get<Paged<SubmissionListItem>>('/submissions', params), placeholderData: keepPreviousData })
  const tabs = mode === 'requests'
    ? [{ value: 'mine', label: 'Başvurularım' }]
    : [{ value: 'approvals', label: 'Onayımı bekleyenler' }, { value: 'assigned', label: 'Tamamlanacaklar' }, { value: 'history', label: 'Karar verdiklerim' }, ...(can('submissions.view_all') ? [{ value: 'all', label: 'Tüm başvurular' }] : [{ value: 'all', label: 'Birimimin başvuruları' }])]

  return (
    <div>
      <PageHeader title={mode === 'requests' ? 'Başvurularım' : 'Onaylarım'}
        description={mode === 'requests' ? 'Oluşturduğunuz form başvuruları ve onay durumları.' : 'Onayınızı bekleyen ve sorumlu olduğunuz başvurular.'}
        actions={mode === 'requests' && <Link to="/apps" className="btn-primary">Yeni başvuru</Link>} />
      {tabs.length > 1 && <Tabs value={scope} onChange={(v) => { setScope(v); setPage(1) }} tabs={tabs} className="mb-4" />}
      <div className="mb-4 flex flex-col gap-2 sm:flex-row">
        <SearchInput className="sm:w-80" value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder="No, form, kişi veya özet…" />
        <select className="input sm:w-48" value={status} onChange={(e) => { setStatus(e.target.value as SubmissionStatus); setPage(1) }} aria-label="Durum">
          <option value="">Tüm durumlar</option>{(Object.keys(submissionMeta) as SubmissionStatus[]).map((s) => <option key={s} value={s}>{submissionMeta[s].label}</option>)}
        </select>
      </div>
      <QueryState query={q} empty={(d) => d.items.length === 0 && <div className="card"><EmptyState title={scope === 'approvals' ? 'Onayınızı bekleyen başvuru yok' : 'Başvuru bulunamadı'} description={scope === 'approvals' ? 'Her şey güncel görünüyor. 🎉' : undefined} /></div>}>
        {(d) => (
          <div className="card overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[820px] text-sm">
                <thead className="bg-ink-50/70 dark:bg-white/[0.02]"><tr><th className="th">Başvuru</th><th className="th">Başvuran</th><th className="th">Özet</th><th className="th">Durum</th><th className="th">Adım</th><th className="th">Tarih</th></tr></thead>
                <tbody>
                  {d.items.map((s) => (
                    <tr key={s.id} className="tr cursor-pointer" onClick={() => nav(`/submissions/${s.id}`)}>
                      <td className="td">
                        <div className="flex items-center gap-3">
                          <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg" style={{ background: tint(s.color, 0.12), color: s.color }}><DynamicIcon name={s.icon} className="h-4 w-4" /></span>
                          <div><div className="font-medium text-ink-900 dark:text-white">{s.formName}</div><div className="text-xs muted">{s.number}</div></div>
                        </div>
                      </td>
                      <td className="td"><PersonCell name={s.submitter.fullName} sub={s.departmentName} /></td>
                      <td className="td max-w-72"><div className="truncate">{s.summary ?? '—'}</div>{s.amount != null && <div className="text-xs muted">{fmt.money(s.amount)}</div>}</td>
                      <td className="td"><Badge tone={submissionMeta[s.status].tone} dot>{submissionMeta[s.status].label}</Badge></td>
                      <td className="td text-xs">{s.status === 'Pending' ? <><b className="text-ink-800 dark:text-ink-100">{s.currentStepName}</b><div className="muted">{s.currentStep + 1}/{s.stepCount}</div></> : <span className="muted">—</span>}</td>
                      <td className="td text-xs muted">{fmt.relative(s.updatedAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={d.page} totalPages={d.totalPages} total={d.totalCount} onPage={setPage} />
          </div>
        )}
      </QueryState>
    </div>
  )
}
