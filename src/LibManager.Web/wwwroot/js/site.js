// ============================================================
// Người thực hiện: Bùi Văn Long – MSSV 2410900047
// Công việc: W3-01 – layout Bootstrap, partial dùng chung, CSS (tuần 3, nhánh feature/long-layout)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
/* LibManager – tiện ích dùng chung cho các trang Razor */
(function () {
  'use strict';

  /** Đọc cookie (dùng cho XSRF-TOKEN). */
  function getCookie(name) {
    var m = document.cookie.match('(?:^|; )' + name.replace(/[.$?*|{}()[\]\\/+^]/g, '\\$&') + '=([^;]*)');
    return m ? decodeURIComponent(m[1]) : null;
  }

  // Gửi token chống CSRF cho mọi request AJAX ghi dữ liệu.
  if (window.jQuery) {
    jQuery.ajaxSetup({
      beforeSend: function (xhr, settings) {
        if (!/^(GET|HEAD|OPTIONS)$/i.test(settings.type)) {
          var t = getCookie('XSRF-TOKEN');
          if (t) xhr.setRequestHeader('RequestVerificationToken', t);
        }
      }
    });
  }

  /**
   * Gọi Web API, trả về { ok, status, data, message, errorCode }.
   * Lỗi theo chuẩn ProblemDetails được chuẩn hóa về message/errorCode.
   */
  window.api = async function (url, options) {
    options = options || {};
    var method = (options.method || 'GET').toUpperCase();
    var headers = Object.assign({ 'Accept': 'application/json' }, options.headers || {});
    if (options.body !== undefined && typeof options.body !== 'string') {
      headers['Content-Type'] = 'application/json';
      options.body = JSON.stringify(options.body);
    }
    if (method !== 'GET') headers['RequestVerificationToken'] = getCookie('XSRF-TOKEN') || '';
    var res;
    try {
      res = await fetch(url, Object.assign({}, options, { method: method, headers: headers, credentials: 'same-origin' }));
    } catch (e) {
      return { ok: false, status: 0, message: 'Không kết nối được máy chủ.', errorCode: 'NETWORK' };
    }
    var body = null;
    try { body = await res.json(); } catch (e) { /* không có nội dung JSON */ }
    if (res.status === 401) return { ok: false, status: 401, message: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.', errorCode: 'UNAUTHORIZED' };
    if (res.status === 403) return { ok: false, status: 403, message: (body && body.title) || 'Bạn không có quyền thực hiện thao tác này.', errorCode: 'FORBIDDEN' };
    if (!res.ok) return { ok: false, status: res.status, message: (body && (body.title || body.message)) || ('Lỗi ' + res.status), errorCode: body && body.errorCode, data: body };
    return { ok: true, status: res.status, data: body && body.data !== undefined ? body.data : body, message: body && body.message };
  };

  /** Hiển thị toast Bootstrap. type: success | danger | warning | info */
  window.toast = function (message, type) {
    var host = document.getElementById('toastHost');
    if (!host || !window.bootstrap) { console.log(message); return; }
    var el = document.createElement('div');
    el.className = 'toast align-items-center text-bg-' + (type || 'success') + ' border-0';
    el.setAttribute('role', 'status');
    el.innerHTML = '<div class="d-flex"><div class="toast-body"></div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button></div>';
    el.querySelector('.toast-body').textContent = message;
    host.appendChild(el);
    var t = new bootstrap.Toast(el, { delay: 3500 });
    el.addEventListener('hidden.bs.toast', function () { el.remove(); });
    t.show();
  };

  /** Định dạng tiền/ngày kiểu Việt Nam. */
  window.fmt = {
    vnd: function (v) { return (Number(v) || 0).toLocaleString('vi-VN') + ' đ'; },
    date: function (s) { if (!s) return ''; var d = new Date(s); return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' }); },
    dateTime: function (s) { if (!s) return ''; var d = new Date(s); return d.toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }); }
  };

  /** Escape HTML khi chèn chuỗi từ máy chủ vào DOM (chống XSS). */
  window.esc = function (s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  };

  // Hộp xác nhận cho form có data-confirm
  document.addEventListener('submit', function (e) {
    var f = e.target;
    if (f && f.dataset && f.dataset.confirm && !window.confirm(f.dataset.confirm)) e.preventDefault();
  }, true);
})();
