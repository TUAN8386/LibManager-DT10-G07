// ============================================================
// Người thực hiện: Lê Huy Hoàng – MSSV 2410900038
// Công việc: W5-04 – dashboard lưu thông bằng React (tuần 5, nhánh feature/hoang-dashboard)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================
import { useMemo, useState } from 'react';

const niceMax = (v) => {
  if (v <= 5) return 5;
  const p = Math.pow(10, Math.floor(Math.log10(v)));
  const n = v / p;
  return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * p;
};

/**
 * Biểu đồ cột nhóm + đường (SVG thuần, không phụ thuộc thư viện).
 * data: [{ label, ...values }], series: [{ key, label, color, type: 'bar' | 'line' }]
 */
export function ComboChart({ data, series, height = 280 }) {
  const [hover, setHover] = useState(null);
  const width = 900;
  const pad = { l: 40, r: 12, t: 14, b: 34 };
  const innerW = width - pad.l - pad.r;
  const innerH = height - pad.t - pad.b;
  const max = useMemo(() => niceMax(Math.max(1, ...data.flatMap((d) => series.map((s) => d[s.key] || 0)))), [data, series]);
  const step = innerW / Math.max(1, data.length);
  const bars = series.filter((s) => s.type !== 'line');
  const lines = series.filter((s) => s.type === 'line');
  const barW = Math.max(2, Math.min(26, (step * 0.7) / Math.max(1, bars.length)));
  const y = (v) => pad.t + innerH - (v / max) * innerH;
  const labelEvery = Math.ceil(data.length / 12);

  return (
    <div className="position-relative">
      <svg viewBox={`0 0 ${width} ${height}`} className="w-100" role="img" aria-label="Biểu đồ lượt mượn – trả">
        {[0, 0.2, 0.4, 0.6, 0.8, 1].map((f) => (
          <g key={f}>
            <line x1={pad.l} x2={width - pad.r} y1={y(max * f)} y2={y(max * f)} stroke="#e2e8f0" />
            <text x={pad.l - 6} y={y(max * f) + 4} fontSize="11" textAnchor="end" fill="#64748b">{Math.round(max * f)}</text>
          </g>
        ))}
        {data.map((d, i) => {
          const cx = pad.l + step * i + step / 2;
          return (
            <g key={i} onMouseEnter={() => setHover(i)} onMouseLeave={() => setHover(null)}>
              <rect x={pad.l + step * i} y={pad.t} width={step} height={innerH} fill={hover === i ? '#f1f5f9' : 'transparent'} />
              {bars.map((s, k) => {
                const v = d[s.key] || 0;
                const x = cx - (bars.length * barW) / 2 + k * barW;
                return <rect key={s.key} x={x} y={y(v)} width={barW - 1} height={Math.max(0, pad.t + innerH - y(v))} rx="2" fill={s.color} />;
              })}
              {i % labelEvery === 0 && (
                <text x={cx} y={height - 12} fontSize="11" textAnchor="middle" fill="#64748b">{d.label}</text>
              )}
            </g>
          );
        })}
        {lines.map((s) => (
          <polyline key={s.key} fill="none" stroke={s.color} strokeWidth="2.5"
            points={data.map((d, i) => `${pad.l + step * i + step / 2},${y(d[s.key] || 0)}`).join(' ')} />
        ))}
        {lines.map((s) => data.map((d, i) => (
          <circle key={s.key + i} cx={pad.l + step * i + step / 2} cy={y(d[s.key] || 0)} r={hover === i ? 4.5 : 2.5} fill={s.color} />
        )))}
      </svg>
      {hover !== null && data[hover] && (
        <div className="position-absolute top-0 end-0 bg-white border rounded shadow-sm small p-2">
          <div className="fw-semibold">{data[hover].label}</div>
          {series.map((s) => (
            <div key={s.key}><span style={{ color: s.color }}>■</span> {s.label}: <strong>{data[hover][s.key] || 0}</strong></div>
          ))}
        </div>
      )}
      <div className="d-flex gap-3 small justify-content-center">
        {series.map((s) => <span key={s.key}><span style={{ color: s.color }}>■</span> {s.label}</span>)}
      </div>
    </div>
  );
}

/** Danh sách thanh ngang (top sách, thể loại). */
export function HBarList({ items, color = '#1d4ed8', valueLabel = 'lượt' }) {
  const max = Math.max(1, ...items.map((i) => i.value));
  if (!items.length) return <div className="text-muted small py-3 text-center">Chưa có dữ liệu trong khoảng thời gian này.</div>;
  return (
    <div className="d-flex flex-column gap-2">
      {items.map((it, i) => (
        <div key={i}>
          <div className="d-flex justify-content-between small">
            <span className="text-truncate me-2" title={it.label}>{it.label}</span>
            <span className="text-muted text-nowrap">{it.value} {valueLabel}</span>
          </div>
          <div className="progress" style={{ height: 8 }}>
            <div className="progress-bar" style={{ width: `${(it.value / max) * 100}%`, background: color }} />
          </div>
        </div>
      ))}
    </div>
  );
}
