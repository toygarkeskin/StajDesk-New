import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type { Category, Department, Group, JobTitle, Role } from './types'

const long = { staleTime: 5 * 60_000 }
export const useDepartments = () => useQuery({ queryKey: ['departments'], queryFn: () => api.get<Department[]>('/org/departments'), ...long })
export const useTitles = () => useQuery({ queryKey: ['titles'], queryFn: () => api.get<JobTitle[]>('/org/titles'), ...long })
export const useCategories = () => useQuery({ queryKey: ['categories'], queryFn: () => api.get<Category[]>('/ticket-categories'), ...long })
export const useGroups = () => useQuery({ queryKey: ['groups'], queryFn: () => api.get<Group[]>('/org/groups'), ...long })
export const useRoles = (enabled = true) => useQuery({ queryKey: ['roles'], queryFn: () => api.get<Role[]>('/org/roles'), enabled, ...long })
