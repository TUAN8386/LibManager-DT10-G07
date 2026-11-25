// ============================================================
// Người thực hiện: Bùi Văn Long – MSSV 2410900047
// Công việc: W5-02 – khởi tạo ClientApp React, hook useBarcodeScanner (tuần 5, nhánh feature/long-react-init)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
// Build dự phòng bằng esbuild (dùng khi chưa cài Vite). Kết quả giống Vite: ../wwwroot/app/{lookup,dashboard,overdue}.js
// Cách chạy: node build-esbuild.mjs   (cần cài esbuild, react, react-dom: npm i -D esbuild && npm i react react-dom)
import { build } from 'esbuild';
import { rmSync } from 'node:fs';

const outdir = new URL('../wwwroot/app', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');
rmSync(outdir, { recursive: true, force: true });
await build({
  entryPoints: { lookup: 'src/lookup/main.jsx', dashboard: 'src/dashboard/main.jsx', overdue: 'src/overdue/main.jsx' },
  bundle: true,
  splitting: true,
  format: 'esm',
  outdir,
  chunkNames: 'chunks/[name]-[hash]',
  jsx: 'automatic',
  minify: true,
  sourcemap: false,
  target: ['es2020'],
  define: { 'process.env.NODE_ENV': '"production"' },
  nodePaths: process.env.NODE_PATH ? process.env.NODE_PATH.split(':') : [],
  logLevel: 'info',
});
