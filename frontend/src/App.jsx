import { useState, useCallback, useEffect, useMemo, useRef } from 'react'
import { useAuth } from './contexts/AuthContext'
import Sidebar from './components/Sidebar'
import TopNav from './components/TopNav'
import KPICards from './components/KPICards'
import StatusTabs from './components/StatusTabs'
import Filters from './components/Filters'
import TicketTable from './components/TicketTable'
import UsersPage from './components/UsersPage'
import ApplicationsPage from './components/ApplicationsPage'
import DepartmentsPage from './components/DepartmentsPage'
import Dashboard from './components/Dashboard'
import TicketDetail from './components/TicketDetail'
import SlaSettings from './components/SlaSettings'
import { exportToCSV, getStatusMeta } from './utils/helpers'
import { ticketService } from './services/ticketService'
import { departmentService } from './services/departmentService'
import { userService } from './services/userService'
import { applicationService } from './services/applicationService'
import { notificationService } from './services/notificationService'
import { startConnection, stopConnection, joinUserGroup, onEvent, offEvent } from './services/signalRService'
import LoginPage from './components/LoginPage'
import TrackingPage from './components/TrackingPage'
import './App.css'

function normalizeTicket(t) {
  const statusMap = { 'Open': 'open', 'In Progress': 'in_progress', 'Waiting': 'waiting', 'Resolved': 'resolved', 'Closed': 'closed' }
  const status = statusMap[t.statusName] || (t.statusName || '').toLowerCase().replace(/\s+/g, '_')
  let sla = ''
  // Use API-provided SLA fields (PascalCase from DTO)
  const slaDeadline = t.SlaDeadline || t.slaDeadline
  const isSlaBreached = t.IsSlaBreached || t.isSlaBreached || false
  const slaStatus = t.SlaStatus || t.slaStatus
  const slaRemainingPercent = t.SlaRemainingPercent || t.slaRemainingPercent || 0
  const slaRemainingTime = t.SlaRemainingTime || t.slaRemainingTime

  if (slaRemainingTime && slaRemainingTime !== 'Completed') {
    sla = slaRemainingTime
  } else if (slaDeadline) {
    const deadline = new Date(slaDeadline)
    const now = new Date()
    const diff = deadline - now
    if (diff < 0) {
      const totalH = Math.floor(Math.abs(diff) / 3600000)
      const d = Math.floor(totalH / 24)
      const h = totalH % 24
      sla = d > 0 ? `Overdue by ${d}d ${h}h` : `Overdue by ${h}h`
    } else {
const totalH = Math.floor(diff / 3600000)
      const d = Math.floor(totalH / 24)
      const h = totalH % 24
      sla = d > 0 ? `${d}d ${h}h left` : `${h}h ${Math.floor((diff % 3600000) / 60000)}m left`
    }
  }
  return {
    id: t.ticketNumber || t.TicketNumber || `TICK-${t.id}`,
    ticketId: t.id,
    subject: t.subject || t.Subject || '',
    description: t.description || t.Description || '',
    status,
    statusName: t.statusName || t.StatusName || status,
    application: t.applicationName || t.ApplicationName || '',
    applicationId: t.applicationId || t.ApplicationId,
    raisedBy: t.requesterName || t.RequesterName || '',
    assignedTo: t.assignedToName || t.AssignedToName || '',
    sla,
    isSlaBreached: isSlaBreached,
    slaDeadline: slaDeadline ? new Date(slaDeadline) : null,
    updated: t.updatedAt ? new Date(t.updatedAt) : (t.UpdatedAt ? new Date(t.UpdatedAt) : new Date()),
    created: t.createdAt ? new Date(t.createdAt) : (t.CreatedAt ? new Date(t.CreatedAt) : new Date()),
    priority: t.priority || t.Priority || '',
    statusId: t.statusId || t.StatusId,
  }
}

function normalizeApp(a) {
  return {
    ...a,
    status: a.isActive ? 'active' : 'inactive',
    assignedUserIds: (a.assignedUsers || []).map(u => u.id),
  }
}

function useAsync(fn, deps) {
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(deps[0] ? true : false)
  const [error, setError] = useState(null)
  useEffect(() => {
    let cancelled = false
    if (!deps[0]) { setData(null); setLoading(false); return }
    setLoading(true)
    setError(null)
    fn().then(d => { if (!cancelled) setData(d) }).catch(e => { if (!cancelled) setError(e) }).finally(() => { if (!cancelled) setLoading(false) })
    return () => { cancelled = true }
  }, deps)
  return {
    data, loading, error,
    refetch: () => { if (deps[0]) { setLoading(true); setError(null); return fn().then(d => { setData(d); return d }).catch(e => { setError(e); throw e }).finally(() => setLoading(false)) } },
    silentRefetch: () => { if (deps[0]) { return fn().then(d => { setData(d); return d }).catch(e => { setError(e); throw e }) } },
    setData: (updater) => { setData(prev => typeof updater === 'function' ? updater(prev) : updater) },
  }
}

export default function App() {
  const { user, loading: authLoading, logout, isAdmin } = useAuth()

  const trackMatch = window.location.pathname.match(/^\/track\/(\d+)/)
  const trackToken = new URLSearchParams(window.location.search).get('token')
  if (trackMatch && trackToken) {
    return <TrackingPage ticketId={trackMatch[1]} token={trackToken} />
  }

  const [currentPage, setCurrentPage] = useState(isAdmin ? 'tickets' : 'dashboard')
  const [activeTab, setActiveTab] = useState('all')
  const [filters, setFilters] = useState({ search: '', status: 'all', app: 'all', from: '', to: '' })
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState(new Set())
  const [viewingTicketId, setViewingTicketId] = useState(null)
  const [notifications, setNotifications] = useState([])
  const ticketsRes = useAsync(() => ticketService.getAll().then(r => (r.items || r || []).map(normalizeTicket)), [user])
  const usersRes = useAsync(() => userService.getAll().then(r => r.items || r || []), [user])
  const deptsRes = useAsync(() => departmentService.getAll().then(r => r.items || r || []), [user])
  const appsRes = useAsync(() => applicationService.getAll().then(r => (r.items || r || []).map(normalizeApp)), [user])
  const notifRes = useAsync(() => notificationService.getAll().then(r => r.items || r || []), [user])
  const tickets = ticketsRes.data || []
  const usersList = usersRes.data || []
  const deptList = deptsRes.data || []
  const appList = appsRes.data || []

  useEffect(() => {
    if (notifRes.data) {
      const items = Array.isArray(notifRes.data) ? notifRes.data : []
      setNotifications(items.map(item => ({
        id: item.id,
        title: item.title || '',
        message: item.message || item.body || '',
        time: item.createdAt ? timeSince(new Date(item.createdAt)) : '',
        isRead: item.isRead || false,
      })))
    }
  }, [notifRes.data])

  useEffect(() => {
    if (!user) return
    const token = localStorage.getItem('accessToken')
    if (!token) return

    startConnection(token).then(() => {
      if (user.id) joinUserGroup(user.id)
    }).catch(() => {})

    onEvent('TicketCreated', (ticketData) => {
      const normalized = normalizeTicket(ticketData)
      ticketsRes.setData(prev => [normalized, ...(prev || [])])
    })
    onEvent('TicketUpdated', (ticketData) => {
      const normalized = normalizeTicket(ticketData)
      ticketsRes.setData(prev => (prev || []).map(t => t.ticketId === normalized.ticketId ? normalized : t))
    })
    onEvent('NotificationReceived', (notification) => {
      setNotifications(prev => [{
        id: notification.id,
        title: notification.title || '',
        message: notification.message || notification.body || '',
        time: timeSince(new Date()),
        isRead: false,
      }, ...prev])
    })

    const pollInterval = setInterval(() => {
      notificationService.getAll().then(r => {
        if (r && r.items) {
          setNotifications(prev => {
            const existing = new Map(prev.map(n => [n.id, n]))
            const fresh = r.items.map(n => ({
              id: n.id,
              title: n.title || '',
              message: n.message || n.body || '',
              time: n.createdAt ? timeSince(new Date(n.createdAt)) : '',
              isRead: n.isRead || false,
            }))
            fresh.forEach(n => existing.set(n.id, n))
            return Array.from(existing.values())
          })
        }
      }).catch(() => {})
    }, 20000)

    return () => {
      offEvent('TicketCreated')
      offEvent('TicketUpdated')
      offEvent('NotificationReceived')
      stopConnection()
      clearInterval(pollInterval)
    }
  }, [user])

  function addNotification(title, message) {
    setNotifications(prev => [{ id: null, title, message, time: timeSince(new Date()), isRead: false }, ...prev])
  }

  function clearNotification(idx) {
    setNotifications(prev => {
      const n = prev[idx]
      if (n?.id) notificationService.markRead(n.id).catch(() => {})
      return prev.filter((_, i) => i !== idx)
    })
  }

  const unreadCount = notifications.filter(n => !n.isRead).length

  function timeSince(d) {
    const s = Math.round((Date.now() - d.getTime()) / 1000)
    if (s < 60) return `${s}s ago`
    if (s < 3600) return `${Math.floor(s / 60)}m ago`
    if (s < 86400) return `${Math.floor(s / 3600)}h ago`
    return `${Math.floor(s / 86400)}d ago`
  }

  const filtered = useMemo(() => {
    return tickets.filter(t => {
      const s = t.status
      if (activeTab !== 'all' && s !== activeTab) return false
      if (filters.status !== 'all') {
        if (s !== filters.status) return false
      }
      if (filters.app !== 'all' && t.application !== filters.app && t.applicationId !== parseInt(filters.app)) return false
      if (filters.search) {
        const q = filters.search.toLowerCase()
        if (!t.id.toLowerCase().includes(q) && !t.subject.toLowerCase().includes(q) && !t.description.toLowerCase().includes(q) && !t.raisedBy.toLowerCase().includes(q) && !t.assignedTo.toLowerCase().includes(q)) return false
      }
      if (filters.from) { const f = new Date(filters.from); if (t.updated < f) return false }
      if (filters.to) { const to = new Date(filters.to); to.setHours(23, 59, 59, 999); if (t.updated > to) return false }
      return true
    })
  }, [tickets, activeTab, filters])

  const handleTabChange = useCallback(tab => { setActiveTab(tab); setFilters(prev => ({ ...prev, status: 'all' })); setPage(1); setSelected(new Set()) }, [])
  const handleFilterChange = useCallback(newFilters => { setFilters(newFilters); setPage(1); setSelected(new Set()) }, [])
  const handleSelectAll = useCallback((pageTickets, currentlyAll) => { setSelected(prev => { const next = new Set(prev); pageTickets.forEach(t => { if (currentlyAll) next.delete(t.id); else next.add(t.id) }); return next }) }, [])
  const handleSelectOne = useCallback(id => { setSelected(prev => { const next = new Set(prev); if (next.has(id)) next.delete(id); else next.add(id); return next }) }, [])

  const handleExportSelected = useCallback(() => {
    const sel = tickets.filter(t => selected.has(t.id))
    if (sel.length === 0) return
    const data = sel.map(t => ({ 'Ticket ID': t.id, Application: t.application, 'Raised By': t.raisedBy, 'Assigned To': t.assignedTo, Subject: t.subject, Description: t.description, Status: getStatusMeta(t.status).label, SLA: t.sla, Updated: timeSince(t.updated) }))
    exportToCSV(data, 'selected_tickets.csv')
  }, [selected, tickets])

  const handleExportAll = useCallback(() => {
    const data = filtered.map(t => ({ 'Ticket ID': t.id, Application: t.application, 'Raised By': t.raisedBy, 'Assigned To': t.assignedTo, Subject: t.subject, Description: t.description, Status: getStatusMeta(t.status).label, SLA: t.sla, Updated: timeSince(t.updated) }))
    exportToCSV(data, 'tickets_export.csv')
  }, [filtered])

  if (authLoading) return <div className="page-loading"><i className="fas fa-spinner fa-spin" /> Loading...</div>
  if (!user) return <LoginPage />

  const adminOnlyPages = ['users', 'departments', 'applications', 'sla-settings']
  if (!isAdmin && adminOnlyPages.includes(currentPage)) {
    setCurrentPage('dashboard')
    return <div className="page-loading"><i className="fas fa-spinner fa-spin" /> Redirecting...</div>
  }

  const initialLoading = ticketsRes.loading && !ticketsRes.data

  return (
    <div className="app">
      <Sidebar currentPage={currentPage} onNavigate={setCurrentPage} />
      <div className="main">
        <TopNav notifications={notifications} unreadCount={unreadCount} onClearNotification={clearNotification} onLogout={logout} user={user} />
        <main className="content">
          {initialLoading ? (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '60vh', color: 'var(--text-muted)' }}>
              <i className="fas fa-spinner fa-spin" style={{ marginRight: 8 }} /> Loading...
            </div>
          ) : (
            <>
              {currentPage === 'dashboard' && (
                <Dashboard isAdmin={isAdmin} />
              )}
              {currentPage === 'tickets' && (
                <>
                  <div className="page-heading">
                    <h1>{isAdmin ? 'Tickets' : 'My Tickets'}</h1>
                  </div>
                  <KPICards tickets={tickets} />
                  <StatusTabs tickets={tickets} activeTab={activeTab} onTabChange={handleTabChange} />
                  <Filters tickets={tickets} filters={filters} onFilterChange={handleFilterChange} onExportAll={handleExportAll} onExportSelected={handleExportSelected} selectedCount={selected.size} />
                  <TicketTable tickets={filtered} page={page} selected={selected} onSelectAll={handleSelectAll} onSelectOne={handleSelectOne} onPageChange={setPage} onViewTicket={t => { setViewingTicketId(t.ticketId); setCurrentPage('ticket-detail') }} onUpdate={() => ticketsRes.refetch()} usersList={usersList} />
                </>
              )}
              {currentPage === 'sla-settings' && isAdmin && (
                <SlaSettings />
              )}
              {currentPage === 'ticket-detail' && viewingTicketId && (
                <TicketDetail ticketId={viewingTicketId} onBack={() => { setViewingTicketId(null); setCurrentPage('tickets') }} onUpdate={() => ticketsRes.refetch()} usersList={usersList} isAdmin={isAdmin} />
              )}
              {currentPage === 'users' && isAdmin && (
                <UsersPage users={usersList} allApps={appList} departments={deptList} onNotify={addNotification} onRefresh={async () => { await usersRes.refetch(); await appsRes.refetch() }} />
              )}
              {currentPage === 'departments' && isAdmin && (
                <DepartmentsPage departments={deptList} onNotify={addNotification} onRefresh={async () => deptsRes.refetch()} />
              )}
              {currentPage === 'applications' && isAdmin && (
                <ApplicationsPage applications={appList} users={usersList} onNotify={addNotification} onRefresh={async () => { await appsRes.refetch(); await usersRes.refetch() }} onSilentRefresh={async () => { await appsRes.silentRefetch(); await usersRes.silentRefetch() }} onUpdateApps={(updater) => appsRes.setData(updater)} normalizeApp={normalizeApp} />
              )}
            </>
          )}
        </main>
      </div>
    </div>
  )
}
