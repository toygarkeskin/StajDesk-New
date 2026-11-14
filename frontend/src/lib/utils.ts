import clsx, { type ClassValue } from 'clsx'

export const cn = (...v: ClassValue[]) => clsx(v)

/** Rengi saydamlık ile yumuşatır (#rrggbb → rgba) */
export const tint = (hex: string, alpha: number) => {
  const h = hex.replace('#', '')
  const n = parseInt(h.length === 3 ? h.split('').map((c) => c + c).join('') : h, 16)
  return `rgba(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}, ${alpha})`
}

/** Kişi adından tutarlı avatar rengi */
export const avatarColor = (seed: string) => {
  const palette = ['#e11d48', '#7c3aed', '#2563eb', '#0891b2', '#059669', '#d97706', '#db2777', '#4f46e5', '#0f766e', '#ea580c']
  let h = 0
  for (const c of seed) h = (h * 31 + c.charCodeAt(0)) >>> 0
  return palette[h % palette.length]
}

export const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms))
