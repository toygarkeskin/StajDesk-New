import { mkdirSync } from 'node:fs'
import { join } from 'node:path'
import { expect, test, type Browser, type Page } from '@playwright/test'
import { login, settle, users } from '../helpers'

/**
 * README ekran görüntüleri: gerçek arayüz, **kurgusal demo verisiyle** (anonim örnek organizasyon).
 * Gerçek organizasyon dosyası yüklü bir sisteme karşı çalıştırmayın.
 *
 *   cd frontend && npm run screenshots      → ../docs/images/*.png
 */
const OUT = join(process.cwd(), '..', 'docs', 'images')
mkdirSync(OUT, { recursive: true })

async function open(browser: Browser, email: string, theme: 'dark' | 'light', viewport = { width: 1440, height: 900 }) {
  const ctx = await browser.newContext({ viewport, deviceScaleFactor: 2, colorScheme: theme })
  await ctx.addInitScript((t) => localStorage.setItem('stajdesk-theme', t), theme)
  const page = await ctx.newPage()
  await login(page, email)
  return page
}

async function shot(page: Page, name: string, path?: string) {
  if (path) await page.goto(path)
  await settle(page)
  await page.mouse.move(0, 0)
  await page.waitForTimeout(600) // grafik ve geçiş animasyonları otursun
  await page.screenshot({ path: join(OUT, `${name}.png`) })
}

test.describe.configure({ mode: 'serial', timeout: 120_000 })

test('genel site ve giriş', async ({ browser }) => {
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 2, colorScheme: 'dark' })
  const page = await ctx.newPage()
  await page.goto('/welcome')
  await shot(page, 'landing')
  await page.goto('/login')
  await page.getByLabel('E-posta').fill(users.employee)
  await page.getByLabel('Parola', { exact: true }).fill('x'.repeat(12))
  await shot(page, 'login')
})

test('çalışan: ana sayfa, izin, başvuru akışı', async ({ browser }) => {
  const page = await open(browser, users.employee, 'dark')
  await shot(page, 'dashboard', '/')
  await shot(page, 'leave', '/apps/leave')
  await page.locator('a[href^="/submissions/"]').first().click()
  await expect(page).toHaveURL(/\/submissions\/\d+$/)
  await shot(page, 'submission')
})

test('IT destek: talep listesi ve talep ayrıntısı', async ({ browser }) => {
  const page = await open(browser, users.it, 'light')
  await shot(page, 'tickets', '/tickets')
  await page.locator('a[href^="/tickets/"]:not([href="/tickets/new"])').first().click()
  await expect(page).toHaveURL(/\/tickets\/\d+$/)
  await shot(page, 'ticket-detail')
})

test('yönetici: form tasarımcısı, raporlar, sistem sağlığı', async ({ browser }) => {
  const page = await open(browser, users.admin, 'dark')
  await page.goto('/admin/forms')
  await settle(page)
  await page.getByRole('button', { name: 'Yeni form' }).click()
  await page.getByRole('dialog').getByRole('tab', { name: /Onay adımları/ }).click()
  await shot(page, 'form-builder')
  await page.keyboard.press('Escape')
  await shot(page, 'reports', '/reports')
  await shot(page, 'system', '/system')
})

test('mobil görünüm', async ({ browser }) => {
  const page = await open(browser, users.employee, 'light', { width: 390, height: 844 })
  await shot(page, 'mobile', '/')
})
