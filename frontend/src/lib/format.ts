import { differenceInMinutes, format, formatDistanceToNowStrict, isToday, isYesterday, parseISO } from 'date-fns'
import { tr } from 'date-fns/locale'
import type { AssetStatus, AssetType, EmploymentType, ProjectStatus, StoreStatus, SubmissionStatus, TicketPriority, TicketStatus, WorkTaskStatus } from './types'

const d = (v: string | Date) => (typeof v === 'string' ? parseISO(v) : v)

export const fmt = {
  date: (v?: string | null) => (v ? format(d(v), 'd MMM yyyy', { locale: tr }) : '—'),
  dateTime: (v?: string | null) => (v ? format(d(v), 'd MMM yyyy HH:mm', { locale: tr }) : '—'),
  time: (v?: string | null) => (v ? format(d(v), 'HH:mm', { locale: tr }) : '—'),
  dayMonth: (v?: string | null) => (v ? format(d(v), 'd MMMM', { locale: tr }) : '—'),
  weekday: (v?: string | null) => (v ? format(d(v), 'EEEE', { locale: tr }) : ''),
  relative: (v?: string | null) => {
    if (!v) return '—'
    const date = d(v)
    if (Math.abs(differenceInMinutes(new Date(), date)) < 1) return 'şimdi'
    return formatDistanceToNowStrict(date, { locale: tr, addSuffix: true })
  },
  smart: (v?: string | null) => {
    if (!v) return '—'
    const date = d(v)
    if (isToday(date)) return `Bugün ${format(date, 'HH:mm')}`
    if (isYesterday(date)) return `Dün ${format(date, 'HH:mm')}`
    return format(date, 'd MMM HH:mm', { locale: tr })
  },
  money: (v?: number | null, currency = 'TRY') =>
    v == null ? '—' : new Intl.NumberFormat('tr-TR', { style: 'currency', currency, maximumFractionDigits: 2 }).format(v),
  number: (v?: number | null, digits = 0) => (v == null ? '—' : new Intl.NumberFormat('tr-TR', { maximumFractionDigits: digits }).format(v)),
  bytes: (n: number) => (n < 1024 ? `${n} B` : n < 1024 * 1024 ? `${(n / 1024).toFixed(0)} KB` : `${(n / 1024 / 1024).toFixed(1)} MB`),
  hours: (h: number) => (h < 1 ? `${Math.round(h * 60)} dk` : h < 48 ? `${h.toFixed(1)} sa` : `${(h / 24).toFixed(1)} gün`),
  initials: (name: string) => name.split(' ').filter(Boolean).slice(0, 2).map((p) => p[0]).join('').toLocaleUpperCase('tr-TR'),
}

/** ISO tarih → yyyy-MM-dd (input[type=date] için) */
export const toInputDate = (v?: string | null) => (v ? v.slice(0, 10) : '')
export const toInputDateTime = (v?: string | null) => (v ? format(d(v), "yyyy-MM-dd'T'HH:mm") : '')

type Tone = 'gray' | 'blue' | 'amber' | 'green' | 'red' | 'violet' | 'brand' | 'cyan'
export const statusMeta: Record<TicketStatus, { label: string; tone: Tone }> = {
  Open: { label: 'Açık', tone: 'blue' },
  InProgress: { label: 'İşlemde', tone: 'violet' },
  Pending: { label: 'Beklemede', tone: 'amber' },
  Resolved: { label: 'Çözüldü', tone: 'green' },
  Closed: { label: 'Kapandı', tone: 'gray' },
}
export const priorityMeta: Record<TicketPriority, { label: string; tone: Tone; rank: number }> = {
  Low: { label: 'Düşük', tone: 'gray', rank: 0 },
  Medium: { label: 'Orta', tone: 'blue', rank: 1 },
  High: { label: 'Yüksek', tone: 'amber', rank: 2 },
  Critical: { label: 'Kritik', tone: 'red', rank: 3 },
}
export const submissionMeta: Record<SubmissionStatus, { label: string; tone: Tone }> = {
  Pending: { label: 'Onay bekliyor', tone: 'amber' },
  Approved: { label: 'Onaylandı', tone: 'blue' },
  Rejected: { label: 'Reddedildi', tone: 'red' },
  Completed: { label: 'Tamamlandı', tone: 'green' },
  Cancelled: { label: 'İptal edildi', tone: 'gray' },
}
export const assetTypeLabel: Record<AssetType, string> = {
  Computer: 'Masaüstü', Laptop: 'Dizüstü', Monitor: 'Monitör', Printer: 'Yazıcı', NetworkDevice: 'Ağ cihazı', Phone: 'Telefon', Server: 'Sunucu', Peripheral: 'Çevre birimi', HandheldTerminal: 'El terminali',
}
export const assetStatusMeta: Record<AssetStatus, { label: string; tone: Tone }> = {
  InUse: { label: 'Kullanımda', tone: 'green' }, InStock: { label: 'Stokta', tone: 'blue' }, InRepair: { label: 'Serviste', tone: 'amber' }, Retired: { label: 'Hurda', tone: 'gray' },
}
export const taskStatusMeta: Record<WorkTaskStatus, { label: string; tone: Tone }> = {
  Todo: { label: 'Yapılacak', tone: 'gray' }, InProgress: { label: 'Devam ediyor', tone: 'violet' }, Review: { label: 'Kontrolde', tone: 'amber' }, Done: { label: 'Tamamlandı', tone: 'green' },
}
export const projectStatusMeta: Record<ProjectStatus, { label: string; tone: Tone }> = {
  Planned: { label: 'Planlandı', tone: 'gray' }, Active: { label: 'Aktif', tone: 'green' }, OnHold: { label: 'Beklemede', tone: 'amber' }, Completed: { label: 'Tamamlandı', tone: 'blue' }, Cancelled: { label: 'İptal', tone: 'red' },
}
export const storeStatusMeta: Record<StoreStatus, { label: string; tone: Tone }> = {
  Open: { label: 'Açık', tone: 'green' }, Renovation: { label: 'Tadilatta', tone: 'amber' }, Closed: { label: 'Kapalı', tone: 'gray' },
}
export const employmentLabel: Record<EmploymentType, string> = { FullTime: 'Tam zamanlı', PartTime: 'Yarı zamanlı', Intern: 'Stajyer' }
export const roleLabel: Record<string, string> = {
  SUPER_ADMIN: 'Super Admin', ADMIN: 'Admin', CEO: 'CEO', GENERAL_MANAGER: 'Genel Müdür', DIRECTOR: 'Direktör', MANAGER: 'Müdür',
  DEPARTMENT_MANAGER: 'Departman Yöneticisi', HR: 'İK', IT: 'IT', EMPLOYEE: 'Çalışan',
}
export const historyFieldLabel: Record<string, string> = {
  created: 'Talep oluşturuldu', status: 'Durum', assignee: 'Atanan', group: 'Grup', priority: 'Öncelik', category: 'Kategori', title: 'Başlık', rule: 'Yönlendirme kuralı', attachment: 'Dosya eklendi',
}
