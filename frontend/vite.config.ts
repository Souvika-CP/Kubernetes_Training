import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/graphql': {
        target: 'http://taskflow.local:8080',
        changeOrigin: true,
        ws: true,
      },
      '/api': {
        target: 'http://taskflow.local:8080',
        changeOrigin: true,
      },
      '/auth': {
        target: 'http://taskflow.local:8080',
        changeOrigin: true,
      },
      '/health': {
        target: 'http://taskflow.local:8080',
        changeOrigin: true,
      },
    },
  },
})
