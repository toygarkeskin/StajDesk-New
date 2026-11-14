import { zodResolver } from '@hookform/resolvers/zod'
import { Info, Send } from 'lucide-react'
import { useMemo } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { Field, FileUploader, Spinner, UserPicker } from '@/components/ui'
import { buildSchema } from '@/lib/formSchema'
import type { FileDto, FormField } from '@/lib/types'
import { cn } from '@/lib/utils'

export function DynamicForm({ fields, onSubmit, submitting, serverErrors, submitLabel = 'Başvuruyu gönder', extra }: {
  fields: FormField[]; onSubmit: (data: Record<string, unknown>) => void; submitting?: boolean; serverErrors?: Record<string, string[]> | null; submitLabel?: string
  extra?: (values: Record<string, unknown>) => React.ReactNode
}) {
  const schema = useMemo(() => buildSchema(fields), [fields])
  const { register, control, handleSubmit, formState: { errors } } = useForm<Record<string, unknown>>({ resolver: zodResolver(schema), mode: 'onTouched' })
  const values = useWatch({ control }) as Record<string, unknown>
  const err = (k: string) => (errors[k]?.message as string | undefined) ?? serverErrors?.[k]?.[0]

  return (
    <form className="space-y-4" noValidate onSubmit={handleSubmit((v) => {
      const out: Record<string, unknown> = {}
      for (const [k, val] of Object.entries(v)) {
        if (val === undefined || val === null || val === '') continue
        // Kişi alanı sunucuya yalnızca kimlik olarak gönderilir
        out[k] = fields.find((f) => f.key === k)?.type === 'user' ? (val as { id: string }).id : val
      }
      onSubmit(out)
    })}>
      <div className="grid gap-4 sm:grid-cols-2">
        {fields.map((f) => {
          const wide = f.type === 'textarea' || f.type === 'description' || f.type === 'file' || f.type === 'multiselect'
          const e = err(f.key)
          const cls = cn('input', e && 'input-error')
          if (f.type === 'description')
            return <div key={f.key} className="flex gap-2.5 rounded-lg border border-sky-200 bg-sky-50 p-3 text-sm text-sky-800 sm:col-span-2 dark:border-sky-400/20 dark:bg-sky-400/[0.06] dark:text-sky-200"><Info className="mt-0.5 h-4 w-4 shrink-0" />{f.label}</div>
          return (
            <Field key={f.key} label={f.type === 'checkbox' ? undefined : f.label} required={f.required} error={e} help={f.helpText} className={wide ? 'sm:col-span-2' : undefined} htmlFor={`f-${f.key}`}>
              {f.type === 'textarea' && <textarea id={`f-${f.key}`} rows={4} className={cls} placeholder={f.placeholder ?? undefined} {...register(f.key)} />}
              {(f.type === 'text' || f.type === 'email') && <input id={`f-${f.key}`} type={f.type === 'email' ? 'email' : 'text'} className={cls} placeholder={f.placeholder ?? undefined} {...register(f.key)} />}
              {f.type === 'date' && <input id={`f-${f.key}`} type="date" className={cls} {...register(f.key)} />}
              {(f.type === 'number' || f.type === 'currency') && (
                <div className="relative">
                  <input id={`f-${f.key}`} type="number" step={f.type === 'currency' ? '0.01' : '1'} min={f.min ?? undefined} max={f.max ?? undefined} className={cn(cls, f.type === 'currency' && 'pr-8')}
                    {...register(f.key, { setValueAs: (v) => (v === '' || v == null ? undefined : Number(v)) })} />
                  {f.type === 'currency' && <span className="pointer-events-none absolute top-1/2 right-3 -translate-y-1/2 text-xs muted">₺</span>}
                </div>
              )}
              {f.type === 'select' && (
                <select id={`f-${f.key}`} className={cls} defaultValue="" {...register(f.key)}>
                  <option value="" disabled>Seçiniz…</option>{f.options?.map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              )}
              {f.type === 'multiselect' && (
                <Controller control={control} name={f.key} render={({ field }) => {
                  const sel = (field.value as string[] | undefined) ?? []
                  return (
                    <div className="flex flex-wrap gap-2" role="group" aria-label={f.label}>
                      {f.options?.map((o) => {
                        const on = sel.includes(o)
                        return (
                          <button type="button" key={o} aria-pressed={on} onClick={() => { field.onChange(on ? sel.filter((x) => x !== o) : [...sel, o]); field.onBlur() }}
                            className={cn('rounded-full border px-3 py-1.5 text-xs font-medium transition', on ? 'border-brand-500 bg-brand-50 text-brand-700 dark:bg-brand-400/10 dark:text-brand-200' : 'border-ink-200 hover:border-ink-300 dark:border-white/10')}>
                            {o}
                          </button>
                        )
                      })}
                    </div>
                  )
                }} />
              )}
              {f.type === 'checkbox' && (
                <label className="flex cursor-pointer items-center gap-2.5 pt-6 text-sm">
                  <input type="checkbox" className="h-4 w-4 rounded accent-brand-600" {...register(f.key)} /> {f.label}{f.required && <span className="text-brand-600">*</span>}
                </label>
              )}
              {f.type === 'user' && (
                <Controller control={control} name={f.key} render={({ field }) => (
                  <UserPicker value={(field.value as { id: string; fullName: string } | null) ?? null} invalid={!!e}
                    onChange={(u) => { field.onChange(u); field.onBlur() }} />
                )} />
              )}
              {f.type === 'file' && (
                <Controller control={control} name={f.key} render={({ field }) => (
                  <FileUploader value={field.value ? [field.value as FileDto] : []} onChange={(files) => { field.onChange(files[0] ?? null); field.onBlur() }} />
                )} />
              )}
            </Field>
          )
        })}
      </div>
      {extra?.(values)}
      <div className="flex justify-end">
        <button className="btn-primary" disabled={submitting}>{submitting ? <Spinner /> : <Send className="h-4 w-4" />} {submitLabel}</button>
      </div>
    </form>
  )
}
