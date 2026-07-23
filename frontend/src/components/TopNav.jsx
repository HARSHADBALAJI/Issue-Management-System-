import { useState, useRef, useEffect } from 'react'

export default function TopNav({ notifications = [], unreadCount = 0, onClearNotification, onLogout, user }) {
  const [open, setOpen] = useState(false)
  const [notifOpen, setNotifOpen] = useState(false)
  const ref = useRef(null)
  const notifRef = useRef(null)
  const initials = user?.name ? user.name.split(' ').map(s => s[0]).join('').toUpperCase().slice(0, 2) : '?'
  const avatarBg = user?.role === 'admin' ? '#2563EB' : '#10B981'

  useEffect(() => {
    const handler = e => {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false)
      if (notifRef.current && !notifRef.current.contains(e.target)) setNotifOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  return (
    <header className="topnav">
      <div className="topnav-right">
        <div ref={notifRef} style={{ position: 'relative' }}>
          <button className="icon-btn" aria-label={`Notifications${unreadCount > 0 ? ` (${unreadCount} unread)` : ''}`} onClick={() => setNotifOpen(!notifOpen)}>
            <i className="fas fa-bell" />
            {unreadCount > 0 && <span className="notif-badge">{unreadCount}</span>}
          </button>
          {notifOpen && (
            <div className="dropdown-menu show" style={{ position: 'absolute', right: 0, top: '100%', width: 320, maxHeight: 360, overflowY: 'auto', marginTop: 8 }}>
              {notifications.length === 0 ? (
                <div style={{ padding: '20px 16px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '.85rem' }}>No notifications</div>
              ) : (
                notifications.map((n, i) => (
                  <div key={n.id || i} style={{ display: 'flex', alignItems: 'flex-start', gap: 10, padding: '10px 14px', borderBottom: '1px solid var(--border)', fontSize: '.85rem', opacity: n.isRead ? 0.6 : 1 }}>
                    <i className="fas fa-info-circle" style={{ color: 'var(--primary)', marginTop: 2, flexShrink: 0 }} />
                    <div style={{ flex: 1 }}>
                      <div style={{ fontWeight: 500, color: 'var(--text)' }}>{n.title}</div>
                      <div style={{ color: 'var(--text-secondary)', fontSize: '.8rem', marginTop: 2 }}>{n.message}</div>
                      <div style={{ color: 'var(--text-muted)', fontSize: '.72rem', marginTop: 4 }}>{n.time}</div>
                    </div>
                    <button style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer', padding: 2, fontSize: '.78rem', flexShrink: 0 }} onClick={() => onClearNotification(i)}>
                      <i className="fas fa-times" />
                    </button>
                  </div>
                ))
              )}
            </div>
          )}
        </div>
        <div className="user-dropdown" ref={ref} onClick={() => setOpen(!open)}>
          <div className="user-avatar" style={{ background: avatarBg }}>{initials}</div>
          <i className="fas fa-chevron-down user-chevron" />
          {open && (
            <div className="dropdown-menu show" onClick={e => e.stopPropagation()}>
              <div style={{ padding: '8px 14px', borderBottom: '1px solid var(--border)' }}>
                <div style={{ fontWeight: 600, fontSize: '.85rem' }}>{user?.name || ''}</div>
                <div style={{ fontSize: '.78rem', color: 'var(--text-muted)' }}>{user?.email || ''}</div>
              </div>
              <a href="#" onClick={e => { e.preventDefault(); setOpen(false) }}>Profile</a>
              <a href="#" onClick={e => { e.preventDefault(); setOpen(false) }}>Settings</a>
              <hr />
              <a href="#" onClick={e => { e.preventDefault(); setOpen(false); onLogout?.() }} style={{ color: '#DC2626' }}>Sign out</a>
            </div>
          )}
        </div>
      </div>
    </header>
  )
}
