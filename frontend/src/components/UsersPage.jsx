import { useState, useRef, useEffect } from 'react'
import { userService } from '../services/userService'
import { extractError } from '../utils/helpers'

export default function UsersPage({ users = [], allApps = [], departments = [], onNotify, onRefresh }) {
  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState('all')
  const [deptFilter, setDeptFilter] = useState('all')
  const [statusFilter, setStatusFilter] = useState('all')
  const [page, setPage] = useState(1)
  const [detailUser, setDetailUser] = useState(null)
  const [showEditModal, setShowEditModal] = useState(false)
  const [showAssignModal, setShowAssignModal] = useState(false)
  const [showAddModal, setShowAddModal] = useState(false)
  const perPage = 10

  const filtered = users.filter(u => {
    if (search) { const q = search.toLowerCase(); if (!u.name?.toLowerCase().includes(q) && !u.email?.toLowerCase().includes(q)) return false }
    if (roleFilter !== 'all' && u.role !== roleFilter) return false
    if (deptFilter !== 'all' && u.departmentName !== deptFilter && u.department !== deptFilter) return false
    if (statusFilter !== 'all' && u.status !== statusFilter) return false
    return true
  })

  const totalPages = Math.ceil(filtered.length / perPage) || 1
  const safePage = Math.min(page, totalPages)
  const start = (safePage - 1) * perPage
  const pageUsers = filtered.slice(start, start + perPage)
  const depts = [...new Set(users.map(u => u.departmentName || u.department || '').filter(Boolean))].sort()

  const total = users.length
  const spocs = users.filter(u => u.role === 'spoc').length
  const admins = users.filter(u => u.role === 'admin').length
  const active = users.filter(u => u.status === 'active').length

  async function handleEditSave(changes) {
    if (!detailUser) return
    try {
      await userService.update(detailUser.id, changes)
      await onRefresh?.()
      setDetailUser(prev => ({ ...prev, ...changes }))
      setShowEditModal(false)
      onNotify?.('User Updated', `${detailUser.name} has been updated.`)
    } catch (e) {
      alert(extractError(e, 'Failed to update user'))
    }
  }

  async function handleAssignSave(appIds) {
    if (!detailUser) return
    try {
      await userService.assignApplications(detailUser.id, { applicationIds: appIds })
      const added = appIds.filter(a => !(detailUser.applicationIds || []).includes(a))
      const removed = (detailUser.applicationIds || []).filter(a => !appIds.includes(a))
      await onRefresh?.()
      setDetailUser(prev => ({ ...prev, applicationIds: appIds }))
      setShowAssignModal(false)
      if (onNotify) {
        if (added.length > 0) onNotify('Applications Assigned', `${added.length} app(s) assigned`)
        if (removed.length > 0) onNotify('Applications Removed', `${removed.length} app(s) removed`)
      }
    } catch (e) {
      alert(extractError(e, 'Failed to assign applications'))
    }
  }

  async function handleResetPassword() {
    if (!detailUser) return
    try {
      await userService.resetPassword(detailUser.id)
      onNotify?.('Password Reset', `Temporary password set for ${detailUser.name}.`)
    } catch { alert('Password reset failed.') }
  }

  async function handleDeleteUser(u) {
    if (!window.confirm(`Are you sure you want to remove ${u.name}?`)) return
    try {
      await userService.delete(u.id)
      setDetailUser(null)
      await onRefresh?.()
      onNotify?.('User Removed', `${u.name} has been removed.`)
    } catch (e) {
      alert(extractError(e, 'Failed to delete user'))
    }
  }

  async function handleCreateUser(data) {
    try {
      await userService.create(data)
      await onRefresh?.()
      setShowAddModal(false)
      onNotify?.('User Created', `${data.name} has been created.`)
    } catch (e) {
      alert(extractError(e, 'Failed to create user'))
    }
  }

  function handleExport() {
    const csv = [['Name', 'Email', 'Department', 'Role', 'Status'], ...filtered.map(u => [u.name, u.email, u.departmentName || u.department || '', u.role, u.status])]
    const blob = new Blob([csv.map(r => r.map(c => `"${String(c).replace(/"/g, '""')}"`).join(',')).join('\n')], { type: 'text/csv' })
    const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = 'users_export.csv'; a.click(); URL.revokeObjectURL(a.href)
  }

  return (
    <div className="users-page">
      <div className="page-heading">
        <div><h1>Users Management</h1><p className="page-subtitle">Manage end users, SPOCs and administrators.</p></div>
        <button className="btn btn-primary" onClick={() => setShowAddModal(true)}><i className="fas fa-plus" /> Add User</button>
      </div>

      <div className="kpi-row">
        {[{ icon: 'fa-users', count: total, label: 'Total Users' }, { icon: 'fa-user-tie', count: spocs, label: 'SPOCs' }, { icon: 'fa-shield-alt', count: admins, label: 'Admins' }, { icon: 'fa-check-circle', count: active, label: 'Active Users' }].map(c => (
          <div className="kpi-card" key={c.label}><div className="kpi-icon"><i className={`fas ${c.icon}`} /></div><div className="kpi-body"><div className="kpi-count">{c.count.toLocaleString()}</div><div className="kpi-label">{c.label}</div></div></div>
        ))}
      </div>

      <div className="filters">
        <div className="filter-item search-box" style={{ maxWidth: 320 }}><i className="fas fa-search" /><input type="text" placeholder="Search by name or email..." value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></div>
        <div className="filter-item"><label>Role</label><select value={roleFilter} onChange={e => { setRoleFilter(e.target.value); setPage(1) }}><option value="all">All Roles</option><option value="spoc">SPOC</option><option value="admin">Admin</option></select></div>
        <div className="filter-item"><label>Department</label><select value={deptFilter} onChange={e => { setDeptFilter(e.target.value); setPage(1) }}><option value="all">All Departments</option>{depts.map(d => <option key={d} value={d}>{d}</option>)}</select></div>
        <div className="filter-item"><label>Status</label><select value={statusFilter} onChange={e => { setStatusFilter(e.target.value); setPage(1) }}><option value="all">All Statuses</option><option value="active">Active</option><option value="inactive">Inactive</option></select></div>
        <div className="filter-export"><button className="btn btn-outline" onClick={handleExport}><i className="fas fa-download" /> Export</button></div>
      </div>

      <div className="table-wrap">
        <table className="user-table">
          <thead><tr><th className="col-u-name">Name</th><th className="col-u-email">Email</th><th className="col-u-dept">Department</th><th className="col-u-role">Role</th><th className="col-u-status">Status</th><th className="col-u-actions">Actions</th></tr></thead>
          <tbody>
            {pageUsers.length === 0 ? (
              <tr><td colSpan={6} style={{ textAlign: 'center', padding: '48px 16px', color: 'var(--text-muted)' }}>No users found matching your filters.</td></tr>
            ) : (
              pageUsers.map(u => (
                <tr key={u.id} className="user-row" onClick={() => setDetailUser(u)}>
                  <td><div className="user-name-cell"><div className="user-avatar-sm">{u.name?.split(' ').map(n => n[0]).join('')}</div><span>{u.name}</span></div></td>
                  <td className="col-u-email">{u.email}</td>
                  <td>{u.departmentName || u.department || ''}</td>
                  <td><span className={`role-badge role-${u.role}`}>{u.role === 'spoc' ? 'SPOC' : 'Admin'}</span></td>
                  <td><span className={`user-status-badge status-${u.status}`}>{u.status}</span></td>
                  <td className="col-u-actions" onClick={e => e.stopPropagation()}>
                    <ActionMenu onView={() => setDetailUser(u)} onEdit={() => { setDetailUser(u); setShowEditModal(true) }} onAssign={() => { setDetailUser(u); setShowAssignModal(true) }} />
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="pagination">
        <span className="pagination-info">Showing {filtered.length ? start + 1 : 0} to {Math.min(start + perPage, filtered.length)} of {filtered.length} users</span>
        <div className="pagination-controls">
          <button className="pagination-btn" disabled={safePage <= 1} onClick={() => setPage(safePage - 1)}><i className="fas fa-chevron-left" /></button>
          {Array.from({ length: totalPages }, (_, i) => i + 1).map(p => (<button key={p} className={`pagination-btn${p === safePage ? ' active' : ''}`} onClick={() => setPage(p)}>{p}</button>))}
          <button className="pagination-btn" disabled={safePage >= totalPages} onClick={() => setPage(safePage + 1)}><i className="fas fa-chevron-right" /></button>
        </div>
      </div>

      {detailUser && <UserDetailModal user={detailUser} allApps={allApps} onClose={() => { setDetailUser(null); setShowEditModal(false); setShowAssignModal(false) }} onEdit={() => setShowEditModal(true)} onAssign={() => setShowAssignModal(true)} onResetPassword={handleResetPassword} onDelete={handleDeleteUser} onRemoveApp={(appId) => handleAssignSave((detailUser.applicationIds || []).filter(a => a !== appId))} showOverlay={showEditModal || showAssignModal} />}
      {showEditModal && detailUser && <EditUserModal user={detailUser} onClose={() => setShowEditModal(false)} onSave={handleEditSave} departments={departments} />}
      {showAssignModal && detailUser && <AssignAppsModal assigned={detailUser.applicationIds || detailUser.assignedApps || []} allApps={allApps} onClose={() => setShowAssignModal(false)} onSave={handleAssignSave} />}
      {showAddModal && <AddUserModal onClose={() => setShowAddModal(false)} onSave={handleCreateUser} departments={departments} />}
    </div>
  )
}

function ActionMenu({ onView, onEdit, onAssign }) {
  const [open, setOpen] = useState(false); const ref = useRef(null)
  useEffect(() => { const handler = e => { if (ref.current && !ref.current.contains(e.target)) setOpen(false) }; document.addEventListener('mousedown', handler); return () => document.removeEventListener('mousedown', handler) }, [])
  return (
    <div ref={ref} style={{ position: 'relative' }}>
      <button className="action-btn" onClick={e => { e.stopPropagation(); setOpen(!open) }}><i className="fas fa-ellipsis-v" /></button>
      {open && <div className="dropdown-menu show" style={{ position: 'absolute', right: 0, top: '100%', zIndex: 10 }}><a href="#" onClick={e => { e.preventDefault(); setOpen(false); onView() }}>View User</a><a href="#" onClick={e => { e.preventDefault(); setOpen(false); onEdit() }}>Edit User</a><a href="#" onClick={e => { e.preventDefault(); setOpen(false); onAssign() }}>Assign Applications</a></div>}
    </div>
  )
}

function UserDetailModal({ user, allApps, onClose, onEdit, onAssign, onResetPassword, onDelete, onRemoveApp, showOverlay }) {
  const ref = useRef(null)
  const appNameMap = Object.fromEntries((allApps || []).map(a => [a.id || a, a.name || a]))
  useEffect(() => { const handler = e => { if (e.key === 'Escape') onClose() }; document.addEventListener('keydown', handler); return () => document.removeEventListener('keydown', handler) }, [onClose])
  useEffect(() => { const handler = e => { if (!showOverlay && ref.current && !ref.current.contains(e.target)) onClose() }; document.addEventListener('mousedown', handler); return () => document.removeEventListener('mousedown', handler) }, [onClose, showOverlay])
  if (!user) return null
  const appIds = user.applicationIds || []
  return (
    <>
      <div className="modal-backdrop open" />
      <div className="modal modal-ud open" ref={ref}>
        <div className="modal-header"><h2 className="modal-title">User Details</h2><button className="modal-close" onClick={onClose}><i className="fas fa-times" /></button></div>
        <div className="modal-body">
          <div className="ud-user-header">
            <div className="ud-avatar">{user.name?.split(' ').map(n => n[0]).join('')}</div>
            <h2 className="ud-name">{user.name}</h2>
            <span className="ud-email">{user.email}</span>
            <div className="ud-badges-row"><span className={`role-badge role-${user.role}`}>{user.role === 'spoc' ? 'SPOC' : 'Admin'}</span><span className={`user-status-badge status-${user.status}`}>{user.status}</span></div>
            <span className="ud-dept">{user.departmentName || user.department || ''}</span>
          </div>
          <div className="ud-section">
            <h3 className="ud-section-title">Quick Actions</h3>
            <div className="ud-actions-row">
              <button className="btn btn-outline btn-sm" onClick={onEdit}><i className="fas fa-pen" /> Edit User</button>
              <button className="btn btn-outline btn-sm" onClick={onAssign}><i className="fas fa-layer-group" /> Assign Applications</button>
              <button className="btn btn-outline btn-sm" onClick={onResetPassword}><i className="fas fa-key" /> Reset Password</button>
              <button className="btn btn-danger btn-sm" onClick={() => onDelete(user)}><i className="fas fa-trash" /> Remove User</button>
            </div>
          </div>
          <div className="ud-section">
            <h3 className="ud-section-title">User Information</h3>
            <div className="ud-info-list">
              {[{ label: 'Name', value: user.name }, { label: 'Email', value: user.email }, { label: 'Department', value: user.departmentName || user.department || '' }, { label: 'Role', value: user.role === 'spoc' ? 'SPOC' : 'Admin' }, { label: 'Status', value: user.status?.charAt(0).toUpperCase() + user.status?.slice(1) }].map(f => (
                <div className="ud-info-item" key={f.label}><span className="ud-info-label">{f.label}</span><span className="ud-info-value">{f.value}</span></div>
              ))}
            </div>
          </div>
          <div className="ud-section">
            <div className="ud-section-header-row"><h3 className="ud-section-title">Assigned Applications</h3><button className="btn btn-outline btn-sm" onClick={onAssign}><i className="fas fa-plus" /> Assign</button></div>
            <div className="ud-app-tags">
              {appIds.length > 0 ? appIds.map(id => (
                <span key={id} className="ud-app-tag">{appNameMap[id] || id}<button className="ud-app-remove" onClick={() => onRemoveApp(id)} title="Remove"><i className="fas fa-times" /></button></span>
              )) : <span className="ud-no-apps">No applications assigned</span>}
            </div>
          </div>
        </div>
      </div>
    </>
  )
}

function EditUserModal({ user, onClose, onSave, departments }) {
  const [form, setForm] = useState({ ...user }); const ref = useRef(null)
  useEffect(() => { const handler = e => { if (e.key === 'Escape') onClose() }; document.addEventListener('keydown', handler); return () => document.removeEventListener('keydown', handler) }, [onClose])
  useEffect(() => { const handler = e => { if (ref.current && !ref.current.contains(e.target)) onClose() }; document.addEventListener('mousedown', handler); return () => document.removeEventListener('mousedown', handler) }, [onClose])
  const deptOpts = departments.map(d => d.name).filter(Boolean)
  return (
    <>
      <div className="modal-backdrop open" />
      <div className="modal open" ref={ref}>
        <div className="modal-header"><h2 className="modal-title">Edit User</h2><button className="modal-close" onClick={onClose}><i className="fas fa-times" /></button></div>
        <div className="modal-body">
          {[{ label: 'Name', key: 'name', type: 'text' }, { label: 'Email', key: 'email', type: 'email' }, { label: 'Department', key: 'departmentName', type: 'select', options: deptOpts }, { label: 'Role', key: 'role', type: 'select', options: ['spoc', 'admin'] }, { label: 'Status', key: 'status', type: 'select', options: ['active', 'inactive'] }].map(f => (
            <div className="modal-field" key={f.key}>
              <label className="modal-field-label">{f.label}</label>
              {f.type === 'select' ? (
                <select className="modal-input" value={form[f.key]} onChange={e => setForm({ ...form, [f.key]: e.target.value })}>
                  {f.options.map(o => <option key={o} value={o}>{o.charAt(0).toUpperCase() + o.slice(1)}</option>)}
                </select>
              ) : (
                <input className="modal-input" type={f.type} value={form[f.key] || ''} onChange={e => setForm({ ...form, [f.key]: e.target.value })} />
              )}
            </div>
          ))}
        </div>
        <div className="modal-footer"><button className="btn btn-outline" onClick={onClose}>Cancel</button><button className="btn btn-primary" onClick={() => onSave({ name: form.name, email: form.email, departmentName: form.departmentName, role: form.role, status: form.status })}>Save Changes</button></div>
      </div>
    </>
  )
}

function AddUserModal({ onClose, onSave, departments }) {
  const ref = useRef(null)
  const deptOpts = departments.map(d => d.name).filter(Boolean)
  const [form, setForm] = useState({ name: '', email: '', departmentName: deptOpts[0] || '', role: 'spoc', status: 'active' })
  useEffect(() => { const handler = e => { if (e.key === 'Escape') onClose() }; document.addEventListener('keydown', handler); return () => document.removeEventListener('keydown', handler) }, [onClose])
  useEffect(() => { const handler = e => { if (ref.current && !ref.current.contains(e.target)) onClose() }; document.addEventListener('mousedown', handler); return () => document.removeEventListener('mousedown', handler) }, [onClose])
  return (
    <>
      <div className="modal-backdrop open" />
      <div className="modal open" ref={ref}>
        <div className="modal-header"><h2 className="modal-title">Add User</h2><button className="modal-close" onClick={onClose}><i className="fas fa-times" /></button></div>
        <div className="modal-body">
          <div className="modal-field"><label className="modal-field-label">Name *</label><input className="modal-input" type="text" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="e.g. Jane Smith" autoFocus /></div>
          <div className="modal-field"><label className="modal-field-label">Email *</label><input className="modal-input" type="email" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} placeholder="e.g. jane.smith@company.com" /></div>
          <div className="modal-field"><label className="modal-field-label">Department</label><select className="modal-input" value={form.departmentName} onChange={e => setForm({ ...form, departmentName: e.target.value })}>{deptOpts.map(d => <option key={d} value={d}>{d}</option>)}</select></div>
          <div className="modal-field"><label className="modal-field-label">Role</label><select className="modal-input" value={form.role} onChange={e => setForm({ ...form, role: e.target.value })}><option value="spoc">SPOC</option><option value="admin">Admin</option></select></div>
          <div className="modal-field"><label className="modal-field-label">Status</label><select className="modal-input" value={form.status} onChange={e => setForm({ ...form, status: e.target.value })}><option value="active">Active</option><option value="inactive">Inactive</option></select></div>
        </div>
        <div className="modal-footer"><button className="btn btn-outline" onClick={onClose}>Cancel</button><button className="btn btn-primary" onClick={() => onSave(form)} disabled={!form.name.trim() || !form.email.trim()}>Create User</button></div>
      </div>
    </>
  )
}

function AssignAppsModal({ assigned, allApps, onClose, onSave }) {
  const [assignedIds, setAssignedIds] = useState([]); const [selectedAvailable, setSelectedAvailable] = useState(new Set()); const [selectedAssigned, setSelectedAssigned] = useState(new Set()); const [searchAvail, setSearchAvail] = useState(''); const [searchAssigned, setSearchAssigned] = useState(''); const ref = useRef(null)
  const allAppIds = allApps.map(a => a.id || a).filter(Boolean)
  const appMap = Object.fromEntries(allApps.map(a => [a.id || a, a.name || a]))
  const available = allAppIds.filter(id => !assignedIds.includes(id))
  useEffect(() => { setAssignedIds([...assigned]) }, [assigned])
  useEffect(() => { const handler = e => { if (e.key === 'Escape') onClose() }; document.addEventListener('keydown', handler); return () => document.removeEventListener('keydown', handler) }, [onClose])
  useEffect(() => { const handler = e => { if (ref.current && !ref.current.contains(e.target)) onClose() }; document.addEventListener('mousedown', handler); return () => document.removeEventListener('mousedown', handler) }, [onClose])
  const filteredAvail = available.filter(id => appMap[id].toLowerCase().includes(searchAvail.toLowerCase()))
  const filteredAssigned = assignedIds.filter(id => appMap[id].toLowerCase().includes(searchAssigned.toLowerCase()))
  function toggleAvail(id) { setSelectedAvailable(prev => prev.has(id) ? new Set([...prev].filter(a => a !== id)) : new Set([...prev, id])) }
  function toggleAssigned(id) { setSelectedAssigned(prev => prev.has(id) ? new Set([...prev].filter(a => a !== id)) : new Set([...prev, id])) }
  return (
    <>
      <div className="modal-backdrop open" />
      <div className="modal modal-lg open" ref={ref}>
        <div className="modal-header"><h2 className="modal-title">Assign Applications</h2><button className="modal-close" onClick={onClose}><i className="fas fa-times" /></button></div>
        <div className="modal-body">
          <div className="transfer-list">
            <div className="transfer-col">
              <div className="transfer-col-header">Available ({available.length})</div>
              <div className="transfer-search"><i className="fas fa-search" /><input type="text" placeholder="Search..." value={searchAvail} onChange={e => setSearchAvail(e.target.value)} /></div>
              <div className="transfer-items">{filteredAvail.map(id => (<label key={id} className={`transfer-item${selectedAvailable.has(id) ? ' selected' : ''}`}><input type="checkbox" checked={selectedAvailable.has(id)} onChange={() => toggleAvail(id)} /><span>{appMap[id]}</span></label>))}{filteredAvail.length === 0 && <span className="transfer-empty">No applications</span>}</div>
            </div>
            <div className="transfer-buttons">
              <button className="transfer-btn" onClick={() => { const m = [...selectedAvailable]; setAssignedIds(prev => [...prev, ...m]); setSelectedAvailable(new Set()) }} disabled={selectedAvailable.size === 0}><i className="fas fa-plus" /> Assign</button>
              <button className="transfer-btn" onClick={() => { const m = [...selectedAssigned]; setAssignedIds(prev => prev.filter(id => !m.includes(id))); setSelectedAssigned(new Set()) }} disabled={selectedAssigned.size === 0}><i className="fas fa-minus" /> Remove</button>
            </div>
            <div className="transfer-col">
              <div className="transfer-col-header">Assigned ({assignedIds.length})</div>
              <div className="transfer-search"><i className="fas fa-search" /><input type="text" placeholder="Search..." value={searchAssigned} onChange={e => setSearchAssigned(e.target.value)} /></div>
              <div className="transfer-items">{filteredAssigned.map(id => (<label key={id} className={`transfer-item${selectedAssigned.has(id) ? ' selected' : ''}`}><input type="checkbox" checked={selectedAssigned.has(id)} onChange={() => toggleAssigned(id)} /><span>{appMap[id]}</span></label>))}{filteredAssigned.length === 0 && <span className="transfer-empty">No applications</span>}</div>
            </div>
          </div>
        </div>
        <div className="modal-footer"><button className="btn btn-outline" onClick={onClose}>Cancel</button><button className="btn btn-primary" onClick={() => onSave(assignedIds)}>Save Assignments</button></div>
      </div>
    </>
  )
}
