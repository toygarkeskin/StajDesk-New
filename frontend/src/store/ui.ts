import { create } from 'zustand'

const read = (k: string) => {
  try {
    return localStorage.getItem(k)
  } catch {
    return null
  }
}
const write = (k: string, v: string) => {
  try {
    localStorage.setItem(k, v)
  } catch {
    /* gizli mod vb. */
  }
}

interface UiState {
  theme: 'dark' | 'light'
  sidebarCollapsed: boolean
  mobileNav: boolean
  toggleTheme: () => void
  toggleSidebar: () => void
  setMobileNav: (v: boolean) => void
}

export const useUi = create<UiState>((set, get) => ({
  theme: read('stajdesk-theme') === 'light' ? 'light' : 'dark',
  sidebarCollapsed: read('stajdesk-sidebar') === '1',
  mobileNav: false,
  toggleTheme: () => {
    const next = get().theme === 'dark' ? 'light' : 'dark'
    document.documentElement.classList.toggle('dark', next === 'dark')
    write('stajdesk-theme', next)
    set({ theme: next })
  },
  toggleSidebar: () => {
    const v = !get().sidebarCollapsed
    write('stajdesk-sidebar', v ? '1' : '0')
    set({ sidebarCollapsed: v })
  },
  setMobileNav: (v) => set({ mobileNav: v }),
}))
