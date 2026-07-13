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
import './App.css'

function normalizeTicket(t) {
  const statusMap = { 'Open': 'open', 'In Progress': 'in_progress', 'Waiting': 'waiting', 'Resolved': 'resolved', 'Closed': 'closed' }
  const status = statusMap[t.statusName] || (t.statusName || '').toLowerCase().replace(/\s+/g, '_')
  let sla = ''
  if (t.slaDeadline) {
    const deadline = new Date(t.slaDeadline)
    const now = new Date()
    const diff = deadline - now
    if (diff < 0) {
      sla = `Overdue by ${Math.ceil(Math.abs(diff) / 3600000)}h`
    } else {
      const h = Math.floor(diff / 3600000)
      const m = Math.floor((diff % 3600000) / 60000)
      sla = `${h}h ${m}m left`
    }
  }
  return {
    id: t.ticketNumber || `TICK-${t.id}`,
    ticketId: t.id,
    subject: t.subject || '',
    description: t.description || '',
    status,
    statusName: t.statusName || status,
    application: t.applicationName || '',
    applicationId: t.applicationId,
    raisedBy: t.requesterName || '',
    assignedTo: t.assignedToName || '',
    sla,
    isSlaBreached: t.isSlaBreached || false,
    slaDeadline: t.slaDeadline ? new Date(t.slaDeadline) : null,
    updated: t.updatedAt ? new Date(t.updatedAt) : new Date(),
    created: t.createdAt ? new Date(t.createdAt) : new Date(),
    priority: t.priority || '',
    statusId: t.statusId,
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
  const { user, loading: authLoading, logout } = useAuth()
  const [currentPage, setCurrentPage] = useState('tickets')
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
      const n = Array.isArray(notifRes.data) ? notifRes.data : []
      setNotifications(n.map(item => ({
        title: item.title || '',
        message: item.message || item.body || '',
        time: item.createdDate ? timeSince(new Date(item.createdDate)) : '',
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
        title: notification.title || '',
        message: notification.message || notification.body || '',
        time: timeSince(new Date()),
      }, ...prev])
    })

    const pollInterval = setInterval(() => {
      notificationService.getAll().then(r => {
        if (r && r.items) {
          setNotifications(r.items.map(n => ({
            title: n.title || '',
            message: n.message || n.body || '',
            time: n.createdDate ? timeSince(new Date(n.createdDate)) : '',
          })))
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
    setNotifications(prev => [{ title, message, time: timeSince(new Date()) }, ...prev])
  }

  function clearNotification(idx) {
    setNotifications(prev => prev.filter((_, i) => i !== idx))
  }

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

  const initialLoading = ticketsRes.loading && !ticketsRes.data

  return (
    <div className="app">
      <Sidebar currentPage={currentPage} onNavigate={setCurrentPage} />
      <div className="main">
        <TopNav notifications={notifications} onClearNotification={clearNotification} onLogout={logout} user={user} />
        <main className="content">
          {initialLoading ? (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '60vh', color: 'var(--text-muted)' }}>
              <i className="fas fa-spinner fa-spin" style={{ marginRight: 8 }} /> Loading...
            </div>
          ) : (
            <>
              {currentPage === 'dashboard' && (
                <Dashboard tickets={tickets} users={usersList} applications={appList} />
              )}
              {currentPage === 'tickets' && (
                <>
                  <div className="page-heading">
                    <h1>Tickets</h1>
                  </div>
                  <KPICards tickets={tickets} />
                  <StatusTabs tickets={tickets} activeTab={activeTab} onTabChange={handleTabChange} />
                  <Filters tickets={tickets} filters={filters} onFilterChange={handleFilterChange} onExportAll={handleExportAll} onExportSelected={handleExportSelected} selectedCount={selected.size} />
                  <TicketTable tickets={filtered} page={page} selected={selected} onSelectAll={handleSelectAll} onSelectOne={handleSelectOne} onPageChange={setPage} onViewTicket={t => { setViewingTicketId(t.ticketId); setCurrentPage('ticket-detail') }} onUpdate={() => ticketsRes.refetch()} usersList={usersList} />
                </>
              )}
              {currentPage === 'sla-settings' && (
                <SlaSettings />
              )}
              {currentPage === 'ticket-detail' && viewingTicketId && (
                <TicketDetail ticketId={viewingTicketId} onBack={() => { setViewingTicketId(null); setCurrentPage('tickets') }} onUpdate={() => ticketsRes.refetch()} usersList={usersList} />
              )}
              {currentPage === 'users' && (
                <UsersPage users={usersList} allApps={appList} departments={deptList} onNotify={addNotification} onRefresh={async () => { await usersRes.refetch(); await appsRes.refetch() }} />
              )}
              {currentPage === 'departments' && (
                <DepartmentsPage departments={deptList} onNotify={addNotification} onRefresh={async () => deptsRes.refetch()} />
              )}
              {currentPage === 'applications' && (
                <ApplicationsPage applications={appList} users={usersList} onNotify={addNotification} onRefresh={async () => { await appsRes.refetch(); await usersRes.refetch() }} onSilentRefresh={async () => { await appsRes.silentRefetch(); await usersRes.silentRefetch() }} onUpdateApps={(updater) => appsRes.setData(updater)} normalizeApp={normalizeApp} />
              )}
            </>
          )}
        </main>
      </div>
    </div>
  )
}
