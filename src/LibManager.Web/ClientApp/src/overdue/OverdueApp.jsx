// ============================================================
// Người thực hiện: Nguyễn Mạnh Tùng – MSSV 2410900086
// Công việc: W5-05 – bảng sách quá hạn bằng React (tuần 5, nhánh feature/tung-qua-han)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
import { useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../lib/api.js';
import { date, dateTime, vnd } from '../lib/format.js';

const columns = [
  { key: 'barcode', label: 'Mã bản sao' },
  { key: 'title', label: 'Nhan đề' },
  { key: 'readerName', label: 'Độc giả' },
  { key: 'dueDate', label: 'Hạn trả' },
  { key: 'overdueDays', label: 'Số ngày quá hạn', num: true },
  { key: 'estimatedFine', label: 'Phí tạm tính', num: true },
  { key: 'remindedAt', label: 'Đã nhắc' },
];

/** Bảng sách quá hạn có lọc, sắp xếp và gửi email nhắc tại chỗ. */
export default function OverdueApp() {
  const [rows, setRows] = useState([]);
  const [q, setQ] = useState('');
  const [sort, setSort] = useState({ key: 'overdueDays', dir: 'desc' });
  const [selected, setSelected] = useState(new Set());
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState(null);
  const [sending, setSending] = useState(new Set());
  const timer = useRef(null);

  const load = async (term) => {
    setLoading(true);
    const r = await api('/api/reports/overdue' + (term ? `?q=${encodeURIComponent(term)}` : ''));
    setLoading(false);
    if (!r.ok) { setMsg({ ok: false, text: r.message }); return; }
    setRows(r.data);
    setSelected(new Set());
  };

  useEffect(() => { load(''); }, []);

  const onSearch = (v) => {
    setQ(v);
    clearTimeout(timer.current);
    timer.current = setTimeout(() => load(v.trim()), 300);
  };

  const sorted = useMemo(() => {
    const arr = [...rows];
    const { key, dir } = sort;
    arr.sort((a, b) => {
      const x = a[key] ?? '', y = b[key] ?? '';
      const c = typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y), 'vi');
      return dir === 'asc' ? c : -c;
    });
    return arr;
  }, [rows, sort]);

  const toggleSort = (key) => setSort((s) => ({ key, dir: s.key === key && s.dir === 'desc' ? 'asc' : 'desc' }));

  const remind = async (ids) => {
    const list = ids.filter((id) => rows.find((r) => r.loanItemId === id)?.email);
    if (!list.length) { setMsg({ ok: false, text: 'Các mục đã chọn không có email độc giả.' }); return; }
    setSending(new Set(list));
    let ok = 0, fail = 0;
    for (const id of list) {
      const r = await api(`/api/reports/overdue/${id}/remind`, { method: 'POST' });
      if (r.ok) {
        ok++;
        setRows((prev) => prev.map((x) => (x.loanItemId === id ? { ...x, remindedAt: new Date().toISOString() } : x)));
      } else fail++;
      setSending((prev) => { const n = new Set(prev); n.delete(id); return n; });
    }
    setMsg({ ok: fail === 0, text: `Đã gửi ${ok} email nhắc${fail ? `, ${fail} lỗi` : ''}.` });
    setSelected(new Set());
  };

  const totalFine = rows.reduce((a, b) => a + b.estimatedFine, 0);
  const allChecked = sorted.length > 0 && sorted.every((r) => selected.has(r.loanItemId));

  return (
    <div>
      <div className="row g-3 mb-3">
        <div className="col-sm-4"><div className="card stat"><div className="num text-danger">{rows.length}</div><div className="lbl">Lượt mượn quá hạn</div></div></div>
        <div className="col-sm-4"><div className="card stat"><div className="num">{new Set(rows.map((r) => r.cardNumber)).size}</div><div className="lbl">Độc giả liên quan</div></div></div>
        <div className="col-sm-4"><div className="card stat"><div className="num">{vnd(totalFine)}</div><div className="lbl">Phí quá hạn tạm tính</div></div></div>
      </div>

      <div className="card">
        <div className="card-header d-flex flex-wrap gap-2 align-items-center">
          <input className="form-control form-control-sm" style={{ maxWidth: 360 }} placeholder="Lọc theo độc giả, mã thẻ, mã bản sao, nhan đề…" value={q} onChange={(e) => onSearch(e.target.value)} />
          {loading && <span className="spinner-border spinner-border-sm text-muted" />}
          <button className="btn btn-sm btn-warning ms-auto" disabled={!selected.size || sending.size > 0} onClick={() => remind([...selected])}>
            <i className="bi bi-envelope" /> Gửi nhắc {selected.size ? `(${selected.size})` : ''}
          </button>
        </div>
        {msg && <div className={`alert ${msg.ok ? 'alert-success' : 'alert-danger'} m-2 py-2 small`}>{msg.text}</div>}
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0">
            <thead>
              <tr>
                <th style={{ width: 32 }}>
                  <input type="checkbox" className="form-check-input" checked={allChecked}
                    onChange={() => setSelected(allChecked ? new Set() : new Set(sorted.map((r) => r.loanItemId)))} />
                </th>
                {columns.map((c) => (
                  <th key={c.key} role="button" className={c.num ? 'text-end' : ''} onClick={() => toggleSort(c.key)}>
                    {c.label} {sort.key === c.key && <i className={`bi bi-caret-${sort.dir === 'asc' ? 'up' : 'down'}-fill`} />}
                  </th>
                ))}
                <th />
              </tr>
            </thead>
            <tbody>
              {!loading && sorted.length === 0 && <tr><td colSpan={columns.length + 2} className="text-center text-muted py-4">Không có sách quá hạn.</td></tr>}
              {sorted.map((r) => (
                <tr key={r.loanItemId}>
                  <td><input type="checkbox" className="form-check-input" checked={selected.has(r.loanItemId)}
                    onChange={() => setSelected((s) => { const n = new Set(s); n.has(r.loanItemId) ? n.delete(r.loanItemId) : n.add(r.loanItemId); return n; })} /></td>
                  <td className="mono"><a href={`/Lookup?code=${r.barcode}`} className="text-decoration-none">{r.barcode}</a></td>
                  <td>{r.title}</td>
                  <td>{r.readerName}<div className="small text-muted"><span className="mono">{r.cardNumber}</span>{r.email ? ` · ${r.email}` : ' · (không có email)'}</div></td>
                  <td>{date(r.dueDate)}</td>
                  <td className="text-end"><span className={`badge ${r.overdueDays > 7 ? 'text-bg-danger' : 'text-bg-warning'}`}>{r.overdueDays}</span></td>
                  <td className="text-end">{vnd(r.estimatedFine)}</td>
                  <td className="small text-muted">{r.remindedAt ? dateTime(r.remindedAt) : '—'}</td>
                  <td className="text-end">
                    <button className="btn btn-sm btn-outline-warning" disabled={!r.email || sending.has(r.loanItemId)} onClick={() => remind([r.loanItemId])} title="Gửi email nhắc trả">
                      {sending.has(r.loanItemId) ? <span className="spinner-border spinner-border-sm" /> : <i className="bi bi-envelope" />}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
