import { useQuery } from '@tanstack/react-query'
import { ArrowUpRight, Workflow } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { EmptyState, PageHeader, QueryState, SearchInput, Skeleton } from '@/components/ui'
import { api } from '@/lib/api'
import { DynamicIcon } from '@/lib/icon'
import type { AppEntry } from '@/lib/types'
import { cn, tint } from '@/lib/utils'

export default function Apps() {
  const q = useQuery({ queryKey: ['apps'], queryFn: () => api.get<AppEntry[]>('/apps'), staleTime: 60_000 })
  const [search, setSearch] = useState('')
  const [module, setModule] = useState<string | null>(null)
  const modules = useMemo(() => [...new Set(q.data?.map((a) => a.module) ?? [])], [q.data])
  const filtered = useMemo(() => {
    const t = search.toLocaleLowerCase('tr-TR')
    return (q.data ?? []).filter((a) => (!module || a.module === module) && (!t || a.name.toLocaleLowerCase('tr-TR').includes(t) || a.module.toLocaleLowerCase('tr-TR').includes(t) || a.description?.toLocaleLowerCase('tr-TR').includes(t)))
  }, [q.data, search, module])
  const grouped = useMemo(() => modules.map((m) => ({ module: m, apps: filtered.filter((a) => a.module === m) })).filter((g) => g.apps.length), [modules, filtered])

  return (
    <div>
      <PageHeader title="Uygulama Merkezi" description="Departman uygulamaları ve kurumsal modüller. Formlar ilgili birime ve onay akışına otomatik yönlendirilir." />
      <div className="mb-5 flex flex-col gap-3 lg:flex-row lg:items-center">
        <SearchInput className="lg:w-80" value={search} onChange={setSearch} placeholder="Uygulama ara (izin, masraf, donanım…)" />
        <div className="flex flex-wrap gap-1.5">
          <button onClick={() => setModule(null)} className={cn('rounded-full px-3 py-1.5 text-xs font-medium', !module ? 'bg-ink-900 text-white dark:bg-white dark:text-ink-900' : 'bg-ink-100 text-ink-600 hover:bg-ink-200 dark:bg-white/[0.06] dark:text-ink-300')}>Tümü</button>
          {modules.map((m) => (
            <button key={m} onClick={() => setModule(m === module ? null : m)} className={cn('rounded-full px-3 py-1.5 text-xs font-medium', module === m ? 'bg-ink-900 text-white dark:bg-white dark:text-ink-900' : 'bg-ink-100 text-ink-600 hover:bg-ink-200 dark:bg-white/[0.06] dark:text-ink-300')}>{m}</button>
          ))}
        </div>
      </div>
      <QueryState query={q} skeleton={<div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">{Array.from({ length: 12 }).map((_, i) => <Skeleton key={i} className="h-24" />)}</div>}
        empty={() => grouped.length === 0 && <div className="card"><EmptyState icon={<Workflow className="h-5 w-5" />} title="Uygulama bulunamadı" description="Arama ifadesini değiştirmeyi deneyin." /></div>}>
        {() => (
          <div className="space-y-8">
            {grouped.map((g) => (
              <section key={g.module}>
                <h2 className="mb-3 text-xs font-semibold uppercase tracking-[0.12em] muted">{g.module} <span className="ml-1 font-normal">({g.apps.length})</span></h2>
                <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                  {g.apps.map((a) => (
                    <Link key={a.key} to={a.route} className="card group relative flex items-start gap-3 p-4 transition hover:-translate-y-0.5 hover:shadow-pop">
                      <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl" style={{ background: tint(a.color, 0.12), color: a.color }}><DynamicIcon name={a.icon} className="h-5 w-5" /></span>
                      <span className="min-w-0 flex-1">
                        <span className="flex items-center gap-2"><span className="truncate font-semibold text-ink-900 dark:text-white">{a.name}</span>{a.kind === 'module' && <span className="chip bg-ink-100 text-[10px] text-ink-500 dark:bg-white/[0.06]">Modül</span>}</span>
                        <span className="mt-0.5 line-clamp-2 block text-xs muted">{a.description}</span>
                      </span>
                      {a.badge > 0 && <span className="absolute top-3 right-3 rounded-full bg-brand-600 px-1.5 text-[10px] font-bold leading-[18px] text-white" title="Onayınızı bekleyen başvuru">{a.badge}</span>}
                      <ArrowUpRight className="absolute right-3 bottom-3 h-4 w-4 text-ink-300 opacity-0 transition group-hover:opacity-100" />
                    </Link>
                  ))}
                </div>
              </section>
            ))}
          </div>
        )}
      </QueryState>
    </div>
  )
}
