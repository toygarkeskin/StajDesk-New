import { expect, test } from '@playwright/test'
import { sessionFor, settle, users, watchHealth } from './helpers'

/**
 * Tüm portal sayfalarını her rolle açar ve şunları doğrular:
 *  - sayfa hatasız yüklenir (yakalanmamış istisna, konsol hatası, 5xx API yanıtı yok)
 *  - "Veriler yüklenemedi" / "Sayfa bulunamadı" durumu görünmez
 *  - yetkisi olmayan rol, sayfa yerine anlaşılır "erişim yetkiniz yok" ekranı görür (çökme yok)
 *  - sayfa yatayda taşmaz (masaüstü)
 */
const routes = [
  '/', '/tickets', '/tickets/new', '/apps', '/apps/limits', '/apps/leave', '/apps/store-revenue', '/apps/shift', '/apps/hardware-request',
  '/requests', '/approvals', '/inventory', '/kb', '/directory', '/profile', '/announcements', '/events', '/achievements', '/surveys',
  '/documents', '/messages', '/jobs', '/notifications', '/tasks', '/projects', '/stores', '/on-call', '/reports',
  '/admin/users', '/admin/account-requests', '/admin/organization', '/admin/groups', '/admin/roles', '/admin/helpdesk', '/admin/forms',
  '/admin/content', '/admin/leave', '/admin/audit', '/system', '/system/cache',
]

const roles = [
  ['Çalışan', users.employee], ['Departman müdürü', users.manager], ['IT destek', users.it], ['İK uzmanı', users.hr],
  ['Super Admin', users.admin], ['CEO', users.ceo], ['Mağaza yöneticisi', users.store],
] as const

for (const [role, email] of roles) {
  test.describe(`Tarama · ${role}`, () => {
    test('tüm sayfalar hatasız açılır', async ({ page }) => {
      test.setTimeout(240_000)
      await sessionFor(page, email)
      const problems = watchHealth(page)
      const report: string[] = []
      for (const path of routes) {
        const before = problems.length
        await page.goto(path)
        await settle(page)
        const failed = await page.getByText('Veriler yüklenemedi').count()
        const notFound = await page.getByText('Sayfa bulunamadı').count()
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
        if (failed) report.push(`${path}: "Veriler yüklenemedi" görünüyor`)
        if (notFound) report.push(`${path}: "Sayfa bulunamadı"`)
        if (overflow > 1) report.push(`${path}: yatay taşma ${overflow}px`)
        for (const p of problems.slice(before)) report.push(`${path}: ${p}`)
      }
      expect(report, report.join('\n')).toEqual([])
    })
  })
}

test.describe('Detay sayfaları', () => {
  test('liste → detay geçişleri çalışır', async ({ page }) => {
    test.setTimeout(120_000)
    await sessionFor(page, users.admin)
    const problems = watchHealth(page)
    const cases: [string, RegExp][] = [['/tickets', /\/tickets\/\d+$/], ['/inventory', /\/inventory\/\d+$/], ['/kb', /\/kb\/\d+$/], ['/surveys', /\/surveys\/\d+$/], ['/requests', /\/submissions\/\d+$/]]
    const opened: string[] = []
    for (const [list, detail] of cases) {
      await page.goto(list)
      await settle(page)
      const href = await page.evaluate((re) => [...document.querySelectorAll('a[href]')].map((a) => a.getAttribute('href')!).find((h) => new RegExp(re).test(h)), detail.source)
      if (!href) {
        // Tablo satırı tıklanarak açılan listeler (bağlantı yerine onClick); kayıt yoksa (boş liste) geç
        const rows = page.locator('tbody tr')
        if ((await rows.count()) === 0) { opened.push(`${list}: boş`); continue }
        await rows.first().click()
      } else {
        await page.goto(href)
      }
      await expect(page).toHaveURL(detail)
      await settle(page)
      await expect(page.getByText('Veriler yüklenemedi')).toHaveCount(0)
      opened.push(list)
    }
    expect(opened.filter((o) => !o.endsWith('boş')).length, opened.join(', ')).toBeGreaterThanOrEqual(4)
    expect(problems, problems.join('\n')).toEqual([])
  })
})

test.describe('Mobil (375px)', () => {
  test.use({ viewport: { width: 375, height: 812 }, isMobile: true, hasTouch: true })
  test('sayfalar telefonda yatay taşmaz ve menü açılır', async ({ page }) => {
    test.setTimeout(240_000)
    await sessionFor(page, users.admin)
    const problems = watchHealth(page)
    const report: string[] = []
    for (const path of routes) {
      await page.goto(path)
      await settle(page)
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
      if (overflow > 1) report.push(`${path}: yatay taşma ${overflow}px`)
    }
    await page.goto('/')
    await page.getByRole('button', { name: 'Menüyü aç' }).click()
    await expect(page.getByRole('navigation', { name: 'Ana menü' })).toBeVisible()
    const all = [...report, ...problems]
    expect(all, all.join(String.fromCharCode(10))).toEqual([])
  })
})
