import { useState, useRef, useEffect, useCallback } from 'react'
import { applicationService } from '../services/applicationService'

function extractError(e, fallback) {
  const data = e?.response?.data
  if (typeof data === 'string') return data
  if (data?.detail) return data.detail
  if (data?.title) return data.title
  if (data?.message) return data.message
  return e?.message || fallback
}

export default function ApplicationsPage({ applications = [], users = [], onNotify, onRefresh, onSilentRefresh, onUpdateApps, normalizeApp }) {
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('all')
  const [page, setPage] = useState(1)
  const [detailApp, setDetailApp] = useState(null)
  const [detailTab, setDetailTab] = useState('details')
  const [showAddModal, setShowAddModal] = useState(false)
  const [showEditModal, setShowEditModal] = useState(false)
  const [editTarget, setEditTarget] = useState(null)
  const tableWrapRef = useRef(null)
  const [perPage, setPerPage] = useState(10)

  const calcPerPage = useCallback(() => {
    const wrap = tableWrapRef.current
    if (!wrap) return
    const wrapHeight = wrap.clientHeight
    const rowEstimate = 48
    const headerEstimate = 50
    const paginationEstimate = 50
    const available = wrapHeight - headerEstimate - paginationEstimate
    const rows = Math.max(5, Math.floor(available / rowEstimate))
    setPerPage(rows)
  }, [])

  useEffect(() => {
    calcPerPage()
    window.addEventListener('resize', calcPerPage)
    return () => window.removeEventListener('resize', calcPerPage)
  }, [calcPerPage])

  const filtered = applications.filter(a => {
    if (search) { const q = search.toLowerCase(); if (!a.name?.toLowerCase().includes(q)) return false }
    if (statusFilter !== 'all' && a.status !== statusFilter) return false
    return true
  })

  const totalPages = Math.ceil(filtered.length / perPage) || 1
  const safePage = Math.min(page, totalPages)
  const start = (safePage - 1) * perPage
  const pageApps = filtered.slice(start, start + perPage)

  const totalApps = applications.length
  const activeApps = applications.filter(a => a.status === 'active').length
  const totalMappings = applications.reduce((sum, a) => sum + (a.assignedUserIds?.length || 0), 0)

  function getUserById(id) { return users.find(u => u.id === id) }

  async function handleSaveMapping(app, userIds) {
    try {
      await applicationService.assignUsers(app.id, { userIds })
      if (onNotify) {
        const added = userIds.filter(id => !(app.assignedUserIds || []).includes(id))
        const removed = (app.assignedUserIds || []).filter(id => !userIds.includes(id))
        if (added.length > 0) onNotify('Users Assigned', `${added.length} user(s) assigned to ${app.name}`)
        if (removed.length > 0) onNotify('Users Removed', `${removed.length} user(s) removed from ${app.name}`)
      }
      if (onUpdateApps) {
        onUpdateApps(prev => (prev || []).map(a => a.id === app.id ? { ...a, assignedUserIds: userIds } : a))
      }
      setDetailApp(prev => ({ ...prev, assignedUserIds: userIds }))
    } catch (e) { alert(`Assign users failed: ${extractError(e, 'Failed to save user assignments')}`) }
  }

  async function handleToggleStatus(app) {
    try {
      const newStatus = app.status === 'active' ? 'inactive' : 'active'
      await applicationService.update(app.id, { name: app.name, isActive: newStatus === 'active' })
      const refreshFn = onSilentRefresh || onRefresh
      await refreshFn?.()
    } catch (e) { alert(`Toggle status failed: ${extractError(e, 'Failed to toggle status')}`) }
  }

  async function handleDelete(app) {
    if (!window.confirm(`Are you sure you want to delete ${app.name}? This cannot be undone.`)) return
    try {
      await applicationService.delete(app.id)
      setDetailApp(null)
      const refreshFn = onSilentRefresh || onRefresh
      await refreshFn?.()
      onNotify?.('Application Deleted', `${app.name} has been permanently deleted.`)
    } catch (e) { alert(`Delete failed: ${extractError(e, 'Failed to delete application')}`) }
  }

  async function handleCreateApp(data) {
    try {
      const created = await applicationService.create({ name: data.name, isActive: data.status === 'active' })
      if (onUpdateApps && normalizeApp) {
        onUpdateApps(prev => [...(prev || []), normalizeApp(created)])
      }
      setShowAddModal(false)
    } catch (e) { alert(`Create failed: ${extractError(e, 'Failed to create application')}`) }
  }

  async function handleEditSave(changes) {
    try {
      const updated = await applicationService.update(changes.id, { name: changes.name, isActive: changes.status === 'active' })
      if (onUpdateApps && normalizeApp) {
        onUpdateApps(prev => (prev || []).map(a => a.id === changes.id ? normalizeApp(updated) : a))
      }
      if (detailApp && detailApp.id === changes.id) setDetailApp(prev => ({ ...prev, ...changes }))
      setShowEditModal(false)
      setEditTarget(null)
    } catch (e) { alert(`Save failed: ${extractError(e, 'Failed to save changes')}`) }
  }

  function handleExport() {
    const csv = [['Application Name', 'Status', 'Assigned Users'], ...filtered.map(a => [a.name, a.status, (a.assignedUserIds?.length || 0)])]
    const blob = new Blob([csv.map(r => r.map(c => `"${String(c).replace(/"/g, '""')}"`).join(',')).join('\n')], { type: 'text/csv' })
    const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = 'applications_export.csv'; a.click(); URL.revokeObjectURL(a.href)
  }

  return (
    <div className="apps-page">
      <div className="page-heading">
        <div><h1>Applications</h1><p className="page-subtitle">Manage application ownership and user mappings.</p></div>
        <div className="page-actions"><button className="btn btn-primary" onClick={() => setShowAddModal(true)}><i className="fas fa-plus" /> Add Application</button><button className="btn btn-outline" onClick={handleExport}><i className="fas fa-download" /> Export</button></div>
      </div>

      <div className="kpi-row">
        {[{ icon: 'fa-layer-group', count: totalApps, label: 'Total Applications' }, { icon: 'fa-users', count: totalMappings, label: 'Total Assigned Users' }, { icon: 'fa-check-circle', count: activeApps, label: 'Active Applications' }].map(c => (
          <div className="kpi-card" key={c.label}><div className="kpi-icon"><i className={`fas ${c.icon}`} /></div><div className="kpi-body"><div className="kpi-count">{c.count.toLocaleString()}</div><div className="kpi-label">{c.label}</div></div></div>
        ))}
      </div>

      <div className="filters">
        <div className="filter-item search-box" style={{ flex: 1, maxWidth: 400 }}><i className="fas fa-search" /><input type="text" placeholder="Search applications by name..." value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></div>
        <div className="filter-item"><label>Status</label><select value={statusFilter} onChange={e => { setStatusFilter(e.target.value); setPage(1) }}><option value="all">All Statuses</option><option value="active">Active</option><option value="inactive">Inactive</option></select></div>
      </div>

      <div className="table-wrap" ref={tableWrapRef}>
        <table className="app-table">
          <thead><tr><th className="col-a-slno">S.No.</th><th className="col-a-name">Application Name</th><th className="col-a-users">Assigned Users</th><th className="col-a-status">Status</th><th className="col-a-actions">Actions</th></tr></thead>
          <tbody>
            {pageApps.length === 0 ? (
              <tr><td colSpan={5} style={{ textAlign: 'center', padding: '48px 16px', color: 'var(--text-muted)' }}>No applications found.</td></tr>
            ) : (
              pageApps.map((a, i) => (
                <tr key={a.id} className="app-row" onClick={() => { setDetailApp(a); setDetailTab('details') }}>
                  <td className="col-a-slno">{start + i + 1}</td>
                  <td>
                    <div className="app-name-cell">
                      <div className="app-icon"><i className="fas fa-cube" /></div>
                      <div className="app-name">{a.name}</div>
                    </div>
                  </td>
                  <td><a href="#" className="app-users-link" onClick={e => { e.preventDefault(); e.stopPropagation(); setDetailApp(a); setDetailTab('users') }}><i className="fas fa-users" /> {(a.assignedUserIds?.length || 0)} user{(a.assignedUserIds?.length || 0) !== 1 ? 's' : ''}</a></td>
                  <td><span className={`app-status-badge status-${a.status}`}>{a.status}</span></td>
                  <td className="col-a-actions" onClick={e => e.stopPropagation()}>
                    <AppActionMenu onView={() => { setDetailApp(a); setDetailTab('details') }} onManageUsers={() => { setDetailApp(a); setDetailTab('users') }} onEdit={() => { setEditTarget({ ...a }); setShowEditModal(true) }} onToggleStatus={() => handleToggleStatus(a)} onDelete={() => handleDelete(a)} status={a.status} />
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="pagination">
        <span className="pagination-info">Showing {filtered.length ? start + 1 : 0} to {Math.min(start + perPage, filtered.length)} of {filtered.length} applications</span>
        <div className="pagination-controls">{Array.from({ length: totalPages }, (_, i) => i + 1).map(p => (<button key={p} className={`pagination-btn${p === safePage ? ' active' : ''}`} onClick={() => setPage(p)}>{p}</button>))}</div>
      </div>

      {detailApp && <AppDetailModal app={detailApp} tab={detailTab} onTabChange={setDetailTab} onClose={() => setDetailApp(null)} onSaveMapping={handleSaveMapping} users={users} getUserById={getUserById} />}
      {showAddModal && <AddApplicationModal onClose={() => setShowAddModal(false)} onSave={handleCreateApp} />}
      {showEditModal && editTarget && <EditApplicationModal app={editTarget} onClose={() => { setShowEditModal(false); setEditTarget(null) }} onSave={handleEditSave} />}
    </div>
  )
}

function AppActionMenu({ onView, onManageUsers, onEdit, onToggleStatus, onDelete, status }) {
  const [open, setOpen] = useState(false); const ref = useRef(null)
  useEffect(() => { const handler = e => { if (ref.current && !ref.current.contains(e.target)) setOpen(false) }; document.addEventListener('mousedown', handler); return () => document.removeEventListener('mousedown', handler) }, [])
  return (
    <div ref={ref} style={{ position: 'relative' }}>
      <button className="action-btn" onClick={e => { e.stopPropagation(); setOpen(!open) }}><i className="fas fa-ellipsis-v" /></button>
      {open && <div className="dropdown-menu show" style={{ position: 'absolute', right: 0, top: '100%', zIndex: 10 }}><a href="#" onClick={e => { e.preventDefault(); setOpen(false); onView() }}>View Details</a><a href="#" onClick={e => { e.preventDefault(); setOpen(false); onManageUsers() }}>Manage Users</a><a href="#" onClick={e => { e.preventDefault(); setOpen(false); onEdit() }}>Edit Application</a><a href="#" onClick={e => { e.preventDefault(); setOpen(false); onToggleStatus() }}>{status === 'active' ? 'Deactivate' : 'Activate'} Application</a><a href="#" onClick={e => { e.preventDefault(); setOpen(false); onDelete() }} style={{ color: '#B91C1C' }}>Delete Application</a></div>}
    </div>
  )
}

function AddApplicationModal({ onClose, onSave }) {
  const ref = useRef(null); const [form, setForm] = useState({ name: '', status: 'active' })
  useEffect(() => { const handler = e => { if (e.key === 'Escape') onClose() }; document.addEventListener('keydown', handler); return () => document.removeEventListener('keydown', handler) }, [onClose])
  useEffect(() => { const handler = e => { if (ref.current && !ref.current.contains(e.target)) onClose() }; document.addEventListener('mousedown', handler); return () => document.removeEventListener('mousedown', handler) }, [onClose])
  return (
    <><div className="modal-backdrop open" /><div className="modal open" ref={ref}><div className="modal-header"><h2 className="modal-title">Add Application</h2><button className="modal-close" onClick={onClose}><i className="fas fa-times" /></button></div><div className="modal-body"><div className="modal-field"><label className="modal-field-label">Application Name *</label><input className="modal-input" type="text" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="e.g. Site Monitoring" autoFocus /></div><div className="modal-field"><label className="modal-field-label">Status</label><select className="modal-input" value={form.status} onChange={e => setForm({ ...form, status: e.target.value })}><option value="active">Active</option><option value="inactive">Inactive</option></select></div></div><div className="modal-footer"><button className="btn btn-outline" onClick={onClose}>Cancel</button><button className="btn btn-primary" onClick={() => onSave(form)} disabled={!form.name.trim()}>Create Application</button></div></div></>
  )
}

function EditApplicationModal({ app, onClose, onSave }) {
  const ref = useRef(null); const [form, setForm] = useState({ name: app.name || '', status: app.status || 'active' })
  useEffect(() => { const handler = e => { if (e.key === 'Escape') onClose() }; document.addEventListener('keydown', handler); return () => document.removeEventListener('keydown', handler) }, [onClose])
  useEffect(() => { const handler = e => { if (ref.current && !ref.current.contains(e.target)) onClose() }; document.addEventListener('mousedown', handler); return () => document.removeEventListener('mousedown', handler) }, [onClose])
  return (
    <><div className="modal-backdrop open" /><div className="modal open" ref={ref}><div className="modal-header"><h2 className="modal-title">Edit Application</h2><button className="modal-close" onClick={onClose}><i className="fas fa-times" /></button></div><div className="modal-body"><div className="modal-field"><label className="modal-field-label">Application Name *</label><input className="modal-input" type="text" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /></div><div className="modal-field"><label className="modal-field-label">Status</label><select className="modal-input" value={form.status} onChange={e => setForm({ ...form, status: e.target.value })}><option value="active">Active</option><option value="inactive">Inactive</option></select></div></div><div className="modal-footer"><button className="btn btn-outline" onClick={onClose}>Cancel</button><button className="btn btn-primary" onClick={() => onSave({ id: app.id, ...form })} disabled={!form.name.trim()}>Save Changes</button></div></div></>
  )
}

function AppDetailModal({ app, tab, onTabChange, onClose, onSaveMapping, users, getUserById }) {
  const ref = useRef(null); const [selectedAvailable, setSelectedAvailable] = useState(new Set()); const [selectedAssigned, setSelectedAssigned] = useState(new Set()); const [searchAvail, setSearchAvail] = useState(''); const [searchAssigned, setSearchAssigned] = useState(''); const [localAssigned, setLocalAssigned] = useState([]); const [showOverlay, setShowOverlay] = useState(false); const [saving, setSaving] = useState(false)
  useEffect(() => { if (app) setLocalAssigned([...(app.assignedUserIds || [])]) }, [app])
  useEffect(() => { const handler = e => { if (e.key === 'Escape') onClose() }; document.addEventListener('keydown', handler); return () => document.removeEventListener('keydown', handler) }, [onClose])
  useEffect(() => { const handler = e => { if (ref.current && !ref.current.contains(e.target) && !showOverlay) onClose() }; document.addEventListener('mousedown', handler); return () => document.removeEventListener('mousedown', handler) }, [onClose, showOverlay])
  if (!app) return null
  const allAppUsers = localAssigned.map(id => getUserById(id)).filter(Boolean)
  const available = users.filter(u => !localAssigned.includes(u.id) && (u.name?.toLowerCase().includes(searchAvail.toLowerCase()) || u.email?.toLowerCase().includes(searchAvail.toLowerCase())))
  const assignedFiltered = allAppUsers.filter(u => u.name?.toLowerCase().includes(searchAssigned.toLowerCase()) || u.email?.toLowerCase().includes(searchAssigned.toLowerCase()))
  return (
    <><div className="modal-backdrop open" /><div className="modal open modal-ud" ref={ref}>
      <div className="modal-header"><div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}><h2 className="modal-title">{app.name}</h2><span className={`app-status-badge status-${app.status}`}>{app.status}</span></div><button className="modal-close" onClick={() => { setShowOverlay(false); onClose() }}><i className="fas fa-times" /></button></div>
      <div className="ad-tabs">{['details', 'users'].map(t => (<button key={t} className={`ad-tab${tab === t ? ' active' : ''}`} onClick={() => { setShowOverlay(t === 'users'); onTabChange(t) }}>{t === 'details' ? 'Details' : 'Manage Users'}</button>))}</div>
      {tab === 'details' && (
        <div className="modal-body ad-body-modal">
          <div className="ad-details-grid">
            {[{ label: 'Application Name', value: app.name }, { label: 'Status', value: <span className={`app-status-badge status-${app.status}`}>{app.status}</span> }].map(f => (<div className="ad-detail-item" key={f.label}><span className="ad-detail-label">{f.label}</span><span className="ad-detail-value">{f.value}</span></div>))}
          </div>
          <div className="ad-section"><h3 className="ad-section-title">Assigned Users ({(app.assignedUserIds?.length || 0)})</h3>{(app.assignedUserIds?.length || 0) > 0 ? <div className="ad-users-list">{app.assignedUserIds.map(id => { const u = getUserById(id); return <div key={id} className="ad-user-row"><div className="ad-user-avatar">{u ? u.name?.split(' ').map(n => n[0]).join('') : '?'}</div><div className="ad-user-info"><span className="ad-user-name">{u ? u.name : id}</span><span className={`role-badge role-${u ? u.role : ''}`}>{u ? (u.role === 'spoc' ? 'SPOC' : 'Admin') : ''}</span></div></div> })}</div> : <span className="ad-no-users">No users assigned.</span>}</div>
        </div>
      )}
      {tab === 'users' && (
        <div className="modal-body ad-body-modal" style={{ padding: '16px 24px' }}>
          <div className="transfer-list">
            <div className="transfer-col">
              <div className="transfer-col-header">Available ({available.length})</div>
              <div className="transfer-search"><i className="fas fa-search" /><input type="text" placeholder="Search..." value={searchAvail} onChange={e => setSearchAvail(e.target.value)} /></div>
              <div className="transfer-items">{available.map(u => (<label key={u.id} className={`transfer-item${selectedAvailable.has(u.id) ? ' selected' : ''}`}><input type="checkbox" checked={selectedAvailable.has(u.id)} onChange={() => setSelectedAvailable(prev => prev.has(u.id) ? new Set([...prev].filter(x => x !== u.id)) : new Set([...prev, u.id]))} /><div className="ad-mu-row-avatar">{u.name?.split(' ').map(n => n[0]).join('')}</div><div className="ad-mu-row-info"><span className="ad-mu-row-name">{u.name}</span><span className={`role-badge role-${u.role}`}>{u.role === 'spoc' ? 'SPOC' : 'Admin'}</span></div></label>))}{available.length === 0 && <span className="transfer-empty">No users available</span>}</div>
            </div>
            <div className="transfer-buttons">
              <button className="transfer-btn" onClick={() => { setLocalAssigned(prev => [...prev, ...selectedAvailable]); setSelectedAvailable(new Set()) }} disabled={selectedAvailable.size === 0}><i className="fas fa-plus" /> Assign</button>
              <button className="transfer-btn" onClick={() => { setLocalAssigned(prev => prev.filter(id => !selectedAssigned.has(id))); setSelectedAssigned(new Set()) }} disabled={selectedAssigned.size === 0}><i className="fas fa-minus" /> Remove</button>
            </div>
            <div className="transfer-col">
              <div className="transfer-col-header">Assigned ({localAssigned.length})</div>
              <div className="transfer-search"><i className="fas fa-search" /><input type="text" placeholder="Search..." value={searchAssigned} onChange={e => setSearchAssigned(e.target.value)} /></div>
              <div className="transfer-items">{assignedFiltered.map(u => (<label key={u.id} className={`transfer-item${selectedAssigned.has(u.id) ? ' selected' : ''}`}><input type="checkbox" checked={selectedAssigned.has(u.id)} onChange={() => setSelectedAssigned(prev => prev.has(u.id) ? new Set([...prev].filter(x => x !== u.id)) : new Set([...prev, u.id]))} /><div className="ad-mu-row-avatar">{u.name?.split(' ').map(n => n[0]).join('')}</div><div className="ad-mu-row-info"><span className="ad-mu-row-name">{u.name}</span><span className={`role-badge role-${u.role}`}>{u.role === 'spoc' ? 'SPOC' : 'Admin'}</span></div></label>))}{assignedFiltered.length === 0 && <span className="transfer-empty">No users assigned</span>}</div>
            </div>
          </div>
          <div className="ad-mu-footer" style={{ marginTop: 16 }}><button className="btn btn-outline" onClick={() => { setShowOverlay(false); onClose() }}>Cancel</button><button className="btn btn-primary" disabled={saving} onClick={async () => { setSaving(true); try { await onSaveMapping(app, localAssigned); setShowOverlay(false) } finally { setSaving(false) } }}>{saving ? <><i className="fas fa-spinner fa-spin" /> Saving...</> : 'Save Changes'}</button></div>
        </div>
      )}
    </div></>
  )
}
