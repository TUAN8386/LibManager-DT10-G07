// ============================================================
// Người thực hiện: Bùi Văn Long – MSSV 2410900047
// Công việc: W3-07 – barcode-scanner.js nhận diện máy quét giả lập bàn phím (tuần 3, nhánh feature/long-may-quet)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
/*!
 * barcode-scanner.js – Nhận diện máy quét mã vạch dạng giả lập bàn phím (keyboard wedge)
 * LibManager · DT10 · Nhóm 07
 *
 * Máy quét "gõ" chuỗi mã rất nhanh (thường 5–30 ms/ký tự) rồi gửi Enter/Tab.
 * Người gõ tay chậm hơn nhiều (thường > 80 ms/ký tự). Thư viện dựa vào tốc độ giữa các phím để:
 *   - phân biệt lượt quét với gõ tay (FR-7.2),
 *   - nhận mã ở mọi nơi trên trang kể cả khi ô quét không có focus (FR-7.1),
 *   - chặn Enter/Tab của máy quét làm submit biểu mẫu hoặc nhảy focus,
 *   - trả lại nguyên giá trị nếu máy quét lỡ "gõ" vào một ô nhập khác.
 *
 * Cách dùng:
 *   const sc = new BarcodeScanner({ input: '#scanInput', onScan: (code, meta) => ..., onTyped: (text) => ... });
 *   sc.start();
 *   BarcodeScanner.simulate('BS0000000018');   // giả lập một lượt quét (dùng khi không có thiết bị)
 */
(function (global) {
  'use strict';

  var DEFAULTS = {
    input: null,               // ô quét chính (selector hoặc element) – nhận gõ tay
    maxInterKeyMs: 50,         // khoảng cách trung bình tối đa giữa 2 phím để coi là máy quét
    minLength: 6,              // độ dài tối thiểu của một mã quét
    endKeys: ['Enter', 'Tab'], // ký tự kết thúc do máy quét gửi
    refocus: true,             // tự đưa focus về ô quét sau mỗi lượt
    ignoreSelector: '[data-scanner-ignore]', // vùng không bắt mã (vd. ô ghi chú dài)
    onScan: function () {},
    onTyped: function () {},
    onError: function () {}
  };

  function resolve(el) {
    if (!el) return null;
    return typeof el === 'string' ? document.querySelector(el) : el;
  }

  function isEditable(el) {
    if (!el) return false;
    var tag = (el.tagName || '').toLowerCase();
    if (tag === 'textarea') return true;
    if (tag === 'input') {
      var t = (el.type || 'text').toLowerCase();
      return ['text', 'search', 'email', 'number', 'tel', 'url', 'password'].indexOf(t) >= 0;
    }
    return !!el.isContentEditable;
  }

  function BarcodeScanner(options) {
    this.opt = Object.assign({}, DEFAULTS, options || {});
    this.input = resolve(this.opt.input);
    this._reset();
    this._onKeyDown = this._onKeyDown.bind(this);
    this.running = false;
  }

  BarcodeScanner.prototype._reset = function () {
    this.buffer = '';
    this.times = [];
    this.startEl = null;
    this.startValue = null;
  };

  BarcodeScanner.prototype.start = function () {
    if (this.running) return this;
    document.addEventListener('keydown', this._onKeyDown, true); // capture: nhận trước các handler khác
    this.running = true;
    if (this.opt.refocus) this.focus();
    return this;
  };

  BarcodeScanner.prototype.stop = function () {
    document.removeEventListener('keydown', this._onKeyDown, true);
    this.running = false;
    this._reset();
    return this;
  };

  BarcodeScanner.prototype.focus = function () {
    if (this.input && document.activeElement !== this.input) {
      try { this.input.focus({ preventScroll: true }); } catch (e) { this.input.focus(); }
    }
  };

  /** Thống kê tốc độ của chuỗi phím hiện tại. */
  BarcodeScanner.prototype._stats = function () {
    var gaps = [];
    for (var i = 1; i < this.times.length; i++) gaps.push(this.times[i] - this.times[i - 1]);
    var total = gaps.reduce(function (a, b) { return a + b; }, 0);
    return {
      length: this.buffer.length,
      avgGap: gaps.length ? total / gaps.length : Infinity,
      maxGap: gaps.length ? Math.max.apply(null, gaps) : Infinity,
      durationMs: this.times.length ? Math.round(this.times[this.times.length - 1] - this.times[0]) : 0
    };
  };

  BarcodeScanner.prototype._isScan = function (s) {
    var t = this.opt.maxInterKeyMs;
    // Trung bình nhanh và không có quãng dừng dài (cho phép trễ lẻ tẻ do hệ điều hành/USB).
    return s.length >= this.opt.minLength && s.avgGap <= t && s.maxGap <= t * 3;
  };

  BarcodeScanner.prototype._onKeyDown = function (e) {
    if (e.ctrlKey || e.altKey || e.metaKey) return;
    var target = e.target;
    if (target && target.closest && target.closest(this.opt.ignoreSelector)) return;

    var now = e.timeStamp || performance.now();
    var key = e.key;

    // ---- Ký tự kết thúc
    if (this.opt.endKeys.indexOf(key) >= 0) {
      var stats = this._stats();
      if (this._isScan(stats)) {
        e.preventDefault();                      // không submit form / không nhảy focus
        e.stopPropagation();
        var code = this.buffer.trim().toUpperCase();
        // Nếu máy quét lỡ gõ vào một ô nhập khác ô quét: khôi phục giá trị cũ của ô đó.
        if (this.startEl && this.startEl !== this.input && this.startValue !== null) {
          this.startEl.value = this.startValue;
        }
        if (this.input) this.input.value = '';
        this._reset();
        if (this.opt.refocus) this.focus();
        try { this.opt.onScan(code, stats); } catch (err) { console.error(err); }
        return;
      }
      // Gõ tay rồi nhấn Enter trong ô quét → tìm kiếm thủ công.
      if (key === 'Enter' && this.input && target === this.input) {
        e.preventDefault();
        var text = this.input.value.trim();
        this._reset();
        if (text.length) { try { this.opt.onTyped(text); } catch (err) { console.error(err); } }
        return;
      }
      this._reset();
      return;
    }

    // ---- Ký tự in được
    if (key && key.length === 1) {
      var last = this.times.length ? this.times[this.times.length - 1] : null;
      if (last === null || now - last > this.opt.maxInterKeyMs * 3) {
        // Bắt đầu một chuỗi mới: ghi nhớ ô đang focus để có thể khôi phục.
        this._reset();
        this.startEl = isEditable(target) ? target : null;
        this.startValue = this.startEl ? this.startEl.value : null;
      }
      this.buffer += key;
      this.times.push(now);

      // Từ ký tự thứ 3 trở đi, nếu đã rõ là máy quét và focus không nằm ở ô quét/ô nhập:
      // không để ký tự rơi vào nút/link (tránh kích hoạt phím tắt của trình duyệt).
      if (this.times.length >= 3 && !isEditable(target)) {
        var s = this._stats();
        if (s.avgGap <= this.opt.maxInterKeyMs) e.preventDefault();
      }
    }
  };

  // ------------------------------------------------------------------ Tiện ích
  var audioCtx = null;
  /** Âm báo: 'ok' (cao, ngắn) | 'error' (thấp, dài) | 'warn'. */
  BarcodeScanner.beep = function (type) {
    try {
      audioCtx = audioCtx || new (global.AudioContext || global.webkitAudioContext)();
      var o = audioCtx.createOscillator(), g = audioCtx.createGain();
      var cfg = type === 'error' ? [220, 0.35] : type === 'warn' ? [520, 0.18] : [1320, 0.09];
      o.type = type === 'error' ? 'square' : 'sine';
      o.frequency.value = cfg[0];
      g.gain.setValueAtTime(0.12, audioCtx.currentTime);
      g.gain.exponentialRampToValueAtTime(0.0001, audioCtx.currentTime + cfg[1]);
      o.connect(g); g.connect(audioCtx.destination);
      o.start(); o.stop(audioCtx.currentTime + cfg[1]);
    } catch (e) { /* trình duyệt chặn âm thanh – bỏ qua */ }
  };

  /**
   * Giả lập một lượt quét bằng cách phát chuỗi sự kiện keydown tốc độ cao (mặc định 8 ms/ký tự)
   * rồi Enter. Dùng cho demo/kiểm thử khi không có máy quét thật.
   */
  BarcodeScanner.simulate = function (code, opts) {
    opts = Object.assign({ interval: 8, endKey: 'Enter' }, opts || {});
    var target = opts.target || document.activeElement || document.body;
    var chars = String(code).split('').concat([opts.endKey]);
    return new Promise(function (resolveDone) {
      var i = 0;
      (function next() {
        if (i >= chars.length) { resolveDone(); return; }
        var k = chars[i++];
        var ev = new KeyboardEvent('keydown', { key: k, bubbles: true, cancelable: true });
        var notCancelled = target.dispatchEvent(ev);
        // Sự kiện tổng hợp không tự chèn ký tự vào ô nhập → mô phỏng thủ công để giống bàn phím thật.
        if (notCancelled && k.length === 1 && isEditable(target)) target.value += k;
        setTimeout(next, opts.interval);
      })();
    });
  };

  /** Kiểm tra định dạng mã phía trình duyệt (máy chủ vẫn kiểm tra lại). */
  BarcodeScanner.classify = function (raw) {
    var code = String(raw || '').replace(/\s+/g, '').toUpperCase();
    if (/^\d/.test(code)) code = code.replace(/-/g, '');
    if (/^BS\d{10}$/.test(code)) return { type: 'copy', code: code, valid: BarcodeScanner.luhnValid(code.slice(2)) };
    if (/^DG\d{8}$/.test(code)) return { type: 'card', code: code, valid: true };
    if (/^97[89]\d{10}$/.test(code)) return { type: 'isbn', code: code, valid: BarcodeScanner.isbn13Valid(code) };
    if (/^CMD-[A-Z]+$/.test(code)) return { type: 'command', code: code, valid: true };
    return { type: 'unknown', code: code, valid: false };
  };

  BarcodeScanner.luhnValid = function (digits) {
    var sum = 0, dbl = false;
    for (var i = digits.length - 1; i >= 0; i--) {
      var d = digits.charCodeAt(i) - 48;
      if (dbl) { d *= 2; if (d > 9) d -= 9; }
      sum += d; dbl = !dbl;
    }
    return sum % 10 === 0;
  };

  BarcodeScanner.isbn13Valid = function (s) {
    var sum = 0;
    for (var i = 0; i < 12; i++) sum += (s.charCodeAt(i) - 48) * (i % 2 === 0 ? 1 : 3);
    return (10 - sum % 10) % 10 === s.charCodeAt(12) - 48;
  };

  global.BarcodeScanner = BarcodeScanner;
})(window);
