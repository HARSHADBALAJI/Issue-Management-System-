import { useState, useMemo, useRef, useCallback, useEffect } from 'react'
import { dashboardService } from '../services/dashboardService'

function sameDay(a, b) {
  return a.getDate() === b.getDate() && a.getMonth() === b.getMonth() && a.getFullYear() === b.getFullYear()
}

function formatResolution(hours) {
  if (!hours && hours !== 0) return '-'
  const d = Math.floor(hours / 24)
  const h = Math.round(hours % 24)
  if (d > 0) return `${d}d ${h}h`
  return `${h}h`
}

const PRIORITY_COLORS = { critical: '#DC2626', high: '#EA580C', medium: '#D97706', low: '#2563EB', informational: '#64748B' }
const PRIORITY_LABELS = { critical: 'Critical', high: 'High', medium: 'Medium', low: 'Low', informational: 'Informational' }

const KPI_CONFIG = [
  { key: 'total', label: 'Total Tickets', icon: 'fa-ticket', color: '#2563EB', bg: '#EFF6FF' },
  { key: 'open', label: 'Open', icon: 'fa-envelope-open', color: '#0D6EFD', bg: '#E3F2FD' },
  { key: 'in_progress', label: 'In Progress', icon: 'fa-spinner', color: '#FFC107', bg: '#FFF8E1' },
  { key: 'waiting', label: 'Waiting', icon: 'fa-clock', color: '#FD7E14', bg: '#FFF3E0' },
  { key: 'resolved', label: 'Resolved', icon: 'fa-check-circle', color: '#198754', bg: '#D4EDDA' },
  { key: 'closed', label: 'Closed', icon: 'fa-circle-check', color: '#6C757D', bg: '#E9ECEF' },
]

const FILTER_PRESETS = ['Today', 'Yesterday', 'Last 7 Days', 'Last 30 Days', 'This Month', 'Custom Range']

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

const C_ = 2 * Math.PI * 80

function SectionHeader({ number, title }) {
  return (
    <div className="dash-section-header">
      <span className="dash-section-badge">{number}</span>
      <h2 className="dash-section-title">{title}</h2>
    </div>
  )
}

function SectionFilter({ open, filters, onChange, onClose, dateLabel }) {
  if (!open) return null
  return (
    <div className="dash-sfilter-overlay" onClick={onClose}>
      <div className="dash-sfilter-panel" onClick={e => e.stopPropagation()}>
        <div className="dash-sfilter-date">{dateLabel}</div>
        <div className="dash-sfilter-row">
          <label>Status</label>
            <select value={filters.status} onChange={e => onChange({ ...filters, status: e.target.value })}>
              <option value="">All</option>
              <option value="open">Open</option>
              <option value="in_progress">In Progress</option>
              <option value="waiting">Waiting</option>
              <option value="resolved">Resolved</option>
              <option value="closed">Closed</option>
            </select>
        </div>
        <div className="dash-sfilter-row">
          <label>Priority</label>
          <select value={filters.priority} onChange={e => onChange({ ...filters, priority: e.target.value })}>
            <option value="">All</option>
            <option value="critical">Critical</option>
            <option value="high">High</option>
            <option value="medium">Medium</option>
            <option value="low">Low</option>
          </select>
        </div>
        <div className="dash-sfilter-actions">
          <button className="dash-sfilter-btn" onClick={() => { onChange({ status: '', priority: '' }); onClose() }}>Reset</button>
          <button className="dash-sfilter-btn dash-sfilter-btn-primary" onClick={onClose}>Apply</button>
        </div>
      </div>
    </div>
  )
}

export default function Dashboard({ tickets, users, applications }) {
  const [datePreset, setDatePreset] = useState('Last 30 Days')
  const [customFrom, setCustomFrom] = useState('')
  const [customTo, setCustomTo] = useState('')
  const [filterApp, setFilterApp] = useState('')
  const [filterDept, setFilterDept] = useState('')
  const [filterUser, setFilterUser] = useState('')
  const [filterStatus, setFilterStatus] = useState('')
  const [sfOpen, setSfOpen] = useState(false)
  const [sfFilters, setSfFilters] = useState({ status: '', priority: '' })
  const [kmFilterOpen, setKmFilterOpen] = useState(false)
  const [tooltip, setTooltip] = useState(null)
  const chartRef = useRef(null)
  const TOOLTIP_LABELS = { created: 'Created', resolved: 'Resolved', slaBreached: 'SLA Breached', reopened: 'Reopened' }

  const [stats, setStats] = useState(null)
  const [agentData, setAgentData] = useState([])
  const [slaData, setSlaData] = useState(null)
  const [trendData, setTrendData] = useState([])

  useEffect(() => {
    dashboardService.getStats().then(r => setStats(r)).catch(() => {})
    dashboardService.getAgentPerformance().then(r => setAgentData(r.items || r || [])).catch(() => {})
    dashboardService.getSla().then(r => {
      if (Array.isArray(r)) {
        const breached = r.filter(s => s.percentage < 0).length
        setSlaData({ breached, withinSLA: r.length - breached, compliancePct: r.length > 0 ? Math.round(((r.length - breached) / r.length) * 100) : 100, avgFormatted: '-' })
      } else { setSlaData(r) }
    }).catch(() => {})
    dashboardService.getTrends().then(r => {
      if (r?.series && Array.isArray(r.series)) {
        const created = r.series.find(s => s.key === 'created')?.data || []
        const resolved = r.series.find(s => s.key === 'resolved')?.data || []
        setTrendData(created.map((val, i) => ({ created: val, resolved: resolved[i] || 0, slaBreached: 0, reopened: 0 })))
      } else { setTrendData(r || []) }
    }).catch(() => {})
  }, [])

  const filterOptions = useMemo(() => {
    const apps = [...new Set(tickets.map(t => t.application))].sort()
    const depts = [...new Set(tickets.map(t => t.department))].sort()
    const usersSet = [...new Set(tickets.map(t => t.assignedTo).filter(Boolean))].sort()
    return { apps, depts, users: usersSet, statuses: ['open', 'in_progress', 'waiting', 'resolved', 'closed'] }
  }, [tickets])

  const dateRange = useMemo(() => {
    const now = new Date()
    const end = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59, 999)
    let start
    switch (datePreset) {
      case 'Today':
        start = new Date(end.getFullYear(), end.getMonth(), end.getDate(), 0, 0, 0, 0)
        return { from: start, to: end, label: end.toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' }) }
      case 'Yesterday':
        start = new Date(end); start.setDate(start.getDate() - 1); start.setHours(0, 0, 0, 0)
        end.setDate(end.getDate() - 1)
        return { from: start, to: end, label: start.toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' }) }
      case 'Last 7 Days':
        start = new Date(end); start.setDate(start.getDate() - 6); start.setHours(0, 0, 0, 0); break
      case 'Last 30 Days':
        start = new Date(end); start.setDate(start.getDate() - 29); start.setHours(0, 0, 0, 0); break
      case 'This Month':
        start = new Date(end.getFullYear(), end.getMonth(), 1, 0, 0, 0, 0); break
      case 'Custom Range':
        start = customFrom ? new Date(customFrom + 'T00:00:00') : new Date(0)
        const custEnd = customTo ? new Date(customTo + 'T23:59:59') : new Date(end)
        return { from: start, to: custEnd, label: `${start.toLocaleDateString()} — ${custEnd.toLocaleDateString()}` }
      default:
        start = new Date(end); start.setDate(start.getDate() - 29); start.setHours(0, 0, 0, 0)
    }
    return { from: start, to: end, label: `${start.toLocaleDateString()} — ${end.toLocaleDateString()}` }
  }, [datePreset, customFrom, customTo])

  const filteredTickets = useMemo(() => {
    return tickets.filter(t => {
      if (t.updated < dateRange.from || t.updated > dateRange.to) return false
      if (filterApp && t.application !== filterApp) return false
      if (filterDept && t.department !== filterDept) return false
      if (filterUser && t.assignedTo !== filterUser) return false
      if (filterStatus && t.status !== filterStatus) return false
      return true
    })
  }, [tickets, dateRange, filterApp, filterDept, filterUser, filterStatus])

  const sectionTickets = useMemo(() => {
    return filteredTickets.filter(t => {
      if (sfFilters.status && t.status !== sfFilters.status) return false
      return true
    })
  }, [filteredTickets, sfFilters])

  const statusCounts = useMemo(() => {
    if (stats) return {
      total: stats.total || 0,
      open: (stats.inProgress || 0) + (stats.waiting || 0) + (stats.resolved || 0),
      in_progress: stats.inProgress || 0,
      waiting: stats.waiting || 0,
      resolved: stats.resolved || 0,
      closed: stats.closed || 0,
      sla_breached: stats.slaBreached || 0,
    }
    let total = filteredTickets.length, openStatus = 0, in_progress = 0, waiting = 0, resolved = 0, closed = 0
    filteredTickets.forEach(t => {
      if (t.status === 'open') openStatus++
      else if (t.status === 'in_progress') in_progress++
      else if (t.status === 'waiting') waiting++
      else if (t.status === 'resolved') resolved++
      else if (t.status === 'closed') closed++
    })
    const open = in_progress + waiting + resolved
    const slaBreached = filteredTickets.filter(t => t.isSlaBreached || t.sla?.startsWith('Overdue')).length
    return { total, open, in_progress, waiting, resolved, closed, sla_breached: slaBreached }
  }, [filteredTickets, stats])

  const slaCompliance = useMemo(() => {
    if (slaData) return slaData
    const total = filteredTickets.length || 1
    const breached = filteredTickets.filter(t => t.isSlaBreached || t.sla?.startsWith('Overdue')).length
    const withinSLA = total - breached
    const compliancePct = total > 0 ? Math.round((withinSLA / total) * 100) : 100
    return { breached, withinSLA, compliancePct, avgFormatted: '-' }
  }, [filteredTickets, slaData])

  const priorityDist = useMemo(() => {
    const counts = { critical: 0, high: 0, medium: 0, low: 0, informational: 0 }
    filteredTickets.forEach((t, i) => {
      const p = t.priority?.toLowerCase() || ['critical', 'high', 'medium', 'low', 'informational'][i % 5]
      counts[p] = (counts[p] || 0) + 1
    })
    return Object.entries(counts).filter(([_, c]) => c > 0).map(([key, count]) => ({
      key, label: PRIORITY_LABELS[key] || key, color: PRIORITY_COLORS[key] || '#64748B', count,
      pct: filteredTickets.length > 0 ? ((count / filteredTickets.length) * 100).toFixed(1) : '0',
    }))
  }, [filteredTickets])

  const donutSegments = useMemo(() => {
    let cumulative = 0
    return priorityDist.map(d => {
      const dash = (d.count / (filteredTickets.length || 1)) * C_
      const seg = { ...d, dash, offset: cumulative }
      cumulative += dash
      return seg
    })
  }, [priorityDist, filteredTickets.length])

  const agentStats = useMemo(() => {
    if (agentData.length > 0) return agentData.map(a => ({
      name: a.agentName || a.name || '',
      assigned: a.assigned || a.totalAssigned || 0,
      resolved: a.resolved || a.totalResolved || 0,
      open: (a.assigned || a.totalAssigned || 0) - (a.resolved || a.totalResolved || 0),
      avgResolution: formatResolution(a.avgResolutionHours || a.averageResolutionHours || 0),
      slaPct: (a.slaPercentage || a.slaPct || 0).toString(),
    }))
    const agents = {}
    sectionTickets.forEach(t => {
      if (!t.assignedTo) return
      if (!agents[t.assignedTo]) agents[t.assignedTo] = { assigned: 0, resolved: 0, slaOk: 0 }
      agents[t.assignedTo].assigned++
      if (t.status === 'resolved' || t.status === 'closed') agents[t.assignedTo].resolved++
      if (!t.isSlaBreached && !t.sla?.startsWith('Overdue')) agents[t.assignedTo].slaOk++
    })
    return Object.entries(agents).map(([name, s]) => ({
      name, assigned: s.assigned, resolved: s.resolved,
      open: s.assigned - s.resolved,
      avgResolution: '-',
      slaPct: s.assigned > 0 ? ((s.slaOk / s.assigned) * 100).toFixed(1) : '0',
    }))
  }, [sectionTickets, agentData])

  const isSingleDay = useMemo(() => sameDay(dateRange.from, dateRange.to), [dateRange])

  const xAxisLabels = useMemo(() => {
    if (isSingleDay) return [{ index: 0, label: '12 AM' }, { index: 4, label: '04 AM' }, { index: 8, label: '08 AM' }, { index: 12, label: '12 PM' }, { index: 16, label: '04 PM' }, { index: 20, label: '08 PM' }, { index: 23, label: '11 PM' }]
    const days = Math.ceil((dateRange.to - dateRange.from) / (1000 * 60 * 60 * 24))
    const numPoints = Math.min(days, 30)
    if (numPoints <= 7) return Array.from({ length: numPoints }, (_, i) => { const d = new Date(dateRange.from); d.setDate(d.getDate() + i); return { index: i, label: d.toLocaleDateString('en-US', { weekday: 'short' }) } })
    const labels = []
    for (let i = 0; i < numPoints; i += 7) { const d = new Date(dateRange.from); d.setDate(d.getDate() + i); labels.push({ index: i, label: d.toLocaleDateString('en-US', { day: '2-digit', month: 'short' }) }) }
    if (labels.length > 0 && labels[labels.length - 1].index !== numPoints - 1) { const d = new Date(dateRange.from); d.setDate(d.getDate() + numPoints - 1); labels.push({ index: numPoints - 1, label: d.toLocaleDateString('en-US', { day: '2-digit', month: 'short' }) }) }
    return labels
  }, [isSingleDay, dateRange])

  const trendChartData = useMemo(() => {
    const CHART_LEFT = 48, CHART_RIGHT = 555, CHART_W = CHART_RIGHT - CHART_LEFT, CHART_BOTTOM = 248, CHART_H = 200
    const series = [{ key: 'created', color: '#2563EB' }, { key: 'resolved', color: '#22C55E' }, { key: 'slaBreached', color: '#EF4444' }, { key: 'reopened', color: '#F59E0B' }]
    const days = Math.ceil((dateRange.to - dateRange.from) / (1000 * 60 * 60 * 24))
    const numPoints = Math.min(days, 30)

    if (trendData.length > 0) {
      const points = trendData.slice(0, numPoints)
      const maxVal = Math.max(1, ...points.flatMap(d => [d.created || 0, d.resolved || 0, d.slaBreached || d.sla_breached || 0, d.reopened || 0]))
      return series.map(s => ({
        ...s,
        points: points.map((d, i) => ({
          x: CHART_LEFT + (i / Math.max(numPoints - 1, 1)) * CHART_W,
          y: CHART_BOTTOM - ((d[s.key === 'slaBreached' ? 'sla_breached' : s.key] || 0) / maxVal) * CHART_H,
        })),
      }))
    }

    if (isSingleDay) {
      return series.map(s => ({
        ...s,
        points: Array.from({ length: 24 }, (_, i) => ({
          x: CHART_LEFT + (i / 23) * CHART_W,
          y: CHART_BOTTOM - (0.5 / 5) * CHART_H,
        })),
      }))
    }

    const dayData = []
    for (let i = 0; i < numPoints; i++) {
      const d = new Date(dateRange.from); d.setDate(d.getDate() + i)
      const dayTickets = sectionTickets.filter(t => sameDay(t.updated || t.created, d))
      dayData.push({
        created: dayTickets.length,
        resolved: dayTickets.filter(t => t.status === 'resolved' || t.status === 'closed').length,
        slaBreached: dayTickets.filter(t => t.isSlaBreached).length,
        reopened: Math.round(dayTickets.length * 0.05),
      })
    }
    const maxVal = Math.max(1, ...dayData.flatMap(d => [d.created, d.resolved, d.slaBreached, d.reopened]))
    return series.map(s => ({
      ...s,
      points: dayData.map((d, i) => ({
        x: CHART_LEFT + (i / Math.max(numPoints - 1, 1)) * CHART_W,
        y: CHART_BOTTOM - (d[s.key] / maxVal) * CHART_H,
      })),
    }))
  }, [isSingleDay, dateRange, sectionTickets, trendData])

  const yLabels = useMemo(() => {
    const series = trendChartData[0]
    if (!series) return [0, 1, 2, 3, 4]
    let maxV = 0
    series.points.forEach(p => { maxV = Math.max(maxV, 248 - p.y) })
    const m = Math.max(Math.ceil(maxV / 200), 1)
    return Array.from({ length: m + 1 }, (_, i) => i)
  }, [trendChartData])

  const handleChartMove = useCallback((e) => {
    const svg = chartRef.current
    if (!svg) return
    const rect = svg.getBoundingClientRect()
    const vb = { w: 600, h: 300 }
    const sx = vb.w / rect.width
    const mx = (e.clientX - rect.left) * sx
    if (mx < 48 || mx > 555) { setTooltip(null); return }
    const np = trendChartData[0]?.points.length || 0
    if (np < 2) return
    const idx = Math.round((mx - 48) / (555 - 48) * (np - 1))
    const ci = Math.max(0, Math.min(np - 1, idx))
    const tl = xAxisLabels.find(l => l.index === ci)?.label || ''
    const posX = trendChartData[0].points[ci].x
    const posY = trendChartData[0].points[ci].y - 12
    const sy = vb.h / rect.height
    const sx2 = rect.width / vb.w
    const screenX = posX * sx2
    const screenY = posY * sy
    setTooltip({
      screenX, screenY, timeLabel: tl,
      items: trendChartData.map(s => ({
        label: TOOLTIP_LABELS[s.key], color: s.color,
        value: Math.round((248 - s.points[ci].y) / 200 * (yLabels[yLabels.length - 1] || 1)),
      })),
    })
  }, [trendChartData, xAxisLabels, yLabels])

  const clearTooltip = useCallback(() => setTooltip(null), [])

  return (
    <div className="dashboard">
      <div className="dash-card">
        <div className="dash-card-header">
          <div className="dash-card-header-row">
            <SectionHeader number="1" title="Key Metrics" />
            <button className={`dash-funnel-btn${kmFilterOpen ? ' active' : ''}`} title="Global filters" onClick={() => setKmFilterOpen(!kmFilterOpen)}>
              <i className="fas fa-filter" />
            </button>
          </div>
          {kmFilterOpen && (
            <div className="dash-kmfilter-panel">
              <div className="dash-filter-group">
                <i className="fas fa-calendar-alt" style={{ color: '#6B7280', fontSize: '.82rem' }} />
                <select className="dash-fselect" value={datePreset} onChange={e => setDatePreset(e.target.value)}>
                  {FILTER_PRESETS.map(p => <option key={p} value={p}>{p}</option>)}
                </select>
                {datePreset === 'Custom Range' && (
                  <><input type="date" className="dash-finput" value={customFrom} onChange={e => setCustomFrom(e.target.value)} /><span className="dash-fsep">to</span><input type="date" className="dash-finput" value={customTo} onChange={e => setCustomTo(e.target.value)} /></>
                )}
              </div>
              <div className="dash-filter-group">
                <i className="fas fa-cube" style={{ color: '#6B7280', fontSize: '.82rem' }} />
                <select className="dash-fselect" value={filterApp} onChange={e => setFilterApp(e.target.value)}>
                  <option value="">All Apps</option>
                  {filterOptions.apps.map(a => <option key={a} value={a}>{a}</option>)}
                </select>
              </div>
              <div className="dash-filter-group">
                <i className="fas fa-building" style={{ color: '#6B7280', fontSize: '.82rem' }} />
                <select className="dash-fselect" value={filterDept} onChange={e => setFilterDept(e.target.value)}>
                  <option value="">All Depts</option>
                  {filterOptions.depts.map(d => <option key={d} value={d}>{d}</option>)}
                </select>
              </div>
              <div className="dash-filter-group">
                <i className="fas fa-user" style={{ color: '#6B7280', fontSize: '.82rem' }} />
                <select className="dash-fselect" value={filterUser} onChange={e => setFilterUser(e.target.value)}>
                  <option value="">All Users</option>
                  {filterOptions.users.map(u => <option key={u} value={u}>{u}</option>)}
                </select>
              </div>
              <div className="dash-filter-group">
                <i className="fas fa-tag" style={{ color: '#6B7280', fontSize: '.82rem' }} />
                <select className="dash-fselect" value={filterStatus} onChange={e => setFilterStatus(e.target.value)}>
                  <option value="">All Status</option>
                  <option value="open">Open</option><option value="in_progress">In Progress</option><option value="waiting">Waiting</option><option value="resolved">Resolved</option><option value="closed">Closed</option>
                </select>
              </div>
            </div>
          )}
        </div>
        <div className="dash-kpi-grid" style={{ padding: '12px 16px 16px', marginBottom: 0 }}>
          {KPI_CONFIG.map(k => (
            <div key={k.key} className="dash-kpi-card">
              <div className="dash-kpi-icon" style={{ background: k.bg, color: k.color }}><i className={`fas ${k.icon}`} /></div>
              <div className="dash-kpi-body">
                <div className="dash-kpi-label">{k.label}</div>
                <div className="dash-kpi-count">{statusCounts[k.key] || 0}</div>
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
            <div className="dash-mini-body"><div className="dash-mini-label">Avg Resolution Time</div><div className="dash-mini-count">{slaCompliance.avgFormatted || '-'}</div></div>
          </div>
          <div className="dash-mini-card">
            <div className="dash-mini-icon" style={{ background: '#D1FAE5', color: '#10B981' }}><i className="fas fa-shield" /></div>
            <div className="dash-mini-body"><div className="dash-mini-label">SLA Compliance</div><div className="dash-mini-count" style={{ color: '#10B981' }}>{slaCompliance.compliancePct}%</div></div>
          </div>
        </div>
      </div>

      <div className="dash-card">
        <div className="dash-card-header"><SectionHeader number="3" title="Ticket Distribution" /></div>
        <div className="dash-dist-body">
          <div className="dash-donut-col">
            <div className="dash-donut" style={{ width: 140, height: 140 }}>
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
                <div className="dash-donut-total" style={{ fontSize: '1.2rem' }}>{filteredTickets.length}</div>
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
      </div>

      <div className="dash-card dash-card-grow">
        <div className="dash-card-header">
          <div className="dash-card-header-row">
            <SectionHeader number="4" title="Trends &amp; Team Performance" />
            <button className="dash-funnel-btn" title="Section filters" onClick={() => setSfOpen(true)}><i className="fas fa-filter" /></button>
          </div>
        </div>
        <div className="dash-chart-subtitle">Ticket Trend — {dateRange.label}</div>
        <div className="dash-trend-chart">
          <div className="dash-chart-wrap" style={{ position: 'relative' }}>
            <svg ref={chartRef} viewBox="0 0 600 300" width="100%" height="100%" style={{ display: 'block' }} preserveAspectRatio="xMidYMid meet" onMouseMove={handleChartMove} onMouseLeave={clearTooltip}>
              {yLabels.map((val, i) => (
                <line key={i} x1="48" y1={48 + (yLabels[yLabels.length - 1] - val) * (200 / Math.max(yLabels.length - 1, 1))} x2="555" y2={48 + (yLabels[yLabels.length - 1] - val) * (200 / Math.max(yLabels.length - 1, 1))} stroke="#E5E7EB" strokeWidth="1" />
              ))}
              {yLabels.map((val, i) => (
                <text key={i} x="40" y={52 + (yLabels[yLabels.length - 1] - val) * (200 / Math.max(yLabels.length - 1, 1))} textAnchor="end" fontSize="10" fill="#9CA3AF">{val}</text>
              ))}
              {trendChartData.map(s => (
                <g key={s.key}>
                  <path d={buildSmoothPath(s.points)} fill="none" stroke={s.color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                  {s.points.filter((_, i) => isSingleDay ? i % 4 === 0 || i === 23 : xAxisLabels.some(l => l.index === i)).map((p, i) => (
                    <circle key={i} cx={p.x} cy={p.y} r="3.5" fill={s.color} stroke="#fff" strokeWidth="1.5" />
                  ))}
                </g>
              ))}
              {trendChartData[0]?.points.map((p, i) => (
                <rect key={i} x={p.x - 6} y={p.y - 30} width="12" height="60" fill="transparent"
                  onMouseEnter={(e) => {
                    const tl = xAxisLabels.find(l => l.index === i)?.label || ''
                    const svg = e.currentTarget.closest('svg'); if (!svg) return
                    const r = svg.getBoundingClientRect()
                    setTooltip({ screenX: (e.clientX - r.left), screenY: (e.clientY - r.top), timeLabel: tl,
                      items: trendChartData.map(s => ({ label: TOOLTIP_LABELS[s.key], color: s.color, value: Math.round((248 - s.points[i].y) / 200 * (yLabels[yLabels.length - 1] || 1)) }),
                    )})
                  }}
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
            <span className="dash-trend-legend-item"><span className="dash-trend-dot" style={{ background: '#2563EB' }} /> Created</span>
            <span className="dash-trend-legend-item"><span className="dash-trend-dot" style={{ background: '#22C55E' }} /> Resolved</span>
            <span className="dash-trend-legend-item"><span className="dash-trend-dot" style={{ background: '#EF4444' }} /> SLA Breached</span>
            <span className="dash-trend-legend-item"><span className="dash-trend-dot" style={{ background: '#F59E0B' }} /> Reopened</span>
          </div>
        </div>
      </div>

      <div className="dash-card dash-card-fill">
        <div className="dash-card-header"><SectionHeader number="5" title="Agent Resolution Performance" /></div>
        <div className="dash-agent-table">
          <div className="dash-agent-thead"><span>Agent</span><span>Assigned</span><span>Resolved</span><span>Open</span><span>SLA %</span><span>Avg Resolution</span></div>
          {agentStats.length === 0 && <div className="dash-agent-empty">No agent data available</div>}
          {agentStats.map(a => {
            const pct = parseFloat(a.slaPct)
            const badgeClass = pct >= 100 ? 'excellent' : pct >= 90 ? 'good' : pct >= 70 ? 'warning' : 'poor'
            return (
              <div key={a.name} className="dash-agent-trow">
                <span className="dash-agent-name-cell"><span className="dash-agent-avatar">{a.name.charAt(0)}</span>{a.name}</span>
                <span>{a.assigned}</span><span>{a.resolved}</span><span>{a.open}</span>
                <span><span className={`dash-sla-badge ${badgeClass}`}>{a.slaPct}%</span></span>
                <span className="dash-agent-res-cell">{a.avgResolution}</span>
              </div>
            )
          })}
        </div>
      </div>

      <SectionFilter open={sfOpen} filters={sfFilters} onChange={setSfFilters} onClose={() => setSfOpen(false)} dateLabel={dateRange.label} />
      {kmFilterOpen && <div className="dash-kmfilter-overlay" onClick={() => setKmFilterOpen(false)} />}
    </div>
  )
}
