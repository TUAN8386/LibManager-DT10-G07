// ============================================================
// Người thực hiện: Lê Huy Hoàng – MSSV 2410900038
// Công việc: W5-03 – màn hình React tra cứu bản sao tức thời (tuần 5, nhánh feature/hoang-tra-cuu-react)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
// Gọi Web API cùng ứng dụng: dùng chung cookie xác thực, gửi token chống CSRF cho request ghi.
function getCookie(name) {
  const m = document.cookie.match('(?:^|; )' + name + '=([^;]*)');
  return m ? decodeURIComponent(m[1]) : null;
}

export async function api(url, { method = 'GET', body, signal } = {}) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (method !== 'GET') headers['RequestVerificationToken'] = getCookie('XSRF-TOKEN') || '';
  let res;
  try {
    res = await fetch(url, { method, headers, body: body !== undefined ? JSON.stringify(body) : undefined, signal, credentials: 'same-origin' });
  } catch (e) {
    if (e.name === 'AbortError') throw e;
    return { ok: false, status: 0, message: 'Không kết nối được máy chủ.' };
  }
  let data = null;
  try { data = await res.json(); } catch { /* không có JSON */ }
  if (res.status === 401) return { ok: false, status: 401, message: 'Phiên đăng nhập đã hết hạn. Vui lòng tải lại trang và đăng nhập.' };
  if (res.status === 403) return { ok: false, status: 403, message: 'Bạn không có quyền thực hiện thao tác này.' };
  if (!res.ok) return { ok: false, status: res.status, message: data?.title || data?.message || `Lỗi ${res.status}`, errorCode: data?.errorCode };
  return { ok: true, status: res.status, data: data && data.data !== undefined && data.success !== undefined ? data.data : data, message: data?.message };
}
