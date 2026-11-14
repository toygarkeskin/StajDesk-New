import { describe, expect, it } from 'vitest'
import { fmt, toInputDate } from './format'

// Intl biçimleri bölünmez boşluk kullanabilir; karşılaştırmada normal boşluğa çevrilir.
const plain = (s: string) => s.replace(/\s/g, ' ')

describe('fmt', () => {
  it('formats Turkish lira amounts', () => {
    expect(plain(fmt.money(1234.5))).toBe('₺1.234,50')
    expect(fmt.money(null)).toBe('—')
  })

  it('formats numbers with Turkish separators', () => {
    expect(fmt.number(1234567)).toBe('1.234.567')
    expect(fmt.number(2.75, 1)).toBe('2,8')
  })

  it('formats file sizes and durations', () => {
    expect(fmt.bytes(512)).toBe('512 B')
    expect(fmt.bytes(2048)).toBe('2 KB')
    expect(fmt.bytes(5 * 1024 * 1024)).toBe('5.0 MB')
    expect(fmt.hours(0.5)).toBe('30 dk')
    expect(fmt.hours(72)).toBe('3.0 gün')
  })

  it('builds initials with Turkish casing', () => {
    expect(fmt.initials('ilknur şahin')).toBe('İŞ')
  })

  it('formats dates in Turkish', () => {
    expect(fmt.date('2026-11-16')).toBe('16 Kas 2026')
    expect(fmt.date(null)).toBe('—')
    expect(toInputDate('2026-11-16T09:00:00Z')).toBe('2026-11-16')
  })
})
