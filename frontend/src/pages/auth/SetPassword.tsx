import { zodResolver } from '@hookform/resolvers/zod'
import { Check, KeyRound, X } from 'lucide-react'
import { useForm, useWatch } from 'react-hook-form'
import { Navigate, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { z } from 'zod'
import { Logo } from '@/components/layout'
import { Field, Spinner } from '@/components/ui'
import { api, ApiError } from '@/lib/api'
import type { AuthResponse } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useAuth } from '@/store/auth'

const passwordSchema = z.object({
  currentPassword: z.string().min(1, 'Mevcut (geçici) parolanızı giriniz.'),
  newPassword: z.string().min(10, 'En az 10 karakter olmalıdır.').regex(/[A-ZÇĞİÖŞÜ]/, 'En az bir büyük harf içermelidir.').regex(/[a-zçğıöşü]/, 'En az bir küçük harf içermelidir.').regex(/\d/, 'En az bir rakam içermelidir.'),
  confirm: z.string(),
}).refine((v) => v.newPassword === v.confirm, { message: 'Parolalar eşleşmiyor.', path: ['confirm'] })
  .refine((v) => v.newPassword !== v.currentPassword, { message: 'Yeni parola mevcut paroladan farklı olmalıdır.', path: ['newPassword'] })
type Values = z.infer<typeof passwordSchema>

export function PasswordForm({ onDone }: { onDone?: () => void }) {
  const setSession = useAuth((s) => s.setSession)
  const { register, control, handleSubmit, setError, formState: { errors, isSubmitting } } = useForm<Values>({ resolver: zodResolver(passwordSchema), mode: 'onTouched' })
  const pw = useWatch({ control, name: 'newPassword' }) ?? ''
  const rules = [['En az 10 karakter', pw.length >= 10], ['Büyük harf', /[A-ZÇĞİÖŞÜ]/.test(pw)], ['Küçük harf', /[a-zçğıöşü]/.test(pw)], ['Rakam', /\d/.test(pw)]] as const
  const submit = async (v: Values) => {
    try {
      const r = await api.post<AuthResponse>('/auth/change-password', { currentPassword: v.currentPassword, newPassword: v.newPassword })
      setSession(r)
      toast.success('Parolanız güncellendi.')
      onDone?.()
    } catch (e) {
      if (e instanceof ApiError && e.errors?.newPassword) setError('newPassword', { message: e.errors.newPassword[0] })
      else setError('currentPassword', { message: e instanceof Error ? e.message : 'İşlem başarısız.' })
    }
  }
  return (
    <form className="space-y-4" onSubmit={handleSubmit(submit)} noValidate>
      <Field label="Mevcut / geçici parola" error={errors.currentPassword?.message}>
        <input type="password" autoComplete="current-password" className={cn('input', errors.currentPassword && 'input-error')} {...register('currentPassword')} />
      </Field>
      <Field label="Yeni parola" error={errors.newPassword?.message}>
        <input type="password" autoComplete="new-password" className={cn('input', errors.newPassword && 'input-error')} {...register('newPassword')} />
      </Field>
      <ul className="grid grid-cols-2 gap-1.5 text-xs">
        {rules.map(([label, ok]) => (
          <li key={label} className={cn('flex items-center gap-1.5', ok ? 'text-emerald-600 dark:text-emerald-400' : 'muted')}>{ok ? <Check className="h-3.5 w-3.5" /> : <X className="h-3.5 w-3.5" />}{label}</li>
        ))}
      </ul>
      <Field label="Yeni parola (tekrar)" error={errors.confirm?.message}>
        <input type="password" autoComplete="new-password" className={cn('input', errors.confirm && 'input-error')} {...register('confirm')} />
      </Field>
      <button className="btn-primary w-full" disabled={isSubmitting}>{isSubmitting ? <Spinner /> : <KeyRound className="h-4 w-4" />} Parolayı kaydet</button>
    </form>
  )
}

export default function SetPassword() {
  const { user, bootstrapping } = useAuth()
  const nav = useNavigate()
  if (bootstrapping) return null
  if (!user) return <Navigate to="/login" replace />
  if (!user.mustChangePassword) return <Navigate to="/" replace />
  return (
    <div className="grid min-h-screen place-items-center px-4 py-10">
      <div className="w-full max-w-md">
        <Logo />
        <div className="card mt-6 p-6 sm:p-8">
          <h1 className="text-xl font-bold">Hoş geldiniz, {user.firstName}</h1>
          <p className="mt-1 text-sm muted">Güvenliğiniz için ilk girişte size verilen geçici parolayı kendi belirleyeceğiniz bir parolayla değiştirmeniz gerekmektedir.</p>
          <div className="mt-6"><PasswordForm onDone={() => nav('/', { replace: true })} /></div>
        </div>
      </div>
    </div>
  )
}
