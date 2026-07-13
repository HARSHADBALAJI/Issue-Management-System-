export default function KPICards({ tickets }) {
  const total = tickets.length
  const ip = tickets.filter(t => t.status === 'in_progress').length
  const waiting = tickets.filter(t => t.status === 'waiting').length
  const resolved = tickets.filter(t => t.status === 'resolved').length
  const closed = tickets.filter(t => t.status === 'closed').length
  const open = ip + waiting + resolved

  const cards = [
    { icon: 'fa-ticket', count: total, label: 'Total' },
    { icon: 'fa-envelope-open', count: open, label: 'Open' },
    { icon: 'fa-chart-line', count: ip, label: 'In Progress' },
    { icon: 'fa-clock', count: waiting, label: 'Waiting' },
    { icon: 'fa-check-circle', count: resolved, label: 'Resolved' },
    { icon: 'fa-archive', count: closed, label: 'Closed' },
  ]

  return (
    <div className="kpi-row">
      {cards.map(c => (
        <div className="kpi-card" key={c.label}>
          {c.icon && (
            <div className="kpi-icon"><i className={`fas ${c.icon}`} /></div>
          )}
          <div className="kpi-body">
            <div className="kpi-count">{c.count.toLocaleString()}</div>
            <div className="kpi-label">{c.label}</div>
            {c.sub && <div className="kpi-sub">{c.sub}</div>}
          </div>
        </div>
      ))}
    </div>
  )
}
