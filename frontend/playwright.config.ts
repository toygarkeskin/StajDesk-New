import { defineConfig, devices } from '@playwright/test'

/**
 * Uçtan uca testler çalışan bir StajDesk'e karşı koşar (varsayılan: docker compose → http://localhost:3100).
 *
 *   docker compose up -d --build
 *   cd frontend && npm run e2e
 *
 * Ortam değişkenleri:
 *   E2E_BASE_URL   Portal adresi (varsayılan http://localhost:3100)
 *   E2E_MAILPIT    Mailpit adresi; giriş doğrulama kodu buradan okunur (varsayılan http://localhost:8126)
 *   E2E_PASSWORD   Demo hesap parolası (varsayılan: ../.env içindeki SEED_DEFAULT_PASSWORD)
 *   E2E_BROWSER    msedge | chrome | chromium (varsayılan msedge: Windows'ta kurulu Edge, ek indirme gerekmez)
 */
const channel = process.env.E2E_BROWSER ?? 'msedge'

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1, // testler aynı demo veritabanını paylaşır
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:3100',
    locale: 'tr-TR',
    timezoneId: 'Europe/Istanbul',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    ...devices['Desktop Chrome'],
    channel: channel === 'chromium' ? undefined : channel,
  },
})
