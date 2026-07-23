import { useState, useMemo, useRef, useCallback, useEffect } from 'react'
import { dashboardService } from '../services/dashboardService'

const PRIORITY_COLORS = { critical: '#DC2626', high: '#EA580C', medium: '#D97706', low: '#2563EB', informational: '#64748B' }
const PRIORITY_LABELS = { critical: 'Critical', high: 'High', medium: 'Medium', low: 'Low', informational: 'Informational' }

const KPI_CONFIG = [
  { key: 'total', label: 'Total Tickets', icon: 'fa-ticket', color: '#2563EB', bg: '#EFF6FF' },
  { key: 'open', label: 'Open', icon: 'fa-envelope-open', color: '#0D6EFD', bg: '#E3F2FD' },
  { key: 'inProgress', label: 'In Progress', icon: 'fa-spinner', color: '#FFC107', bg: '#FFF8E1' },
  { key: 'waiting', label: 'Waiting', icon: 'fa-clock', color: '#FD7E14', bg: '#FFF3E0' },
  { key: 'resolved', label: 'Resolved', icon: 'fa-check-circle', color: '#198754', bg: '#D4EDDA' },
  { key: 'closed', label: 'Closed', icon: 'fa-circle-check', color: '#6C757D', bg: '#E9ECEF' },
]

const FILTER_PRESETS = ['Today', 'Yesterday', 'Last 7 Days', 'Last 30 Days', 'This Month']

const C_ = 2 * Math.PI * 80

function buildSmoothPath(points) {
  if (points.length < 2) return ''
  let d = `M ${points[0].x} ${points[0].y}`
  for (let i = 1; i < points.length; i++) {
    const prev = points[i - 1]
    const curr = points[i]
    const cp1x = prev.x + (curr.x - prev.x) / 3
    const cp2x = curr.x - (curr.x - prev.x) / 3
    d += ` C ${cp1x} ${prev.y}, ${cp2x} ${curr.y}, ${curr.x} ${curr.y}`
  }
  return d
}

function EmptyState({ icon, title, description }) {
  return (
    <div className="dash-empty">
      <div className="dash-empty-icon"><i className={`fas ${icon}`} /></div>
      <div className="dash-empty-title">{title}</div>
      <div className="dash-empty-desc">{description}</div>
    </div>
  )
}

function SectionHeader({ number, title }) {
  return (
    <div className="dash-section-header">
      <span className="dash-section-badge">{number}</span>
      <h2 className="dash-section-title">{title}</h2>
    </div>
  )
}

export default function Dashboard({ isAdmin }) {
  const [datePreset, setDatePreset] = useState('Last 30 Days')
  const [stats, setStats] = useState(null)
  const [agentData, setAgentData] = useState([])
  const [slaData, setSlaData] = useState([])
  const [trendData, setTrendData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [tooltip, setTooltip] = useState(null)
  const chartRef = useRef(null)

  const dateRange = useMemo(() => {
    const now = new Date()
    const end = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59, 999)
    let start, days = 30
    switch (datePreset) {
      case 'Today':
        start = new Date(end.getFullYear(), end.getMonth(), end.getDate(), 0, 0, 0, 0)
        days = 1
        break
      case 'Yesterday':
        start = new Date(end); start.setDate(start.getDate() - 1); start.setHours(0, 0, 0, 0)
        end.setDate(end.getDate() - 1)
        days = 1
        break
      case 'Last 7 Days':
        start = new Date(end); start.setDate(start.getDate() - 6); start.setHours(0, 0, 0, 0)
        days = 7
        break
      case 'This Month':
        start = new Date(end.getFullYear(), end.getMonth(), 1, 0, 0, 0, 0)
        days = Math.ceil((end - start) / (1000 * 60 * 60 * 24))
        break
      default:
        start = new Date(end); start.setDate(start.getDate() - 29); start.setHours(0, 0, 0, 0)
        days = 30
    }
    const label = datePreset === 'Today' || datePreset === 'Yesterday'
      ? start.toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' })
      : `${start.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })} — ${end.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}`
    return { from: start, to: end, days, label }
  }, [datePreset])

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    const startDate = dateRange.from.toISOString()
    const endDate = dateRange.to.toISOString()

    Promise.all([
      dashboardService.getStats({ startDate, endDate }).catch(() => null),
      dashboardService.getTrends({ days: dateRange.days }).catch(() => null),
      dashboardService.getSla().catch(() => null),
      dashboardService.getAgentPerformance().catch(() => null),
    ]).then(([statsRes, trendsRes, slaRes, agentRes]) => {
      if (cancelled) return
      setStats(statsRes)
      setTrendData(trendsRes)
      setSlaData(Array.isArray(slaRes) ? slaRes : slaRes?.value || [])
      setAgentData(Array.isArray(agentRes) ? agentRes : agentRes?.value || [])
      setLoading(false)
    })
    return () => { cancelled = true }
  }, [dateRange])

  const statusCounts = useMemo(() => {
    if (!stats) return {}
    return {
      total: stats.total || 0,
      open: stats.open || 0,
      inProgress: stats.inProgress || 0,
      waiting: stats.waiting || 0,
      resolved: stats.resolved || 0,
      closed: stats.closed || 0,
    }
  }, [stats])

  const slaCompliance = useMemo(() => {
    if (stats) {
      const breached = stats.slaBreached || 0
      const total = stats.total || 0
      const compliancePct = total > 0 ? Math.round(((total - breached) / total) * 100) : 100
      return { breached, compliancePct, avgResolutionTime: stats.avgResolutionTime || '-' }
    }
    const total = slaData.length || 1
    const breached = slaData.filter(s => s.slaStatus === 'Breached').length
    return { breached, compliancePct: total > 0 ? Math.round(((total - breached) / total) * 100) : 100, avgResolutionTime: '-' }
  }, [stats, slaData])

  const priorityDist = useMemo(() => {
    if (stats?.priorityDistribution?.length > 0) {
      return stats.priorityDistribution.map(p => ({
        key: p.priority.toLowerCase(),
        label: PRIORITY_LABELS[p.priority.toLowerCase()] || p.priority,
        color: PRIORITY_COLORS[p.priority.toLowerCase()] || '#64748B',
        count: p.count,
        pct: stats.total > 0 ? ((p.count / stats.total) * 100).toFixed(1) : '0',
      }))
    }
    return []
  }, [stats])

  const donutSegments = useMemo(() => {
    const total = priorityDist.reduce((s, d) => s + d.count, 0) || 1
    let cumulative = 0
    return priorityDist.map(d => {
      const dash = (d.count / total) * C_
      const seg = { ...d, dash, offset: cumulative }
      cumulative += dash
      return seg
    })
  }, [priorityDist])

  const isSingleDay = dateRange.days <= 1

  const series = [
    { key: 'created', label: 'Created', color: '#2563EB' },
    { key: 'resolved', label: 'Resolved', color: '#22C55E' },
  ]

  const trendChartData = useMemo(() => {
    const CHART_LEFT = 48, CHART_RIGHT = 555, CHART_W = CHART_RIGHT - CHART_LEFT, CHART_BOTTOM = 248, CHART_H = 200

    if (trendData?.series?.length > 0) {
      const createdSeries = trendData.series.find(s => s.key === 'created')?.data || []
      const resolvedSeries = trendData.series.find(s => s.key === 'resolved')?.data || []
      const numPoints = createdSeries.length
      if (numPoints === 0) return series.map(s => ({ ...s, points: [] }))

      const points = createdSeries.map((val, i) => ({
        created: val,
        resolved: resolvedSeries[i] || 0,
      }))
      const maxVal = Math.max(1, ...points.flatMap(d => [d.created, d.resolved]))

      return series.map(s => ({
        ...s,
        points: points.map((d, i) => ({
          x: CHART_LEFT + (i / Math.max(numPoints - 1, 1)) * CHART_W,
          y: CHART_BOTTOM - ((d[s.key] || 0) / maxVal) * CHART_H,
          val: d[s.key] || 0,
        })),
      }))
    }

    return series.map(s => ({ ...s, points: [] }))
  }, [trendData])

  const trendMaxVal = useMemo(() => {
    const first = trendChartData[0]
    if (!first || first.points.length === 0) return 1
    return Math.max(1, ...first.points.map(p => p.val || 0))
  }, [trendChartData])

  const xAxisLabels = useMemo(() => {
    if (!trendData?.labels) return []
    const labels = trendData.labels
    const numPoints = labels.length
    if (numPoints <= 7) {
      return labels.map((l, i) => ({ index: i, label: l }))
    }
    const result = []
    const step = Math.ceil(numPoints / 7)
    for (let i = 0; i < numPoints; i += step) {
      result.push({ index: i, label: labels[i] })
    }
    if (result.length === 0 || result[result.length - 1].index !== numPoints - 1) {
      result.push({ index: numPoints - 1, label: labels[numPoints - 1] })
    }
    return result
  }, [trendData])

  const yLabels = useMemo(() => {
    const m = Math.max(trendMaxVal, 1)
    const steps = Math.min(m, 5)
    return Array.from({ length: steps + 1 }, (_, i) => Math.round((i / steps) * m))
  }, [trendMaxVal])

  const handleChartHover = useCallback((e, idx) => {
    const svg = e.currentTarget.closest('svg')
    if (!svg) return
    const r = svg.getBoundingClientRect()
    const labels = trendData?.labels || []
    setTooltip({
      screenX: e.clientX - r.left,
      screenY: e.clientY - r.top,
      timeLabel: labels[idx] || '',
      items: trendChartData.map(s => ({
        label: s.label,
        color: s.color,
        value: s.points[idx]?.val || 0,
      })),
    })
  }, [trendChartData, trendData])

  const agentStats = useMemo(() => {
    return agentData.map(a => ({
      name: a.agentName || '',
      assigned: a.assigned || 0,
      resolved: a.resolved || 0,
      open: a.open || 0,
      slaPct: a.slaPercentage ?? 0,
      avgResolution: a.avgResolutionTime || '-',
    }))
  }, [agentData])

  if (loading) {
    return (
      <div className="dashboard">
        <div className="skeleton-card">
          <div style={{ padding: '12px 14px 14px' }}>
            <div style={{ display: 'flex', gap: 8, marginBottom: 10 }}>
              <div className="skeleton skeleton-circle" style={{ width: 20, height: 20 }} />
              <div className="skeleton skeleton-text" style={{ width: 100, height: 14 }} />
            </div>
            <div className="dash-kpi-grid">
              {[...Array(6)].map((_, i) => (
                <div key={i} className="skeleton-kpi">
                  <div className="skeleton skeleton-circle" />
                  <div className="skeleton-body">
                    <div className="skeleton skeleton-text" />
                    <div className="skeleton skeleton-count" />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
        <div className="skeleton-card">
          <div style={{ padding: '12px 14px 12px' }}>
            <div style={{ display: 'flex', gap: 8, marginBottom: 10 }}>
              <div className="skeleton skeleton-circle" style={{ width: 20, height: 20 }} />
              <div className="skeleton skeleton-text" style={{ width: 160, height: 14 }} />
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8 }}>
              {[...Array(3)].map((_, i) => (
                <div key={i} className="skeleton" style={{ height: 60, borderRadius: 8 }} />
              ))}
            </div>
          </div>
        </div>
        <div className="skeleton-card">
          <div style={{ padding: '12px 14px 12px' }}>
            <div style={{ display: 'flex', gap: 8, marginBottom: 10 }}>
              <div className="skeleton skeleton-circle" style={{ width: 20, height: 20 }} />
              <div className="skeleton skeleton-text" style={{ width: 200, height: 14 }} />
            </div>
            <div className="skeleton" style={{ height: 140, borderRadius: 8 }} />
          </div>
        </div>
        <div className="skeleton-card" style={{ minHeight: 220 }}>
          <div style={{ padding: '12px 14px 12px' }}>
            <div style={{ display: 'flex', gap: 8, marginBottom: 10 }}>
              <div className="skeleton skeleton-circle" style={{ width: 20, height: 20 }} />
              <div className="skeleton skeleton-text" style={{ width: 130, height: 14 }} />
            </div>
            <div className="skeleton" style={{ height: 180, borderRadius: 8 }} />
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="dashboard">
      <div className="dash-card">
        <div className="dash-card-header">
          <div className="dash-card-header-row">
            <SectionHeader number="1" title="Key Metrics" />
            <div className="dash-filter-group">
              <i className="fas fa-calendar-alt" style={{ color: '#6B7280', fontSize: '.82rem' }} />
              <select className="dash-fselect" value={datePreset} onChange={e => setDatePreset(e.target.value)}>
                {FILTER_PRESETS.map(p => <option key={p} value={p}>{p}</option>)}
              </select>
            </div>
          </div>
        </div>
        <div className="dash-kpi-grid">
          {KPI_CONFIG.map(k => (
            <div key={k.key} className="dash-kpi-card">
              <div className="dash-kpi-icon" style={{ background: k.bg, color: k.color }}><i className={`fas ${k.icon}`} /></div>
              <div className="dash-kpi-body">
                <div className="dash-kpi-label">{k.label}</div>
                <div className="dash-kpi-count">{statusCounts[k.key] ?? 0}</div>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="dash-card">
        <div className="dash-card-header"><SectionHeader number="2" title="SLA Compliance Summary" /></div>
        <div className="dash-sla-row">
          <div className="dash-mini-card">
            <div className="dash-mini-icon" style={{ background: '#FEE2E2', color: '#DC2626' }}><i className="fas fa-exclamation-triangle" /></div>
            <div className="dash-mini-body"><div className="dash-mini-label">SLA Breaches</div><div className="dash-mini-count" style={{ color: '#DC2626' }}>{slaCompliance.breached}</div></div>
          </div>
          <div className="dash-mini-card">
            <div className="dash-mini-icon" style={{ background: '#FEF3C7', color: '#D97706' }}><i className="fas fa-clock" /></div>
            <div className="dash-mini-body"><div className="dash-mini-label">Avg Resolution Time</div><div className="dash-mini-count">{slaCompliance.avgResolutionTime}</div></div>
          </div>
          <div className="dash-mini-card">
            <div className="dash-mini-icon" style={{ background: '#D1FAE5', color: '#10B981' }}><i className="fas fa-shield" /></div>
            <div className="dash-mini-body"><div className="dash-mini-label">SLA Compliance</div><div className="dash-mini-count" style={{ color: '#10B981' }}>{slaCompliance.compliancePct}%</div></div>
          </div>
        </div>
      </div>

      <div className="dash-card">
        <div className="dash-card-header"><SectionHeader number="3" title="Ticket Distribution by Priority" /></div>
        {priorityDist.length === 0 ? (
          <EmptyState icon="fa-chart-pie" title="No tickets yet" description="Priority distribution will appear once tickets are created." />
        ) : (
          <div className="dash-dist-body">
            <div className="dash-donut-col">
              <div className="dash-donut">
                <svg viewBox="0 0 200 200" width="140" height="140">
                  <g transform="rotate(-90 100 100)">
                    {donutSegments.map((seg, i) => (
                      <circle key={i} cx="100" cy="100" r="80" fill="none" stroke={seg.color} strokeWidth="22"
                        strokeDasharray={`${seg.dash} ${C_ - seg.dash}`} strokeDashoffset={-seg.offset} strokeLinecap="butt" />
                    ))}
                    <circle cx="100" cy="100" r="52" fill="#fff" />
                  </g>
                </svg>
                <div className="dash-donut-center">
                  <div className="dash-donut-total">{stats?.total || 0}</div>
                  <div className="dash-donut-total-label">TOTAL</div>
                </div>
              </div>
            </div>
            <div className="dash-dist-list">
              {priorityDist.map(d => (
                <div key={d.key} className="dash-dist-row">
                  <div className="dash-dist-row-left"><span className="dash-legend-dot" style={{ background: d.color }} /><span className="dash-dist-label">{d.label}</span></div>
                  <div className="dash-dist-row-right"><span className="dash-dist-count">{d.count}</span><span className="dash-dist-pct">{d.pct}%</span></div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      <div className="dash-card dash-card-grow">
        <div className="dash-card-header"><SectionHeader number="4" title="Ticket Trends" /></div>
        <div className="dash-chart-subtitle">Created vs Resolved — {dateRange.label}</div>
        <div className="dash-trend-chart">
          {trendChartData[0]?.points.length === 0 ? (
            <EmptyState icon="fa-chart-line" title="No trend data" description="Ticket trends will appear once tickets are created in this period." />
          ) : (
            <>
              <div className="dash-chart-wrap">
                <svg ref={chartRef} viewBox="0 0 600 300" preserveAspectRatio="xMidYMid meet">
                  {yLabels.map((val, i) => (
                    <g key={i}>
                      <line x1="48" y1={48 + (yLabels.length - 1 - i) * (200 / Math.max(yLabels.length - 1, 1))} x2="555" y2={48 + (yLabels.length - 1 - i) * (200 / Math.max(yLabels.length - 1, 1))} stroke="#E5E7EB" strokeWidth="1" />
                      <text x="40" y={52 + (yLabels.length - 1 - i) * (200 / Math.max(yLabels.length - 1, 1))} textAnchor="end" fontSize="10" fill="#9CA3AF">{val}</text>
                    </g>
                  ))}
                  {trendChartData.map(s => (
                    <g key={s.key}>
                      <path d={buildSmoothPath(s.points)} fill="none" stroke={s.color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                      {s.points.map((p, i) => (
                        <circle key={i} cx={p.x} cy={p.y} r="3" fill={s.color} stroke="#fff" strokeWidth="1.5" />
                      ))}
                    </g>
                  ))}
                  {trendChartData[0]?.points.map((p, i) => (
                    <rect key={i} x={p.x - 8} y={48} width="16" height={200} fill="transparent"
                      onMouseEnter={(e) => handleChartHover(e, i)}
                      onMouseLeave={() => setTooltip(null)} />
                  ))}
                  {xAxisLabels.map(l => {
                    const np = trendChartData[0]?.points.length || 1
                    const x = 48 + (l.index / Math.max(np - 1, 1)) * (555 - 48)
                    return <text key={l.index} x={x} y="280" textAnchor="middle" fontSize="9" fill="#9CA3AF">{l.label}</text>
                  })}
                </svg>
                {tooltip && (
                  <div className="dash-tt" style={{ left: Math.min(tooltip.screenX + 16, 400), top: Math.max(tooltip.screenY - 10, 0) }}>
                    {tooltip.timeLabel && <div className="dash-tt-time">{tooltip.timeLabel}</div>}
                    {tooltip.items.filter(it => it.value > 0).map(it => (
                      <div key={it.label} className="dash-tt-row"><span className="dash-tt-dot" style={{ background: it.color }} /><span className="dash-tt-label">{it.label}</span><span className="dash-tt-val">{it.value}</span></div>
                    ))}
                  </div>
                )}
              </div>
              <div className="dash-trend-legend">
                {series.map(s => (
                  <span key={s.key} className="dash-trend-legend-item"><span className="dash-trend-dot" style={{ background: s.color }} /> {s.label}</span>
                ))}
              </div>
            </>
          )}
        </div>
      </div>

      {isAdmin && (
      <div className="dash-card dash-card-fill">
        <div className="dash-card-header"><SectionHeader number="5" title="Agent Performance" /></div>
        <div className="dash-agent-scroll">
          <div className="dash-agent-table">
            <div className="dash-agent-thead">
              <span className="dash-ag-col-name">Agent</span>
              <span className="dash-ag-col-num">Assigned</span>
              <span className="dash-ag-col-num">Resolved</span>
              <span className="dash-ag-col-num">Open</span>
              <span className="dash-ag-col-num">SLA %</span>
              <span className="dash-ag-col-res">Avg Resolution</span>
            </div>
            {agentStats.length === 0 ? (
              <EmptyState icon="fa-users" title="No agents" description="Agent performance data will appear once users are assigned tickets." />
            ) : agentStats.map(a => {
              const pct = a.slaPct
              const badgeClass = pct >= 95 ? 'excellent' : pct >= 80 ? 'good' : pct >= 60 ? 'warning' : 'poor'
              return (
                <div key={a.name} className="dash-agent-trow">
                  <span className="dash-ag-col-name dash-agent-name-cell">
                    <span className="dash-agent-avatar">{a.name.charAt(0)}</span>{a.name}
                  </span>
                  <span className="dash-ag-col-num">{a.assigned}</span>
                  <span className="dash-ag-col-num">{a.resolved}</span>
                  <span className="dash-ag-col-num">{a.open}</span>
                  <span className="dash-ag-col-num"><span className={`dash-sla-badge ${badgeClass}`}>{pct}%</span></span>
                  <span className="dash-ag-col-res dash-agent-res-cell">{a.avgResolution}</span>
                </div>
              )
            })}
          </div>
        </div>
      </div>
      )}
    </div>
  )
}
