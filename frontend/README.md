# StajDesk arayüzü

React 19 + TypeScript + Vite tek sayfa uygulaması. Üretimde nginx imajı içinde sunulur ve `/api`,
`/hubs` isteklerini API'ye iletir ([nginx.conf](nginx.conf)).

```bash
npm install
npm run dev          # http://localhost:5173 (/api → http://localhost:5100)
npm test             # Vitest
npm run lint         # oxlint
npm run build        # tip kontrolü + üretim derlemesi
npm run e2e          # Playwright, çalışan sisteme karşı (varsayılan http://localhost:3100)
npm run screenshots  # README görüntüleri → ../docs/images
```

| Klasör | İçerik |
|---|---|
| `src/pages/` | Sayfalar, alana göre: `tickets/`, `workflow/`, `admin/`, `comms/`, `inventory/`, … |
| `src/components/` | Ortak arayüz parçaları, düzen, form alanları, onay penceresi |
| `src/lib/` | API istemcisi, TanStack Query sorguları, form şeması (Zod), SignalR bağlantısı |
| `src/store/` | Oturum ve arayüz durumu (Zustand) |
| `e2e/` | Uçtan uca testler ve ekran görüntüsü betiği |

Ana belgeler: [../README.md](../README.md) · [../docs/](../docs/README.md)
