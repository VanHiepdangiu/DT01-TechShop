import { defineConfig } from 'vite';

export default defineConfig({
  base: './',
  server: {
    proxy: {
      '/api': {
        target: process.env.TECHSHOP_API_ORIGIN || 'http://localhost:5000',
        changeOrigin: true
      }
    }
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true
  }
});
