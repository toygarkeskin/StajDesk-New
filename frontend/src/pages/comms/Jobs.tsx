import { useQuery } from '@tanstack/react-query'
import { Briefcase, Globe, MapPin } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Badge, EmptyState, Modal, PageHeader, QueryState } from '@/components/ui'
import { api } from '@/lib/api'
import { employmentLabel, fmt } from '@/lib/format'
import type { Job } from '@/lib/types'
import { useCan } from '@/store/auth'

export default function Jobs() {
  const q = useQuery({ queryKey: ['jobs'], queryFn: () => api.get<Job[]>('/jobs') })
  const [open, setOpen] = useState<Job | null>(null)
  const { can } = useCan()
  return (
    <div className="max-w-4xl">
      <PageHeader title="İş İlanları" description="Açık pozisyonlar. Uygun gördüğünüz çalışma arkadaşlarınızı İK'ya önerebilirsiniz." actions={can('content.manage') && <Link to="/admin/content?tab=jobs" className="btn-secondary">İlanları yönet</Link>} />
      <QueryState query={q} empty={(d) => d.length === 0 && <div className="card"><EmptyState icon={<Briefcase className="h-5 w-5" />} title="Açık pozisyon yok" /></div>}>
        {(list) => (
          <div className="space-y-3">
            {list.map((j) => (
              <button key={j.id} onClick={() => setOpen(j)} className="card flex w-full items-center gap-4 p-5 text-left transition hover:shadow-pop">
                <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-brand-50 text-brand-600 dark:bg-brand-400/10 dark:text-brand-300"><Briefcase className="h-5 w-5" /></span>
                <span className="min-w-0 flex-1">
                  <span className="block font-semibold text-ink-900 dark:text-white">{j.title}</span>
                  <span className="mt-0.5 flex flex-wrap gap-x-3 text-xs muted"><span>{j.department?.name ?? 'Devoplus'}</span><span className="inline-flex items-center gap-1"><MapPin className="h-3 w-3" />{j.location}</span>{j.closesAt && <span>Son başvuru {fmt.date(j.closesAt)}</span>}</span>
                </span>
                <Badge>{employmentLabel[j.employmentType]}</Badge>{j.isPublic ? <Badge tone="cyan"><Globe className="h-3 w-3" /> Genel</Badge> : <Badge tone="violet">İç ilan</Badge>}
              </button>
            ))}
          </div>
        )}
      </QueryState>
      <Modal open={!!open} onClose={() => setOpen(null)} title={open?.title} size="lg">
        {open && <div className="space-y-3 text-sm leading-6"><p className="muted">{open.department?.name} · {open.location} · {employmentLabel[open.employmentType]}</p><p>{open.description}</p>{open.requirements && <pre className="whitespace-pre-wrap font-sans">{open.requirements}</pre>}</div>}
      </Modal>
    </div>
  )
}
