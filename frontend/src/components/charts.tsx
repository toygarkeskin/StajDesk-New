import { Area, AreaChart, Bar, BarChart, CartesianGrid, Cell, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { format, parseISO } from 'date-fns'
import { tr } from 'date-fns/locale'
import type { Count, TrendPoint } from '@/lib/types'
import { fmt } from '@/lib/format'
import { useUi } from '@/store/ui'

const palette = ['#f0345a', '#6366f1', '#0ea5e9', '#10b981', '#f59e0b', '#a855f7', '#14b8a6', '#ec4899', '#64748b', '#84cc16']

function useChartTheme() {
  const dark = useUi((s) => s.theme) === 'dark'
  return { grid: dark ? 'rgba(255,255,255,0.06)' : '#eceef2', text: dark ? '#858fa3' : '#667086', tooltipBg: dark ? '#1a1e26' : '#ffffff', tooltipBorder: dark ? 'rgba(255,255,255,0.08)' : '#e5e7eb' }
}

function ChartTooltip({ active, payload, label, labels }: { active?: boolean; payload?: { name: string; value: number; color: string }[]; label?: string; labels?: Record<string, string> }) {
  const t = useChartTheme()
  if (!active || !payload?.length) return null
  return (
    <div className="rounded-lg px-3 py-2 text-xs shadow-pop" style={{ background: t.tooltipBg, border: `1px solid ${t.tooltipBorder}` }}>
      {label && <div className="mb-1 font-semibold text-ink-900 dark:text-white">{label}</div>}
      {payload.map((p) => (
        <div key={p.name} className="flex items-center gap-2">
          <span className="h-2 w-2 rounded-full" style={{ background: p.color }} />
          <span className="muted">{labels?.[p.name] ?? p.name}</span>
          <b className="ml-auto pl-3 text-ink-900 dark:text-white">{fmt.number(p.value)}</b>
        </div>
      ))}
    </div>
  )
}

/** Günlük tutar (₺) trendi — ör. mağaza cirosu */
export function MoneyTrendChart({ data, height = 220, color = '#6366f1', name = 'Ciro' }: { data: { date: string; amount: number }[]; height?: number; color?: string; name?: string }) {
  const t = useChartTheme()
  const rows = data.map((d) => ({ amount: d.amount, label: format(parseISO(d.date), 'd MMM', { locale: tr }) }))
  return (
    <ResponsiveContainer width="100%" height={height}>
      <AreaChart data={rows} margin={{ top: 8, right: 8, left: 4, bottom: 0 }}>
        <defs><linearGradient id="gMoney" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor={color} stopOpacity={0.28} /><stop offset="100%" stopColor={color} stopOpacity={0} /></linearGradient></defs>
        <CartesianGrid stroke={t.grid} vertical={false} />
        <XAxis dataKey="label" tick={{ fill: t.text, fontSize: 11 }} axisLine={false} tickLine={false} interval="preserveStartEnd" minTickGap={20} />
        <YAxis tick={{ fill: t.text, fontSize: 11 }} axisLine={false} tickLine={false} width={56}
          tickFormatter={(v: number) => new Intl.NumberFormat('tr-TR', { notation: 'compact', maximumFractionDigits: 1 }).format(v)} />
        <Tooltip content={({ active, payload, label }) => active && payload?.length ? (
          <div className="rounded-lg px-3 py-2 text-xs shadow-pop" style={{ background: t.tooltipBg, border: `1px solid ${t.tooltipBorder}` }}>
            <div className="mb-1 font-semibold text-ink-900 dark:text-white">{label}</div>
            <div className="flex items-center gap-2"><span className="h-2 w-2 rounded-full" style={{ background: color }} /><span className="muted">{name}</span><b className="ml-auto pl-3 text-ink-900 dark:text-white">{fmt.money(Number(payload[0].value))}</b></div>
          </div>
        ) : null} />
        <Area type="monotone" dataKey="amount" name={name} stroke={color} strokeWidth={2} fill="url(#gMoney)" />
      </AreaChart>
    </ResponsiveContainer>
  )
}

/** Oluşturulan / çözülen trendi */
export function TrendChart({ data, height = 240, labels = { created: 'Oluşturulan', resolved: 'Çözülen' } }: { data: TrendPoint[]; height?: number; labels?: Record<string, string> }) {
  const t = useChartTheme()
  const rows = data.map((d) => ({ ...d, label: format(parseISO(d.date), 'd MMM', { locale: tr }) }))
  return (
    <ResponsiveContainer width="100%" height={height}>
      <AreaChart data={rows} margin={{ top: 8, right: 8, left: -18, bottom: 0 }}>
        <defs>
          <linearGradient id="gCreated" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#f0345a" stopOpacity={0.28} /><stop offset="100%" stopColor="#f0345a" stopOpacity={0} /></linearGradient>
          <linearGradient id="gResolved" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#10b981" stopOpacity={0.22} /><stop offset="100%" stopColor="#10b981" stopOpacity={0} /></linearGradient>
        </defs>
        <CartesianGrid stroke={t.grid} vertical={false} />
        <XAxis dataKey="label" tick={{ fill: t.text, fontSize: 11 }} axisLine={false} tickLine={false} interval="preserveStartEnd" minTickGap={16} />
        <YAxis tick={{ fill: t.text, fontSize: 11 }} axisLine={false} tickLine={false} allowDecimals={false} />
        <Tooltip content={<ChartTooltip labels={labels} />} />
        <Area type="monotone" dataKey="created" stroke="#f0345a" strokeWidth={2} fill="url(#gCreated)" />
        <Area type="monotone" dataKey="resolved" stroke="#10b981" strokeWidth={2} fill="url(#gResolved)" />
      </AreaChart>
    </ResponsiveContainer>
  )
}

export function DonutChart({ data, height = 200, center }: { data: Count[]; height?: number; center?: { value: string; label: string } }) {
  const rows = data.filter((d) => d.value > 0)
  const total = rows.reduce((s, d) => s + d.value, 0)
  return (
    <div className="flex flex-col items-center gap-4 sm:flex-row">
      <div className="relative" style={{ width: height, height }}>
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie data={rows} dataKey="value" nameKey="label" innerRadius="68%" outerRadius="100%" paddingAngle={2} stroke="none">
              {rows.map((d, i) => <Cell key={d.key} fill={d.color ?? palette[i % palette.length]} />)}
            </Pie>
            <Tooltip content={<ChartTooltip />} />
          </PieChart>
        </ResponsiveContainer>
        <div className="pointer-events-none absolute inset-0 grid place-items-center text-center">
          <div>
            <div className="font-display text-2xl font-bold text-ink-900 dark:text-white">{center?.value ?? fmt.number(total)}</div>
            <div className="text-[11px] muted">{center?.label ?? 'Toplam'}</div>
          </div>
        </div>
      </div>
      <ul className="w-full flex-1 space-y-1.5 text-sm">
        {rows.map((d, i) => (
          <li key={d.key} className="flex items-center gap-2">
            <span className="h-2.5 w-2.5 shrink-0 rounded-sm" style={{ background: d.color ?? palette[i % palette.length] }} />
            <span className="truncate">{d.label}</span>
            <span className="ml-auto font-semibold text-ink-900 dark:text-white">{fmt.number(d.value)}</span>
            <span className="w-10 text-right text-xs muted">{total ? Math.round((d.value / total) * 100) : 0}%</span>
          </li>
        ))}
      </ul>
    </div>
  )
}

/** Yatay çubuklu sıralama listesi (grafik kütüphanesi gerektirmez) */
export function BarList({ data, max, color = '#f0345a' }: { data: Count[]; max?: number; color?: string }) {
  const top = max ?? Math.max(1, ...data.map((d) => d.value))
  return (
    <ul className="space-y-2.5">
      {data.map((d) => (
        <li key={d.key}>
          <div className="mb-1 flex items-center justify-between gap-3 text-[13px]">
            <span className="truncate">{d.label}</span>
            <span className="font-semibold text-ink-900 dark:text-white">{fmt.number(d.value)}</span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-ink-100 dark:bg-white/[0.06]">
            <div className="h-full rounded-full" style={{ width: `${(d.value / top) * 100}%`, background: d.color ?? color }} />
          </div>
        </li>
      ))}
    </ul>
  )
}

export function ColumnChart({ data, height = 220 }: { data: Count[]; height?: number }) {
  const t = useChartTheme()
  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={data} margin={{ top: 8, right: 8, left: -18, bottom: 0 }}>
        <CartesianGrid stroke={t.grid} vertical={false} />
        <XAxis dataKey="label" tick={{ fill: t.text, fontSize: 11 }} axisLine={false} tickLine={false} interval={0} />
        <YAxis tick={{ fill: t.text, fontSize: 11 }} axisLine={false} tickLine={false} allowDecimals={false} />
        <Tooltip content={<ChartTooltip />} cursor={{ fill: t.grid }} />
        <Bar dataKey="value" name="Adet" radius={[6, 6, 0, 0]} maxBarSize={42}>
          {data.map((d, i) => <Cell key={d.key} fill={d.color ?? palette[i % palette.length]} />)}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  )
}
