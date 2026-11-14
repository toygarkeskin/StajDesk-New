import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Eye, EyeOff, Globe, Pencil, Pin, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { Badge, Drawer, EmptyState, Field, PageHeader, QueryState, Spinner, Tabs, Toggle } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api, errorMessage } from '@/lib/api'
import { employmentLabel, fmt, toInputDate, toInputDateTime } from '@/lib/format'
import { useDepartments } from '@/lib/queries'
import { DepartmentOptions } from '@/components/DepartmentOptions'
import type { Announcement, AnnouncementKind, Banner, CompanyEvent, EmploymentType, Job } from '@/lib/types'
import { cn } from '@/lib/utils'

type Tab = 'banners' | 'announcements' | 'events' | 'jobs'
const themes = { crimson: 'from-brand-700 to-rose-500', midnight: 'from-ink-950 to-indigo-900', sunset: 'from-orange-600 to-fuchsia-700', ocean: 'from-sky-700 to-teal-600' } as Record<string, string>
const iso = (v: string) => (v ? new Date(v).toISOString() : null)

function useSave(path: string, key: string, done: () => void) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id?: number; body: unknown }) => (id ? api.put(`${path}/${id}`, body) : api.post(path, body)),
    onSuccess: () => { qc.invalidateQueries({ queryKey: [key] }); qc.invalidateQueries({ queryKey: ['public-home'] }); toast.success('Kaydedildi.'); done() },
    onError: (e) => toast.error(errorMessage(e)),
  })
}

function BannerDrawer({ b, onClose }: { b: Banner | null; onClose: () => void }) {
  const [v, setV] = useState({ title: b?.title ?? '', subtitle: b?.subtitle ?? '', imageUrl: b?.imageUrl ?? '', theme: b?.theme ?? 'crimson', linkUrl: b?.linkUrl ?? '', linkText: b?.linkText ?? '', sortOrder: b?.sortOrder ?? 0, startsAt: toInputDateTime(b?.startsAt), endsAt: toInputDateTime(b?.endsAt), isActive: b?.isActive ?? true })
  const save = useSave('/banners', 'banners', onClose)
  return (
    <Drawer open onClose={onClose} title={b ? 'Banner düzenle' : 'Yeni banner'} footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate({ id: b?.id, body: { ...v, imageUrl: v.imageUrl || null, linkUrl: v.linkUrl || null, startsAt: iso(v.startsAt), endsAt: iso(v.endsAt) } })}>{save.isPending && <Spinner />} Kaydet</button></>}>
      <div className={cn('mb-5 rounded-2xl bg-gradient-to-br p-6 text-white', themes[v.theme])}><div className="font-display text-xl font-bold">{v.title || 'Banner başlığı'}</div><div className="mt-1 text-sm text-white/80">{v.subtitle}</div>{v.linkText && <span className="mt-3 inline-block rounded-lg bg-white px-3 py-1.5 text-xs font-semibold text-ink-900">{v.linkText}</span>}</div>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Başlık" required className="sm:col-span-2"><input className="input" value={v.title} onChange={(e) => setV({ ...v, title: e.target.value })} /></Field>
        <Field label="Alt metin" className="sm:col-span-2"><input className="input" value={v.subtitle} onChange={(e) => setV({ ...v, subtitle: e.target.value })} /></Field>
        <Field label="Tema"><select className="input" value={v.theme} onChange={(e) => setV({ ...v, theme: e.target.value })}><option value="crimson">Kırmızı</option><option value="midnight">Gece</option><option value="sunset">Gün batımı</option><option value="ocean">Okyanus</option></select></Field>
        <Field label="Görsel adresi (isteğe bağlı)" help="https:// ile başlamalı"><input className="input" value={v.imageUrl} onChange={(e) => setV({ ...v, imageUrl: e.target.value })} /></Field>
        <Field label="Buton metni"><input className="input" value={v.linkText} onChange={(e) => setV({ ...v, linkText: e.target.value })} /></Field>
        <Field label="Bağlantı" help="/login, #kariyer veya https://…"><input className="input" value={v.linkUrl} onChange={(e) => setV({ ...v, linkUrl: e.target.value })} /></Field>
        <Field label="Yayın başlangıcı"><input type="datetime-local" className="input" value={v.startsAt} onChange={(e) => setV({ ...v, startsAt: e.target.value })} /></Field>
        <Field label="Yayın bitişi"><input type="datetime-local" className="input" value={v.endsAt} onChange={(e) => setV({ ...v, endsAt: e.target.value })} /></Field>
        <Toggle checked={v.isActive} onChange={(x) => setV({ ...v, isActive: x })} label="Aktif" />
      </div>
    </Drawer>
  )
}

function AnnDrawer({ a, onClose }: { a: Announcement | null; onClose: () => void }) {
  const departments = useDepartments()
  const [v, setV] = useState({ title: a?.title ?? '', summary: a?.summary ?? '', body: a?.body ?? '', kind: a?.kind ?? 'Announcement' as AnnouncementKind, isPublic: a?.isPublic ?? false, isPinned: a?.isPinned ?? false, publishAt: toInputDateTime(a?.publishAt), expiresAt: toInputDateTime(a?.expiresAt), departmentId: a?.department?.id?.toString() ?? '', notify: !a })
  const save = useSave('/announcements', 'announcements', onClose)
  return (
    <Drawer open onClose={onClose} width="max-w-2xl" title={a ? 'İçeriği düzenle' : 'Yeni duyuru / haber'} footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate({ id: a?.id, body: { ...v, publishAt: iso(v.publishAt), expiresAt: iso(v.expiresAt), departmentId: v.departmentId ? Number(v.departmentId) : null } })}>{save.isPending && <Spinner />} Yayınla</button></>}>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Tür"><select className="input" value={v.kind} onChange={(e) => setV({ ...v, kind: e.target.value as AnnouncementKind })}><option value="Announcement">Duyuru</option><option value="News">Haber</option></select></Field>
        <Field label="Hedef kitle"><select className="input" value={v.departmentId} onChange={(e) => setV({ ...v, departmentId: e.target.value })}><option value="">Tüm çalışanlar</option><DepartmentOptions departments={departments.data} /></select></Field>
        <Field label="Başlık" required className="sm:col-span-2"><input className="input" value={v.title} onChange={(e) => setV({ ...v, title: e.target.value })} /></Field>
        <Field label="Özet" required className="sm:col-span-2"><input className="input" value={v.summary} onChange={(e) => setV({ ...v, summary: e.target.value })} /></Field>
        <Field label="İçerik (Markdown)" required className="sm:col-span-2"><textarea className="input" rows={8} value={v.body} onChange={(e) => setV({ ...v, body: e.target.value })} /></Field>
        <Field label="Yayın tarihi"><input type="datetime-local" className="input" value={v.publishAt} onChange={(e) => setV({ ...v, publishAt: e.target.value })} /></Field>
        <Field label="Bitiş tarihi"><input type="datetime-local" className="input" value={v.expiresAt} onChange={(e) => setV({ ...v, expiresAt: e.target.value })} /></Field>
        <div className="space-y-3 sm:col-span-2">
          <Toggle checked={v.isPublic} onChange={(x) => setV({ ...v, isPublic: x })} label="Genel tanıtım sitesinde göster (kişisel bilgi içermemeli)" />
          <Toggle checked={v.isPinned} onChange={(x) => setV({ ...v, isPinned: x })} label="Sabitle" />
          {!a && <Toggle checked={v.notify} onChange={(x) => setV({ ...v, notify: x })} label="Hedef kitleye bildirim gönder" />}
        </div>
      </div>
    </Drawer>
  )
}

function EventDrawer({ e: ev, onClose }: { e: CompanyEvent | null; onClose: () => void }) {
  const [v, setV] = useState({ title: ev?.title ?? '', description: ev?.description ?? '', location: ev?.location ?? '', category: ev?.category ?? 'Kurumsal', startsAt: toInputDateTime(ev?.startsAt), endsAt: toInputDateTime(ev?.endsAt), isPublic: ev?.isPublic ?? false })
  const save = useSave('/events', 'events', onClose)
  return (
    <Drawer open onClose={onClose} title={ev ? 'Etkinliği düzenle' : 'Yeni etkinlik'} footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" disabled={!v.startsAt || save.isPending} onClick={() => save.mutate({ id: ev?.id, body: { ...v, startsAt: iso(v.startsAt), endsAt: iso(v.endsAt) } })}>{save.isPending && <Spinner />} Kaydet</button></>}>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Etkinlik" required className="sm:col-span-2"><input className="input" value={v.title} onChange={(e) => setV({ ...v, title: e.target.value })} /></Field>
        <Field label="Açıklama" className="sm:col-span-2"><textarea className="input" rows={3} value={v.description} onChange={(e) => setV({ ...v, description: e.target.value })} /></Field>
        <Field label="Yer"><input className="input" value={v.location} onChange={(e) => setV({ ...v, location: e.target.value })} /></Field>
        <Field label="Kategori"><input className="input" value={v.category} onChange={(e) => setV({ ...v, category: e.target.value })} /></Field>
        <Field label="Başlangıç" required><input type="datetime-local" className="input" value={v.startsAt} onChange={(e) => setV({ ...v, startsAt: e.target.value })} /></Field>
        <Field label="Bitiş"><input type="datetime-local" className="input" value={v.endsAt} onChange={(e) => setV({ ...v, endsAt: e.target.value })} /></Field>
        <Toggle checked={v.isPublic} onChange={(x) => setV({ ...v, isPublic: x })} label="Genel sitede göster" />
      </div>
    </Drawer>
  )
}

function JobDrawer({ j, onClose }: { j: Job | null; onClose: () => void }) {
  const departments = useDepartments()
  const [v, setV] = useState({ title: j?.title ?? '', departmentId: j?.department?.id?.toString() ?? '', location: j?.location ?? '', employmentType: j?.employmentType ?? 'FullTime' as EmploymentType, description: j?.description ?? '', requirements: j?.requirements ?? '', isPublic: j?.isPublic ?? true, isActive: j?.isActive ?? true, closesAt: toInputDate(j?.closesAt) })
  const save = useSave('/jobs', 'jobs-admin', onClose)
  return (
    <Drawer open onClose={onClose} width="max-w-2xl" title={j ? 'İlanı düzenle' : 'Yeni iş ilanı'} footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate({ id: j?.id, body: { ...v, departmentId: v.departmentId ? Number(v.departmentId) : null, closesAt: v.closesAt || null } })}>{save.isPending && <Spinner />} Kaydet</button></>}>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Pozisyon" required className="sm:col-span-2"><input className="input" value={v.title} onChange={(e) => setV({ ...v, title: e.target.value })} /></Field>
        <Field label="Departman"><select className="input" value={v.departmentId} onChange={(e) => setV({ ...v, departmentId: e.target.value })}><option value="">Belirtilmedi</option><DepartmentOptions departments={departments.data} /></select></Field>
        <Field label="Lokasyon"><input className="input" value={v.location} onChange={(e) => setV({ ...v, location: e.target.value })} /></Field>
        <Field label="Çalışma türü"><select className="input" value={v.employmentType} onChange={(e) => setV({ ...v, employmentType: e.target.value as EmploymentType })}>{Object.entries(employmentLabel).map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></Field>
        <Field label="Son başvuru"><input type="date" className="input" value={v.closesAt} onChange={(e) => setV({ ...v, closesAt: e.target.value })} /></Field>
        <Field label="Açıklama" required className="sm:col-span-2"><textarea className="input" rows={4} value={v.description} onChange={(e) => setV({ ...v, description: e.target.value })} /></Field>
        <Field label="Aranan nitelikler" className="sm:col-span-2"><textarea className="input" rows={4} value={v.requirements} onChange={(e) => setV({ ...v, requirements: e.target.value })} /></Field>
        <Toggle checked={v.isPublic} onChange={(x) => setV({ ...v, isPublic: x })} label="Genel sitede yayınla" />
        <Toggle checked={v.isActive} onChange={(x) => setV({ ...v, isActive: x })} label="Aktif" />
      </div>
    </Drawer>
  )
}

export default function ContentAdmin() {
  const [sp, setSp] = useSearchParams()
  const tab = (sp.get('tab') ?? 'banners') as Tab
  const qc = useQueryClient()
  const confirm = useConfirm()
  const banners = useQuery({ queryKey: ['banners'], queryFn: () => api.get<Banner[]>('/banners'), enabled: tab === 'banners' })
  const anns = useQuery({ queryKey: ['announcements', 'manage'], queryFn: () => api.get<Announcement[]>('/announcements', { manage: true, take: 100 }), enabled: tab === 'announcements' })
  const events = useQuery({ queryKey: ['events', 'all'], queryFn: () => api.get<CompanyEvent[]>('/events', { upcoming: false }), enabled: tab === 'events' })
  const jobs = useQuery({ queryKey: ['jobs-admin'], queryFn: () => api.get<Job[]>('/jobs', { all: true }), enabled: tab === 'jobs' })
  const [edit, setEdit] = useState<{ kind: Tab; item: unknown } | null>(null)
  const del = useMutation({ mutationFn: (path: string) => api.del(path), onSuccess: () => { qc.invalidateQueries(); toast.success('Silindi.') } })
  const reorder = useMutation({ mutationFn: (ids: number[]) => api.put('/banners/order', { ids }), onSuccess: () => qc.invalidateQueries({ queryKey: ['banners'] }) })
  const remove = async (title: string, path: string) => (await confirm({ title: 'Silinsin mi?', message: title, danger: true, confirmText: 'Sil' })) && del.mutate(path)

  return (
    <div>
      <PageHeader title="İçerik & Banner Yönetimi" description="Genel site bannerları, duyurular, etkinlikler ve iş ilanları." actions={<button className="btn-primary" onClick={() => setEdit({ kind: tab, item: null })}><Plus className="h-4 w-4" /> Yeni</button>} />
      <Tabs className="mb-4" value={tab} onChange={(t) => setSp({ tab: t })} tabs={[{ value: 'banners', label: 'Bannerlar' }, { value: 'announcements', label: 'Duyuru & Haber' }, { value: 'events', label: 'Etkinlikler' }, { value: 'jobs', label: 'İş ilanları' }]} />
      {tab === 'banners' && (
        <QueryState query={banners} empty={(d) => d.length === 0 && <div className="card"><EmptyState title="Banner yok" /></div>}>
          {(list) => (
            <div className="space-y-3">
              {list.map((b, i) => (
                <div key={b.id} className="card flex items-center gap-4 p-3">
                  <div className={cn('h-16 w-28 shrink-0 rounded-lg bg-gradient-to-br', themes[b.theme])} />
                  <div className="min-w-0 flex-1"><div className="flex items-center gap-2"><span className="font-semibold text-ink-900 dark:text-white">{b.title}</span>{b.isLive ? <Badge tone="green"><Eye className="h-3 w-3" /> Yayında</Badge> : <Badge><EyeOff className="h-3 w-3" /> Yayında değil</Badge>}</div><div className="truncate text-xs muted">{b.subtitle}</div><div className="text-xs muted">{b.startsAt ? fmt.dateTime(b.startsAt) : 'Hemen'} → {b.endsAt ? fmt.dateTime(b.endsAt) : 'Süresiz'}</div></div>
                  <button className="btn-ghost btn-icon" disabled={i === 0} onClick={() => { const ids = list.map((x) => x.id); [ids[i - 1], ids[i]] = [ids[i], ids[i - 1]]; reorder.mutate(ids) }} aria-label="Yukarı"><ArrowUp className="h-4 w-4" /></button>
                  <button className="btn-ghost btn-icon" disabled={i === list.length - 1} onClick={() => { const ids = list.map((x) => x.id); [ids[i + 1], ids[i]] = [ids[i], ids[i + 1]]; reorder.mutate(ids) }} aria-label="Aşağı"><ArrowDown className="h-4 w-4" /></button>
                  <button className="btn-ghost btn-icon" onClick={() => setEdit({ kind: 'banners', item: b })} aria-label="Düzenle"><Pencil className="h-4 w-4" /></button>
                  <button className="btn-ghost btn-icon text-red-600" onClick={() => remove(b.title, `/banners/${b.id}`)} aria-label="Sil"><Trash2 className="h-4 w-4" /></button>
                </div>
              ))}
            </div>
          )}
        </QueryState>
      )}
      {tab === 'announcements' && (
        <QueryState query={anns}>
          {(list) => (
            <div className="card divide-y divide-ink-100 dark:divide-white/[0.05]">
              {list.map((a) => (
                <div key={a.id} className="flex items-center gap-3 px-5 py-3">
                  <div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2">{a.isPinned && <Pin className="h-3.5 w-3.5 text-brand-500" />}<span className="font-medium text-ink-900 dark:text-white">{a.title}</span><Badge>{a.kind === 'News' ? 'Haber' : 'Duyuru'}</Badge>{a.isPublic && <Badge tone="cyan"><Globe className="h-3 w-3" /> Genel</Badge>}{a.department && <Badge tone="violet">{a.department.name}</Badge>}</div><div className="text-xs muted">{fmt.dateTime(a.publishAt)} · {a.author.fullName}</div></div>
                  <button className="btn-ghost btn-icon" onClick={() => setEdit({ kind: 'announcements', item: a })} aria-label="Düzenle"><Pencil className="h-4 w-4" /></button>
                  <button className="btn-ghost btn-icon text-red-600" onClick={() => remove(a.title, `/announcements/${a.id}`)} aria-label="Sil"><Trash2 className="h-4 w-4" /></button>
                </div>
              ))}
            </div>
          )}
        </QueryState>
      )}
      {tab === 'events' && (
        <QueryState query={events}>
          {(list) => (
            <div className="card divide-y divide-ink-100 dark:divide-white/[0.05]">
              {list.map((e) => (
                <div key={e.id} className="flex items-center gap-3 px-5 py-3">
                  <div className="min-w-0 flex-1"><div className="flex items-center gap-2"><span className="font-medium text-ink-900 dark:text-white">{e.title}</span><Badge tone="violet">{e.category}</Badge>{e.isPublic && <Badge tone="cyan">Genel</Badge>}</div><div className="text-xs muted">{fmt.dateTime(e.startsAt)} · {e.location} · {e.attendeeCount} katılımcı</div></div>
                  <button className="btn-ghost btn-icon" onClick={() => setEdit({ kind: 'events', item: e })} aria-label="Düzenle"><Pencil className="h-4 w-4" /></button>
                  <button className="btn-ghost btn-icon text-red-600" onClick={() => remove(e.title, `/events/${e.id}`)} aria-label="Sil"><Trash2 className="h-4 w-4" /></button>
                </div>
              ))}
            </div>
          )}
        </QueryState>
      )}
      {tab === 'jobs' && (
        <QueryState query={jobs}>
          {(list) => (
            <div className="card divide-y divide-ink-100 dark:divide-white/[0.05]">
              {list.map((j) => (
                <div key={j.id} className="flex items-center gap-3 px-5 py-3">
                  <div className="min-w-0 flex-1"><div className="flex items-center gap-2"><span className="font-medium text-ink-900 dark:text-white">{j.title}</span>{j.isActive ? <Badge tone="green">Aktif</Badge> : <Badge>Pasif</Badge>}{j.isPublic && <Badge tone="cyan">Genel</Badge>}</div><div className="text-xs muted">{j.department?.name} · {j.location} · Son başvuru {fmt.date(j.closesAt)}</div></div>
                  <button className="btn-ghost btn-icon" onClick={() => setEdit({ kind: 'jobs', item: j })} aria-label="Düzenle"><Pencil className="h-4 w-4" /></button>
                  <button className="btn-ghost btn-icon text-red-600" onClick={() => remove(j.title, `/jobs/${j.id}`)} aria-label="Sil"><Trash2 className="h-4 w-4" /></button>
                </div>
              ))}
            </div>
          )}
        </QueryState>
      )}
      {edit?.kind === 'banners' && <BannerDrawer b={edit.item as Banner | null} onClose={() => setEdit(null)} />}
      {edit?.kind === 'announcements' && <AnnDrawer a={edit.item as Announcement | null} onClose={() => setEdit(null)} />}
      {edit?.kind === 'events' && <EventDrawer e={edit.item as CompanyEvent | null} onClose={() => setEdit(null)} />}
      {edit?.kind === 'jobs' && <JobDrawer j={edit.item as Job | null} onClose={() => setEdit(null)} />}
    </div>
  )
}
