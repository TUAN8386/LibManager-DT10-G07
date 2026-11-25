// ============================================================
// Người thực hiện: Nguyễn Mạnh Tùng – MSSV 2410900086
// Công việc: W5-05 – bảng sách quá hạn bằng React (tuần 5, nhánh feature/tung-qua-han)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
import { createRoot } from 'react-dom/client';
import OverdueApp from './OverdueApp.jsx';

const el = document.getElementById('overdue-root');
if (el) createRoot(el).render(<OverdueApp />);
