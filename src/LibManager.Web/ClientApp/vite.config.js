// ============================================================
// Người thực hiện: Bùi Văn Long – MSSV 2410900047
// Công việc: W5-02 – khởi tạo ClientApp React, hook useBarcodeScanner (tuần 5, nhánh feature/long-react-init)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { resolve } from 'node:path';

// Build nhiều entry, xuất tên file cố định để trang Razor nhúng: ~/app/lookup.js, ~/app/dashboard.js, ~/app/overdue.js
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: resolve(__dirname, '../wwwroot/app'),
    emptyOutDir: true,
    rollupOptions: {
      input: {
        lookup: resolve(__dirname, 'src/lookup/main.jsx'),
        dashboard: resolve(__dirname, 'src/dashboard/main.jsx'),
        overdue: resolve(__dirname, 'src/overdue/main.jsx'),
      },
      output: {
        entryFileNames: '[name].js',
        chunkFileNames: 'chunks/[name]-[hash].js',
        assetFileNames: '[name][extname]',
      },
    },
  },
});
