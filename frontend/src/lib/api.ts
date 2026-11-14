import axios, { AxiosError, type AxiosRequestConfig } from 'axios'
import type { AuthResponse, Envelope } from './types'
import { useAuth } from '@/store/auth'

/**
 * API istemcisi. Erişim token'ı yalnızca bellekte tutulur (XSS ile localStorage'dan çalınamaz);
 * refresh token httpOnly çerezdedir. 401 alınırsa tek seferlik sessiz yenileme yapılır.
 */
export const http = axios.create({ baseURL: import.meta.env.VITE_API_URL ?? '/api', withCredentials: true, timeout: 20000 })

export class ApiError extends Error {
  status: number
  code?: string | null
  errors?: Record<string, string[]> | null
  constructor(message: string, status: number, code?: string | null, errors?: Record<string, string[]> | null) {
    super(message)
    this.status = status
    this.code = code
    this.errors = errors
  }
}

http.interceptors.request.use((config) => {
  const token = useAuth.getState().accessToken
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

let refreshing: Promise<string | null> | null = null

export async function refreshSession(): Promise<string | null> {
  refreshing ??= axios
    .post<Envelope<AuthResponse>>(`${http.defaults.baseURL}/auth/refresh`, null, { withCredentials: true })
    .then((r) => {
      useAuth.getState().setSession(r.data.data)
      return r.data.data.accessToken
    })
    .catch(() => {
      useAuth.getState().clear()
      return null
    })
    .finally(() => setTimeout(() => (refreshing = null), 0))
  return refreshing
}

http.interceptors.response.use(
  (r) => r,
  async (error: AxiosError<Envelope<unknown>>) => {
    const original = error.config as AxiosRequestConfig & { _retried?: boolean }
    const status = error.response?.status ?? 0
    const url = original?.url ?? ''
    if (status === 401 && !original._retried && !url.includes('/auth/')) {
      original._retried = true
      const token = await refreshSession()
      if (token) return http(original)
    }
    const body = error.response?.data
    if (status === 403 && body?.code === 'PASSWORD_CHANGE_REQUIRED') useAuth.getState().requirePasswordChange()
    const message =
      body?.message ??
      (status === 0 ? 'Sunucuya ulaşılamıyor. İnternet bağlantınızı kontrol ediniz.' : status === 429 ? 'Çok fazla istek gönderildi, lütfen biraz bekleyiniz.' : 'Beklenmeyen bir hata oluştu.')
    return Promise.reject(new ApiError(message, status, body?.code, body?.errors))
  },
)

async function unwrap<T>(p: Promise<{ data: Envelope<T> }>): Promise<T> {
  const r = await p
  return r.data?.data as T
}

export const api = {
  get: <T>(url: string, params?: object) => unwrap<T>(http.get(url, { params })),
  post: <T>(url: string, body?: unknown) => unwrap<T>(http.post(url, body)),
  put: <T>(url: string, body?: unknown) => unwrap<T>(http.put(url, body)),
  patch: <T>(url: string, body?: unknown) => unwrap<T>(http.patch(url, body)),
  del: (url: string, params?: object) => http.delete(url, { params }).then(() => undefined),
  /** Mesajı da döndürür (toast için) */
  send: async <T>(method: 'post' | 'put' | 'patch', url: string, body?: unknown) => {
    const r = await http[method]<Envelope<T>>(url, body)
    return { data: r.data.data, message: r.data.message ?? undefined }
  },
  upload: async (file: File) => {
    const fd = new FormData()
    fd.append('file', file)
    const r = await http.post<Envelope<import('./types').FileDto>>('/files', fd, { headers: { 'Content-Type': 'multipart/form-data' } })
    return r.data.data
  },
  /** Yetki kontrollü dosya indirme (token ile) */
  download: async (url: string, fileName: string) => {
    const r = await http.get(url, { responseType: 'blob' })
    const href = URL.createObjectURL(r.data as Blob)
    const a = document.createElement('a')
    a.href = href
    a.download = fileName
    a.click()
    setTimeout(() => URL.revokeObjectURL(href), 1000)
  },
}

export const errorMessage = (e: unknown) => (e instanceof ApiError || e instanceof Error ? e.message : 'Beklenmeyen bir hata oluştu.')
