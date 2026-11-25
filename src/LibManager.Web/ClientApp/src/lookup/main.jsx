// ============================================================
// Người thực hiện: Lê Huy Hoàng – MSSV 2410900038
// Công việc: W5-03 – màn hình React tra cứu bản sao tức thời (tuần 5, nhánh feature/hoang-tra-cuu-react)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
import { createRoot } from 'react-dom/client';
import LookupApp from './LookupApp.jsx';

const el = document.getElementById('lookup-root');
if (el) {
  createRoot(el).render(
    <LookupApp
      initialCode={el.dataset.initial || ''}
      maxInterKeyMs={Number(el.dataset.maxInterKeyMs) || 50}
      minLength={Number(el.dataset.minLength) || 6}
    />,
  );
}
