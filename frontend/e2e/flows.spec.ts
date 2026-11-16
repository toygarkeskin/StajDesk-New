import { expect, test, type Page } from '@playwright/test'
import { login, settle, users, watchHealth } from './helpers'

const stamp = () => new Date().toISOString().slice(11, 19)
/** Yerel tarih (saat dilimi kaymasıyla bir önceki güne düşmesin) */
const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

/** Hafta içi, ileri bir gün (çakışmayı azaltmak için rastgele) */
function futureWeekday(minDays: number, maxDays: number) {
  for (;;) {
    const d = new Date()
    d.setDate(d.getDate() + minDays + Math.floor(Math.random() * (maxDays - minDays)))
    if (d.getDay() !== 0 && d.getDay() !== 6) return iso(d)
  }
}

async function confirmDialog(page: Page, button: string, text?: string) {
  const dialog = page.getByRole('dialog')
  if (text) await dialog.getByRole('textbox').fill(text)
  await dialog.getByRole('button', { name: button, exact: true }).click()
  await expect(dialog).toHaveCount(0)
}

test.describe('İş akışları (uçtan uca)', () => {
  test('destek talebi: oluştur → IT üstlenir, işleme alır, yanıtlar (anlık bildirim) → çözer → çalışan kapatır', async ({ browser }) => {
    test.setTimeout(180_000)
    const employee = await (await browser.newContext()).newPage()
    const agent = await (await browser.newContext()).newPage()
    const problems = [...watchHealth(employee)]
    const agentProblems = watchHealth(agent)
    const title = `E2E yazıcı arızası ${stamp()}`

    // 1) Çalışan talep açar
    await login(employee, users.employee)
    await employee.goto('/tickets/new')
    await employee.getByRole('radio').first().click()
    await employee.getByLabel(/^Başlık/).fill(title)
    await employee.getByLabel(/^Açıklama/).fill('Kat yazıcısı her çıktıda kağıt sıkıştırıyor. Uçtan uca test kaydıdır.')
    await employee.getByRole('button', { name: 'Talebi gönder' }).click()
    await expect(employee).toHaveURL(/\/tickets\/\d+$/)
    const ticketUrl = new URL(employee.url()).pathname
    await expect(employee.getByRole('heading', { name: title })).toBeVisible()
    await expect(employee.getByText('Açık').first()).toBeVisible()

    // 2) IT destek üstlenir ve işleme alır
    await login(agent, users.it)
    await agent.goto(ticketUrl)
    await settle(agent)
    await agent.getByRole('button', { name: 'Üzerime al' }).click()
    await expect(agent.getByRole('button', { name: 'Üzerime al' })).toHaveCount(0)
    await agent.getByRole('button', { name: 'İşleme al' }).click()
    await expect(agent.getByText('İşlemde').first()).toBeVisible()

    // 3) IT yanıt yazar → çalışanın açık sayfasında anlık bildirim ve yanıt (sayfa yenilemeden)
    const reply = `Toner ve silindir değiştirildi (${stamp()}).`
    await agent.getByLabel('Yanıt').fill(reply)
    await agent.getByRole('button', { name: 'Gönder' }).click()
    await expect(agent.getByText(reply)).toBeVisible()
    await expect(employee.getByText('Talebinize yanıt geldi')).toBeVisible({ timeout: 20_000 })
    await expect(employee.getByText(reply)).toBeVisible({ timeout: 20_000 })

    // 4) IT çözer, çalışan onaylayıp kapatır
    await expect(agent.getByLabel('Atanan personel')).toHaveValue(/.+/) // atanan kişi kutuda görünür ("Atanmamış" değil)
    await agent.getByRole('button', { name: 'Çözüldü' }).click()
    await confirmDialog(agent, 'Çözüldü', 'Toner ve silindir değişimiyle giderildi.')
    await expect(agent.getByText('Çözüldü').first()).toBeVisible()
    await employee.reload()
    await employee.getByRole('button', { name: 'Onayla ve kapat' }).click()
    await expect(employee.getByText('Kapandı').first()).toBeVisible()
    await expect(employee.getByText('Bu talep kapatılmıştır')).toBeVisible()

    expect([...problems, ...agentProblems]).toEqual([])
  })

  test('izin: çalışan başvurur → yönetici onaylar → İK onaylar → onaylandı', async ({ browser }) => {
    test.setTimeout(180_000)
    const employee = await (await browser.newContext()).newPage()
    await login(employee, users.employee)
    await employee.goto('/apps/leave')
    await settle(employee)

    // Mazeret izni, ileri bir hafta içi gün (önceki çalıştırmalarla çakışırsa başka gün dene)
    for (let attempt = 0; ; attempt++) {
      if (attempt > 0) { await employee.reload(); await settle(employee) }
      const day = futureWeekday(40, 330)
      await employee.getByLabel(/^İzin türü/).selectOption('Mazeret İzni')
      await employee.getByLabel(/^Başlangıç/).fill(day)
      await employee.getByLabel(/^Bitiş/).fill(day)
      await employee.getByRole('button', { name: 'Başvuruyu gönder' }).click()
      const done = await Promise.race([
        employee.waitForURL(/\/submissions\/\d+$/, { timeout: 15_000 }).then(() => true),
        // Çakışan izin veya resmî tatile denk gelen gün: sunucu doğru şekilde reddeder, başka gün denenir
        employee.getByText(/başka bir izin talebiniz|iş günü bulunmuyor/).first().waitFor({ timeout: 15_000 }).then(() => false),
      ])
      if (done) break
      if (attempt > 4) throw new Error('Boş gün bulunamadı')
    }
    await expect(employee.getByText('Onay bekliyor').first()).toBeVisible()
    await expect(employee.getByText('1 gün')).toBeVisible() // hesaplanan iş günü
    const url = new URL(employee.url()).pathname
    const number = (await employee.getByText(/^FRM-\d{4}-\d+$/).first().textContent())!.trim()

    // Yönetici onay kuyruğunda görür ve onaylar
    const manager = await (await browser.newContext()).newPage()
    await login(manager, users.manager)
    await manager.goto('/approvals')
    await settle(manager)
    await expect(manager.getByText(number, { exact: true })).toBeVisible() // anlık bildirim de aynı numarayı gösterebilir
    await manager.goto(url)
    await manager.getByRole('button', { name: 'Onayla' }).click()
    await confirmDialog(manager, 'Onayla', 'Uygundur (E2E).')
    await expect(manager.getByText('Onaylama yetkiniz').or(manager.getByText('İK Onayı')).first()).toBeVisible()

    // İK onaylar → başvuru onaylanır
    const hr = await (await browser.newContext()).newPage()
    await login(hr, users.hr)
    await hr.goto(url)
    await hr.getByRole('button', { name: 'Onayla' }).click()
    await confirmDialog(hr, 'Onayla')
    await expect(hr.getByText('Onaylandı').first()).toBeVisible()

    await employee.reload()
    await expect(employee.getByText('Onaylandı').first()).toBeVisible()
  })

  test('ciro: nakit + kart tutmazsa engellenir, doğru bildirim özet ve panele yansır', async ({ page }) => {
    const problems = watchHealth(page)
    await login(page, users.employee)
    await page.goto('/apps/store-revenue')
    await settle(page)
    await expect(page.getByText('Onaylı ciro (30 gün)')).toBeVisible()
    const store = page.getByLabel(/^Mağaza/)
    const options = await store.locator('option:not([disabled])').allTextContents()
    expect(options.length).toBeGreaterThan(10)

    for (let attempt = 0; ; attempt++) {
      const d = new Date()
      d.setDate(d.getDate() - 1 - Math.floor(Math.random() * 40))
      await store.selectOption(options[Math.floor(Math.random() * options.length)])
      await page.getByLabel(/^Tarih/).fill(iso(d))
      await page.getByLabel(/^Nakit/).fill('100')
      await page.getByLabel(/^Kredi kartı/).fill('200')
      await page.getByLabel(/^Toplam ciro/).fill('500')
      await expect(page.getByRole('status')).toContainText('toplam ciro ile eşleşmiyor') // istemci önizlemesi
      await page.getByRole('button', { name: 'Başvuruyu gönder' }).click()
      await expect(page.getByText(/nakit ve kart toplamına/)).toBeVisible() // sunucu kuralı
      await page.getByLabel(/^Toplam ciro/).fill('300')
      await page.getByLabel(/^Fiş adedi/).fill('3')
      await expect(page.getByRole('status')).toContainText('Ortalama sepet')
      await page.getByRole('button', { name: 'Başvuruyu gönder' }).click()
      const ok = await Promise.race([
        page.waitForURL(/\/submissions\/\d+$/, { timeout: 15_000 }).then(() => true),
        page.getByText(/zaten var/).waitFor({ timeout: 15_000 }).then(() => false),
      ])
      if (ok) break
      if (attempt > 4) throw new Error('Mükerrer olmayan mağaza/gün bulunamadı')
      await page.goto('/apps/store-revenue')
      await settle(page)
    }
    await expect(page.getByText(/₺300,00 · 3 fiş · ort\. sepet ₺100,00/).first()).toBeVisible()
    expect(problems).toEqual([])
  })

  test('vardiya: takas bilgisi ve ekip listesi görünür, aynı vardiya seçilemez', async ({ page }) => {
    await login(page, users.employee)
    await page.goto('/apps/shift')
    await settle(page)
    await expect(page.getByText('onun onayına')).toBeVisible()
    await expect(page.getByText('Ekip vardiya değişiklikleri')).toBeVisible()
    await page.getByLabel(/^Mağaza \/ birim/).fill('Muhasebe')
    await page.getByLabel(/^Tarih/).fill(futureWeekday(3, 50))
    await page.getByLabel(/^Mevcut vardiya/).selectOption({ index: 1 })
    await page.getByLabel(/^İstenen vardiya/).selectOption({ index: 1 })
    await page.getByRole('button', { name: 'Başvuruyu gönder' }).click()
    await expect(page.getByText('İstenen vardiya mevcut vardiyanızdan farklı olmalıdır.')).toBeVisible()
  })

  test('form tasarımcısı: onaylayıcı kişi isimle aranıp seçilir (GUID yok), Escape yalnızca üst katmanı kapatır', async ({ page }) => {
    await login(page, users.admin)
    await page.goto('/admin/forms')
    await settle(page)
    await page.getByRole('button', { name: 'Yeni form' }).click()
    const drawer = page.getByRole('dialog')
    await drawer.getByRole('tab', { name: /Onay adımları/ }).click()
    await drawer.getByLabel('Onaylayıcı').first().selectOption('User')
    await expect(drawer.getByPlaceholder(/GUID/)).toHaveCount(0)
    await drawer.getByPlaceholder('Onaylayıcı ara…').fill('Ahmet')
    await drawer.getByRole('button', { name: /Ahmet Yılmaz/ }).first().click()
    await expect(drawer.getByText('Ahmet Yılmaz')).toBeVisible()
    await drawer.getByLabel('Onaylayıcı').first().selectOption('FieldUser')
    await expect(drawer.getByLabel('Kişi alanı')).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog')).toHaveCount(0)
    await expect(page.locator('body')).not.toHaveCSS('overflow', 'hidden') // gövde kilidi kalkar
  })

  test('kaydırma: sayfa değişince en üste döner, modal açıkken arka plan kaymaz', async ({ page }) => {
    await login(page, users.admin)
    await page.goto('/tickets')
    await settle(page)
    await page.mouse.wheel(0, 3000)
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(100)
    await page.getByRole('navigation', { name: 'Ana menü' }).getByRole('link', { name: 'Bilgi Bankası' }).first().click()
    await expect(page).toHaveURL(/\/kb/)
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0)

    const width = await page.evaluate(() => document.querySelector('main')!.getBoundingClientRect().width)
    await page.keyboard.press('Control+k')
    await expect(page.getByRole('dialog', { name: 'Hızlı arama' })).toBeVisible()
    const after = await page.evaluate(() => document.querySelector('main')!.getBoundingClientRect().width)
    expect(after, `ana içerik genişliği ${width} → ${after}`).toBe(width) // kaydırma çubuğu kaybolunca içerik kaymaz
  })
})
