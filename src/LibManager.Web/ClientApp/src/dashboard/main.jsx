// ============================================================
// Người thực hiện: Lê Huy Hoàng – MSSV 2410900038
// Công việc: W5-04 – dashboard lưu thông bằng React (tuần 5, nhánh feature/hoang-dashboard)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
import { createRoot } from 'react-dom/client';
import DashboardApp from './DashboardApp.jsx';

const el = document.getElementById('dashboard-root');
if (el) createRoot(el).render(<DashboardApp />);
