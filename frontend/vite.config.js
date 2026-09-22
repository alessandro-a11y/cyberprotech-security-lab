import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Base da API configurável via .env (VITE_API_BASE_URL).
// Padrão: backend local em http://localhost:5000.
export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: 5173,
  },
  preview: {
    host: true,
    port: 5173,
  },
});
