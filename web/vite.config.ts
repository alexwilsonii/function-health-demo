import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// In dev the SPA runs on :5173 and proxies /api to the API on :5000 (same-origin to the browser,
// so the auth cookie is first-party and there's no CORS). In production the API serves the built
// SPA from wwwroot, so this proxy is dev-only.
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
