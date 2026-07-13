export default function StatusTabs({ tickets, activeTab, onTabChange }) {
  const all = tickets.length
  const ip = tickets.filter(t => t.status === 'in_progress').length
  const waiting = tickets.filter(t => t.status === 'waiting').length
  const resolved = tickets.filter(t => t.status === 'resolved').length
  const closed = tickets.filter(t => t.status === 'closed').length
  const open = ip + waiting + resolved

  const tabs = [
    { key: 'all', label: 'All', badge: all },
    { key: 'open', label: 'Open', badge: open },
    { key: 'in_progress', label: 'In Progress', badge: ip },
    { key: 'waiting', label: 'Waiting', badge: waiting },
    { key: 'resolved', label: 'Resolved', badge: resolved },
    { key: 'closed', label: 'Closed', badge: closed },
  ]

  return (
    <div className="tabs">
      {tabs.map(t => (
        <button
          key={t.key}
          className={`tab${activeTab === t.key ? ' active' : ''}`}
          onClick={() => onTabChange(t.key)}
        >
          {t.label}
          <span className="tab-badge">{t.badge}</span>
        </button>
      ))}
    </div>
  )
}
