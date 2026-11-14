import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Save } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { EmptyState, PageHeader, PersonCell, Progress, QueryState, SearchInput } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { fmt } from '@/lib/format'
import type { LeaveBalance } from '@/lib/types'

export default function LeaveAdmin() {
  const qc = useQueryClient()
  const [search, setSearch] = useState('')
  const q = useQuery({ queryKey: ['leave-balances', search], queryFn: () => api.get<LeaveBalance[]>('/leave/balances', { search: search || undefined }) })
  const [edit, setEdit] = useState<Record<string, { entitled: string; carriedOver: string }>>({})
  const save = useMutation({
    mutationFn: (b: LeaveBalance) => api.put(`/leave/balances/${b.userId}`, { entitled: Number(edit[b.userId].entitled), carriedOver: Number(edit[b.userId].carriedOver) }),
    onSuccess: (_, b) => { qc.invalidateQueries({ queryKey: ['leave-balances'] }); setEdit((e) => { const n = { ...e }; delete n[b.userId]; return n }); toast.success('Bakiye güncellendi.') },
    onError: (e) => toast.error(errorMessage(e)),
  })
  return (
    <div>
      <PageHeader title="İzin Bakiyeleri" description="Yıllık izin hak edişleri (4857 sayılı kanuna göre kıdemle hesaplanır) ve devreden günler. Onaylanan yıllık izinler bakiyeden otomatik düşülür." />
      <SearchInput className="mb-4 sm:w-80" value={search} onChange={setSearch} placeholder="Çalışan ara…" />
      <QueryState query={q} empty={(d) => d.length === 0 && <div className="card"><EmptyState title="Kayıt yok" /></div>}>
        {(list) => (
          <div className="card overflow-hidden"><div className="overflow-x-auto"><table className="w-full min-w-[860px] text-sm">
            <thead className="bg-ink-50/70 dark:bg-white/[0.02]"><tr><th className="th">Çalışan</th><th className="th">Hak ediş</th><th className="th">Devreden</th><th className="th">Kullanılan</th><th className="th">Onay bekleyen</th><th className="th w-56">Kalan</th><th className="th" /></tr></thead>
            <tbody>{list.map((b) => {
              const e = edit[b.userId]
              const total = b.entitled + b.carriedOver
              return (
                <tr key={b.userId} className="tr">
                  <td className="td"><PersonCell name={b.fullName} sub={b.department} /></td>
                  <td className="td">{e ? <input type="number" step="0.5" className="input h-8 w-20" value={e.entitled} onChange={(x) => setEdit({ ...edit, [b.userId]: { ...e, entitled: x.target.value } })} /> : fmt.number(b.entitled, 1)}</td>
                  <td className="td">{e ? <input type="number" step="0.5" className="input h-8 w-20" value={e.carriedOver} onChange={(x) => setEdit({ ...edit, [b.userId]: { ...e, carriedOver: x.target.value } })} /> : fmt.number(b.carriedOver, 1)}</td>
                  <td className="td">{fmt.number(b.used, 1)}</td><td className="td">{fmt.number(b.pending, 1)}</td>
                  <td className="td"><div className="flex items-center gap-2"><Progress value={total ? (b.remaining / total) * 100 : 0} tone="green" /><b className="w-12 text-right">{fmt.number(b.remaining, 1)}</b></div></td>
                  <td className="td text-right">{e ? <button className="btn-primary btn-sm" onClick={() => save.mutate(b)}><Save className="h-3.5 w-3.5" /> Kaydet</button> : <button className="btn-ghost btn-sm" onClick={() => setEdit({ ...edit, [b.userId]: { entitled: String(b.entitled), carriedOver: String(b.carriedOver) } })}>Düzenle</button>}</td>
                </tr>
              )
            })}</tbody>
          </table></div></div>
        )}
      </QueryState>
    </div>
  )
}
