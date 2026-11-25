// ============================================================
// Người thực hiện: Lê Huy Hoàng – MSSV 2410900038
// Công việc: W5-03 – màn hình React tra cứu bản sao tức thời (tuần 5, nhánh feature/hoang-tra-cuu-react)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
import { useCallback, useEffect, useRef, useState } from 'react';
import { api } from '../lib/api.js';
import { date, dateTime, statusClass } from '../lib/format.js';
import { beep, classify, useBarcodeScanner } from '../lib/useBarcodeScanner.js';

/**
 * Màn hình tra cứu bản sao tức thời (UC-M3).
 * - Quét mã bản sao → chi tiết bản sao, trạng thái, người đang mượn, lịch sử 5 lượt.
 * - Quét ISBN → tất cả bản sao của đầu sách.
 * - Gõ tay một phần nhan đề → gợi ý (debounce 250 ms).
 * Mỗi lần tra cứu mới sẽ hủy request cũ (AbortController) để kết quả không bị lẫn.
 */
export default function LookupApp({ initialCode, maxInterKeyMs, minLength }) {
  const inputRef = useRef(null);
  const abortRef = useRef(null);
  const debounceRef = useRef(null);
  const [text, setText] = useState('');
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(false);
  const [timing, setTiming] = useState(null);
  const [recent, setRecent] = useState([]);
  const [focused, setFocused] = useState(true);
  const [actionMsg, setActionMsg] = useState(null);

  const lookup = useCallback(async (raw, source = 'typed', meta = null) => {
    const code = (raw || '').trim();
    if (!code) return;
    clearTimeout(debounceRef.current);
    abortRef.current?.abort();
    const ctrl = new AbortController();
    abortRef.current = ctrl;
    setLoading(true);
    setActionMsg(null);
    const t0 = performance.now();
    try {
      const r = await api(`/api/copies/lookup?code=${encodeURIComponent(code)}&source=${source}`, { signal: ctrl.signal });
      const total = Math.round(performance.now() - t0);
      if (!r.ok) {
        beep('error');
        setResult({ found: false, message: r.message, code });
        return;
      }
      setResult(r.data);
      setTiming({ server: r.data.elapsedMs, total, source, scanMs: meta?.durationMs });
      if (source === 'scan') beep(r.data.found ? 'ok' : 'error');
      if (r.data.found && (r.data.codeType === 'copy' || r.data.codeType === 'isbn')) {
        setRecent((prev) => [{ code: r.data.code, title: r.data.book?.title, status: r.data.copy?.statusText }, ...prev.filter((p) => p.code !== r.data.code)].slice(0, 12));
      }
      if (source === 'scan' || r.data.codeType !== 'text') setText('');
    } catch (e) {
      if (e.name !== 'AbortError') setResult({ found: false, message: 'Lỗi khi tra cứu.', code });
    } finally {
      if (abortRef.current === ctrl) setLoading(false);
    }
  }, []);

  useBarcodeScanner(inputRef, {
    maxInterKeyMs, minLength,
    onScan: (code, meta) => lookup(code, 'scan', meta),
    onTyped: (t) => lookup(t, 'typed'),
  });

  // Gõ tay: gợi ý tức thời khi chuỗi không giống mã (FR-7.4)
  const onChange = (e) => {
    const v = e.target.value;
    setText(v);
    clearTimeout(debounceRef.current);
    const c = classify(v);
    if (v.trim().length >= 2 && c.type === 'unknown') debounceRef.current = setTimeout(() => lookup(v, 'typed'), 250);
  };

  useEffect(() => { if (initialCode) lookup(initialCode, 'typed'); }, [initialCode, lookup]);

  const checkin = async () => {
    if (!result?.copy) return;
    if (!window.confirm(`Nhận trả bản sao ${result.copy.barcode}?`)) return;
    const r = await api('/api/circulation/checkin', { method: 'POST', body: { barcode: result.copy.barcode } });
    if (!r.ok) { beep('error'); setActionMsg({ ok: false, text: r.message }); return; }
    beep('ok');
    setActionMsg({ ok: true, text: r.message });
    lookup(result.copy.barcode, 'typed');
  };

  return (
    <div className="row g-3">
      <div className="col-xl-9">
        <div className="card mb-3">
          <div className="card-body">
            <div className="d-flex justify-content-between align-items-center mb-2">
              <label htmlFor="lookupInput" className="form-label fw-semibold m-0">Quét hoặc nhập mã</label>
              <span className={`scan-state ${focused ? 'ready' : 'away'}`}><span className="dot" />{focused ? 'Sẵn sàng quét' : 'Vẫn nhận mã quét'}</span>
            </div>
            <div className="scan-box">
              <i className="bi bi-upc-scan" />
              <input id="lookupInput" ref={inputRef} className="form-control" value={text} onChange={onChange}
                onFocus={() => setFocused(true)} onBlur={() => setFocused(false)}
                placeholder="BS… · 978… · hoặc gõ nhan đề" autoComplete="off" spellCheck="false" autoFocus />
            </div>
            <div className="d-flex flex-wrap justify-content-between small text-muted mt-2">
              <span>Mã bản sao (BS + 10 số), ISBN-13 trên bìa, hoặc ít nhất 2 ký tự của nhan đề/tác giả.</span>
              {timing && (
                <span title="NFR-08: máy chủ < 300 ms, từ lúc quét đến lúc hiển thị < 1 giây">
                  <i className="bi bi-stopwatch" /> Máy chủ <strong className={timing.server > 300 ? 'text-danger' : 'text-success'}>{timing.server} ms</strong>
                  {' · '}hiển thị sau <strong>{timing.total} ms</strong>
                  {timing.scanMs !== undefined && <> · quét {timing.scanMs} ms</>}
                </span>
              )}
            </div>
          </div>
        </div>

        {loading && <div className="text-muted small mb-2"><span className="spinner-border spinner-border-sm" /> Đang tra cứu…</div>}
        {actionMsg && <div className={`alert ${actionMsg.ok ? 'alert-success' : 'alert-danger'} py-2`}>{actionMsg.text}</div>}
        {!result && !loading && <EmptyState />}
        {result && <ResultView r={result} onPick={(code) => lookup(code, 'typed')} onCheckin={checkin} />}
      </div>

      <div className="col-xl-3">
        <div className="card">
          <div className="card-header">Tra cứu gần đây</div>
          <ul className="list-group list-group-flush">
            {recent.length === 0 && <li className="list-group-item small text-muted">Chưa có.</li>}
            {recent.map((r) => (
              <li key={r.code} className="list-group-item list-group-item-action small" role="button" onClick={() => lookup(r.code, 'typed')}>
                <div className="mono">{r.code}</div>
                <div className="text-truncate text-muted">{r.title}{r.status ? ` · ${r.status}` : ''}</div>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}

function EmptyState() {
  return (
    <div className="card"><div className="card-body text-center text-muted py-5">
      <i className="bi bi-upc display-4" />
      <p className="mt-2 mb-1">Quét nhãn mã vạch trên sách để biết ngay bản sao đang ở đâu.</p>
      <p className="small mb-0">Ví dụ: <span className="mono">BS0000000018</span> (đang được mượn) · <span className="mono">BS0000000448</span> (trên kệ) · <span className="mono">9786041703339</span> (ISBN)</p>
    </div></div>
  );
}

function ResultView({ r, onPick, onCheckin }) {
  if (!r.found && !r.suggestions?.length) {
    return (
      <div className="alert alert-warning d-flex gap-2 align-items-start">
        <i className="bi bi-question-circle fs-5" />
        <div><strong>{r.message || 'Không tìm thấy.'}</strong>{r.code && <div className="small mono">{r.code}</div>}</div>
      </div>
    );
  }
  if (r.codeType === 'text') return <Suggestions items={r.suggestions} onPick={onPick} />;
  if (r.codeType === 'isbn') return <IsbnView r={r} onPick={onPick} />;
  return <CopyView r={r} onCheckin={onCheckin} />;
}

function Cover({ book }) {
  if (book?.coverUrl) return <img src={book.coverUrl} alt="" className="cover" />;
  const hue = Math.abs([...(book?.title || '?')].reduce((h, c) => (h * 31 + c.charCodeAt(0)) | 0, 17)) % 360;
  const initials = (book?.title || '?').split(' ').filter(Boolean).slice(0, 2).map((w) => w[0].toUpperCase()).join('');
  return <div className="cover cover-fallback" style={{ '--h': hue }}><span>{initials}</span></div>;
}

function CopyView({ r, onCheckin }) {
  const { copy, book, currentLoan: loan, hold } = r;
  return (
    <div className="card">
      <div className="card-body">
        <div className="row g-3">
          <div className="col-sm-3 col-lg-2"><Cover book={book} /></div>
          <div className="col-sm-9 col-lg-10">
            <div className="d-flex flex-wrap justify-content-between gap-2">
              <div>
                <div className="mono text-muted">{copy.barcode}</div>
                <h2 className="h4 fw-bold mb-1">{book.title}</h2>
                <div className="text-muted">{book.authors}</div>
              </div>
              <div className="text-end">
                <span className={`badge fs-5 ${statusClass(copy.status)}`}>{copy.statusText}</span>
                <div className="mt-2"><i className="bi bi-geo-alt-fill text-danger" /> Kệ <strong className="fs-5">{copy.shelf || '—'}</strong></div>
              </div>
            </div>
            <div className="row small mt-3 g-2">
              <div className="col-md-4"><span className="text-muted">ISBN:</span> <span className="mono">{book.isbn || '—'}</span></div>
              <div className="col-md-4"><span className="text-muted">Thể loại:</span> {book.category}</div>
              <div className="col-md-4"><span className="text-muted">NXB:</span> {book.publisher || '—'} {book.publishYear ? `(${book.publishYear})` : ''}</div>
              <div className="col-md-4"><span className="text-muted">Tình trạng:</span> {copy.condition || '—'}</div>
              <div className="col-md-4"><span className="text-muted">Bản sao của đầu sách:</span> {book.availableCopies}/{book.totalCopies} sẵn sàng</div>
              <div className="col-md-4">{book.isReferenceOnly && <span className="badge text-bg-secondary">Chỉ đọc tại chỗ</span>}</div>
            </div>

            {loan && (
              <div className={`alert ${loan.overdueDays > 0 ? 'alert-danger' : 'alert-primary'} mt-3 mb-0`}>
                <div className="d-flex flex-wrap justify-content-between gap-2 align-items-center">
                  <div>
                    <i className="bi bi-person-fill" /> Đang được mượn bởi <strong>{loan.readerName}</strong> (<span className="mono">{loan.cardNumber}</span>)
                    <div className="small">Mượn {date(loan.loanDate)} · hạn trả <strong>{date(loan.dueDate)}</strong> · đã gia hạn {loan.renewCount} lần
                      {loan.overdueDays > 0 && <> · <strong>quá hạn {loan.overdueDays} ngày</strong></>}</div>
                  </div>
                  <button className="btn btn-sm btn-light" onClick={onCheckin}><i className="bi bi-box-arrow-in-down-left" /> Nhận trả</button>
                </div>
              </div>
            )}
            {hold && (
              <div className="alert alert-warning mt-3 mb-0">
                <i className="bi bi-bookmark-star-fill" /> Đang giữ cho <strong>{hold.readerName}</strong> (<span className="mono">{hold.cardNumber}</span>) đến hết ngày {date(hold.expiresAt)} – để riêng, không xếp lên kệ.
              </div>
            )}
          </div>
        </div>
      </div>
      <div className="card-footer bg-white">
        <div className="d-flex justify-content-between align-items-center mb-2">
          <strong className="small">Lịch sử lưu thông gần nhất</strong>
          <a className="small" href={`/Books/Details/${book.id}`}>Xem đầu sách →</a>
        </div>
        {r.history.length === 0 ? <div className="small text-muted">Bản sao chưa từng được mượn.</div> : (
          <div className="table-responsive">
            <table className="table table-sm mb-0">
              <thead><tr><th>Độc giả</th><th>Ngày mượn</th><th>Ngày trả</th><th>Trạng thái</th></tr></thead>
              <tbody>
                {r.history.map((h, i) => (
                  <tr key={i}><td>{h.readerName} <span className="mono text-muted small">{h.cardNumber}</span></td><td>{dateTime(h.loanDate)}</td><td>{h.returnedAt ? dateTime(h.returnedAt) : '—'}</td><td>{h.status}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

function IsbnView({ r, onPick }) {
  const { book } = r;
  return (
    <div className="card">
      <div className="card-body">
        <div className="d-flex gap-3">
          <div style={{ width: 90 }}><Cover book={book} /></div>
          <div className="flex-grow-1">
            <div className="small text-muted">ISBN <span className="mono">{book.isbn}</span></div>
            <h2 className="h4 fw-bold mb-1">{book.title}</h2>
            <div className="text-muted">{book.authors}</div>
            <div className="mt-2"><span className={`badge ${book.availableCopies > 0 ? 'text-bg-success' : 'text-bg-warning'}`}>{book.availableCopies}/{book.totalCopies} bản sao sẵn sàng</span></div>
          </div>
        </div>
      </div>
      <div className="table-responsive">
        <table className="table align-middle mb-0">
          <thead><tr><th>Mã bản sao</th><th>Kệ</th><th>Trạng thái</th><th>Hạn trả</th></tr></thead>
          <tbody>
            {r.copies.map((c) => (
              <tr key={c.id} role="button" onClick={() => onPick(c.barcode)}>
                <td className="mono">{c.barcode}</td><td>{c.shelf}</td>
                <td><span className={`badge ${statusClass(c.status)}`}>{c.statusText}</span></td>
                <td>{c.dueDate ? date(c.dueDate) : ''}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function Suggestions({ items, onPick }) {
  return (
    <div className="card">
      <div className="card-header">Đầu sách phù hợp ({items.length})</div>
      <ul className="list-group list-group-flush">
        {items.map((s) => (
          <li key={s.id} className="list-group-item d-flex justify-content-between align-items-center">
            <div>
              <div className="fw-semibold">{s.title}</div>
              <div className="small text-muted">{s.authors}{s.isbn ? ` · ${s.isbn}` : ''}</div>
            </div>
            <div className="d-flex gap-2 align-items-center">
              <span className={`badge ${s.availableCopies > 0 ? 'text-bg-success' : 'text-bg-warning'}`}>{s.availableCopies} sẵn sàng</span>
              {s.isbn
                ? <button className="btn btn-sm btn-outline-primary" onClick={() => onPick(s.isbn)}>Xem bản sao</button>
                : <a className="btn btn-sm btn-outline-primary" href={`/Books/Details/${s.id}`}>Xem</a>}
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}

