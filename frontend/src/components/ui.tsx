import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, ChevronLeft, ChevronRight, FileUp, Inbox, Loader2, Paperclip, RotateCw, Search, X } from 'lucide-react'
import { useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { api, errorMessage } from '@/lib/api'
import { ConfirmCtx, type ConfirmOpts } from './confirm'
import { fmt } from '@/lib/format'
import type { DirectoryEntry, FileDto, Paged } from '@/lib/types'
import { avatarColor, cn } from '@/lib/utils'

// ---------------------------------------------------------------- Rozet
type Tone = 'gray' | 'blue' | 'amber' | 'green' | 'red' | 'violet' | 'brand' | 'cyan'
const tones: Record<Tone, string> = {
  gray: 'bg-ink-100 text-ink-600 dark:bg-white/[0.07] dark:text-ink-300',
  blue: 'bg-sky-50 text-sky-700 dark:bg-sky-400/10 dark:text-sky-300',
  amber: 'bg-amber-50 text-amber-700 dark:bg-amber-400/10 dark:text-amber-300',
  green: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-400/10 dark:text-emerald-300',
  red: 'bg-red-50 text-red-700 dark:bg-red-400/10 dark:text-red-300',
  violet: 'bg-violet-50 text-violet-700 dark:bg-violet-400/10 dark:text-violet-300',
  brand: 'bg-brand-50 text-brand-700 dark:bg-brand-400/10 dark:text-brand-300',
  cyan: 'bg-cyan-50 text-cyan-700 dark:bg-cyan-400/10 dark:text-cyan-300',
}
const dots: Record<Tone, string> = { gray: 'bg-ink-400', blue: 'bg-sky-500', amber: 'bg-amber-500', green: 'bg-emerald-500', red: 'bg-red-500', violet: 'bg-violet-500', brand: 'bg-brand-500', cyan: 'bg-cyan-500' }

export function Badge({ tone = 'gray', dot, children, className }: { tone?: Tone; dot?: boolean; children: ReactNode; className?: string }) {
  return (
    <span className={cn('chip', tones[tone], className)}>
      {dot && <span className={cn('h-1.5 w-1.5 rounded-full', dots[tone])} />}
      {children}
    </span>
  )
}

export function ColorDot({ color, label }: { color: string; label: string }) {
  return (
    <span className="inline-flex items-center gap-1.5 text-[13px]">
      <span className="h-2 w-2 shrink-0 rounded-full" style={{ background: color }} />
      <span className="truncate">{label}</span>
    </span>
  )
}

// ---------------------------------------------------------------- Yerleşim
export function PageHeader({ title, description, actions, back }: { title: string; description?: ReactNode; actions?: ReactNode; back?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
      <div className="min-w-0">
        {back}
        <h1 className="text-[22px] font-bold leading-tight sm:text-2xl">{title}</h1>
        {description && <p className="mt-1 muted">{description}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  )
}

export function Card({ title, actions, children, className, bodyClass, footer }: { title?: ReactNode; actions?: ReactNode; children: ReactNode; className?: string; bodyClass?: string; footer?: ReactNode }) {
  return (
    <section className={cn('card', className)}>
      {(title || actions) && (
        <header className="card-header">
          <h2 className="card-title">{title}</h2>
          {actions && <div className="flex items-center gap-2">{actions}</div>}
        </header>
      )}
      <div className={cn(bodyClass ?? 'p-5')}>{children}</div>
      {footer && <footer className="border-t border-ink-100 px-5 py-3 dark:border-white/[0.06]">{footer}</footer>}
    </section>
  )
}

export function StatCard({ label, value, hint, icon, tone = 'brand', onClick }: { label: string; value: ReactNode; hint?: ReactNode; icon?: ReactNode; tone?: Tone; onClick?: () => void }) {
  return (
    <button type="button" onClick={onClick} disabled={!onClick}
      className={cn('card flex w-full items-start justify-between gap-3 p-4 text-left transition', onClick && 'cursor-pointer hover:border-brand-300/60 dark:hover:border-brand-400/30')}>
      <div className="min-w-0">
        <div className="text-xs font-medium muted">{label}</div>
        <div className="mt-1.5 font-display text-2xl font-bold text-ink-900 dark:text-white">{value}</div>
        {hint && <div className="mt-1 text-xs muted">{hint}</div>}
      </div>
      {icon && <div className={cn('grid h-9 w-9 shrink-0 place-items-center rounded-lg', tones[tone])}>{icon}</div>}
    </button>
  )
}

export function Avatar({ name, size = 32, className }: { name: string; size?: number; className?: string }) {
  return (
    <span className={cn('inline-grid shrink-0 place-items-center rounded-full font-semibold text-white ring-2 ring-white dark:ring-ink-900', className)}
      style={{ width: size, height: size, fontSize: size * 0.38, background: avatarColor(name) }} title={name}>
      {fmt.initials(name)}
    </span>
  )
}

export function PersonCell({ name, sub }: { name: string; sub?: ReactNode }) {
  return (
    <div className="flex min-w-0 items-center gap-2.5">
      <Avatar name={name} size={28} />
      <div className="min-w-0">
        <div className="truncate font-medium text-ink-900 dark:text-white">{name}</div>
        {sub && <div className="truncate text-xs muted">{sub}</div>}
      </div>
    </div>
  )
}

export function Progress({ value, tone = 'brand', className }: { value: number; tone?: 'brand' | 'green' | 'amber'; className?: string }) {
  const c = tone === 'green' ? 'bg-emerald-500' : tone === 'amber' ? 'bg-amber-500' : 'bg-brand-500'
  return (
    <div className={cn('h-1.5 w-full overflow-hidden rounded-full bg-ink-100 dark:bg-white/[0.07]', className)}>
      <div className={cn('h-full rounded-full transition-all', c)} style={{ width: `${Math.max(0, Math.min(100, value))}%` }} />
    </div>
  )
}

// ---------------------------------------------------------------- Durumlar
export function Spinner({ className }: { className?: string }) {
  return <Loader2 className={cn('h-4 w-4 animate-spin', className)} aria-label="Yükleniyor" />
}

export function Skeleton({ className }: { className?: string }) {
  return <div className={cn('animate-pulse rounded-md bg-ink-100 dark:bg-white/[0.06]', className)} />
}

export function SkeletonRows({ rows = 6 }: { rows?: number }) {
  return (
    <div className="space-y-3 p-5">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="flex items-center gap-3">
          <Skeleton className="h-8 w-8 rounded-full" />
          <Skeleton className="h-4 flex-1" />
          <Skeleton className="h-4 w-24" />
        </div>
      ))}
    </div>
  )
}

export function EmptyState({ title, description, action, icon }: { title: string; description?: string; action?: ReactNode; icon?: ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center px-6 py-14 text-center">
      <div className="mb-3 grid h-11 w-11 place-items-center rounded-xl bg-ink-100 text-ink-500 dark:bg-white/[0.06] dark:text-ink-400">{icon ?? <Inbox className="h-5 w-5" />}</div>
      <div className="font-semibold text-ink-900 dark:text-white">{title}</div>
      {description && <p className="mt-1 max-w-sm text-sm muted">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  )
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center px-6 py-12 text-center">
      <div className="mb-3 grid h-11 w-11 place-items-center rounded-xl bg-red-50 text-red-600 dark:bg-red-400/10 dark:text-red-300"><AlertTriangle className="h-5 w-5" /></div>
      <div className="font-semibold text-ink-900 dark:text-white">Veriler yüklenemedi</div>
      <p className="mt-1 max-w-sm text-sm muted">{errorMessage(error)}</p>
      {onRetry && <button className="btn-secondary btn-sm mt-4" onClick={onRetry}><RotateCw className="h-3.5 w-3.5" /> Tekrar dene</button>}
    </div>
  )
}

/** Sorgu durumunu tek yerden yönetir: yükleniyor / hata / boş / içerik */
export function QueryState<T>({ query, empty, skeleton, children }: { query: { data?: T; isLoading: boolean; error: unknown; refetch: () => void }; empty?: (d: T) => ReactNode | false; skeleton?: ReactNode; children: (d: T) => ReactNode }) {
  if (query.isLoading) return <>{skeleton ?? <SkeletonRows />}</>
  if (query.error) return <ErrorState error={query.error} onRetry={query.refetch} />
  if (query.data === undefined) return null
  const e = empty?.(query.data)
  if (e) return <>{e}</>
  return <>{children(query.data)}</>
}

// ---------------------------------------------------------------- Form öğeleri
export function Field({ label, error, help, required, children, className, htmlFor }: { label?: ReactNode; error?: string; help?: ReactNode; required?: boolean; children: ReactNode; className?: string; htmlFor?: string }) {
  return (
    <div className={className}>
      {label && <label className="label" htmlFor={htmlFor}>{label}{required && <span className="ml-0.5 text-brand-600">*</span>}</label>}
      {children}
      {error ? <p className="error-text" role="alert">{error}</p> : help ? <p className="help">{help}</p> : null}
    </div>
  )
}

export function SearchInput({ value, onChange, placeholder = 'Ara…', className }: { value: string; onChange: (v: string) => void; placeholder?: string; className?: string }) {
  const [v, setV] = useState(value)
  // Dışarıdan değer değişirse (ör. filtre temizleme) yerel metni eşitle — efekt yerine render sırasında (React önerisi)
  const [prev, setPrev] = useState(value)
  if (value !== prev) { setPrev(value); setV(value) }
  useEffect(() => {
    const t = setTimeout(() => v !== value && onChange(v), 300)
    return () => clearTimeout(t)
  }, [v]) // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <div className={cn('relative', className)}>
      <Search className="pointer-events-none absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-ink-400" />
      <input className="input pl-9" value={v} onChange={(e) => setV(e.target.value)} placeholder={placeholder} aria-label={placeholder} />
    </div>
  )
}

export function Toggle({ checked, onChange, label, disabled }: { checked: boolean; onChange: (v: boolean) => void; label?: ReactNode; disabled?: boolean }) {
  return (
    <label className={cn('inline-flex cursor-pointer select-none items-center gap-2.5', disabled && 'opacity-50')}>
      <button type="button" role="switch" aria-checked={checked} disabled={disabled} onClick={() => onChange(!checked)}
        className={cn('relative h-5 w-9 rounded-full transition-colors', checked ? 'bg-brand-600' : 'bg-ink-200 dark:bg-white/15')}>
        <span className={cn('absolute top-0.5 left-0.5 h-4 w-4 rounded-full bg-white shadow transition-transform', checked && 'translate-x-4')} />
      </button>
      {label && <span className="text-sm">{label}</span>}
    </label>
  )
}

export function Tabs<T extends string>({ value, onChange, tabs, className }: { value: T; onChange: (v: T) => void; tabs: { value: T; label: ReactNode; count?: number }[]; className?: string }) {
  return (
    <div className={cn('flex gap-1 overflow-x-auto border-b border-ink-200 no-scrollbar dark:border-white/[0.07]', className)} role="tablist">
      {tabs.map((t) => (
        <button key={t.value} role="tab" aria-selected={value === t.value} onClick={() => onChange(t.value)}
          className={cn('-mb-px flex shrink-0 items-center gap-2 border-b-2 px-3 py-2.5 text-sm font-medium transition-colors',
            value === t.value ? 'border-brand-600 text-ink-900 dark:border-brand-400 dark:text-white' : 'border-transparent text-ink-500 hover:text-ink-800 dark:text-ink-400 dark:hover:text-ink-200')}>
          {t.label}
          {t.count != null && <span className="rounded-full bg-ink-100 px-1.5 text-[11px] font-semibold dark:bg-white/10">{t.count}</span>}
        </button>
      ))}
    </div>
  )
}

export function Segmented<T extends string>({ value, onChange, options }: { value: T; onChange: (v: T) => void; options: { value: T; label: ReactNode; icon?: ReactNode }[] }) {
  return (
    <div className="inline-flex rounded-lg border border-ink-200 bg-white p-0.5 dark:border-white/10 dark:bg-white/[0.03]">
      {options.map((o) => (
        <button key={o.value} type="button" onClick={() => onChange(o.value)} aria-pressed={value === o.value}
          className={cn('flex h-8 items-center gap-1.5 rounded-md px-3 text-xs font-medium transition', value === o.value ? 'bg-ink-900 text-white dark:bg-white dark:text-ink-900' : 'text-ink-600 hover:text-ink-900 dark:text-ink-300')}>
          {o.icon}{o.label}
        </button>
      ))}
    </div>
  )
}

export function Pagination({ page, totalPages, total, onPage }: { page: number; totalPages: number; total: number; onPage: (p: number) => void }) {
  if (total === 0) return null
  return (
    <div className="flex items-center justify-between gap-3 border-t border-ink-100 px-4 py-3 text-xs muted dark:border-white/[0.06]">
      <span>Toplam <b className="text-ink-800 dark:text-ink-100">{fmt.number(total)}</b> kayıt · Sayfa {page}/{Math.max(1, totalPages)}</span>
      <div className="flex gap-1">
        <button className="btn-secondary btn-sm" disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label="Önceki sayfa"><ChevronLeft className="h-4 w-4" /></button>
        <button className="btn-secondary btn-sm" disabled={page >= totalPages} onClick={() => onPage(page + 1)} aria-label="Sonraki sayfa"><ChevronRight className="h-4 w-4" /></button>
      </div>
    </div>
  )
}

// ---------------------------------------------------------------- Katmanlar
/**
 * Açık katman yığını: iç içe modal/çekmecede Escape yalnızca en üsttekini kapatır ve gövde kaydırma kilidi
 * son katman kapanana kadar korunur (sıra karışsa bile kilit takılı kalmaz).
 */
const layerStack: symbol[] = []

function useEscape(onClose: () => void, open: boolean) {
  const closeRef = useRef(onClose)
  useEffect(() => { closeRef.current = onClose }, [onClose])
  useEffect(() => {
    if (!open) return
    const id = Symbol('layer')
    layerStack.push(id)
    document.body.style.overflow = 'hidden'
    const h = (e: KeyboardEvent) => {
      if (e.key !== 'Escape' || layerStack[layerStack.length - 1] !== id) return
      e.stopPropagation()
      closeRef.current()
    }
    window.addEventListener('keydown', h)
    return () => {
      window.removeEventListener('keydown', h)
      layerStack.splice(layerStack.indexOf(id), 1)
      if (layerStack.length === 0) document.body.style.overflow = ''
    }
  }, [open])
}

export function Modal({ open, onClose, title, children, footer, size = 'md' }: { open: boolean; onClose: () => void; title: ReactNode; children: ReactNode; footer?: ReactNode; size?: 'sm' | 'md' | 'lg' }) {
  useEscape(onClose, open)
  if (!open) return null
  return createPortal(
    <div className="fixed inset-0 z-50 flex items-end justify-center p-0 sm:items-center sm:p-4" role="dialog" aria-modal="true">
      <div className="absolute inset-0 bg-ink-950/50 backdrop-blur-[2px]" onClick={onClose} />
      <div className={cn('relative flex max-h-[92dvh] w-full flex-col rounded-t-2xl bg-white shadow-pop animate-in sm:rounded-2xl dark:bg-ink-900 dark:ring-1 dark:ring-white/10',
        size === 'sm' ? 'sm:max-w-md' : size === 'lg' ? 'sm:max-w-3xl' : 'sm:max-w-xl')}>
        <div className="flex items-center justify-between border-b border-ink-100 px-5 py-4 dark:border-white/[0.07]">
          <h3 className="text-base font-semibold">{title}</h3>
          <button className="btn-ghost btn-icon -mr-2" onClick={onClose} aria-label="Kapat"><X className="h-4 w-4" /></button>
        </div>
        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4 scroll-thin">{children}</div>
        {footer && <div className="flex justify-end gap-2 border-t border-ink-100 px-5 py-3 dark:border-white/[0.07]">{footer}</div>}
      </div>
    </div>,
    document.body,
  )
}

export function Drawer({ open, onClose, title, subtitle, children, footer, width = 'max-w-xl' }: { open: boolean; onClose: () => void; title: ReactNode; subtitle?: ReactNode; children: ReactNode; footer?: ReactNode; width?: string }) {
  useEscape(onClose, open)
  if (!open) return null
  return createPortal(
    <div className="fixed inset-0 z-50" role="dialog" aria-modal="true">
      <div className="absolute inset-0 bg-ink-950/40 backdrop-blur-[1px]" onClick={onClose} />
      <aside className={cn('absolute top-0 right-0 flex h-full w-full flex-col bg-white shadow-pop animate-drawer dark:bg-ink-900 dark:ring-1 dark:ring-white/10', width)}>
        <div className="flex items-start justify-between gap-3 border-b border-ink-100 px-5 py-4 dark:border-white/[0.07]">
          <div className="min-w-0">
            <h3 className="truncate text-base font-semibold">{title}</h3>
            {subtitle && <div className="mt-0.5 text-xs muted">{subtitle}</div>}
          </div>
          <button className="btn-ghost btn-icon -mr-2" onClick={onClose} aria-label="Kapat"><X className="h-4 w-4" /></button>
        </div>
        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5 scroll-thin">{children}</div>
        {footer && <div className="flex justify-end gap-2 border-t border-ink-100 px-5 py-3 dark:border-white/[0.07]">{footer}</div>}
      </aside>
    </div>,
    document.body,
  )
}

// ---------------------------------------------------------------- Onay diyaloğu

export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<(ConfirmOpts & { resolve: (v: string | false) => void }) | null>(null)
  const [text, setText] = useState('')
  const close = (v: string | false) => {
    state?.resolve(v)
    setState(null)
    setText('')
  }
  return (
    <ConfirmCtx.Provider value={(o) => new Promise((resolve) => setState({ ...o, resolve }))}>
      {children}
      <Modal open={!!state} onClose={() => close(false)} title={state?.title} size="sm"
        footer={<>
          <button className="btn-secondary" onClick={() => close(false)}>Vazgeç</button>
          <button className={state?.danger ? 'btn-danger' : 'btn-primary'} disabled={!!state?.input?.required && !text.trim()} onClick={() => close(text || 'ok')}>
            {state?.confirmText ?? 'Onayla'}
          </button>
        </>}>
        <div className="text-sm">{state?.message}</div>
        {state?.input && (
          <Field label={state.input.label} className="mt-4" required={state.input.required}>
            <textarea className="input" rows={3} autoFocus value={text} onChange={(e) => setText(e.target.value)} placeholder={state.input.placeholder} />
          </Field>
        )}
      </Modal>
    </ConfirmCtx.Provider>
  )
}

// ---------------------------------------------------------------- Kişi seçici (çalışan rehberinden arama)
export function UserPicker({ value, onChange, placeholder = 'Çalışan ara…', invalid }: { value?: { id: string; fullName: string } | null; onChange: (u: { id: string; fullName: string } | null) => void; placeholder?: string; invalid?: boolean }) {
  const [q, setQ] = useState('')
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const listId = useId()
  const { data, isFetching } = useQuery({
    queryKey: ['directory-pick', q],
    queryFn: () => api.get<Paged<DirectoryEntry>>('/directory', { search: q, pageSize: 8 }),
    enabled: open,
  })
  useEffect(() => {
    const h = (e: MouseEvent) => ref.current && !ref.current.contains(e.target as Node) && setOpen(false)
    document.addEventListener('mousedown', h)
    return () => document.removeEventListener('mousedown', h)
  }, [])
  if (value)
    return (
      <div className="input flex items-center justify-between gap-2">
        <span className="flex min-w-0 items-center gap-2"><Avatar name={value.fullName} size={20} /><span className="truncate">{value.fullName}</span></span>
        <button type="button" className="text-ink-400 hover:text-ink-700 dark:hover:text-white" onClick={() => onChange(null)} aria-label="Seçimi kaldır"><X className="h-4 w-4" /></button>
      </div>
    )
  return (
    <div className="relative" ref={ref}>
      <input className={cn('input', invalid && 'input-error')} value={q} placeholder={placeholder} onFocus={() => setOpen(true)} onChange={(e) => { setQ(e.target.value); setOpen(true) }}
        role="combobox" aria-expanded={open} aria-controls={listId} />
      {open && (
        <div id={listId} className="absolute z-30 mt-1 max-h-64 w-full overflow-y-auto rounded-lg border border-ink-200 bg-white p-1 shadow-pop dark:border-white/10 dark:bg-ink-850">
          {isFetching && !data && <div className="p-3 text-xs muted">Aranıyor…</div>}
          {data?.items.length === 0 && <div className="p-3 text-xs muted">Sonuç bulunamadı.</div>}
          {data?.items.map((u) => (
            <button type="button" key={u.id} className="flex w-full items-center gap-2.5 rounded-md px-2 py-1.5 text-left hover:bg-ink-50 dark:hover:bg-white/5"
              onClick={() => { onChange({ id: u.id, fullName: u.fullName }); setOpen(false); setQ('') }}>
              <Avatar name={u.fullName} size={26} />
              <span className="min-w-0">
                <span className="block truncate text-sm font-medium text-ink-900 dark:text-white">{u.fullName}</span>
                <span className="block truncate text-xs muted">{u.jobTitle} · {u.department?.name}</span>
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

// ---------------------------------------------------------------- Dosya
export function FileUploader({ value, onChange, multiple, accept = '.pdf,.png,.jpg,.jpeg,.webp,.docx,.xlsx,.csv,.txt' }: { value: FileDto[]; onChange: (f: FileDto[]) => void; multiple?: boolean; accept?: string }) {
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState<string | null>(null)
  const input = useRef<HTMLInputElement>(null)
  const pick = async (files: FileList | null) => {
    if (!files?.length) return
    setBusy(true)
    setErr(null)
    try {
      const uploaded: FileDto[] = []
      for (const f of Array.from(files)) {
        if (f.size > 10 * 1024 * 1024) throw new Error(`${f.name}: dosya en fazla 10 MB olabilir.`)
        uploaded.push(await api.upload(f))
      }
      onChange(multiple ? [...value, ...uploaded] : uploaded.slice(0, 1))
    } catch (e) {
      setErr(errorMessage(e))
    } finally {
      setBusy(false)
      if (input.current) input.current.value = ''
    }
  }
  return (
    <div>
      {value.length > 0 && (
        <ul className="mb-2 space-y-1.5">
          {value.map((f) => (
            <li key={f.id} className="flex items-center justify-between gap-2 rounded-lg border border-ink-200 px-3 py-2 text-sm dark:border-white/10">
              <span className="flex min-w-0 items-center gap-2"><Paperclip className="h-4 w-4 shrink-0 text-ink-400" /><span className="truncate">{f.fileName}</span><span className="text-xs muted">{fmt.bytes(f.size)}</span></span>
              <button type="button" className="text-ink-400 hover:text-red-600" onClick={() => onChange(value.filter((x) => x.id !== f.id))} aria-label={`${f.fileName} kaldır`}><X className="h-4 w-4" /></button>
            </li>
          ))}
        </ul>
      )}
      {(multiple || value.length === 0) && (
        <button type="button" onClick={() => input.current?.click()} disabled={busy}
          onDragOver={(e) => e.preventDefault()} onDrop={(e) => { e.preventDefault(); pick(e.dataTransfer.files) }}
          className="flex w-full flex-col items-center justify-center gap-1 rounded-lg border border-dashed border-ink-300 px-4 py-5 text-sm muted transition hover:border-brand-400 hover:bg-brand-50/40 dark:border-white/15 dark:hover:bg-brand-400/5">
          {busy ? <Spinner /> : <FileUp className="h-5 w-5" />}
          <span><b className="text-ink-800 dark:text-ink-100">Dosya seçin</b> veya sürükleyip bırakın</span>
          <span className="text-xs">PDF, görsel, Word, Excel, CSV · en fazla 10 MB</span>
        </button>
      )}
      <input ref={input} type="file" className="hidden" accept={accept} multiple={multiple} onChange={(e) => pick(e.target.files)} />
      {err && <p className="error-text">{err}</p>}
    </div>
  )
}

export function FileList({ files }: { files: FileDto[] }) {
  if (files.length === 0) return null
  return (
    <ul className="flex flex-wrap gap-2">
      {files.map((f) => (
        <li key={f.id}>
          <button type="button" className="btn-secondary btn-sm" onClick={() => api.download(`/files/${f.id}`, f.fileName)}>
            <Paperclip className="h-3.5 w-3.5" /><span className="max-w-48 truncate">{f.fileName}</span><span className="muted">{fmt.bytes(f.size)}</span>
          </button>
        </li>
      ))}
    </ul>
  )
}

export function DefinitionList({ items }: { items: { label: string; value: ReactNode }[] }) {
  return (
    <dl className="divide-y divide-ink-100 text-sm dark:divide-white/[0.06]">
      {items.map((i) => (
        <div key={i.label} className="flex items-start justify-between gap-4 py-2.5">
          <dt className="shrink-0 muted">{i.label}</dt>
          <dd className="min-w-0 text-right font-medium text-ink-900 dark:text-ink-100">{i.value ?? '—'}</dd>
        </div>
      ))}
    </dl>
  )
}
