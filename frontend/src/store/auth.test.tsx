import { act, render, renderHook, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import { Guard } from '@/components/layout'
import type { Me } from '@/lib/types'
import { useAuth, useCan } from './auth'

const user = (roles: string[], permissions: string[]): Me => ({
  id: 'u1', firstName: 'Ece', lastName: 'Çalışan', fullName: 'Ece Çalışan', email: 'ece@devoplus.local', employmentType: 'FullTime',
  roles: roles.map((code) => ({ code, name: code })), permissions, mustChangePassword: false,
} as Me)

const signIn = (u: Me) => act(() => useAuth.getState().setSession({ accessToken: 'token', expiresAt: '', user: u }))

afterEach(() => act(() => useAuth.getState().clear()))

describe('useCan', () => {
  it('grants only the permissions the user holds', () => {
    signIn(user(['EMPLOYEE'], ['tickets.create']))
    const { result } = renderHook(() => useCan())
    expect(result.current.can('tickets.create')).toBe(true)
    expect(result.current.can('users.manage')).toBe(false)
    expect(result.current.can('users.manage', 'tickets.create')).toBe(true) // herhangi biri yeterli
    expect(result.current.isAdmin).toBe(false)
  })

  it('recognises admin roles', () => {
    signIn(user(['ADMIN'], []))
    expect(renderHook(() => useCan()).result.current.isAdmin).toBe(true)
  })

  it('denies everything after sign-out', () => {
    signIn(user(['EMPLOYEE'], ['tickets.create']))
    act(() => useAuth.getState().clear())
    expect(renderHook(() => useCan()).result.current.can('tickets.create')).toBe(false)
  })
})

describe('Guard', () => {
  const page = () => render(<MemoryRouter><Guard perms={['users.manage']}><p>Kullanıcı yönetimi</p></Guard></MemoryRouter>)

  it('hides protected content without permission and offers an access request', () => {
    signIn(user(['EMPLOYEE'], []))
    page()
    expect(screen.queryByText('Kullanıcı yönetimi')).not.toBeInTheDocument()
    expect(screen.getByText('Bu sayfaya erişim yetkiniz yok')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Erişim talebi oluştur' })).toHaveAttribute('href', '/apps/access-request')
  })

  it('renders protected content with permission', () => {
    signIn(user(['IT'], ['users.manage']))
    page()
    expect(screen.getByText('Kullanıcı yönetimi')).toBeInTheDocument()
  })
})
