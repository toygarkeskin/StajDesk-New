import { defineConfig } from '@playwright/test'
import base from './playwright.config'

/** README ekran görüntüleri (npm run screenshots). Normal e2e koşusuna dahil değildir. */
export default defineConfig({
  ...base,
  testDir: './e2e/screenshots',
  testMatch: '**/*.shots.ts',
  reporter: 'list',
})
