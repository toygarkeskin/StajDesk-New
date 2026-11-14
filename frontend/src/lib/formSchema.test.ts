import { describe, expect, it } from 'vitest'
import { buildSchema, revenueBreakdown } from './formSchema'
import type { FormField } from './types'

const fields: FormField[] = [
  { key: 'title', label: 'Başlık', type: 'text', required: true },
  { key: 'quantity', label: 'Adet', type: 'number', required: true, min: 1, max: 20 },
  { key: 'email', label: 'E-posta', type: 'email', required: false },
  { key: 'approver', label: 'Onaylayan', type: 'user', required: true },
  { key: 'tags', label: 'Etiketler', type: 'multiselect', required: true, options: ['A', 'B'] },
  { key: 'info', label: 'Bilgi metni', type: 'description', required: false },
]

describe('buildSchema', () => {
  const schema = buildSchema(fields)

  it('accepts a valid submission', () => {
    const r = schema.safeParse({ title: 'Monitör', quantity: 2, approver: { id: 'u1', fullName: 'Ece' }, tags: ['A'] })
    expect(r.success).toBe(true)
  })

  it('reports required fields with Turkish messages', () => {
    const r = schema.safeParse({})
    expect(r.success).toBe(false)
    const msgs = r.error!.issues.map((i) => `${String(i.path[0])}: ${i.message}`)
    expect(msgs).toContain('title: Başlık zorunludur.')
    expect(msgs.some((m) => m.startsWith('approver:'))).toBe(true)
    expect(msgs.some((m) => m.startsWith('tags:'))).toBe(true)
  })

  it('enforces numeric range and email format', () => {
    const r = schema.safeParse({ title: 'x', quantity: 50, email: 'gecersiz', approver: { id: 'u1' }, tags: ['A'] })
    const msgs = r.error!.issues.map((i) => i.message)
    expect(msgs).toContain('Adet en fazla 20 olabilir.')
    expect(msgs).toContain('Geçerli bir e-posta giriniz.')
  })

  it('ignores description-only fields', () => {
    expect(Object.keys(schema.shape)).not.toContain('info')
  })
})

describe('revenueBreakdown', () => {
  it('detects cash + card mismatch with the total', () => {
    const r = revenueBreakdown({ cashAmount: 100, cardAmount: 200, amount: 500 })
    expect(r).toMatchObject({ hasSplit: true, sum: 300, mismatch: true })
  })

  it('accepts matching totals and computes average basket', () => {
    const r = revenueBreakdown({ cashAmount: 4000, cardAmount: 6000, amount: 10000, receiptCount: 40 })
    expect(r.mismatch).toBe(false)
    expect(r.averageBasket).toBe(250)
  })

  it('has no split when only the total is entered', () => {
    expect(revenueBreakdown({ amount: 1000 })).toMatchObject({ hasSplit: false, mismatch: false, averageBasket: undefined })
  })
})
