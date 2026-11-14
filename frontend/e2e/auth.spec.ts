import { expect, test } from '@playwright/test'
import { demoPassword, login, MAILPIT, users, waitForLoginCode, watchHealth } from './helpers'

test.describe('Genel site ve giriş', () => {
  test('tanıtım sitesi açılır, çalışan bilgisi göstermez, portala yönlendirir', async ({ page }) => {
    const problems = watchHealth(page)
    await page.goto('/')
    await expect(page).toHaveURL(/\/welcome$/)
    // Giriş yapmamış ziyaretçiye çalışan e-postası / kişisel bilgi gösterilmez
    await expect(page.locator('body')).not.toContainText('@devoplus.local')
    // Piyasa verisi: gerçek değer ya da açık hata mesajı (uydurma değer yok)
    await expect(page.getByText(/ABD Doları|alınamıyor/).first()).toBeVisible()
    await page.getByRole('link', { name: /Portala Git/i }).first().click()
    await expect(page).toHaveURL(/\/login$/)
    expect(problems, problems.join('\n')).toEqual([])
  })

  test('oturum açmadan korumalı sayfa açılmaz', async ({ page }) => {
    await page.goto('/tickets')
    await expect(page).toHaveURL(/\/login$/)
  })

  test('hatalı parola anlaşılır hata verir; 5 hatadan sonra hesap geçici olarak kilitlenir (429)', async ({ page }) => {
    const email = `olmayan.${Date.now()}@devoplus.local`
    await page.goto('/login')
    await page.getByLabel('E-posta').fill(email)
    for (let i = 1; i <= 5; i++) {
      await page.getByLabel('Parola', { exact: true }).fill('YanlisParola1')
      await page.getByRole('button', { name: 'Giriş yap' }).click()
      await expect(page.getByRole('alert')).toContainText('hatalıdır')
    }
    await page.getByRole('button', { name: 'Giriş yap' }).click()
    await expect(page.getByRole('alert')).toContainText('Çok fazla deneme')
  })

  test('doğrulama kodu: yanlış kod kalan hakkı gösterir, doğru kod giriş yapar, çıkışta oturum kapanır', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('E-posta').fill(users.employee)
    await page.getByLabel('Parola', { exact: true }).fill(demoPassword())
    const since = Date.now()
    await page.getByRole('button', { name: 'Giriş yap' }).click()
    await expect(page.getByText('E-postanızı kontrol edin')).toBeVisible()
    await expect(page.getByText(/ca\*+@devoplus\.local/)).toBeVisible() // e-posta maskeli gösterilir
    await expect(page.getByRole('button', { name: /Yeniden gönder \(\d+ sn\)/ })).toBeDisabled() // bekleme süresi
    await expect(page.getByRole('link', { name: /Mailpit/ })).toHaveAttribute('href', MAILPIT)

    const code = await waitForLoginCode(page.request, users.employee, since)
    const wrong = code === '000000' ? '111111' : '000000'
    await page.getByLabel('1. hane').fill(wrong)
    await expect(page.getByRole('alert')).toContainText('Kalan deneme hakkı: 4')

    await page.getByLabel('1. hane').fill(code)
    await expect(page).toHaveURL(/\/$/)
    await expect(page.getByRole('navigation', { name: 'Ana menü' })).toBeVisible()

    // Sayfa yenilense de oturum (httpOnly refresh çerezi) korunur
    await page.reload()
    await expect(page.getByRole('navigation', { name: 'Ana menü' })).toBeVisible()

    await page.getByRole('button', { name: /Ahmet Yılmaz/ }).click()
    await page.getByRole('button', { name: 'Çıkış yap' }).click()
    await expect(page).toHaveURL(/\/login$/)
    await page.goto('/tickets')
    await expect(page).toHaveURL(/\/login$/)
  })

  test('ilk girişte parola belirlemeden portala geçilemez', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('E-posta').fill(users.newHire)
    await page.getByLabel('Parola', { exact: true }).fill(demoPassword())
    await page.getByRole('button', { name: 'Giriş yap' }).click()
    const alert = page.getByRole('alert')
    if (await alert.isVisible().catch(() => false)) test.skip(true, 'Bu demo hesabın parolası daha önceki bir çalıştırmada değiştirildi.')
    const since = Date.now() - 3000
    await expect(page.getByText('E-postanızı kontrol edin')).toBeVisible()
    await page.getByLabel('1. hane').fill(await waitForLoginCode(page.request, users.newHire, since))
    await expect(page).toHaveURL(/\/set-password$/)
    // Doğrudan portal adresine gidilse de parola sayfasına geri döner
    await page.goto('/tickets')
    await expect(page).toHaveURL(/\/set-password$/)
  })

  test('kodla giriş yardımcı akışı her rolde çalışır', async ({ page }) => {
    await login(page, users.manager)
    await expect(page.getByRole('navigation', { name: 'Ana menü' })).toBeVisible()
  })
})
