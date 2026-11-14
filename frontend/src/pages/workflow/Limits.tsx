import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Gauge, Save } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Card, PageHeader, QueryState } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { fmt } from '@/lib/format'
import type { Limit } from '@/lib/types'
import { useCan } from '@/store/auth'

export default function Limits() {
  const q = useQuery({ queryKey: ['limits'], queryFn: () => api.get<Limit[]>('/limits') })
  const { can } = useCan()
  const qc = useQueryClient()
  const [edit, setEdit] = useState<Record<number, string>>({})
  const save = useMutation({
    mutationFn: ({ id, value }: { id: number; value: number }) => api.put<Limit>(`/limits/${id}`, { maxAmount: value }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['limits'] }); toast.success('Limit güncellendi.') },
    onError: (e) => toast.error(errorMessage(e)),
  })
  return (
    <div className="max-w-3xl">
      <PageHeader title="Limitler" description="Avans ve masraf başvurularında sunucu tarafında otomatik uygulanan üst limitler." />
      <QueryState query={q}>
        {(list) => (
          <div className="grid gap-4 sm:grid-cols-2">
            {list.map((l) => (
              <Card key={l.id} title={<span className="inline-flex items-center gap-2"><Gauge className="h-4 w-4 text-brand-600" />{l.kind === 'advance' ? 'Avans' : 'Masraf'}</span>}>
                <div className="text-sm muted">{l.label}</div>
                <div className="mt-1 font-display text-3xl font-bold text-ink-900 dark:text-white">{fmt.money(l.maxAmount, l.currency)}</div>
                {can('limits.manage') && (
                  <div className="mt-4 flex gap-2">
                    <input type="number" className="input" placeholder="Yeni limit" value={edit[l.id] ?? ''} onChange={(e) => setEdit({ ...edit, [l.id]: e.target.value })} aria-label="Yeni limit" />
                    <button className="btn-primary" disabled={!edit[l.id]} onClick={() => save.mutate({ id: l.id, value: Number(edit[l.id]) })}><Save className="h-4 w-4" /></button>
                  </div>
                )}
              </Card>
            ))}
          </div>
        )}
      </QueryState>
    </div>
  )
}
