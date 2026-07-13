import { useState, useRef, useEffect } from 'react'
import { departmentService } from '../services/departmentService'
import { extractError } from '../utils/helpers'

export default function DepartmentsPage({ departments = [], onNotify, onRefresh }) {
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [showAddModal, setShowAddModal] = useState(false)
  const [showEditModal, setShowEditModal] = useState(false)
  const [editTarget, setEditTarget] = useState(null)
  const perPage = 10

  const filtered = departments.filter(d => !search || d.name.toLowerCase().includes(search.toLowerCase()))
  const totalPages = Math.ceil(filtered.length / perPage) || 1
  const safePage = Math.min(page, totalPages)
  const start = (safePage - 1) * perPage
  const pageItems = filtered.slice(start, start + perPage)

  const totalDepts = departments.length
  const totalUsers = departments.reduce((s, d) => s + (d.userCount || 0), 0)

  async function handleAdd(data) {
    try {
      await departmentService.create({ name: data.name, headName: data.head })
      await onRefresh?.()
      setShowAddModal(false)
      onNotify?.('Department Created', `${data.name} has been created.`)
    } catch (e) {
      alert(extractError(e, 'Failed to create department'))
    }
  }

  async function handleEdit(data) {
    try {
      await departmentService.update(data.id, { name: data.name, headName: data.head })
      await onRefresh?.()
      setShowEditModal(false)
      setEditTarget(null)
      onNotify?.('Department Updated', `${data.name} has been updated.`)
    } catch (e) {
      alert(extractError(e, 'Failed to update department'))
    }
  }

  async function handleDelete(dept) {
    if (!window.confirm(`Delete department "${dept.name}"? This cannot be undone.`)) return
    try {
      await departmentService.delete(dept.id)
      await onRefresh?.()
      onNotify?.('Department Deleted', `${dept.name} has been deleted.`)
    } catch (e) {
      alert(extractError(e, 'Failed to delete department'))
    }
  }

  return (
    <div className="depts-page">
      <div className="page-heading">
        <div>
          <h1>Departments</h1>
          <p className="page-subtitle">Manage organizational departments and view user distribution.</p>
        </div>
        <button className="btn btn-primary" onClick={() => setShowAddModal(true)}><i className="fas fa-plus" /> Add Department</button>
      </div>

      <div className="kpi-row" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
        {[
          { icon: 'fa-building', count: totalDepts, label: 'Total Departments' },
          { icon: 'fa-users', count: totalUsers, label: 'Total Users Across Departments' },
          { icon: 'fa-user-tie', count: departments.filter(d => (d.spocCount || 0) > 0).length, label: 'Departments with SPOCs' },
        ].map(c => (
          <div className="kpi-card" key={c.label}>
            <div className="kpi-icon"><i className={`fas ${c.icon}`} /></div>
            <div className="kpi-body">
              <div className="kpi-count">{c.count.toLocaleString()}</div>
              <div className="kpi-label">{c.label}</div>
            </div>
          </div>
        ))}
      </div>

      <div className="filters">
        <div className="filter-item search-box" style={{ maxWidth: 320 }}>
          <i className="fas fa-search" />
          <input type="text" placeholder="Search departments..." value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} />
        </div>
      </div>

      <div className="table-wrap">
        <table className="dept-table">
          <thead>
            <tr>
              <th className="col-d-name">Department Name</th>
              <th className="col-d-head">Department Head</th>
              <th className="col-d-users">Total Users</th>
              <th className="col-d-spoc">SPOCs</th>
              <th className="col-d-admin">Admins</th>
              <th className="col-d-actions">Actions</th>
            </tr>
          </thead>
          <tbody>
            {pageItems.length === 0 ? (
              <tr><td colSpan={6} style={{ textAlign: 'center', padding: '48px 16px', color: 'var(--text-muted)' }}>No departments found.</td></tr>
            ) : (
              pageItems.map(d => (
                <tr key={d.id} className="dept-row">
                  <td className="col-d-name">
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <div className="dept-icon"><i className="fas fa-building" /></div>
                      <div>
                        <div style={{ fontWeight: 600, fontSize: '.95rem' }}>{d.name}</div>
                      </div>
                    </div>
                  </td>
                  <td className="col-d-head">{d.headName || d.head || '—'}</td>
                  <td className="col-d-users" style={{ fontWeight: 600 }}>{d.userCount || 0}</td>
                  <td className="col-d-spoc"><span className="role-badge role-spoc">{d.spocCount || 0}</span></td>
                  <td className="col-d-admin"><span className="role-badge role-admin">{d.adminCount || 0}</span></td>
                  <td className="col-d-actions" onClick={e => e.stopPropagation()}>
                    <DeptActionMenu
                      onEdit={() => { setEditTarget({ ...d }); setShowEditModal(true) }}
                      onDelete={() => handleDelete(d)}
                    />
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="pagination">
        <span className="pagination-info">Showing {filtered.length ? start + 1 : 0} to {Math.min(start + perPage, filtered.length)} of {filtered.length} departments</span>
        <div className="pagination-controls">
          <button className="pagination-btn" disabled={safePage <= 1} onClick={() => setPage(safePage - 1)}><i className="fas fa-chevron-left" /></button>
          {Array.from({ length: totalPages }, (_, i) => i + 1).map(p => (
            <button key={p} className={`pagination-btn${p === safePage ? ' active' : ''}`} onClick={() => setPage(p)}>{p}</button>
          ))}
          <button className="pagination-btn" disabled={safePage >= totalPages} onClick={() => setPage(safePage + 1)}><i className="fas fa-chevron-right" /></button>
        </div>
      </div>

      {showAddModal && <DeptFormModal title="Add Department" onClose={() => setShowAddModal(false)} onSave={handleAdd} />}
      {showEditModal && editTarget && <DeptFormModal title="Edit Department" dept={editTarget} onClose={() => { setShowEditModal(false); setEditTarget(null) }} onSave={handleEdit} />}
    </div>
  )
}

function DeptActionMenu({ onEdit, onDelete }) {
  const [open, setOpen] = useState(false)
  const ref = useRef(null)
  useEffect(() => { const handler = e => { if (ref.current && !ref.current.contains(e.target)) setOpen(false) }; document.addEventListener('mousedown', handler); return () => document.removeEventListener('mousedown', handler) }, [])
  return (
    <div ref={ref} style={{ position: 'relative' }}>
      <button className="action-btn" onClick={e => { e.stopPropagation(); setOpen(!open) }}><i className="fas fa-ellipsis-v" /></button>
      {open && (
        <div className="dropdown-menu show" style={{ position: 'absolute', right: 0, top: '100%', zIndex: 10 }}>
          <a href="#" onClick={e => { e.preventDefault(); setOpen(false); onEdit() }}>Edit Department</a>
          <a href="#" onClick={e => { e.preventDefault(); setOpen(false); onDelete() }} style={{ color: '#B91C1C' }}>Delete Department</a>
        </div>
      )}
    </div>
  )
}

function DeptFormModal({ title, dept, onClose, onSave }) {
  const ref = useRef(null)
  const [form, setForm] = useState(dept ? { name: dept.name, head: dept.headName || dept.head || '' } : { name: '', head: '' })
  useEffect(() => { const handler = e => { if (e.key === 'Escape') onClose() }; document.addEventListener('keydown', handler); return () => document.removeEventListener('keydown', handler) }, [onClose])
  useEffect(() => { const handler = e => { if (ref.current && !ref.current.contains(e.target)) onClose() }; document.addEventListener('mousedown', handler); return () => document.removeEventListener('mousedown', handler) }, [onClose])
  function handleSave() { if (dept) onSave({ ...dept, ...form }); else onSave(form) }
  return (
    <>
      <div className="modal-backdrop open" />
      <div className="modal open" ref={ref}>
        <div className="modal-header"><h2 className="modal-title">{title}</h2><button className="modal-close" onClick={onClose}><i className="fas fa-times" /></button></div>
        <div className="modal-body">
          <div className="modal-field"><label className="modal-field-label">Department Name *</label><input className="modal-input" type="text" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="e.g. Engineering" autoFocus /></div>
          <div className="modal-field"><label className="modal-field-label">Department Head</label><input className="modal-input" type="text" value={form.head} onChange={e => setForm({ ...form, head: e.target.value })} placeholder="e.g. John Doe" /></div>
        </div>
        <div className="modal-footer"><button className="btn btn-outline" onClick={onClose}>Cancel</button><button className="btn btn-primary" onClick={handleSave} disabled={!form.name.trim()}>{dept ? 'Save Changes' : 'Create Department'}</button></div>
      </div>
    </>
  )
}
