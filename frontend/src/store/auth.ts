import { create } from 'zustand'
import type { AuthResponse, Me } from '@/lib/types'

interface AuthState {
  accessToken: string | null
  user: Me | null
  /** Uygulama açılışında refresh çereziyle oturum geri yüklenene kadar true */
  bootstrapping: boolean
  setSession: (r: AuthResponse) => void
  setUser: (u: Me) => void
  requirePasswordChange: () => void
  clear: () => void
  done: () => void
}

export const useAuth = create<AuthState>((set) => ({
  accessToken: null,
  user: null,
  bootstrapping: true,
  setSession: (r) => set({ accessToken: r.accessToken, user: r.user }),
  setUser: (user) => set({ user }),
  requirePasswordChange: () => set((s) => (s.user ? { user: { ...s.user, mustChangePassword: true } } : s)),
  clear: () => set({ accessToken: null, user: null }),
  done: () => set({ bootstrapping: false }),
}))

/** İzin kontrolü (arayüz için; asıl kontrol her zaman sunucuda yapılır). */
export function useCan() {
  const user = useAuth((s) => s.user)
  const perms = new Set(user?.permissions ?? [])
  const roles = new Set(user?.roles.map((r) => r.code) ?? [])
  const isAdmin = roles.has('SUPER_ADMIN') || roles.has('ADMIN')
  return {
    can: (...anyOf: string[]) => anyOf.length === 0 || anyOf.some((p) => perms.has(p)),
    hasRole: (...anyOf: string[]) => anyOf.some((r) => roles.has(r)),
    isAdmin,
    user,
  }
}
