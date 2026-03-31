import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [tailwindcss(), vue()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5118',
        changeOrigin: true,
      },
    },
  },
})
