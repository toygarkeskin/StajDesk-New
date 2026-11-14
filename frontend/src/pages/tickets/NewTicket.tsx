import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { ArrowLeft, BookOpen, Lightbulb, Send } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { Link, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { z } from 'zod'
import { Card, Field, FileUploader, PageHeader, Spinner } from '@/components/ui'
import { api, ApiError, errorMessage } from '@/lib/api'
import { assetTypeLabel, priorityMeta } from '@/lib/format'
import { useCategories } from '@/lib/queries'
import type { ArticleListItem, Asset, FileDto, Paged, TicketDetail } from '@/lib/types'
import { cn, tint } from '@/lib/utils'

const schema = z.object({
  title: z.string().trim().min(5, 'Başlık en az 5 karakter olmalıdır.').max(200, 'Başlık en fazla 200 karakter olabilir.'),
  description: z.string().trim().min(10, 'Lütfen sorunu en az 10 karakterle açıklayınız.').max(8000),
  categoryId: z.number({ message: 'Kategori seçiniz.' }).min(1, 'Kategori seçiniz.'),
  priority: z.enum(['Low', 'Medium', 'High', 'Critical']),
  type: z.enum(['Incident', 'Request']),
  assetId: z.number().nullable().optional(),
})
type Values = z.infer<typeof schema>

export default function NewTicket() {
  const nav = useNavigate()
  const categories = useCategories()
  const myAssets = useQuery({ queryKey: ['my-assets'], queryFn: () => api.get<Paged<Asset>>('/assets', { mine: true, pageSize: 50 }) })
  const [files, setFiles] = useState<FileDto[]>([])
  const { register, control, handleSubmit, setError, formState: { errors } } = useForm<Values>({
    resolver: zodResolver(schema), mode: 'onTouched', defaultValues: { priority: 'Medium', type: 'Incident', categoryId: 0, assetId: null },
  })
  const title = useWatch({ control, name: 'title' }) ?? ''
  const [term, setTerm] = useState('')
  useEffect(() => { const t = setTimeout(() => setTerm(title.trim()), 500); return () => clearTimeout(t) }, [title])
  const suggestions = useQuery({ queryKey: ['kb-suggest', term], queryFn: () => api.get<Paged<ArticleListItem>>('/kb', { search: term, pageSize: 3 }), enabled: term.length >= 4 })

  const create = useMutation({
    mutationFn: (v: Values) => api.send<TicketDetail>('post', '/tickets', { ...v, attachmentIds: files.map((f) => f.id) }),
    onSuccess: ({ data, message }) => { toast.success(message ?? 'Talebiniz oluşturuldu.'); nav(`/tickets/${data.id}`) },
    onError: (e) => {
      if (e instanceof ApiError && e.errors) Object.entries(e.errors).forEach(([k, m]) => setError(k as keyof Values, { message: m[0] }))
      toast.error(errorMessage(e))
    },
  })

  return (
    <div className="mx-auto max-w-5xl">
      <Link to="/tickets" className="mb-3 inline-flex items-center gap-1.5 text-sm muted hover:text-ink-900 dark:hover:text-white"><ArrowLeft className="h-4 w-4" /> Talepler</Link>
      <PageHeader title="Yeni destek talebi" description="Sorununuzu kısaca anlatın; talebiniz kategoriye ve yönlendirme kurallarına göre doğru ekibe otomatik iletilir." />
      <form className="grid gap-6 lg:grid-cols-3" onSubmit={handleSubmit((v) => create.mutate(v))} noValidate>
        <div className="space-y-6 lg:col-span-2">
          <Card title="Kategori">
            <Controller control={control} name="categoryId" render={({ field }) => (
              <div className="grid gap-2 sm:grid-cols-2" role="radiogroup" aria-label="Kategori">
                {categories.data?.filter((c) => c.isActive).map((c) => (
                  <button type="button" key={c.id} role="radio" aria-checked={field.value === c.id} onClick={() => field.onChange(c.id)}
                    className={cn('flex items-start gap-3 rounded-xl border p-3 text-left transition', field.value === c.id ? 'border-brand-500 ring-3 ring-brand-500/15' : 'border-ink-200 hover:border-ink-300 dark:border-white/10 dark:hover:border-white/20')}>
                    <span className="mt-0.5 h-3 w-3 shrink-0 rounded-full" style={{ background: c.color, boxShadow: `0 0 0 4px ${tint(c.color, 0.15)}` }} />
                    <span><span className="block text-sm font-semibold text-ink-900 dark:text-white">{c.name}</span><span className="block text-xs muted">{c.description}</span></span>
                  </button>
                ))}
              </div>
            )} />
            {errors.categoryId && <p className="error-text">{errors.categoryId.message}</p>}
          </Card>
          <Card title="Talep bilgileri">
            <div className="space-y-4">
              <Field label="Başlık" required error={errors.title?.message} htmlFor="title">
                <input id="title" className={cn('input', errors.title && 'input-error')} placeholder="ör. VPN bağlantısı sürekli kopuyor" {...register('title')} />
              </Field>
              <Field label="Açıklama" required error={errors.description?.message} help="Hata mesajı, ne zamandan beri yaşandığı ve denedikleriniz çözümü hızlandırır." htmlFor="desc">
                <textarea id="desc" rows={7} className={cn('input', errors.description && 'input-error')} {...register('description')} />
              </Field>
              <div className="grid gap-4 sm:grid-cols-3">
                <Field label="Tür"><select className="input" {...register('type')}><option value="Incident">Arıza / sorun</option><option value="Request">Hizmet talebi</option></select></Field>
                <Field label="Öncelik"><select className="input" {...register('priority')}>{(['Low', 'Medium', 'High', 'Critical'] as const).map((p) => <option key={p} value={p}>{priorityMeta[p].label}</option>)}</select></Field>
                <Field label="İlgili cihaz">
                  <Controller control={control} name="assetId" render={({ field }) => (
                    <select className="input" value={field.value ?? ''} onChange={(e) => field.onChange(e.target.value ? Number(e.target.value) : null)}>
                      <option value="">Seçilmedi</option>
                      {myAssets.data?.items.map((a) => <option key={a.id} value={a.id}>{assetTypeLabel[a.type]} · {a.assetTag}</option>)}
                    </select>
                  )} />
                </Field>
              </div>
              <Field label="Ekler" help="Ekran görüntüsü veya belge ekleyebilirsiniz.">
                <FileUploader value={files} onChange={setFiles} multiple />
              </Field>
            </div>
          </Card>
          <div className="flex justify-end gap-2">
            <Link to="/tickets" className="btn-secondary">Vazgeç</Link>
            <button className="btn-primary" disabled={create.isPending}>{create.isPending ? <Spinner /> : <Send className="h-4 w-4" />} Talebi gönder</button>
          </div>
        </div>
        <aside className="space-y-4">
          <div className="card p-5">
            <div className="flex items-center gap-2 font-semibold text-ink-900 dark:text-white"><Lightbulb className="h-4 w-4 text-amber-500" /> Belki yardımcı olur</div>
            {term.length < 4 ? <p className="mt-2 text-sm muted">Başlığı yazdıkça bilgi bankasından ilgili makaleler önerilir.</p>
              : suggestions.data?.items.length ? (
                <ul className="mt-3 space-y-2">
                  {suggestions.data.items.map((a) => (
                    <li key={a.id}><Link to={`/kb/${a.id}`} target="_blank" className="block rounded-lg border border-ink-200 p-3 hover:border-brand-300 dark:border-white/10">
                      <span className="flex items-center gap-2 text-sm font-medium text-ink-900 dark:text-white"><BookOpen className="h-3.5 w-3.5 text-brand-500" /> {a.title}</span>
                      <span className="mt-1 line-clamp-2 block text-xs muted">{a.summary}</span>
                    </Link></li>
                  ))}
                </ul>
              ) : <p className="mt-2 text-sm muted">İlgili makale bulunamadı.</p>}
          </div>
          <div className="card p-5 text-sm">
            <div className="font-semibold text-ink-900 dark:text-white">Öncelik ve SLA hedefleri</div>
            <ul className="mt-3 space-y-1.5 muted">
              <li className="flex justify-between"><span>Kritik</span><b>4 saat</b></li>
              <li className="flex justify-between"><span>Yüksek</span><b>8 saat</b></li>
              <li className="flex justify-between"><span>Orta</span><b>24 saat</b></li>
              <li className="flex justify-between"><span>Düşük</span><b>72 saat</b></li>
            </ul>
          </div>
        </aside>
      </form>
    </div>
  )
}
