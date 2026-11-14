import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Eye, LifeBuoy, Pencil, ThumbsUp, Trash2 } from 'lucide-react'
import { useState } from 'react'
import ReactMarkdown from 'react-markdown'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { Avatar, Badge, ColorDot, ErrorState, Skeleton } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api } from '@/lib/api'
import { fmt } from '@/lib/format'
import type { Article } from '@/lib/types'
import { useCan } from '@/store/auth'
import { ArticleEditor } from './Knowledge'

export default function ArticlePage() {
  const { id } = useParams()
  const nav = useNavigate()
  const qc = useQueryClient()
  const confirm = useConfirm()
  const { can } = useCan()
  const q = useQuery({ queryKey: ['article', id], queryFn: () => api.get<Article>(`/kb/${id}`) })
  const [voted, setVoted] = useState(false)
  const [edit, setEdit] = useState(false)
  const helpful = useMutation({ mutationFn: () => api.post(`/kb/${id}/helpful`), onSuccess: () => { setVoted(true); toast.success('Geri bildiriminiz için teşekkürler!') } })
  const del = useMutation({ mutationFn: () => api.del(`/kb/${id}`), onSuccess: () => { qc.invalidateQueries({ queryKey: ['kb'] }); nav('/kb') } })
  if (q.isLoading) return <Skeleton className="mx-auto h-96 max-w-3xl" />
  if (q.error || !q.data) return <ErrorState error={q.error} onRetry={q.refetch} />
  const a = q.data
  return (
    <div className="mx-auto max-w-3xl">
      <Link to="/kb" className="mb-3 inline-flex items-center gap-1.5 text-sm muted hover:text-ink-900 dark:hover:text-white"><ArrowLeft className="h-4 w-4" /> Bilgi Bankası</Link>
      <article className="card p-6 sm:p-8">
        <div className="flex flex-wrap items-center gap-2">{a.categoryName && <ColorDot color={a.categoryColor ?? '#94a3b8'} label={a.categoryName} />}{a.isFaq && <Badge tone="violet">SSS</Badge>}{!a.isPublished && <Badge tone="amber">Taslak</Badge>}</div>
        <h1 className="mt-3 text-3xl font-bold">{a.title}</h1>
        <p className="mt-2 text-lg muted">{a.summary}</p>
        <div className="mt-5 flex items-center gap-3 border-y border-ink-100 py-3 text-xs dark:border-white/[0.06]">
          <Avatar name={a.author.fullName} size={28} /><span><b className="text-ink-800 dark:text-ink-100">{a.author.fullName}</b> · Güncelleme {fmt.date(a.updatedAt)}</span>
          <span className="ml-auto inline-flex items-center gap-1 muted"><Eye className="h-3.5 w-3.5" /> {a.views}</span>
          {can('kb.manage') && <><button className="btn-ghost btn-sm" onClick={() => setEdit(true)}><Pencil className="h-3.5 w-3.5" /> Düzenle</button><button className="btn-ghost btn-sm text-red-600" aria-label="Sil" onClick={async () => (await confirm({ title: 'Makaleyi sil', message: 'Makale kalıcı olarak silinecek.', danger: true, confirmText: 'Sil' })) && del.mutate()}><Trash2 className="h-3.5 w-3.5" /></button></>}
        </div>
        <div className="prose-sd mt-4 text-[15px]"><ReactMarkdown>{a.content}</ReactMarkdown></div>
      </article>
      <div className="card mt-4 flex flex-col items-center justify-between gap-3 p-5 sm:flex-row">
        <span className="text-sm">Bu makale işinize yaradı mı?</span>
        <div className="flex gap-2">
          <button className="btn-secondary" disabled={voted || helpful.isPending} onClick={() => helpful.mutate()}><ThumbsUp className="h-4 w-4" /> {voted ? 'Teşekkürler' : `Evet (${a.helpfulCount})`}</button>
          <Link to="/tickets/new" className="btn-primary"><LifeBuoy className="h-4 w-4" /> Hayır, talep aç</Link>
        </div>
      </div>
      {edit && <ArticleEditor article={a} open={edit} onClose={() => setEdit(false)} />}
    </div>
  )
}
