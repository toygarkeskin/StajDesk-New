import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'

const api = process.env.VITE_PROXY_TARGET ?? 'http://localhost:5100'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  server: {
    port: 5173,
    // Geliştirmede API aynı köken üzerinden: refresh çerezi (SameSite=Strict) ve CORS sorunsuz çalışır.
    proxy: { '/api': { target: api, changeOrigin: true }, '/swagger': api, '/openapi': api, '/health': api, '/hubs': { target: api, ws: true } },
  },
  build: {
    chunkSizeWarningLimit: 1200,
    rollupOptions: {
      output: {
        manualChunks: (id: string) =>
          !id.includes('node_modules') ? undefined
            : /recharts|d3-|victory/.test(id) ? 'charts'
            : /lucide-react/.test(id) ? 'icons'
            : /react-markdown|remark|micromark|mdast|unified|hast/.test(id) ? 'markdown'
            : /react|scheduler|@tanstack|axios|zustand|zod|date-fns|sonner/.test(id) ? 'vendor'
            : undefined,
      },
    },
  },
})
