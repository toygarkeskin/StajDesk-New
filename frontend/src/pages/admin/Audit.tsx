import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Fragment, useState } from 'react'
import { Badge, EmptyState, PageHeader, Pagination, QueryState, SearchInput } from '@/components/ui'
import { api } from '@/lib/api'
import { fmt } from '@/lib/format'
import type { AuditLog, Paged } from '@/lib/types'

const actionTone: Record<string, 'green' | 'blue' | 'red' | 'violet' | 'amber' | 'gray'> = { Create: 'green', Update: 'blue', Delete: 'red', Login: 'violet', PasswordChanged: 'amber', PasswordReset: 'amber' }

export default function Audit() {
  const [f, setF] = useState({ search: '', entityType: '', action: '', from: '', to: '', page: 1 })
  const types = useQuery({ queryKey: ['audit-types'], queryFn: () => api.get<string[]>('/audit-logs/entity-types') })
  const params = { search: f.search || undefined, entityType: f.entityType || undefined, action: f.action || undefined, from: f.from || undefined, to: f.to || undefined, page: f.page, pageSize: 30 }
  const q = useQuery({ queryKey: ['audit', params], queryFn: () => api.get<Paged<AuditLog>>('/audit-logs', params), placeholderData: keepPreviousData })
  const [open, setOpen] = useState<number | null>(null)
  return (
    <div>
      <PageHeader title="Denetim Kayıtları" description="Kullanıcı ve yönetici işlemlerinin değişmez kaydı. Parola ve token gibi hassas alanlar kaydedilmez." />
      <div className="mb-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-5">
        <SearchInput value={f.search} onChange={(v) => setF({ ...f, search: v, page: 1 })} placeholder="Kullanıcı adı veya kayıt no…" />
        <select className="input" value={f.entityType} onChange={(e) => setF({ ...f, entityType: e.target.value, page: 1 })} aria-label="Kayıt türü"><option value="">Tüm kayıt türleri</option>{types.data?.map((t) => <option key={t}>{t}</option>)}</select>
        <select className="input" value={f.action} onChange={(e) => setF({ ...f, action: e.target.value, page: 1 })} aria-label="İşlem"><option value="">Tüm işlemler</option>{Object.keys(actionTone).map((a) => <option key={a}>{a}</option>)}</select>
        <input type="date" className="input" value={f.from} onChange={(e) => setF({ ...f, from: e.target.value, page: 1 })} aria-label="Başlangıç" />
        <input type="date" className="input" value={f.to} onChange={(e) => setF({ ...f, to: e.target.value, page: 1 })} aria-label="Bitiş" />
      </div>
      <QueryState query={q} empty={(d) => d.items.length === 0 && <div className="card"><EmptyState title="Kayıt bulunamadı" /></div>}>
        {(d) => (
          <div className="card overflow-hidden">
            <div className="overflow-x-auto"><table className="w-full min-w-[820px] text-sm">
              <thead className="bg-ink-50/70 dark:bg-white/[0.02]"><tr><th className="th">Zaman</th><th className="th">Kullanıcı</th><th className="th">İşlem</th><th className="th">Kayıt</th><th className="th">IP</th></tr></thead>
              <tbody>{d.items.map((a) => (
                <Fragment key={a.id}>
                  <tr className="tr cursor-pointer" onClick={() => setOpen(open === a.id ? null : a.id)}>
                    <td className="td text-xs muted">{fmt.dateTime(a.createdAt)}</td><td className="td">{a.userName ?? 'Sistem'}</td>
                    <td className="td"><Badge tone={actionTone[a.action] ?? 'gray'}>{a.action}</Badge></td><td className="td"><b>{a.entityType}</b> <span className="font-mono text-xs muted">{a.entityId}</span></td><td className="td font-mono text-xs muted">{a.ipAddress ?? '—'}</td>
                  </tr>
                  {open === a.id && a.changes && <tr><td colSpan={5} className="bg-ink-50 px-4 py-3 dark:bg-white/[0.02]"><pre className="max-h-64 overflow-auto font-mono text-[11px] leading-5 whitespace-pre-wrap scroll-thin">{JSON.stringify(JSON.parse(a.changes), null, 2)}</pre></td></tr>}
                </Fragment>
              ))}</tbody>
            </table></div>
            <Pagination page={d.page} totalPages={d.totalPages} total={d.totalCount} onPage={(p) => setF({ ...f, page: p })} />
          </div>
        )}
      </QueryState>
    </div>
  )
}
