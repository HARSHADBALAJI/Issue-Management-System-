import { useAuth } from '../contexts/AuthContext'

const adminItems = [
  { icon: 'fa-gauge', label: 'Dashboard', page: 'dashboard' },
  { icon: 'fa-ticket', label: 'Tickets', page: 'tickets' },
  { icon: 'fa-layer-group', label: 'Applications', page: 'applications' },
  { icon: 'fa-users', label: 'Users', page: 'users' },
  { icon: 'fa-building', label: 'Departments', page: 'departments' },
  { icon: 'fa-clock', label: 'SLA Settings', page: 'sla-settings' },
]

const userItems = [
  { icon: 'fa-gauge', label: 'Dashboard', page: 'dashboard' },
  { icon: 'fa-ticket', label: 'My Tickets', page: 'tickets' },
]

export default function Sidebar({ currentPage, onNavigate }) {
  const { isAdmin } = useAuth()
  const items = isAdmin ? adminItems : userItems

  return (
    <aside className="sidebar" role="navigation" aria-label="Main navigation">
      <div className="sidebar-brand">
        <div className="brand-icon">
          <img src="/site-logo.jpeg" alt="Issue Management System Logo" />
        </div>

      </div>
      <nav className="sidebar-nav">
        {items.map(item => (
          <a
            key={item.label}
            href="#"
            className={`nav-item${currentPage === item.page || (item.page === 'tickets' && currentPage === 'ticket-detail') ? ' active' : ''}`}
            aria-current={currentPage === item.page ? 'page' : undefined}
            onClick={e => {
              e.preventDefault()
              if (item.page) onNavigate(item.page)
            }}
          >
            <i className={`fas ${item.icon}`} aria-hidden="true" />
            <span>{item.label}</span>
          </a>
        ))}
      </nav>
    </aside>
  )
}
