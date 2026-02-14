import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
    plugins: [react()],
    publicDir: '../public',
    server: {
        port: 3000,
        proxy: {
            // Proxy API requests to the ASP.NET backend in development
            '/search': 'http://localhost:5110',
            '/index': 'http://localhost:5110',
            '/health': 'http://localhost:5110',
        },
    },
});
