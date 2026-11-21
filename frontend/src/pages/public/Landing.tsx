import { useQuery } from '@tanstack/react-query'
import { ArrowRight, Briefcase, Building2, CalendarDays, ChevronLeft, ChevronRight, CloudRain, Cloud, CloudSun, Coins, DollarSign, Euro, MapPin, Snowflake, Store, Sun, Users, Zap, CloudFog } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Logo } from '@/components/layout'
import { Skeleton } from '@/components/ui'
import { api } from '@/lib/api'
import { employmentLabel, fmt } from '@/lib/format'
import type { Banner, MarketData, PublicHome, Weather } from '@/lib/types'
import { cn } from '@/lib/utils'

const themes: Record<string, string> = {
  crimson: 'from-brand-700 via-brand-600 to-rose-500',
  midnight: 'from-ink-950 via-indigo-950 to-ink-900',
  sunset: 'from-orange-600 via-brand-600 to-fuchsia-700',
  ocean: 'from-sky-700 via-cyan-700 to-teal-600',
}

function WeatherIcon({ code, className }: { code: number; className?: string }) {
  const I = code === 0 || code === 1 ? Sun : code === 2 ? CloudSun : code === 3 ? Cloud : code >= 45 && code <= 48 ? CloudFog : code >= 71 && code <= 86 && code !== 80 && code !== 81 && code !== 82 ? Snowflake : code >= 95 ? Zap : CloudRain
  return <I className={className} />
}

function BannerSlider({ banners }: { banners: Banner[] }) {
  const [i, setI] = useState(0)
  useEffect(() => {
    if (banners.length < 2) return
    const t = setInterval(() => setI((x) => (x + 1) % banners.length), 7000)
    return () => clearInterval(t)
  }, [banners.length])
  if (banners.length === 0) return null
  const b = banners[i]
  return (
    <section className={cn('relative overflow-hidden rounded-3xl bg-gradient-to-br text-white', themes[b.theme] ?? themes.crimson)} aria-roledescription="carousel">
      {b.imageUrl && <img src={b.imageUrl} alt="" className="absolute inset-0 h-full w-full object-cover opacity-30" />}
      <div className="absolute -top-24 -right-24 h-80 w-80 rounded-full bg-white/10 blur-3xl" />
      <div className="relative grid min-h-[300px] items-end gap-6 p-8 sm:p-12 md:min-h-[340px]">
        <div key={b.id} className="max-w-2xl animate-in">
          <span className="inline-flex items-center gap-2 rounded-full bg-white/15 px-3 py-1 text-xs font-medium backdrop-blur">Devoplus · Kurumsal</span>
          <h1 className="mt-4 font-display text-3xl font-extrabold leading-tight text-white sm:text-5xl">{b.title}</h1>
          {b.subtitle && <p className="mt-3 max-w-xl text-base text-white/80 sm:text-lg">{b.subtitle}</p>}
          {b.linkUrl && (
            b.linkUrl.startsWith('/')
              ? <Link to={b.linkUrl} className="btn mt-6 h-11 bg-white px-5 text-ink-900 hover:bg-white/90">{b.linkText ?? 'Detaylar'} <ArrowRight className="h-4 w-4" /></Link>
              : <a href={b.linkUrl} className="btn mt-6 h-11 bg-white px-5 text-ink-900 hover:bg-white/90">{b.linkText ?? 'Detaylar'} <ArrowRight className="h-4 w-4" /></a>
          )}
        </div>
      </div>
      {banners.length > 1 && (
        <div className="absolute right-6 bottom-6 flex items-center gap-2">
          <button className="grid h-9 w-9 place-items-center rounded-full bg-white/15 backdrop-blur hover:bg-white/25" onClick={() => setI((i - 1 + banners.length) % banners.length)} aria-label="Önceki"><ChevronLeft className="h-4 w-4" /></button>
          <div className="flex gap-1.5">{banners.map((x, n) => <button key={x.id} onClick={() => setI(n)} aria-label={`${n + 1}. banner`} className={cn('h-1.5 rounded-full transition-all', n === i ? 'w-6 bg-white' : 'w-1.5 bg-white/50')} />)}</div>
          <button className="grid h-9 w-9 place-items-center rounded-full bg-white/15 backdrop-blur hover:bg-white/25" onClick={() => setI((i + 1) % banners.length)} aria-label="Sonraki"><ChevronRight className="h-4 w-4" /></button>
        </div>
      )}
    </section>
  )
}

function MarketStrip() {
  const market = useQuery({ queryKey: ['public-market'], queryFn: () => api.get<MarketData>('/public/market'), refetchInterval: 5 * 60_000 })
  const [city, setCity] = useState('istanbul')
  const weather = useQuery({ queryKey: ['public-weather', city], queryFn: () => api.get<Weather | null>('/public/weather', { city }), refetchInterval: 10 * 60_000 })
  const icon = { USD: DollarSign, EUR: Euro, GRAM: Coins } as Record<string, typeof DollarSign>
  return (
    <section className="grid gap-4 lg:grid-cols-[1fr_1.1fr]">
      <div className="card p-5">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="card-title">Piyasalar</h2>
          <span className="text-[11px] muted">{market.data ? `Güncelleme ${fmt.time(market.data.fetchedAt)}` : ''}</span>
        </div>
        <div className="grid gap-3 sm:grid-cols-3">
          {market.isLoading && [0, 1, 2].map((n) => <Skeleton key={n} className="h-20" />)}
          {market.data?.rates.map((r) => {
            const I = icon[r.code] ?? Coins
            return (
              <div key={r.code} className="rounded-xl border border-ink-100 p-3.5 dark:border-white/[0.06]" title={r.source}>
                <div className="flex items-center gap-2 text-xs muted"><I className="h-3.5 w-3.5" /> {r.name}</div>
                <div className="mt-1.5 font-display text-xl font-bold text-ink-900 dark:text-white">{fmt.money(r.value)}</div>
                <div className="mt-0.5 truncate text-[10px] muted">{r.source}</div>
              </div>
            )
          })}
        </div>
        {market.data?.errors.map((e) => <p key={e} className="mt-3 text-xs text-amber-600 dark:text-amber-400">{e}</p>)}
        {market.error && <p className="mt-3 text-xs text-amber-600">Piyasa verisi şu anda alınamıyor.</p>}
      </div>
      <div className="card p-5">
        <div className="mb-4 flex items-center justify-between gap-3">
          <h2 className="card-title">Hava Durumu</h2>
          <select className="input h-8 w-36 text-xs" value={city} onChange={(e) => setCity(e.target.value)} aria-label="Şehir">
            <option value="istanbul">İstanbul</option><option value="ankara">Ankara</option><option value="izmir">İzmir</option><option value="adana">Adana</option>
          </select>
        </div>
        {weather.isLoading && <Skeleton className="h-20" />}
        {weather.data === null && <p className="text-sm muted">Hava durumu verisi şu anda alınamıyor.</p>}
        {weather.data && (
          <div className="flex flex-wrap items-center gap-6">
            <div className="flex items-center gap-3">
              <WeatherIcon code={weather.data.code} className="h-11 w-11 text-amber-500" />
              <div>
                <div className="font-display text-3xl font-bold text-ink-900 dark:text-white">{Math.round(weather.data.temperature)}°</div>
                <div className="text-xs muted">{weather.data.description} · Nem %{weather.data.humidity} · Rüzgâr {Math.round(weather.data.wind ?? 0)} km/s</div>
              </div>
            </div>
            <div className="flex flex-1 justify-end gap-2">
              {weather.data.days.slice(1).map((d) => (
                <div key={d.date} className="min-w-16 rounded-xl border border-ink-100 px-3 py-2 text-center dark:border-white/[0.06]">
                  <div className="text-[11px] capitalize muted">{fmt.weekday(d.date).slice(0, 3)}</div>
                  <WeatherIcon code={d.code} className="mx-auto my-1 h-5 w-5 text-ink-500 dark:text-ink-300" />
                  <div className="text-xs font-semibold text-ink-900 dark:text-white">{Math.round(d.max)}° <span className="font-normal muted">{Math.round(d.min)}°</span></div>
                </div>
              ))}
            </div>
            <div className="w-full text-[10px] muted">Kaynak: {weather.data.source}</div>
          </div>
        )}
      </div>
    </section>
  )
}

const YEAR = new Date().getFullYear()

export default function Landing() {
  // Canlı akış: yönetimde yapılan değişiklikler (banner, haber, ilan, etkinlik) sayfa açıkken en geç 1 dk içinde görünür.
  // Sunucu önbelleği içerik değişince temizlenir; yenileme sırasında mevcut içerik ekranda kalır.
  const home = useQuery({ queryKey: ['public-home'], queryFn: () => api.get<PublicHome>('/public/home'), refetchInterval: 60_000 })
  const d = home.data
  const stats = d ? [
    { label: 'Çalışan', value: d.stats.employees, icon: Users },
    { label: 'Mağaza', value: d.stats.stores, icon: Store },
    { label: 'Şehir', value: d.stats.cities, icon: MapPin },
    { label: 'Merkez birim', value: d.stats.headOfficeDepartments, icon: Building2 },
  ] : []

  return (
    <div className="min-h-screen bg-ink-50 dark:bg-ink-950">
      <header className="sticky top-0 z-20 border-b border-ink-200/70 bg-white/80 backdrop-blur-md dark:border-white/[0.06] dark:bg-ink-950/80">
        <div className="mx-auto flex h-16 max-w-7xl items-center gap-6 px-4 sm:px-6">
          <Logo />
          <nav className="ml-6 hidden items-center gap-1 text-sm md:flex" aria-label="Site menüsü">
            {[['#kurumsal', 'Kurumsal'], ['#haberler', 'Haberler'], ['#etkinlikler', 'Etkinlikler'], ['#kariyer', 'Kariyer']].map(([href, label]) => (
              <a key={href} href={href} className="rounded-lg px-3 py-2 text-ink-600 hover:bg-ink-100 hover:text-ink-900 dark:text-ink-300 dark:hover:bg-white/5 dark:hover:text-white">{label}</a>
            ))}
          </nav>
          <Link to="/login" className="btn-primary ml-auto">Portala Git <ArrowRight className="h-4 w-4" /></Link>
        </div>
      </header>

      <main className="mx-auto max-w-7xl space-y-10 px-4 py-8 sm:px-6">
        {home.isLoading ? <Skeleton className="h-[320px] rounded-3xl" /> : <BannerSlider banners={d?.banners ?? []} />}
        <MarketStrip />

        <section id="kurumsal" className="grid items-center gap-8 lg:grid-cols-2">
          <div>
            <span className="text-xs font-semibold uppercase tracking-[0.14em] text-brand-600 dark:text-brand-400">Hakkımızda</span>
            <h2 className="mt-2 text-3xl font-extrabold">Perakendede insan odaklı dijital dönüşüm</h2>
            <p className="mt-4 leading-7 muted">
              Devoplus; merkez ofisi, bölge yönetimleri ve Türkiye genelindeki mağazalarıyla müşterilerine ulaşan bir perakende ve teknoloji şirketidir.
              StajDesk, tüm çalışanlarımızın destek taleplerini, onay süreçlerini ve kurum içi iletişimini tek bir güvenli portalda bir araya getirir.
            </p>
            <div className="mt-6 flex flex-wrap gap-3">
              <Link to="/login" className="btn-dark h-10 px-5">Çalışan Girişi</Link>
              <a href="#kariyer" className="btn-secondary h-10 px-5">Açık Pozisyonlar</a>
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            {home.isLoading && [0, 1, 2, 3].map((n) => <Skeleton key={n} className="h-28" />)}
            {stats.map((s) => (
              <div key={s.label} className="card p-5">
                <s.icon className="h-5 w-5 text-brand-600 dark:text-brand-400" />
                <div className="mt-3 font-display text-3xl font-extrabold text-ink-900 dark:text-white">{fmt.number(s.value)}</div>
                <div className="text-sm muted">{s.label}</div>
              </div>
            ))}
          </div>
        </section>

        <section id="haberler">
          <div className="mb-4 flex items-end justify-between"><h2 className="text-2xl font-bold">Haberler ve duyurular</h2></div>
          <div className="grid gap-4 md:grid-cols-3">
            {d?.news.map((n, i) => (
              <article key={n.id} className={cn('card flex flex-col p-6', i === 0 && 'md:col-span-2 md:row-span-2')}>
                <div className="text-xs muted">{fmt.date(n.publishAt)}</div>
                <h3 className={cn('mt-2 font-bold', i === 0 ? 'text-2xl' : 'text-base')}>{n.title}</h3>
                <p className="mt-2 flex-1 text-sm leading-6 muted">{i === 0 ? n.body : n.summary}</p>
              </article>
            ))}
            {d?.news.length === 0 && <p className="muted">Yayında haber bulunmuyor.</p>}
          </div>
        </section>

        {(d?.events.length ?? 0) > 0 && (
          <section id="etkinlikler">
            <h2 className="mb-4 text-2xl font-bold">Yaklaşan etkinlikler</h2>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {d?.events.map((e) => (
                <div key={e.id} className="card p-5">
                  <div className="flex items-center gap-2 text-xs font-semibold text-brand-600 dark:text-brand-400"><CalendarDays className="h-3.5 w-3.5" /> {fmt.dateTime(e.startsAt)}</div>
                  <h3 className="mt-2 font-semibold">{e.title}</h3>
                  <p className="mt-1 text-sm muted">{e.location}</p>
                </div>
              ))}
            </div>
          </section>
        )}

        <section id="kariyer">
          <div className="mb-4"><h2 className="text-2xl font-bold">Açık pozisyonlar</h2><p className="mt-1 muted">Ekibimize katılın.</p></div>
          <div className="card divide-y divide-ink-100 dark:divide-white/[0.06]">
            {d?.jobs.map((j) => (
              <details key={j.id} className="group px-5 py-4">
                <summary className="flex cursor-pointer list-none flex-wrap items-center gap-3">
                  <Briefcase className="h-4 w-4 text-brand-600" />
                  <span className="font-semibold text-ink-900 dark:text-white">{j.title}</span>
                  <span className="text-sm muted">{j.department?.name ?? 'Devoplus'} · {j.location} · {employmentLabel[j.employmentType]}</span>
                  <ChevronRight className="ml-auto h-4 w-4 text-ink-400 transition group-open:rotate-90" />
                </summary>
                <div className="mt-3 space-y-2 pl-7 text-sm leading-6">
                  <p>{j.description}</p>
                  {j.requirements && <pre className="font-sans whitespace-pre-wrap muted">{j.requirements}</pre>}
                  {j.closesAt && <p className="text-xs muted">Son başvuru: {fmt.date(j.closesAt)}</p>}
                </div>
              </details>
            ))}
            {d?.jobs.length === 0 && <p className="p-5 muted">Şu anda açık pozisyon bulunmuyor.</p>}
          </div>
        </section>
      </main>

      <footer className="mt-10 border-t border-ink-200 py-8 dark:border-white/[0.06]">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-4 px-4 text-sm muted sm:px-6">
          <Logo compact />
          <span>© {YEAR} Devoplus · StajDesk kurumsal portal</span>
          <Link to="/login" className="font-semibold text-brand-600">Portala Git →</Link>
        </div>
      </footer>
    </div>
  )
}
