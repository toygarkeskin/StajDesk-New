import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { ThemedToaster } from './components/ThemedToaster'
import App from './App'
import { ApiError } from './lib/api'
import { ConfirmProvider } from './components/ui'
// Yazı tipleri uygulamayla birlikte sunulur: Google Fonts'a istek gitmez (çalışan IP'si üçüncü tarafa iletilmez, CSP sıkı kalır)
import '@fontsource/inter/400.css'
import '@fontsource/inter/500.css'
import '@fontsource/inter/600.css'
import '@fontsource/inter/700.css'
import '@fontsource/manrope/600.css'
import '@fontsource/manrope/700.css'
import '@fontsource/manrope/800.css'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 15_000,
      refetchOnWindowFocus: false,
      retry: (count, err) => !(err instanceof ApiError && [400, 401, 403, 404].includes(err.status)) && count < 2,
    },
  },
})


createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <ConfirmProvider>
          <App />
          <ThemedToaster />
        </ConfirmProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)
