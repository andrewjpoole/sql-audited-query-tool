import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Support Aspire service discovery env vars, fallback to launchSettings port
const apiUrl = process.env.services__api__https__0
  || process.env.services__api__http__0
  || 'http://localhost:5001';

console.log(`[vite] API proxy target: ${apiUrl}`);
if (apiUrl === 'http://localhost:5001') {
  console.log('[vite] ⚠️  Using fallback URL. If running via Aspire, services__api__http__0 env var may not be set.');
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: apiUrl,
        changeOrigin: true,
        secure: false,
      },
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
})
