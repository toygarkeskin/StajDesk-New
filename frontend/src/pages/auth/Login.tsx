import { zodResolver } from '@hookform/resolvers/zod'
import { ArrowLeft, Eye, EyeOff, LifeBuoy, LogIn, ShieldCheck, Workflow } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { Logo } from '@/components/layout'
import { Field, Spinner } from '@/components/ui'
import { api, ApiError } from '@/lib/api'
import type { AuthResponse, LoginChallenge, LoginResult } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useAuth } from '@/store/auth'
import { CodeStep } from './CodeStep'

const schema = z.object({
  email: z.string().trim().min(1, 'E-posta adresinizi giriniz.').email('Geçerli bir e-posta adresi giriniz.'),
  password: z.string().min(1, 'Parolanızı giriniz.'),
})
type Values = z.infer<typeof schema>

const demo = [
  ['Çalışan', 'calisan@devoplus.local'], ['Departman müdürü', 'mudur@devoplus.local'], ['IT destek', 'it.destek@devoplus.local'],
  ['İK uzmanı', 'ik.uzman@devoplus.local'], ['Super Admin', 'superadmin@devoplus.local'], ['CEO', 'ceo@devoplus.local'],
  ['Mağaza yöneticisi', 'magaza.yonetici@devoplus.local'], ['İlk giriş (parola belirleme)', 'yeni.personel@devoplus.local'],
]

const YEAR = new Date().getFullYear()

export default function Login() {
  const { user, setSession, bootstrapping } = useAuth()
  const nav = useNavigate()
  const location = useLocation() as { state?: { from?: string } }
  const [show, setShow] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [challenge, setChallenge] = useState<LoginChallenge | null>(null)
  const { register, handleSubmit, setValue, formState: { errors, isSubmitting } } = useForm<Values>({ resolver: zodResolver(schema), mode: 'onTouched' })

  if (!bootstrapping && user) return <Navigate to={user.mustChangePassword ? '/set-password' : location.state?.from ?? '/'} replace />

  const finish = (res: AuthResponse) => {
    setSession(res)
    nav(res.user.mustChangePassword ? '/set-password' : location.state?.from ?? '/', { replace: true })
  }

  const onSubmit = async (v: Values) => {
    setFormError(null)
    try {
      const res = await api.post<LoginResult>('/auth/login', v)
      if (res.requiresCode && res.challenge) setChallenge(res.challenge)
      else if (res.auth) finish(res.auth)
    } catch (e) {
      setFormError(e instanceof ApiError ? e.message : 'Giriş yapılamadı.')
    }
  }

  return (
    <div className="grid min-h-screen lg:grid-cols-[1.05fr_1fr]">
      <aside className="relative hidden overflow-hidden bg-ink-950 p-12 text-white lg:flex lg:flex-col">
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_20%_20%,rgba(240,52,90,0.35),transparent_45%),radial-gradient(circle_at_80%_80%,rgba(99,102,241,0.25),transparent_45%)]" />
        <div className="relative"><Logo light /></div>
        <div className="relative mt-auto max-w-md">
          <h1 className="font-display text-4xl font-extrabold leading-tight text-white">Tüm iş süreçleriniz,<br />tek bir portalda.</h1>
          <p className="mt-4 text-white/70">Destek talepleri, onay akışları, envanter ve kurum içi iletişim — rolünüze göre kişiselleştirilmiş tek ekranda.</p>
          <ul className="mt-8 space-y-3 text-sm text-white/80">
            <li className="flex items-center gap-3"><LifeBuoy className="h-4 w-4 text-brand-400" /> GLPI yaklaşımıyla akıllı yönlendirilen destek talepleri</li>
            <li className="flex items-center gap-3"><Workflow className="h-4 w-4 text-brand-400" /> 40+ departman uygulaması, çok adımlı onay akışları</li>
            <li className="flex items-center gap-3"><ShieldCheck className="h-4 w-4 text-brand-400" /> Rol ve yetki tabanlı güvenli erişim</li>
          </ul>
        </div>
        <div className="relative mt-10 text-xs text-white/40">© {YEAR} Devoplus</div>
      </aside>

      <main className="flex flex-col justify-center px-5 py-10 sm:px-12">
        <div className="mx-auto w-full max-w-sm">
          <Link to="/welcome" className="mb-8 inline-flex items-center gap-1.5 text-sm muted hover:text-ink-900 dark:hover:text-white"><ArrowLeft className="h-4 w-4" /> Tanıtım sayfası</Link>
          <div className="lg:hidden"><Logo /></div>
          <h2 className="mt-6 text-2xl font-bold">{challenge ? 'Giriş doğrulama' : 'Çalışan girişi'}</h2>
          <p className="mt-1 text-sm muted">{challenge ? 'Hesabınızın güvenliği için e-postanıza gelen kodu giriniz.' : 'Kurumsal e-posta adresiniz ve parolanızla giriş yapınız.'}</p>

          {challenge ? <CodeStep key={challenge.challengeId} challenge={challenge} onSuccess={finish} onBack={() => { setChallenge(null); setValue('password', '') }} /> : <>

          <form className="mt-8 space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
            {formError && <div role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-sm text-red-700 dark:border-red-400/20 dark:bg-red-400/10 dark:text-red-300">{formError}</div>}
            <Field label="E-posta" error={errors.email?.message} htmlFor="email">
              <input id="email" type="email" autoComplete="username" className={cn('input h-10', errors.email && 'input-error')} placeholder="ad.soyad@devoplus.local" {...register('email')} />
            </Field>
            <Field label="Parola" error={errors.password?.message} htmlFor="password">
              <div className="relative">
                <input id="password" type={show ? 'text' : 'password'} autoComplete="current-password" className={cn('input h-10 pr-10', errors.password && 'input-error')} {...register('password')} />
                <button type="button" className="absolute top-1/2 right-2 -translate-y-1/2 p-1 text-ink-400 hover:text-ink-700 dark:hover:text-white" onClick={() => setShow((v) => !v)} aria-label={show ? 'Parolayı gizle' : 'Parolayı göster'}>
                  {show ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </Field>
            <button className="btn-primary h-10 w-full" disabled={isSubmitting}>{isSubmitting ? <Spinner /> : <LogIn className="h-4 w-4" />} Giriş yap</button>
            <p className="text-center text-xs muted">Hesabınız yoksa İnsan Kaynakları ile iletişime geçiniz. Dışarıdan kayıt yapılamaz.</p>
          </form>

          <details className="mt-8 rounded-xl border border-dashed border-ink-300 p-4 text-xs dark:border-white/15">
            <summary className="cursor-pointer font-semibold text-ink-700 dark:text-ink-200">Demo hesapları</summary>
            <p className="mt-2 muted">Tüm demo hesaplarının parolası <code className="kbd">.env</code> dosyasındaki <code className="kbd">SEED_DEFAULT_PASSWORD</code> değeridir.</p>
            <div className="mt-3 grid gap-1.5">
              {demo.map(([label, email]) => (
                <button key={email} type="button" className="flex items-center justify-between rounded-lg px-2 py-1.5 text-left hover:bg-ink-100 dark:hover:bg-white/5" onClick={() => setValue('email', email, { shouldValidate: true })}>
                  <span className="font-medium text-ink-800 dark:text-ink-100">{label}</span><span className="muted">{email}</span>
                </button>
              ))}
            </div>
          </details>
          </>}
        </div>
      </main>
    </div>
  )
}
