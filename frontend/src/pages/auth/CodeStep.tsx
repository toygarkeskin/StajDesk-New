import { ArrowLeft, MailCheck, RotateCw, ShieldCheck } from 'lucide-react'
import { useEffect, useRef, useState, type ClipboardEvent, type KeyboardEvent } from 'react'
import { Spinner } from '@/components/ui'
import { api, ApiError } from '@/lib/api'
import type { AuthResponse, LoginChallenge } from '@/lib/types'
import { cn } from '@/lib/utils'

/** Tek haneli kutulardan oluşan kod girişi: yapıştırma, geri silme, ok tuşları ve SMS/e-posta otomatik doldurma desteklenir. */
export function OtpInput({ length, value, onChange, disabled, invalid, onComplete }: {
  length: number; value: string; onChange: (v: string) => void; disabled?: boolean; invalid?: boolean; onComplete?: (v: string) => void
}) {
  const refs = useRef<(HTMLInputElement | null)[]>([])
  const focus = (i: number) => refs.current[Math.max(0, Math.min(length - 1, i))]?.focus()
  const set = (next: string) => {
    const clean = next.replace(/\D/g, '').slice(0, length)
    onChange(clean)
    if (clean.length === length) onComplete?.(clean)
    return clean
  }
  const onInput = (i: number, raw: string) => {
    const digits = raw.replace(/\D/g, '')
    if (!digits) return
    // Birden çok rakam geldiyse (otomatik doldurma) bu kutudan itibaren yerleştir
    const chars = value.padEnd(length, ' ').split('')
    digits.split('').forEach((d, k) => { if (i + k < length) chars[i + k] = d })
    const next = set(chars.join('').replace(/\s+$/, '').replace(/\s/g, ''))
    focus(Math.min(i + digits.length, next.length, length - 1))
  }
  const onKey = (i: number, e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Backspace') {
      e.preventDefault()
      if (value[i]) set(value.slice(0, i) + value.slice(i + 1))
      else if (i > 0) { set(value.slice(0, i - 1) + value.slice(i)); focus(i - 1) }
    } else if (e.key === 'ArrowLeft') { e.preventDefault(); focus(i - 1) }
    else if (e.key === 'ArrowRight') { e.preventDefault(); focus(i + 1) }
  }
  const onPaste = (e: ClipboardEvent<HTMLInputElement>) => {
    e.preventDefault()
    const next = set(e.clipboardData.getData('text'))
    focus(next.length)
  }
  return (
    <div className="flex justify-between gap-2" role="group" aria-label="Doğrulama kodu">
      {Array.from({ length }, (_, i) => (
        <input key={i} ref={(el) => { refs.current[i] = el }} value={value[i] ?? ''} disabled={disabled}
          inputMode="numeric" autoComplete={i === 0 ? 'one-time-code' : 'off'} maxLength={length} aria-label={`${i + 1}. hane`}
          autoFocus={i === 0}
          onChange={(e) => onInput(i, e.target.value)} onKeyDown={(e) => onKey(i, e)} onPaste={onPaste} onFocus={(e) => e.target.select()}
          className={cn('input h-12 w-full max-w-12 px-0 text-center font-mono text-xl font-semibold', invalid && 'input-error')} />
      ))}
    </div>
  )
}

/** Giriş 2. adım: e-postaya gönderilen kodun doğrulanması. */
export function CodeStep({ challenge: initial, onBack, onSuccess }: { challenge: LoginChallenge; onBack: () => void; onSuccess: (r: AuthResponse) => void }) {
  const [challenge, setChallenge] = useState(initial)
  const [code, setCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [fatal, setFatal] = useState(false)
  const [busy, setBusy] = useState(false)
  const [resending, setResending] = useState(false)
  const [info, setInfo] = useState<string | null>(null)
  const [now, setNow] = useState(() => Date.now())
  const [resendAt, setResendAt] = useState(() => Date.now() + initial.resendAfterSeconds * 1000)

  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(t)
  }, [])
  const resendIn = Math.max(0, Math.ceil((resendAt - now) / 1000))
  const expiresIn = Math.max(0, Math.floor((new Date(challenge.expiresAt).getTime() - now) / 1000))
  const expired = expiresIn === 0

  const verify = async (value: string) => {
    if (busy || value.length !== challenge.codeLength) return
    setBusy(true); setError(null); setInfo(null)
    try {
      onSuccess(await api.post<AuthResponse>('/auth/login/verify', { challengeId: challenge.challengeId, code: value }))
    } catch (e) {
      const err = e instanceof ApiError ? e : null
      setError(err?.errors?.code?.[0] ?? err?.message ?? 'Kod doğrulanamadı.')
      // Süresi dolan / kilitlenen kodda yeniden giriş gerekir
      if (err && ['CODE_EXPIRED', 'TOO_MANY_ATTEMPTS'].includes(err.code ?? '')) setFatal(true)
      setCode('')
    } finally { setBusy(false) }
  }

  const resend = async () => {
    setResending(true); setError(null); setInfo(null)
    try {
      const c = await api.post<LoginChallenge>('/auth/login/resend', { challengeId: challenge.challengeId })
      setChallenge(c); setCode(''); setFatal(false)
      setResendAt(Date.now() + c.resendAfterSeconds * 1000)
      setInfo('Yeni kod gönderildi. Önceki kod artık geçersiz.')
    } catch (e) {
      const err = e instanceof ApiError ? e : null
      setError(err?.message ?? 'Kod gönderilemedi.')
      if (err?.code === 'CODE_EXPIRED' || err?.code === 'TOO_MANY_CODES') setFatal(true)
    } finally { setResending(false) }
  }

  const mmss = `${Math.floor(expiresIn / 60)}:${String(expiresIn % 60).padStart(2, '0')}`
  return (
    <div className="mt-8 space-y-5">
      <div className="flex gap-3 rounded-xl border border-ink-200 bg-white p-4 dark:border-white/10 dark:bg-white/[0.03]">
        <span className="grid h-10 w-10 shrink-0 place-items-center rounded-lg bg-brand-50 text-brand-600 dark:bg-brand-400/10 dark:text-brand-300"><MailCheck className="h-5 w-5" /></span>
        <div className="text-sm">
          <div className="font-semibold text-ink-900 dark:text-white">E-postanızı kontrol edin</div>
          <div className="muted"><b className="text-ink-700 dark:text-ink-200">{challenge.maskedEmail}</b> adresine {challenge.codeLength} haneli bir doğrulama kodu gönderdik.</div>
        </div>
      </div>

      <form noValidate onSubmit={(e) => { e.preventDefault(); verify(code) }} className="space-y-4">
        {error && <div role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-sm text-red-700 dark:border-red-400/20 dark:bg-red-400/10 dark:text-red-300">{error}</div>}
        {info && !error && <div role="status" className="rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2.5 text-sm text-emerald-700 dark:border-emerald-400/20 dark:bg-emerald-400/10 dark:text-emerald-300">{info}</div>}
        <OtpInput length={challenge.codeLength} value={code} onChange={(v) => { setCode(v); if (error && !fatal) setError(null) }}
          onComplete={verify} disabled={busy || fatal || expired} invalid={!!error && !fatal} />
        <div className="flex items-center justify-between text-xs muted">
          <span>{expired ? 'Kodun süresi doldu.' : <>Kod <b className="tabular-nums text-ink-700 dark:text-ink-200">{mmss}</b> içinde geçersiz olur</>}</span>
          <button type="button" className="inline-flex items-center gap-1 font-medium text-brand-600 hover:underline disabled:pointer-events-none disabled:text-ink-400 dark:text-brand-400"
            disabled={resendIn > 0 || resending || fatal} onClick={resend}>
            {resending ? <Spinner className="h-3 w-3" /> : <RotateCw className="h-3 w-3" />}{resendIn > 0 ? `Yeniden gönder (${resendIn} sn)` : 'Kodu yeniden gönder'}
          </button>
        </div>
        {fatal || expired
          ? <button type="button" className="btn-primary h-10 w-full" onClick={onBack}><ArrowLeft className="h-4 w-4" /> Yeniden giriş yap</button>
          : <button className="btn-primary h-10 w-full" disabled={busy || code.length !== challenge.codeLength}>{busy ? <Spinner /> : <ShieldCheck className="h-4 w-4" />} Doğrula ve giriş yap</button>}
      </form>

      <div className="flex items-center justify-between text-xs">
        <button type="button" className="inline-flex items-center gap-1 muted hover:text-ink-900 dark:hover:text-white" onClick={onBack}><ArrowLeft className="h-3.5 w-3.5" /> Farklı hesapla giriş</button>
        {challenge.devMailboxUrl && <a className="font-medium text-brand-600 hover:underline dark:text-brand-400" href={challenge.devMailboxUrl} target="_blank" rel="noreferrer">Test posta kutusunu aç (Mailpit)</a>}
      </div>
    </div>
  )
}
