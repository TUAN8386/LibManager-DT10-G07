// ============================================================
// Người thực hiện: Bùi Văn Long – MSSV 2410900047
// Công việc: W5-02 – khởi tạo ClientApp React, hook useBarcodeScanner (tuần 5, nhánh feature/long-react-init)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
import { useEffect, useRef } from 'react';

/**
 * Hook React bọc thư viện dùng chung window.BarcodeScanner (wwwroot/js/barcode-scanner.js).
 * onScan(code, meta) – lượt quét từ máy quét; onTyped(text) – người dùng gõ tay rồi Enter.
 */
export function useBarcodeScanner(inputRef, { onScan, onTyped, maxInterKeyMs = 50, minLength = 6 }) {
  const handlers = useRef({ onScan, onTyped });
  handlers.current = { onScan, onTyped };

  useEffect(() => {
    const Scanner = window.BarcodeScanner;
    if (!Scanner || !inputRef.current) return undefined;
    const sc = new Scanner({
      input: inputRef.current,
      maxInterKeyMs,
      minLength,
      onScan: (code, meta) => handlers.current.onScan?.(code, meta),
      onTyped: (text) => handlers.current.onTyped?.(text),
    }).start();
    return () => sc.stop();
  }, [inputRef, maxInterKeyMs, minLength]);
}

export const beep = (type) => window.BarcodeScanner?.beep(type);
export const classify = (code) => window.BarcodeScanner?.classify(code) ?? { type: 'unknown', code, valid: false };
