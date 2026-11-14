import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AppWindow, Bell, BookOpen, Boxes, Briefcase, Building2, CalendarHeart, ChartColumn, CheckCheck, ChevronsLeft, ClipboardCheck, ClipboardList, Cog, Eraser,
  FileStack, FolderKanban, FolderOpen, Gauge, Home, Inbox, KeyRound, LifeBuoy, ListChecks, LogOut, Megaphone, Menu, MessagesSquare, Moon, Network, PhoneCall,
  Plus, ScrollText, Search, Shield, ShieldCheck, Store, Sun, Trophy, UserCog, UserPlus, Users, Wallet, Workflow, X,
} from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { api } from '@/lib/api'
import { fmt, roleLabel } from '@/lib/format'
import { DynamicIcon } from '@/lib/icon'
import { showRealtimeToast, usePollInterval, useRealtime, type RealtimeNotification } from '@/lib/realtime'
import type { AppEntry, DirectoryEntry, NotificationItem, Paged } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useAuth, useCan } from '@/store/auth'
import { useUi } from '@/store/ui'
import { Avatar, Spinner } from './ui'

export function Logo({ compact, light }: { compact?: boolean; light?: boolean }) {
  return (
    <span className="inline-flex items-center gap-2.5">
      <svg viewBox="0 0 32 32" className="h-8 w-8 shrink-0" aria-hidden>
        <defs><linearGradient id="lg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stopColor="#f43f5e" /><stop offset="1" stopColor="#9f1239" /></linearGradient></defs>
        <rect width="32" height="32" rx="9" fill="url(#lg)" />
        <path d="M10 11.5a2.5 2.5 0 0 1 2.5-2.5h7a2.5 2.5 0 0 1 2.5 2.5v5a2.5 2.5 0 0 1-2.5 2.5H16l-3.5 3v-3A2.5 2.5 0 0 1 10 16.5z" fill="#fff" />
        <path d="M13.5 13.6l1.7 1.7 3.3-3.3" fill="none" stroke="#be123c" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
      {!compact && (
        <span className="leading-none">
          <span className={cn('block font-display text-[17px] font-extrabold tracking-tight', light ? 'text-white' : 'text-ink-900 dark:text-white')}>StajDesk</span>
          <span className={cn('block text-[10px] font-medium uppercase tracking-[0.14em]', light ? 'text-white/60' : 'text-ink-400')}>by Devoplus</span>
        </span>
      )}
    </span>
  )
}

type NavItem = { to: string; label: string; icon: React.ComponentType<{ className?: string }>; perms?: string[]; badge?: 'approvals' | 'messages' | 'notifications'; end?: boolean }
type NavGroup = { title: string; items: NavItem[] }

const NAV: NavGroup[] = [
  { title: 'Genel', items: [
    { to: '/', label: 'Ana Sayfa', icon: Home, end: true },
    { to: '/notifications', label: 'Bildirim Merkezi', icon: Bell, badge: 'notifications' },
    { to: '/messages', label: 'Mesajlar', icon: MessagesSquare, badge: 'messages' },
  ] },
  { title: 'Destek', items: [
    { to: '/tickets', label: 'Talepler', icon: LifeBuoy },
    { to: '/kb', label: 'Bilgi Bankası & SSS', icon: BookOpen },
    { to: '/inventory', label: 'Envanter', icon: Boxes },
  ] },
  { title: 'İş Akışları', items: [
    { to: '/apps', label: 'Uygulama Merkezi', icon: AppWindow },
    { to: '/requests', label: 'Başvurularım', icon: FileStack },
    { to: '/approvals', label: 'Onaylarım', icon: ClipboardCheck, badge: 'approvals' },
    { to: '/tasks', label: 'Görevler', icon: ListChecks },
    { to: '/projects', label: 'Projeler', icon: FolderKanban },
  ] },
  { title: 'Kurum', items: [
    { to: '/announcements', label: 'Duyurular & Haberler', icon: Megaphone },
    { to: '/events', label: 'Etkinlikler', icon: CalendarHeart },
    { to: '/directory', label: 'Çalışan Rehberi', icon: Users },
    { to: '/achievements', label: 'Çalışan Başarıları', icon: Trophy },
    { to: '/surveys', label: 'Anketler', icon: ClipboardList },
    { to: '/documents', label: 'Dokümanlar', icon: FolderOpen },
    { to: '/jobs', label: 'İş İlanları', icon: Briefcase },
    { to: '/stores', label: 'Mağazalar', icon: Store },
    { to: '/on-call', label: 'Nöbetçi Kişi', icon: PhoneCall },
  ] },
  { title: 'Analiz', items: [
    { to: '/reports', label: 'Raporlar', icon: ChartColumn, perms: ['reports.view', 'reports.view_department', 'tickets.view_all', 'inventory.view'] },
  ] },
  { title: 'Yönetim', items: [
    { to: '/admin/users', label: 'Kullanıcılar', icon: UserCog, perms: ['users.manage'] },
    { to: '/admin/account-requests', label: 'Hesap Talepleri', icon: UserPlus, perms: ['accounts.request', 'accounts.approve'] },
    { to: '/admin/organization', label: 'Organizasyon', icon: Building2, perms: ['org.manage', 'users.manage'] },
    { to: '/admin/groups', label: 'Destek Grupları', icon: Network, perms: ['org.manage', 'tickets.configure'] },
    { to: '/admin/roles', label: 'Roller & Yetkiler', icon: KeyRound, perms: ['roles.manage', 'users.manage'] },
    { to: '/admin/helpdesk', label: 'Kategoriler & Kurallar', icon: Workflow, perms: ['tickets.configure'] },
    { to: '/admin/forms', label: 'Form Tasarımcısı', icon: Cog, perms: ['forms.manage'] },
    { to: '/admin/content', label: 'İçerik & Banner', icon: ScrollText, perms: ['content.manage'] },
    { to: '/admin/leave', label: 'İzin Bakiyeleri', icon: Wallet, perms: ['leave.manage'] },
    { to: '/admin/audit', label: 'Denetim Kayıtları', icon: Shield, perms: ['audit.view'] },
    { to: '/system', label: 'Sistem Durumu', icon: Gauge, perms: ['system.manage'] },
    { to: '/system/cache', label: 'Önbellek', icon: Eraser, perms: ['system.manage'] },
  ] },
]

function useCounters() {
  const token = useAuth((s) => s.accessToken)
  const poll = usePollInterval(20000)
  const notifications = useQuery({ queryKey: ['notif-count'], queryFn: () => api.get<number>('/notifications/unread-count'), refetchInterval: poll, enabled: !!token })
  const messages = useQuery({ queryKey: ['msg-count'], queryFn: () => api.get<number>('/messages/unread-count'), refetchInterval: poll, enabled: !!token })
  const approvals = useQuery({ queryKey: ['approval-count'], queryFn: () => api.get<Paged<unknown>>('/submissions', { scope: 'approvals', pageSize: 1 }).then((p) => p.totalCount), refetchInterval: poll, enabled: !!token })
  return { notifications: notifications.data ?? 0, messages: messages.data ?? 0, approvals: approvals.data ?? 0 }
}

function Sidebar() {
  const { can } = useCan()
  const { sidebarCollapsed: collapsed, toggleSidebar, mobileNav, setMobileNav } = useUi()
  const counters = useCounters()
  const location = useLocation()
  useEffect(() => setMobileNav(false), [location.pathname, setMobileNav])
  const groups = NAV.map((g) => ({ ...g, items: g.items.filter((i) => !i.perms || can(...i.perms)) })).filter((g) => g.items.length > 0)

  const content = (
    <div className="flex h-full flex-col">
      <div className={cn('flex h-16 items-center gap-2 px-4', collapsed ? 'lg:justify-center lg:px-0' : 'justify-between')}>
        <Link to="/" aria-label="Ana sayfa"><Logo compact={collapsed} /></Link>
        <button className="btn-ghost btn-icon lg:hidden" onClick={() => setMobileNav(false)} aria-label="Menüyü kapat"><X className="h-4 w-4" /></button>
      </div>
      <nav className="flex-1 overflow-y-auto px-3 pb-4 scroll-thin" aria-label="Ana menü">
        {groups.map((g) => (
          <div key={g.title} className="mt-4 first:mt-1">
            {!collapsed && <div className="px-3 pb-1.5 text-[10px] font-semibold uppercase tracking-[0.12em] text-ink-400 dark:text-ink-500">{g.title}</div>}
            <ul className="space-y-0.5">
              {g.items.map((i) => {
                const count = i.badge ? counters[i.badge] : 0
                return (
                  <li key={i.to}>
                    <NavLink to={i.to} end={i.end} title={collapsed ? i.label : undefined}
                      className={({ isActive }) => cn('group relative flex h-9 items-center gap-3 rounded-lg px-3 text-[13px] font-medium transition-colors',
                        collapsed && 'lg:justify-center lg:px-0',
                        isActive ? 'bg-brand-50 text-brand-700 dark:bg-white/[0.07] dark:text-white' : 'text-ink-600 hover:bg-ink-100 hover:text-ink-900 dark:text-ink-400 dark:hover:bg-white/[0.04] dark:hover:text-ink-100')}>
                      {({ isActive }) => (
                        <>
                          {isActive && <span className="absolute top-1.5 bottom-1.5 left-0 w-[3px] rounded-r bg-brand-600 dark:bg-brand-400" />}
                          <i.icon className={cn('h-[18px] w-[18px] shrink-0', isActive ? 'text-brand-600 dark:text-brand-400' : 'text-ink-400 group-hover:text-ink-600 dark:group-hover:text-ink-200')} />
                          {!collapsed && <span className="truncate">{i.label}</span>}
                          {count > 0 && (
                            <span className={cn('rounded-full bg-brand-600 px-1.5 text-[10px] font-bold leading-[18px] text-white', collapsed ? 'absolute top-0.5 right-1' : 'ml-auto')}>{count > 99 ? '99+' : count}</span>
                          )}
                        </>
                      )}
                    </NavLink>
                  </li>
                )
              })}
            </ul>
          </div>
        ))}
      </nav>
      <div className="hidden border-t border-ink-100 p-3 lg:block dark:border-white/[0.06]">
        <button className={cn('btn-ghost w-full', collapsed ? 'justify-center' : 'justify-start')} onClick={toggleSidebar} aria-label="Menüyü daralt/genişlet">
          <ChevronsLeft className={cn('h-4 w-4 transition', collapsed && 'rotate-180')} />{!collapsed && <span>Menüyü daralt</span>}
        </button>
      </div>
    </div>
  )

  return (
    <>
      <aside className={cn('fixed inset-y-0 left-0 z-30 hidden border-r border-ink-200/80 bg-white transition-[width] lg:block dark:border-white/[0.06] dark:bg-ink-900', collapsed ? 'w-[72px]' : 'w-64')}>{content}</aside>
      {mobileNav && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <div className="absolute inset-0 bg-ink-950/50" onClick={() => setMobileNav(false)} />
          <aside className="absolute inset-y-0 left-0 w-72 bg-white shadow-pop animate-drawer dark:bg-ink-900">{content}</aside>
        </div>
      )}
    </>
  )
}

function NotificationBell() {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const nav = useNavigate()
  const qc = useQueryClient()
  const { notifications: count } = useCounters()
  const list = useQuery({ queryKey: ['notifications', 'menu'], queryFn: () => api.get<NotificationItem[]>('/notifications', { take: 8 }), enabled: open })
  const readAll = useMutation({ mutationFn: () => api.post('/notifications/read-all'), onSuccess: () => qc.invalidateQueries({ queryKey: ['notif-count'] }).then(() => list.refetch()) })
  useEffect(() => {
    const h = (e: MouseEvent) => ref.current && !ref.current.contains(e.target as Node) && setOpen(false)
    document.addEventListener('mousedown', h)
    return () => document.removeEventListener('mousedown', h)
  }, [])
  const openItem = async (n: NotificationItem) => {
    if (!n.isRead) await api.post(`/notifications/${n.id}/read`).catch(() => {})
    qc.invalidateQueries({ queryKey: ['notif-count'] })
    setOpen(false)
    if (n.link) nav(n.link)
  }
  return (
    <div className="relative" ref={ref}>
      <button className="btn-ghost btn-icon relative" onClick={() => setOpen((v) => !v)} aria-label={`Bildirimler (${count} okunmamış)`} aria-expanded={open}>
        <Bell className="h-[18px] w-[18px]" />
        {count > 0 && <span className="absolute top-1.5 right-1.5 grid h-4 min-w-4 place-items-center rounded-full bg-brand-600 px-1 text-[9px] font-bold text-white ring-2 ring-white dark:ring-ink-900">{count > 9 ? '9+' : count}</span>}
      </button>
      {open && (
        <div className="absolute right-0 z-40 mt-2 w-[min(92vw,380px)] overflow-hidden rounded-xl border border-ink-200 bg-white shadow-pop animate-in dark:border-white/10 dark:bg-ink-850">
          <div className="flex items-center justify-between border-b border-ink-100 px-4 py-3 dark:border-white/[0.07]">
            <span className="font-semibold text-ink-900 dark:text-white">Bildirimler</span>
            <button className="btn-ghost btn-sm" disabled={count === 0 || readAll.isPending} onClick={() => readAll.mutate()}><CheckCheck className="h-3.5 w-3.5" /> Tümünü okundu yap</button>
          </div>
          <div className="max-h-96 overflow-y-auto scroll-thin">
            {list.isLoading && <div className="grid place-items-center p-6"><Spinner /></div>}
            {list.data?.length === 0 && <div className="p-6 text-center text-sm muted">Bildiriminiz yok.</div>}
            {list.data?.map((n) => (
              <button key={n.id} onClick={() => openItem(n)} className="flex w-full gap-3 border-b border-ink-100 px-4 py-3 text-left last:border-0 hover:bg-ink-50 dark:border-white/[0.05] dark:hover:bg-white/[0.03]">
                <span className={cn('mt-1.5 h-2 w-2 shrink-0 rounded-full', n.isRead ? 'bg-transparent' : 'bg-brand-500')} />
                <span className="min-w-0">
                  <span className="block text-[13px] font-semibold text-ink-900 dark:text-white">{n.title}</span>
                  <span className="line-clamp-2 block text-xs muted">{n.message}</span>
                  <span className="mt-1 block text-[11px] text-ink-400">{fmt.relative(n.createdAt)}</span>
                </span>
              </button>
            ))}
          </div>
          <Link to="/notifications" onClick={() => setOpen(false)} className="block border-t border-ink-100 py-2.5 text-center text-xs font-semibold text-brand-600 hover:bg-ink-50 dark:border-white/[0.07] dark:text-brand-400 dark:hover:bg-white/[0.03]">Bildirim merkezini aç</Link>
        </div>
      )}
    </div>
  )
}

function UserMenu() {
  const user = useAuth((s) => s.user)!
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const qc = useQueryClient()
  useEffect(() => {
    const h = (e: MouseEvent) => ref.current && !ref.current.contains(e.target as Node) && setOpen(false)
    document.addEventListener('mousedown', h)
    return () => document.removeEventListener('mousedown', h)
  }, [])
  const logout = async () => {
    await api.post('/auth/logout').catch(() => {})
    qc.clear()
    useAuth.getState().clear()
    // Tam sayfa geçişi: bellekteki tüm durum (sorgu önbelleği, gerçek zamanlı bağlantı) temizlenir ve
    // korumalı sayfanın "/welcome" yönlendirmesi çıkış yönlendirmesinin önüne geçemez.
    window.location.replace('/login')
  }
  return (
    <div className="relative" ref={ref}>
      <button className="flex items-center gap-2.5 rounded-lg py-1 pr-2 pl-1 hover:bg-ink-100 dark:hover:bg-white/[0.05]" onClick={() => setOpen((v) => !v)} aria-expanded={open}>
        <Avatar name={user.fullName} size={30} />
        <span className="hidden text-left leading-tight md:block">
          <span className="block text-[13px] font-semibold text-ink-900 dark:text-white">{user.fullName}</span>
          <span className="block max-w-40 truncate text-[11px] muted">{user.jobTitle ?? roleLabel[user.roles[0]?.code]}</span>
        </span>
      </button>
      {open && (
        <div className="absolute right-0 z-40 mt-2 w-64 overflow-hidden rounded-xl border border-ink-200 bg-white p-1.5 shadow-pop animate-in dark:border-white/10 dark:bg-ink-850">
          <div className="px-3 py-2">
            <div className="text-sm font-semibold text-ink-900 dark:text-white">{user.fullName}</div>
            <div className="truncate text-xs muted">{user.email}</div>
            <div className="mt-2 flex flex-wrap gap-1">{user.roles.filter((r) => r.code !== 'EMPLOYEE').map((r) => <span key={r.code} className="chip bg-brand-50 text-brand-700 dark:bg-brand-400/10 dark:text-brand-300">{r.name}</span>)}</div>
          </div>
          <div className="my-1 h-px bg-ink-100 dark:bg-white/[0.07]" />
          <Link to="/profile" onClick={() => setOpen(false)} className="flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm hover:bg-ink-50 dark:hover:bg-white/5"><UserCog className="h-4 w-4 text-ink-400" /> Profilim</Link>
          <Link to="/profile?tab=security" onClick={() => setOpen(false)} className="flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm hover:bg-ink-50 dark:hover:bg-white/5"><ShieldCheck className="h-4 w-4 text-ink-400" /> Parola ve güvenlik</Link>
          <button onClick={logout} className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-sm text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-400/10"><LogOut className="h-4 w-4" /> Çıkış yap</button>
        </div>
      )}
    </div>
  )
}

/** ⌘K / Ctrl+K: sayfa, uygulama ve çalışan araması */
function CommandPalette({ onClose }: { onClose: () => void }) {
  const [q, setQ] = useState('')
  const [idx, setIdx] = useState(0)
  const nav = useNavigate()
  const { can } = useCan()
  const apps = useQuery({ queryKey: ['apps'], queryFn: () => api.get<AppEntry[]>('/apps'), staleTime: 60000 })
  const people = useQuery({ queryKey: ['cmd-people', q], queryFn: () => api.get<Paged<DirectoryEntry>>('/directory', { search: q, pageSize: 5 }), enabled: q.length >= 2 })
  const items = useMemo(() => {
    const term = q.toLocaleLowerCase('tr-TR')
    const pages = NAV.flatMap((g) => g.items).filter((i) => !i.perms || can(...i.perms)).map((i) => ({ key: 'p' + i.to, label: i.label, sub: 'Sayfa', to: i.to, icon: <i.icon className="h-4 w-4" /> }))
    const appItems = (apps.data ?? []).map((a) => ({ key: 'a' + a.key, label: a.name, sub: a.module, to: a.route, icon: <DynamicIcon name={a.icon} className="h-4 w-4" /> }))
    const ticket = /^#?\d+$/.test(q.trim()) ? [{ key: 't', label: `Talep #${q.replace('#', '')}`, sub: 'Talebe git', to: `/tickets/${q.replace('#', '')}`, icon: <LifeBuoy className="h-4 w-4" /> }] : []
    const actions = [{ key: 'new', label: 'Yeni destek talebi', sub: 'Eylem', to: '/tickets/new', icon: <Plus className="h-4 w-4" /> }]
    const ppl = (people.data?.items ?? []).map((p) => ({ key: 'u' + p.id, label: p.fullName, sub: `${p.jobTitle ?? ''} · ${p.department?.name ?? ''}`, to: `/directory?u=${p.id}`, icon: <Avatar name={p.fullName} size={18} /> }))
    return [...ticket, ...actions, ...pages, ...appItems].filter((i) => !term || i.label.toLocaleLowerCase('tr-TR').includes(term) || i.sub.toLocaleLowerCase('tr-TR').includes(term)).slice(0, 12).concat(ppl)
  }, [q, apps.data, people.data, can])
  const go = (to: string) => { onClose(); nav(to) }
  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center p-4 pt-[12vh]" role="dialog" aria-modal="true" aria-label="Hızlı arama">
      <div className="absolute inset-0 bg-ink-950/50 backdrop-blur-[2px]" onClick={onClose} />
      <div className="relative w-full max-w-xl overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-pop animate-in dark:border-white/10 dark:bg-ink-850">
        <div className="flex items-center gap-3 border-b border-ink-100 px-4 dark:border-white/[0.07]">
          <Search className="h-4 w-4 text-ink-400" />
          <input autoFocus value={q} onChange={(e) => { setQ(e.target.value); setIdx(0) }} placeholder="Sayfa, uygulama, çalışan veya #talep no…" className="h-12 flex-1 bg-transparent text-sm outline-none"
            onKeyDown={(e) => {
              if (e.key === 'ArrowDown') { e.preventDefault(); setIdx((i) => Math.min(items.length - 1, i + 1)) }
              if (e.key === 'ArrowUp') { e.preventDefault(); setIdx((i) => Math.max(0, i - 1)) }
              if (e.key === 'Enter' && items[idx]) go(items[idx].to)
              if (e.key === 'Escape') onClose()
            }} />
          <span className="kbd">ESC</span>
        </div>
        <ul className="max-h-[50vh] overflow-y-auto p-1.5 scroll-thin">
          {items.length === 0 && <li className="p-6 text-center text-sm muted">Sonuç bulunamadı.</li>}
          {items.map((i, n) => (
            <li key={i.key}>
              <button onMouseEnter={() => setIdx(n)} onClick={() => go(i.to)} className={cn('flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left', n === idx && 'bg-ink-100 dark:bg-white/[0.06]')}>
                <span className="grid h-7 w-7 place-items-center rounded-md bg-ink-100 text-ink-500 dark:bg-white/[0.06] dark:text-ink-300">{i.icon}</span>
                <span className="min-w-0 flex-1"><span className="block truncate text-sm font-medium text-ink-900 dark:text-white">{i.label}</span><span className="block truncate text-xs muted">{i.sub}</span></span>
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}

function Topbar() {
  const { theme, toggleTheme, setMobileNav } = useUi()
  const { messages } = useCounters()
  const [cmd, setCmd] = useState(false)
  useEffect(() => {
    const h = (e: KeyboardEvent) => { if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); setCmd(true) } }
    window.addEventListener('keydown', h)
    return () => window.removeEventListener('keydown', h)
  }, [])
  return (
    <header className="sticky top-0 z-20 flex h-16 items-center gap-2 border-b sm:gap-3 border-ink-200/80 bg-white/80 px-4 backdrop-blur-md sm:px-6 dark:border-white/[0.06] dark:bg-ink-950/75">
      <button className="btn-ghost btn-icon shrink-0 lg:hidden" onClick={() => setMobileNav(true)} aria-label="Menüyü aç"><Menu className="h-5 w-5" /></button>
      <button onClick={() => setCmd(true)} aria-label="Hızlı arama (Ctrl K)" className="flex h-9 w-full min-w-0 max-w-sm items-center gap-2.5 rounded-lg border border-ink-200 bg-ink-50/60 px-3 text-sm text-ink-400 transition hover:border-ink-300 dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-white/20">
        <Search className="h-4 w-4 shrink-0" /><span className="min-w-0 flex-1 truncate text-left">Ara: sayfa, uygulama, çalışan…</span><span className="kbd hidden sm:inline">Ctrl K</span>
      </button>
      <div className="ml-auto flex shrink-0 items-center gap-0.5 sm:gap-1">
        <Link to="/tickets/new" className="btn-primary hidden sm:inline-flex"><Plus className="h-4 w-4" /> Talep aç</Link>
        <Link to="/messages" className="btn-ghost btn-icon relative" aria-label={`Mesajlar (${messages} okunmamış)`}>
          <MessagesSquare className="h-[18px] w-[18px]" />
          {messages > 0 && <span className="absolute top-2 right-2 h-2 w-2 rounded-full bg-brand-500 ring-2 ring-white dark:ring-ink-950" />}
        </Link>
        <NotificationBell />
        <button className="btn-ghost btn-icon" onClick={toggleTheme} aria-label={theme === 'dark' ? 'Açık temaya geç' : 'Koyu temaya geç'}>
          {theme === 'dark' ? <Sun className="h-[18px] w-[18px]" /> : <Moon className="h-[18px] w-[18px]" />}
        </button>
        <div className="mx-1 hidden h-6 w-px bg-ink-200 sm:block dark:bg-white/10" />
        <UserMenu />
      </div>
      {cmd && <CommandPalette onClose={() => setCmd(false)} />}
    </header>
  )
}

export function PortalLayout() {
  const collapsed = useUi((s) => s.sidebarCollapsed)
  const navigate = useNavigate()
  const location = useLocation()
  const pathRef = useRef(location.pathname)
  useEffect(() => { pathRef.current = location.pathname }, [location.pathname])
  const onNotify = useCallback((n: RealtimeNotification) => showRealtimeToast(n, navigate, pathRef.current), [navigate])
  useRealtime(onNotify)
  return (
    <div className="min-h-screen">
      <a href="#main" className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50 focus:rounded-lg focus:bg-white focus:px-3 focus:py-2">İçeriğe geç</a>
      <Sidebar />
      <div className={cn('transition-[padding]', collapsed ? 'lg:pl-[72px]' : 'lg:pl-64')}>
        <Topbar />
        <main id="main" className="mx-auto w-full max-w-[1500px] px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

export function Guard({ perms, children }: { perms: string[]; children: ReactNode }) {
  const { can } = useCan()
  if (!can(...perms))
    return (
      <div className="card mx-auto mt-10 max-w-md p-8 text-center">
        <Inbox className="mx-auto h-8 w-8 text-ink-400" />
        <h2 className="mt-3 text-lg font-semibold">Bu sayfaya erişim yetkiniz yok</h2>
        <p className="mt-1 text-sm muted">Yetkiye ihtiyacınız olduğunu düşünüyorsanız Erişim Yönetimi uygulamasından talep oluşturabilirsiniz.</p>
        <Link to="/apps/access-request" className="btn-primary mt-4">Erişim talebi oluştur</Link>
      </div>
    )
  return <>{children}</>
}
