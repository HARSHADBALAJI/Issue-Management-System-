import { useState, useRef, useEffect } from 'react'
import { ticketService } from '../services/ticketService'
import SearchableSelect from './SearchableSelect'

const statusFlow = ['open', 'in_progress', 'waiting', 'resolved', 'closed']
const statusMap = { 'open': 5, 'in_progress': 1, 'waiting': 2, 'resolved': 3, 'closed': 4 }

export default function BulkActionModal({ ticketIds, usersList, onClose, onUpdate, defaultAction = 'status' }) {
  const [action, setAction] = useState(defaultAction)
  const [newStatus, setNewStatus] = useState('')
  const [assignedSpoc, setAssignedSpoc] = useState('')
  const [applying, setApplying] = useState(false)
  const modalRef = useRef(null)

  useEffect(() => {
    const handler = e => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [onClose])

  async function handleApply() {
    if (applying) return
    setApplying(true)
    try {
      if (action === 'status') {
        if (!newStatus) { alert('Please select a status.'); setApplying(false); return }
        await ticketService.bulkUpdateStatus({ ticketIds, statusId: statusMap[newStatus] })
      } else {
        if (!assignedSpoc) { alert('Please select a SPOC.'); setApplying(false); return }
        const match = usersList.find(u => `${u.name} <${u.email}>` === assignedSpoc)
        if (!match) { alert('Selected user not found.'); setApplying(false); return }
        await ticketService.bulkAssign({ ticketIds, assignedToUserId: match.id })
      }
      await onUpdate?.()
      onClose()
    } catch { alert('Operation failed.'); setApplying(false) }
  }

  return (
    <div className="modal-backdrop open" onClick={onClose}>
      <div className="modal bam open" ref={modalRef} onClick={e => e.stopPropagation()} style={{ maxWidth: 420 }}>
        <div className="modal-header">
          <h3>Bulk Actions — {ticketIds.length} ticket{ticketIds.length > 1 ? 's' : ''} selected</h3>
          <button className="modal-close" onClick={onClose}><i className="fas fa-times" /></button>
        </div>
        <div className="modal-body">
          <div className="bam-tabs" style={{ display: 'flex', gap: 4, marginBottom: 16 }}>
            <button
              className={`btn btn-sm${action === 'status' ? ' btn-primary' : ' btn-outline'}`}
              onClick={() => setAction('status')}
            >
              <i className="fas fa-tag" /> Change Status
            </button>
            <button
              className={`btn btn-sm${action === 'assign' ? ' btn-primary' : ' btn-outline'}`}
              onClick={() => setAction('assign')}
            >
              <i className="fas fa-user-plus" /> Assign SPOC
            </button>
          </div>

          {action === 'status' ? (
            <div className="bam-field">
              <label className="bam-label">New Status</label>
              <select className="bam-select" value={newStatus} onChange={e => setNewStatus(e.target.value)}>
                <option value="">Select status...</option>
                {statusFlow.map(s => (
                  <option key={s} value={s}>{s.replace(/_/g, ' ').replace(/\b\w/g, l => l.toUpperCase())}</option>
                ))}
              </select>
            </div>
          ) : (
            <div className="bam-field">
              <label className="bam-label">Assign SPOC</label>
              <SearchableSelect
                value={assignedSpoc}
                options={(usersList || []).map(u => `${u.name} <${u.email}>`)}
                onChange={setAssignedSpoc}
                placeholder="Search SPOC..."
                searchPlaceholder="Type name or email..."
                clearLabel="Clear"
              />
            </div>
          )}
        </div>
        <div className="modal-footer">
          <button className="btn btn-outline btn-sm" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary btn-sm" onClick={handleApply} disabled={applying}>
            {applying ? <><i className="fas fa-spinner fa-spin" /> Applying...</> : 'Apply'}
          </button>
        </div>
      </div>
    </div>
  )
}
