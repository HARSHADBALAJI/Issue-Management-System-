const items = [
  { icon: 'fa-gauge', label: 'Dashboard', page: 'dashboard' },
  { icon: 'fa-ticket', label: 'Tickets', page: 'tickets' },
  { icon: 'fa-layer-group', label: 'Applications', page: 'applications' },
  { icon: 'fa-users', label: 'Users', page: 'users' },
  { icon: 'fa-building', label: 'Departments', page: 'departments' },
  { icon: 'fa-clock', label: 'SLA Settings', page: 'sla-settings' },
  { icon: 'fa-gear', label: 'Settings', page: null },
]

export default function Sidebar({ currentPage, onNavigate }) {
  return (
    <aside className="sidebar">
      <div className="sidebar-brand">
        <div className="brand-icon">
          <svg viewBox="30 240 180 250" preserveAspectRatio="xMidYMid meet" xmlns="http://www.w3.org/2000/svg">
            <polygon fill="#0C0407" points="44.8,269.7 87.1,269.7 87.1,299.5 65.9,299.5 65.9,352.4 87.1,352.4 87.1,376.5 44.8,376.5"/>
            <path fill="#0C0407" d="M107,269.7h30.4l15.2,26.3l15.2-26.3h30.4l-30.4,52.7l30.4,52.7h-30.4l-15.2-26.3l-15.2,26.3H107l30.4-52.7L107,269.7z"/>
            <path fill="#004F8A" d="M65.4,376.5c0-29.1,23.6-52.7,52.7-52.7c29.1,0,52.7,23.6,52.7,52.7l-52.7,91.2L65.4,376.5z"/>
          </svg>
        </div>
        <span className="brand-name">Issue Management</span>
      </div>
      <nav className="sidebar-nav">
        {items.map(item => (
          <a
            key={item.label}
            href="#"
            className={`nav-item${currentPage === item.page || (item.page === 'tickets' && currentPage === 'ticket-detail') ? ' active' : ''}`}
            onClick={e => {
              e.preventDefault()
              if (item.page) onNavigate(item.page)
            }}
          >
            <i className={`fas ${item.icon}`} />
            <span>{item.label}</span>
          </a>
        ))}
      </nav>
    </aside>
  )
}
