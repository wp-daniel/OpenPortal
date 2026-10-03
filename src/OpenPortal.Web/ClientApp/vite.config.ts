import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'
import { defineConfig, loadEnv } from 'vite'

// The dev server is proxied by Microsoft.AspNetCore.SpaProxy, so the browser always talks to the ASP.NET
// origin and the session cookie stays first-party. Vite therefore never needs a proxy of its own in the
// normal `npm run dev` flow; the one below is for running the client standalone against an already-started
// API, which is the only situation where the two origins differ.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_')

  const backend = env.VITE_BACKEND_URL ?? 'https://localhost:7001'

  return {
    plugins: [react(), tailwindcss()],
    resolve: { alias: { '@': path.resolve(import.meta.dirname, './src') } },
    server: {
      port: 5173,
      strictPort: true,
      proxy: Object.fromEntries(
        ['/api', '/health', '/.well-known'].map((path) => [
          path,
          {
            target: backend,
            changeOrigin: false,
          },
        ]),
      ),
    },
    build: {
      // Kept out of wwwroot on purpose: the build empties its own outDir, and wwwroot also holds the
      // server-rendered error.html, which must survive every client build. The csproj copies this into
      // wwwroot afterwards.
      outDir: 'dist',
      emptyOutDir: true,
      sourcemap: mode !== 'production',
    },
  }
})