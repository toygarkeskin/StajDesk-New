import { useQuery } from '@tanstack/react-query'
import { Megaphone, Newspaper, Pin } from 'lucide-react'
import { useState } from 'react'
import ReactMarkdown from 'react-markdown'
import { Link } from 'react-router-dom'
import { Avatar, Badge, EmptyState, Modal, PageHeader, QueryState, Tabs } from '@/components/ui'
import { api } from '@/lib/api'
import { fmt } from '@/lib/format'
import type { Announcement, AnnouncementKind } from '@/lib/types'
import { useCan } from '@/store/auth'

export default function Announcements() {
  const [kind, setKind] = useState<AnnouncementKind | 'all'>('all')
  const [open, setOpen] = useState<Announcement | null>(null)
  const { can } = useCan()
  const q = useQuery({ queryKey: ['announcements', kind], queryFn: () => api.get<Announcement[]>('/announcements', { kind: kind === 'all' ? undefined : kind, take: 50 }) })
  return (
    <div className="max-w-4xl">
      <PageHeader title="Duyurular & Haberler" description="Kurum içi duyurular ve şirket haberleri." actions={can('content.manage') && <Link to="/admin/content" className="btn-secondary">İçerik yönetimi</Link>} />
      <Tabs className="mb-5" value={kind} onChange={setKind} tabs={[{ value: 'all', label: 'Tümü' }, { value: 'Announcement', label: 'Duyurular' }, { value: 'News', label: 'Haberler' }]} />
      <QueryState query={q} empty={(d) => d.length === 0 && <div className="card"><EmptyState title="Yayında içerik yok" /></div>}>
        {(list) => (
          <div className="space-y-3">
            {list.map((a) => (
              <button key={a.id} onClick={() => setOpen(a)} className="card flex w-full gap-4 p-5 text-left transition hover:shadow-pop">
                <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-brand-50 text-brand-600 dark:bg-brand-400/10 dark:text-brand-300">{a.kind === 'News' ? <Newspaper className="h-5 w-5" /> : <Megaphone className="h-5 w-5" />}</span>
                <span className="min-w-0 flex-1">
                  <span className="flex flex-wrap items-center gap-2">{a.isPinned && <Badge tone="brand"><Pin className="h-3 w-3" /> Sabit</Badge>}<Badge>{a.kind === 'News' ? 'Haber' : 'Duyuru'}</Badge>{a.department && <Badge tone="violet">{a.department.name}</Badge>}<span className="text-xs muted">{fmt.relative(a.publishAt)}</span></span>
                  <span className="mt-2 block font-semibold text-ink-900 dark:text-white">{a.title}</span>
                  <span className="mt-1 block text-sm muted">{a.summary}</span>
                </span>
              </button>
            ))}
          </div>
        )}
      </QueryState>
      <Modal open={!!open} onClose={() => setOpen(null)} title={open?.title} size="lg">
        {open && <><div className="mb-4 flex items-center gap-2 text-xs muted"><Avatar name={open.author.fullName} size={22} />{open.author.fullName} · {fmt.dateTime(open.publishAt)}</div><div className="prose-sd"><ReactMarkdown>{open.body}</ReactMarkdown></div></>}
      </Modal>
    </div>
  )
}
