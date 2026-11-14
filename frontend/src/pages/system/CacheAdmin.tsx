import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Eraser, RefreshCw } from 'lucide-react'
import { toast } from 'sonner'
import { EmptyState, PageHeader, QueryState } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api, errorMessage } from '@/lib/api'

const groups: { prefix: string; label: string; desc: string }[] = [
  { prefix: 'stajdesk:lookup:', label: 'Referans veriler', desc: 'Kategori, departman, unvan listeleri (60 sn - 5 dk)' },
  { prefix: 'stajdesk:stats:', label: 'İstatistik ve paneller', desc: 'Kişisel dashboard ve özet istatistikler (30 sn)' },
  { prefix: 'stajdesk:public:', label: 'Genel site', desc: 'Ana sayfa, piyasa ve hava durumu verisi (2-20 dk)' },
]

export default function CacheAdmin() {
  const qc = useQueryClient()
  const confirm = useConfirm()
  const q = useQuery({ queryKey: ['cache-keys'], queryFn: () => api.get<{ key: string; ttlSeconds?: number | null }[]>('/system/cache') })
  const clear = useMutation({
    mutationFn: (prefix?: string) => api.del(`/system/cache`, prefix ? { prefix } : undefined),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['cache-keys'] }); toast.success('Önbellek temizlendi. Veriler bir sonraki istekte veritabanından yeniden okunacak.') },
    onError: (e) => toast.error(errorMessage(e)),
  })
  return (
    <div className="max-w-5xl">
      <PageHeader title="Önbellek Yönetimi (Cache Clear)" description="Redis'te cache-aside deseniyle tutulan anahtarlar. Güvenlik sayaçları (giriş hız sınırı) buradan silinemez."
        actions={<><button className="btn-secondary" onClick={() => q.refetch()}><RefreshCw className="h-4 w-4" /> Yenile</button><button className="btn-danger" onClick={async () => (await confirm({ title: 'Tüm önbelleği temizle', message: 'Tüm StajDesk önbellek anahtarları silinecek (hız sınırı sayaçları hariç).', danger: true, confirmText: 'Temizle' })) && clear.mutate(undefined)}><Eraser className="h-4 w-4" /> Tümünü temizle</button></>} />
      <div className="mb-6 grid gap-4 md:grid-cols-3">
        {groups.map((g) => (
          <div key={g.prefix} className="card p-5">
            <div className="font-semibold text-ink-900 dark:text-white">{g.label}</div>
            <div className="mt-1 text-xs muted">{g.desc}</div>
            <div className="mt-3 flex items-center justify-between"><span className="font-display text-2xl font-bold">{q.data?.filter((k) => k.key.startsWith(g.prefix)).length ?? '—'}</span><button className="btn-secondary btn-sm" onClick={() => clear.mutate(g.prefix)}><Eraser className="h-3.5 w-3.5" /> Temizle</button></div>
          </div>
        ))}
      </div>
      <QueryState query={q} empty={(d) => d.length === 0 && <div className="card"><EmptyState title="Önbellekte anahtar yok" /></div>}>
        {(list) => (
          <div className="card overflow-hidden"><table className="w-full text-sm">
            <thead className="bg-ink-50/70 dark:bg-white/[0.02]"><tr><th className="th">Anahtar</th><th className="th text-right">Kalan süre</th></tr></thead>
            <tbody>{list.map((k) => <tr key={k.key} className="tr"><td className="td font-mono text-xs">{k.key}</td><td className="td text-right text-xs muted">{k.ttlSeconds != null ? `${Math.round(k.ttlSeconds)} sn` : 'Süresiz'}</td></tr>)}</tbody>
          </table></div>
        )}
      </QueryState>
    </div>
  )
}
