import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BookOpen, CircleHelp, Eye, Plus, ThumbsUp } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { Badge, ColorDot, Drawer, EmptyState, Field, PageHeader, Pagination, QueryState, SearchInput, Spinner, Tabs, Toggle } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { fmt } from '@/lib/format'
import { useCategories } from '@/lib/queries'
import type { Article, ArticleListItem, Paged } from '@/lib/types'
import { useCan } from '@/store/auth'

export function ArticleEditor({ article, open, onClose }: { article?: Article | null; open: boolean; onClose: () => void }) {
  const qc = useQueryClient()
  const categories = useCategories()
  const [v, setV] = useState(() => ({ title: article?.title ?? '', summary: article?.summary ?? '', content: article?.content ?? '', categoryId: article?.categoryId ?? null as number | null, isFaq: article?.isFaq ?? false, isPublished: article?.isPublished ?? true }))
  const save = useMutation({
    mutationFn: () => (article ? api.put(`/kb/${article.id}`, v) : api.post('/kb', v)),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['kb'] }); qc.invalidateQueries({ queryKey: ['article'] }); toast.success('Makale kaydedildi.'); onClose() },
    onError: (e) => toast.error(errorMessage(e)),
  })
  return (
    <Drawer open={open} onClose={onClose} title={article ? 'Makaleyi düzenle' : 'Yeni makale'} width="max-w-3xl"
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate()}>{save.isPending && <Spinner />} Kaydet</button></>}>
      <div className="space-y-4">
        <Field label="Başlık" required><input className="input" value={v.title} onChange={(e) => setV({ ...v, title: e.target.value })} /></Field>
        <Field label="Özet" required><input className="input" value={v.summary} onChange={(e) => setV({ ...v, summary: e.target.value })} /></Field>
        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="Kategori"><select className="input" value={v.categoryId ?? ''} onChange={(e) => setV({ ...v, categoryId: e.target.value ? Number(e.target.value) : null })}><option value="">Seçilmedi</option>{categories.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select></Field>
          <div className="pt-7"><Toggle checked={v.isFaq} onChange={(x) => setV({ ...v, isFaq: x })} label="SSS olarak göster" /></div>
          <div className="pt-7"><Toggle checked={v.isPublished} onChange={(x) => setV({ ...v, isPublished: x })} label="Yayında" /></div>
        </div>
        <Field label="İçerik (Markdown)" required help="## başlık, **kalın**, - liste, `kod` biçimleri desteklenir."><textarea className="input font-mono text-[13px]" rows={16} value={v.content} onChange={(e) => setV({ ...v, content: e.target.value })} /></Field>
      </div>
    </Drawer>
  )
}

export default function Knowledge() {
  const [sp, setSp] = useSearchParams()
  const faq = sp.get('faq') === '1'
  const { can } = useCan()
  const [search, setSearch] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [page, setPage] = useState(1)
  const [editor, setEditor] = useState(false)
  const categories = useCategories()
  const params = { search: search || undefined, categoryId: categoryId || undefined, faq: faq || undefined, page, pageSize: 12 }
  const q = useQuery({ queryKey: ['kb', params], queryFn: () => api.get<Paged<ArticleListItem>>('/kb', params), placeholderData: keepPreviousData })

  return (
    <div>
      <PageHeader title="Bilgi Bankası & SSS" description="Sık karşılaşılan sorunların çözümleri ve kurum içi rehberler. Talep açmadan önce göz atın."
        actions={can('kb.manage') && <button className="btn-primary" onClick={() => setEditor(true)}><Plus className="h-4 w-4" /> Makale yaz</button>} />
      <Tabs className="mb-4" value={faq ? 'faq' : 'all'} onChange={(v) => { setSp(v === 'faq' ? { faq: '1' } : {}); setPage(1) }}
        tabs={[{ value: 'all', label: <span className="inline-flex items-center gap-2"><BookOpen className="h-4 w-4" /> Tüm makaleler</span> }, { value: 'faq', label: <span className="inline-flex items-center gap-2"><CircleHelp className="h-4 w-4" /> SSS</span> }]} />
      <div className="mb-5 flex flex-col gap-2 sm:flex-row">
        <SearchInput className="sm:w-96" value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder="Ne arıyorsunuz? (VPN, şifre, yazıcı…)" />
        <select className="input sm:w-56" value={categoryId} onChange={(e) => { setCategoryId(e.target.value); setPage(1) }} aria-label="Kategori"><option value="">Tüm kategoriler</option>{categories.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select>
      </div>
      <QueryState query={q} empty={(d) => d.items.length === 0 && <div className="card"><EmptyState title="Makale bulunamadı" description="Aradığınızı bulamadıysanız destek talebi açabilirsiniz." action={<Link to="/tickets/new" className="btn-primary">Talep aç</Link>} /></div>}>
        {(d) => (
          <>
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {d.items.map((a) => (
                <Link key={a.id} to={`/kb/${a.id}`} className="card flex flex-col p-5 transition hover:-translate-y-0.5 hover:shadow-pop">
                  <div className="flex items-center justify-between gap-2">{a.categoryName && <ColorDot color={a.categoryColor ?? '#94a3b8'} label={a.categoryName} />}<div className="flex gap-1">{a.isFaq && <Badge tone="violet">SSS</Badge>}{!a.isPublished && <Badge tone="amber">Taslak</Badge>}</div></div>
                  <h3 className="mt-3 font-semibold text-ink-900 dark:text-white">{a.title}</h3>
                  <p className="mt-1 line-clamp-2 flex-1 text-sm muted">{a.summary}</p>
                  <div className="mt-4 flex items-center gap-4 text-xs muted"><span className="inline-flex items-center gap-1"><Eye className="h-3.5 w-3.5" />{a.views}</span><span className="inline-flex items-center gap-1"><ThumbsUp className="h-3.5 w-3.5" />{a.helpfulCount}</span><span className="ml-auto">{fmt.relative(a.updatedAt)}</span></div>
                </Link>
              ))}
            </div>
            <div className="card mt-4"><Pagination page={d.page} totalPages={d.totalPages} total={d.totalCount} onPage={setPage} /></div>
          </>
        )}
      </QueryState>
      {editor && <ArticleEditor open={editor} onClose={() => setEditor(false)} />}
    </div>
  )
}
