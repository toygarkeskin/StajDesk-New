import { z } from 'zod'
import type { FormField } from './types'

/** Form tanımındaki alanlardan istemci tarafı doğrulama şeması üretir (sunucu aynı kuralları ayrıca uygular). */
export function buildSchema(fields: FormField[]) {
  const shape: Record<string, z.ZodTypeAny> = {}
  for (const f of fields) {
    if (f.type === 'description') continue
    const req = `${f.label} zorunludur.`
    let s: z.ZodTypeAny
    switch (f.type) {
      case 'number':
      case 'currency': {
        // Boş bırakılan zorunlu sayı alanı "zorunludur", geçersiz giriş "sayı olmalıdır" mesajı verir
        let n = z.number({ error: (i) => (i.input === undefined ? req : `${f.label} sayı olmalıdır.`) })
        if (f.min != null) n = n.min(f.min, `${f.label} en az ${f.min} olmalıdır.`)
        if (f.max != null) n = n.max(f.max, `${f.label} en fazla ${f.max} olabilir.`)
        s = f.required ? n : n.optional().nullable()
        break
      }
      case 'multiselect':
        s = f.required ? z.array(z.string(), { message: req }).min(1, req) : z.array(z.string()).optional()
        break
      case 'checkbox':
        s = f.required ? z.literal(true, { message: req }) : z.boolean().optional()
        break
      case 'user':
        s = f.required ? z.object({ id: z.string() }, { message: req }) : z.object({ id: z.string() }).nullable().optional()
        break
      case 'file':
        s = f.required ? z.object({ id: z.string() }, { message: req }) : z.object({ id: z.string() }).nullable().optional()
        break
      case 'email':
        s = f.required ? z.string({ message: req }).trim().min(1, req).email('Geçerli bir e-posta giriniz.') : z.string().trim().email('Geçerli bir e-posta giriniz.').or(z.literal('')).optional()
        break
      default: {
        let t = z.string({ message: f.required ? req : undefined }).trim().max(f.maxLength ?? (f.type === 'textarea' ? 4000 : 200), 'Çok uzun.')
        if (f.required) t = t.min(1, req)
        s = f.required ? t : t.optional()
      }
    }
    shape[f.key] = s
  }
  return z.object(shape)
}

/** Ciro formu: nakit + kart toplamı, toplamla uyum ve ortalama sepet (sunucu aynı kuralı uygular). */
export function revenueBreakdown(v: Record<string, unknown>) {
  const num = (x: unknown) => (typeof x === 'number' && Number.isFinite(x) ? x : undefined)
  const cash = num(v.cashAmount), card = num(v.cardAmount), amount = num(v.amount), receipts = num(v.receiptCount)
  const hasSplit = cash != null || card != null
  const sum = (cash ?? 0) + (card ?? 0)
  return {
    hasSplit,
    sum,
    mismatch: hasSplit && amount != null && Math.abs(sum - amount) > 0.01,
    averageBasket: amount != null && receipts ? amount / receipts : undefined,
  }
}
