import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { expect, type APIRequestContext, type Page } from '@playwright/test'

export const MAILPIT = process.env.E2E_MAILPIT ?? 'http://localhost:8126'

/** Demo parolası: E2E_PASSWORD ya da ../.env (değer hiçbir çıktıya yazılmaz). */
export function demoPassword(): string {
  if (process.env.E2E_PASSWORD) return process.env.E2E_PASSWORD
  const env = join(process.cwd(), '..', '.env')
  if (existsSync(env)) {
    const m = readFileSync(env, 'utf8').match(/^SEED_DEFAULT_PASSWORD=(.*)$/m)
    if (m?.[1]) return m[1].trim()
  }
  throw new Error('Demo parolası bulunamadı: E2E_PASSWORD tanımlayın veya kök dizinde .env oluşturun (node scripts/setup-secrets.mjs).')
}

export const users = {
  employee: 'calisan@devoplus.local',
  manager: 'mudur@devoplus.local',
  it: 'it.destek@devoplus.local',
  hr: 'ik.uzman@devoplus.local',
  admin: 'superadmin@devoplus.local',
  ceo: 'ceo@devoplus.local',
  store: 'magaza.yonetici@devoplus.local',
  newHire: 'yeni.personel@devoplus.local',
} as const

interface MailSummary { ID: string; Created: string; Subject: string; To: { Address: string }[] }

/** Mailpit'te belirtilen alıcıya, verilen zamandan sonra gelen son doğrulama kodunu bekler. */
export async function waitForLoginCode(request: APIRequestContext, email: string, since: number): Promise<string> {
  for (let i = 0; i < 40; i++) {
    const res = await request.get(`${MAILPIT}/api/v1/search`, { params: { query: `to:"${email}" subject:"doğrulama kodunuz"`, limit: '5' } })
    if (res.ok()) {
      const { messages } = (await res.json()) as { messages: MailSummary[] }
      const fresh = messages.find((m) => new Date(m.Created).getTime() >= since - 2000)
      if (fresh) {
        const msg = (await (await request.get(`${MAILPIT}/api/v1/message/${fresh.ID}`)).json()) as { Text: string }
        const code = msg.Text.match(/\b(\d{6})\b/)?.[1]
        if (code) return code
      }
    }
    await new Promise((r) => setTimeout(r, 500))
  }
  throw new Error(`Doğrulama kodu e-postası gelmedi: ${email}`)
}

/** Parola + e-posta kodu ile UI üzerinden giriş yapar. */
export async function login(page: Page, email: string, password = demoPassword()) {
  await page.goto('/login')
  await page.getByLabel('E-posta').fill(email)
  await page.getByLabel('Parola', { exact: true }).fill(password)
  const since = Date.now()
  await page.getByRole('button', { name: 'Giriş yap' }).click()
  await expect(page.getByText('E-postanızı kontrol edin')).toBeVisible()
  const code = await waitForLoginCode(page.request, email, since)
  await page.getByLabel('1. hane').fill(code) // tek kutuya tüm kod: otomatik doldurma gibi dağıtılır ve gönderilir
  await page.waitForURL((u) => !u.pathname.startsWith('/login'))
}

/**
 * Testte oturum açar. Oturum çerezi testler arasında paylaşılmaz: refresh token her kullanımda döndürüldüğü için
 * eski çerezin yeniden kullanımı (doğru şekilde) tüm oturumları kapatır.
 */
export async function sessionFor(page: Page, email: string) {
  await login(page, email)
}

/**
 * Sayfa sağlığı gözcüsü: konsol hataları, yakalanmamış istisnalar ve 5xx API yanıtlarını toplar.
 * Beklenen 401/403/404 gibi istemci yanıtları hata sayılmaz (uygulama bunları kullanıcıya düzgün gösterir).
 */
export function watchHealth(page: Page) {
  const problems: string[] = []
  page.on('pageerror', (e) => problems.push(`pageerror: ${e.message}`))
  page.on('console', (m) => {
    if (m.type() !== 'error') return
    const t = m.text()
    // Tarayıcının kendi ağ günlüğü (ör. beklenen 401/403) uygulama hatası değildir; yanıtlar aşağıda ayrıca denetlenir
    if (/Failed to load resource/.test(t)) return
    problems.push(`console: ${t}`)
  })
  page.on('response', (r) => {
    if (r.status() >= 500 && r.url().includes('/api/')) problems.push(`HTTP ${r.status()} ${r.request().method()} ${new URL(r.url()).pathname}`)
  })
  return problems
}

/** Sayfa yüklenmesini ve sorguların bitmesini bekler. */
export async function settle(page: Page) {
  await page.waitForLoadState('networkidle')
  await expect(page.locator('.animate-spin')).toHaveCount(0, { timeout: 15_000 })
}
