// ============================================================
// Người thực hiện: Lê Huy Hoàng – MSSV 2410900038
// Công việc: W5-04 – dashboard lưu thông bằng React (tuần 5, nhánh feature/hoang-dashboard)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
import { useEffect, useState } from 'react';
import { api } from '../lib/api.js';
import { date, isoDate, vnd } from '../lib/format.js';
import { ComboChart, HBarList } from '../components/Charts.jsx';

const ranges = [
  { key: '7', label: '7 ngày', days: 7 },
  { key: '30', label: '30 ngày', days: 30 },
  { key: '90', label: '90 ngày', days: 90 },
  { key: 'year', label: 'Năm nay' },
];

function rangeOf(key) {
  const to = new Date();
  if (key === 'year') return { from: new Date(to.getFullYear(), 0, 1), to };
  const r = ranges.find((x) => x.key === key);
  const from = new Date(to);
  from.setDate(to.getDate() - (r.days - 1));
  return { from, to };
}

/** Dashboard lưu thông (FR-5.1 – FR-5.3) – gọi Web API /api/reports/*. */
export default function DashboardApp() {
  const [rangeKey, setRangeKey] = useState('30');
  const [from, setFrom] = useState(isoDate(rangeOf('30').from));
  const [to, setTo] = useState(isoDate(new Date()));
  const [groupBy, setGroupBy] = useState('day');
  const [summary, setSummary] = useState(null);
  const [stats, setStats] = useState([]);
  const [top, setTop] = useState([]);
  const [cats, setCats] = useState([]);
  const [overdue, setOverdue] = useState([]);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  const pickRange = (key) => {
    const r = rangeOf(key);
    setRangeKey(key);
    setFrom(isoDate(r.from));
    setTo(isoDate(r.to));
    setGroupBy(key === 'year' || key === '90' ? (key === 'year' ? 'month' : 'day') : 'day');
  };

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(null);
      const q = `from=${from}&to=${to}`;
      const [s, l, t, c, o] = await Promise.all([
        api('/api/reports/summary'),
        api(`/api/reports/loans?${q}&groupBy=${groupBy}`),
        api(`/api/reports/top-books?${q}&take=8`),
        api(`/api/reports/categories?${q}`),
        api('/api/reports/overdue'),
      ]);
      if (cancelled) return;
      const failed = [s, l, t, c, o].find((x) => !x.ok);
      if (failed) setError(failed.message);
      if (s.ok) setSummary(s.data);
      if (l.ok) setStats(l.data);
      if (t.ok) setTop(t.data);
      if (c.ok) setCats(c.data);
      if (o.ok) setOverdue(o.data);
      setLoading(false);
    })();
    return () => { cancelled = true; };
  }, [from, to, groupBy]);

  const totalLoans = stats.reduce((a, b) => a + b.loans, 0);
  const totalReturns = stats.reduce((a, b) => a + b.returns, 0);

  return (
    <div>
      <div className="card card-body mb-3">
        <div className="d-flex flex-wrap gap-2 align-items-end">
          <div className="btn-group" role="group">
            {ranges.map((r) => (
              <button key={r.key} className={`btn btn-sm ${rangeKey === r.key ? 'btn-primary' : 'btn-outline-primary'}`} onClick={() => pickRange(r.key)}>{r.label}</button>
            ))}
          </div>
          <div><label className="form-label small text-muted mb-0">Từ ngày</label><input type="date" className="form-control form-control-sm" value={from} max={to} onChange={(e) => { setFrom(e.target.value); setRangeKey(''); }} /></div>
          <div><label className="form-label small text-muted mb-0">Đến ngày</label><input type="date" className="form-control form-control-sm" value={to} min={from} onChange={(e) => { setTo(e.target.value); setRangeKey(''); }} /></div>
          <div><label className="form-label small text-muted mb-0">Nhóm theo</label>
            <select className="form-select form-select-sm" value={groupBy} onChange={(e) => setGroupBy(e.target.value)}>
              <option value="day">Ngày</option><option value="month">Tháng</option>
            </select>
          </div>
          <a className="btn btn-sm btn-success ms-auto" href={`/Reports/Export?from=${from}&to=${to}`}><i className="bi bi-file-earmark-excel" /> Xuất Excel</a>
        </div>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      {summary && (
        <div className="row g-3 mb-3">
          <Stat icon="bi-journal-bookmark" color="text-primary" value={summary.totalBooks} label="Đầu sách" sub={`${summary.totalCopies} bản sao`} />
          <Stat icon="bi-box-arrow-up-right" color="text-info" value={summary.onLoan} label="Đang cho mượn" sub={`Hôm nay: ${summary.loansToday} mượn · ${summary.returnsToday} trả`} />
          <Stat icon="bi-alarm" color="text-danger" value={summary.overdue} label="Quá hạn" sub={<a href="/Reports/Overdue">Xem danh sách</a>} />
          <Stat icon="bi-cash-coin" color="text-warning" value={vnd(summary.unpaidFines)} label="Phí chưa thu" sub={`${summary.readers} độc giả`} />
        </div>
      )}

      <div className="row g-3">
        <div className="col-xl-8">
          <div className="card h-100">
            <div className="card-header d-flex justify-content-between">
              <span>Lượt mượn – trả</span>
              <span className="small text-muted">Tổng: {totalLoans} mượn · {totalReturns} trả {loading && <span className="spinner-border spinner-border-sm ms-1" />}</span>
            </div>
            <div className="card-body">
              <ComboChart data={stats} series={[
                { key: 'loans', label: 'Lượt mượn', color: '#1d4ed8', type: 'bar' },
                { key: 'returns', label: 'Lượt trả', color: '#16a34a', type: 'line' },
              ]} />
            </div>
          </div>
        </div>
        <div className="col-xl-4">
          <div className="card h-100">
            <div className="card-header">Top đầu sách được mượn</div>
            <div className="card-body">
              <HBarList items={top.map((t) => ({ label: t.title, value: t.loanCount }))} />
            </div>
          </div>
        </div>
        <div className="col-lg-6">
          <div className="card h-100">
            <div className="card-header">Lượt mượn theo thể loại</div>
            <div className="card-body"><HBarList items={cats.map((c) => ({ label: c.category, value: c.loans }))} color="#7c3aed" /></div>
          </div>
        </div>
        <div className="col-lg-6">
          <div className="card h-100">
            <div className="card-header d-flex justify-content-between"><span>Cảnh báo quá hạn</span><a className="small" href="/Reports/Overdue">Tất cả ({overdue.length})</a></div>
            <ul className="list-group list-group-flush">
              {overdue.length === 0 && <li className="list-group-item small text-muted">Không có sách quá hạn. 🎉</li>}
              {overdue.slice(0, 6).map((o) => (
                <li key={o.loanItemId} className="list-group-item small d-flex justify-content-between">
                  <span className="text-truncate me-2"><strong>{o.readerName}</strong> · {o.title}</span>
                  <span className="text-danger text-nowrap">{o.overdueDays} ngày · hạn {date(o.dueDate)}</span>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}

function Stat({ icon, color, value, label, sub }) {
  return (
    <div className="col-sm-6 col-xl-3">
      <div className="card stat h-100">
        <div className="d-flex justify-content-between">
          <div><div className="num">{value}</div><div className="lbl">{label}</div></div>
          <i className={`bi ${icon} ${color}`} />
        </div>
        <div className="small text-muted mt-1">{sub}</div>
      </div>
    </div>
  );
}
