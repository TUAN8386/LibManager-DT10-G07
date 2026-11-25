// ============================================================
// Người thực hiện: Lê Huy Hoàng – MSSV 2410900038
// Công việc: W5-03 – màn hình React tra cứu bản sao tức thời (tuần 5, nhánh feature/hoang-tra-cuu-react)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
export const vnd = (v) => (Number(v) || 0).toLocaleString('vi-VN') + ' đ';
export const date = (s) => (s ? new Date(s).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' }) : '');
export const dateTime = (s) => (s ? new Date(s).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '');
export const isoDate = (d) => {
  const z = new Date(d.getTime() - d.getTimezoneOffset() * 60000);
  return z.toISOString().slice(0, 10);
};
export const statusClass = (s) => ({
  Available: 'text-bg-success', OnLoan: 'text-bg-primary', OnHold: 'text-bg-warning',
  Damaged: 'text-bg-danger', Lost: 'text-bg-dark', Withdrawn: 'text-bg-secondary',
}[s] || 'text-bg-secondary');
