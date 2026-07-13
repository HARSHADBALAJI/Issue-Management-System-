import { useState, useRef, useCallback } from 'react'
import { timeAgo, getStatusMeta, slaDot } from '../utils/helpers'
import TicketDetailModal from './TicketDetailModal'
import BulkActionModal from './BulkActionModal'

export default function TicketTable({
  tickets,
  page,
  selected,
  onSelectAll,
  onSelectOne,
  onPageChange,
  onViewTicket,
  onUpdate,
  usersList,
}) {
  const [hoverDesc, setHoverDesc] = useState(null)
  const [descPos, setDescPos] = useState({ top: 0, left: 0 })
  const [detailTicketId, setDetailTicketId] = useState(null)
  const [detailTicketStrId, setDetailTicketStrId] = useState(null)
  const [showBulkModal, setShowBulkModal] = useState(false)
  const [bulkAction, setBulkAction] = useState('status')
  const descTimer = useRef(null)
  const popoverRef = useRef(null)

  const start = (page - 1) * 10
  const pageTickets = tickets.slice(start, start + 10)
  const totalPages = Math.ceil(tickets.length / 10) || 1
  const allVisibleSelected = pageTickets.length > 0 && pageTickets.every(t => selected.has(t.id))

  const selectedInView = tickets.filter(t => selected.has(t.id))
  const actualCount = selectedInView.length

  const handleCheckboxChange = (ticket, checked) => {
    onSelectOne(ticket.id)
    if (!checked && selected.size === 0) {
      setDetailTicketId(ticket.ticketId)
      setDetailTicketStrId(ticket.id)
    }
  }

  const handleDescEnter = useCallback((e, desc) => {
    if (!desc) return
    clearTimeout(descTimer.current)
    descTimer.current = setTimeout(() => {
      const rect = e.currentTarget.getBoundingClientRect()
      const spaceBelow = window.innerHeight - rect.bottom
      const tooltipH = Math.min(250, 250)
      const top = spaceBelow >= tooltipH || spaceBelow >= rect.top
        ? rect.bottom + 4
        : Math.max(4, rect.top - tooltipH - 4)
      const left = Math.max(4, Math.min(rect.left, window.innerWidth - 404))
      setDescPos({ top, left })
      setHoverDesc(desc)
    }, 300)
  }, [])

  const handleDescLeave = useCallback(() => {
    clearTimeout(descTimer.current)
    descTimer.current = setTimeout(() => {
      setHoverDesc(null)
    }, 150)
  }, [])

  const handlePopoverEnter = useCallback(() => {
    clearTimeout(descTimer.current)
  }, [])

  const handlePopoverLeave = useCallback(() => {
    descTimer.current = setTimeout(() => {
      setHoverDesc(null)
    }, 150)
  }, [])

  const handleRowClick = (ticket, e) => {
    if (e.target.closest('input,button,a,.col-check')) return
    onViewTicket?.(ticket)
  }

  const handleModalClose = () => {
    setDetailTicketId(null)
    if (detailTicketStrId) {
      onSelectOne(detailTicketStrId)
      setDetailTicketStrId(null)
    }
  }

  const columns = [
    { key: 'id', label: 'Ticket ID', width: 'col-id' },
    { key: 'application', label: 'Application', width: 'col-app' },
    { key: 'subject', label: 'Subject', width: 'col-subject' },
    { key: 'description', label: 'Description', width: 'col-desc' },
    { key: 'status', label: 'Status', width: 'col-status' },
    { key: 'sla', label: 'SLA', width: 'col-sla' },
    { key: 'updated', label: 'Updated', width: 'col-updated' },
    { key: 'raisedBy', label: 'Raised By', width: 'col-user' },
    { key: 'assignedTo', label: 'Assigned To', width: 'col-assignee' },
  ]

  return (
    <>
      {actualCount > 1 && (
        <div className="bulk-bar-sticky">
          <div className="bulk-bar visible">
            <span className="bulk-count">{actualCount} Selected</span>
            <div className="bulk-actions">
              <button className="btn btn-outline btn-sm" onClick={() => { setBulkAction('status'); setShowBulkModal(true) }}>
                <i className="fas fa-tag" /> Change Status
              </button>
              <button className="btn btn-outline btn-sm" onClick={() => { setBulkAction('assign'); setShowBulkModal(true) }}>
                <i className="fas fa-user-plus" /> Assign SPOC
              </button>
            </div>
          </div>
        </div>
      )}
      <div className="table-wrap">
        <table className="ticket-table">
          <thead>
            <tr>
              <th className="col-check">
                <input
                  type="checkbox"
                  checked={allVisibleSelected}
                  onChange={() => onSelectAll(pageTickets, allVisibleSelected)}
                />
              </th>
              {columns.map(col => (
                <th key={col.key} className={col.width}>{col.label}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {pageTickets.length === 0 ? (
              <tr>
                <td colSpan={columns.length + 1} style={{ textAlign: 'center', padding: '24px 16px', color: 'var(--text-muted)' }}>
                  No tickets found matching your filters.
                </td>
              </tr>
            ) : (
              pageTickets.map(ticket => {
                const sm = getStatusMeta(ticket.status)
                const sd = slaDot(ticket.sla)
                const checked = selected.has(ticket.id)
                return (
                  <tr key={ticket.id} className={`td-tr${checked ? ' selected' : ''}`} onClick={e => handleRowClick(ticket, e)}>
                    <td className="col-check" onClick={e => e.stopPropagation()}>
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => handleCheckboxChange(ticket, checked)}
                      />
                    </td>
                    <td className="col-id">
                      <a href="#" className="ticket-id-link" onClick={e => { e.preventDefault(); onViewTicket?.(ticket) }}>
                        {ticket.id}
                      </a>
                    </td>
                    <td className="col-app">{ticket.application}</td>
                    <td className="col-subject"><span className="subject-text">{ticket.subject}</span></td>
                    <td className="col-desc">
                      <div
                        className="desc-box"
                        onMouseEnter={e => handleDescEnter(e, ticket.description)}
                        onMouseLeave={handleDescLeave}
                      >
                        {ticket.description || 'No description available'}
                      </div>
                    </td>
                    <td className="col-status">
                      <span className={`status-pill status-${sm.cls}`}>
                        <span className="dot" />{sm.label}
                      </span>
                    </td>
                    <td className="col-sla">
                      <span className="sla-cell">
                        <span className={`sla-dot ${sd}`} />
                        {ticket.sla}
                      </span>
                    </td>
                    <td className="col-updated"><span className="updated-cell">{timeAgo(ticket.updated)}</span></td>
                    <td className="col-user">{ticket.raisedBy}</td>
                    <td className="col-assignee">{ticket.assignedTo}</td>
                  </tr>
                )
              })
            )}
          </tbody>
        </table>
      </div>
      <PaginationBar
        page={page}
        totalPages={totalPages}
        total={tickets.length}
        start={start}
        onPageChange={onPageChange}
      />

      {hoverDesc && (
        <div ref={popoverRef} className="desc-popover" style={{ top: descPos.top, left: descPos.left }} onMouseEnter={handlePopoverEnter} onMouseLeave={handlePopoverLeave}>
          {hoverDesc}
        </div>
      )}

      {detailTicketId && (
        <TicketDetailModal ticketId={detailTicketId} onClose={handleModalClose} onUpdate={onUpdate} usersList={usersList} simple />
      )}

      {showBulkModal && (
        <BulkActionModal
          defaultAction={bulkAction}
          ticketIds={selectedInView.map(t => t.ticketId)}
          usersList={usersList}
          onClose={() => setShowBulkModal(false)}
          onUpdate={onUpdate}
        />
      )}
    </>
  )
}

function PaginationBar({ page, totalPages, total, start, onPageChange }) {
  if (total === 0) return null
  const pages = []
  for (let i = 1; i <= totalPages; i++) pages.push(i)
  return (
    <div className="pagination">
      <span className="pagination-info">
        Showing {total ? start + 1 : 0} to {Math.min(start + 10, total)} of {total} tickets
      </span>
      <div className="pagination-controls">
        <button className="pagination-btn" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
          <i className="fas fa-chevron-left" />
        </button>
        {pages.map(p => (
          <button key={p} className={`pagination-btn${p === page ? ' active' : ''}`} onClick={() => onPageChange(p)}>
            {p}
          </button>
        ))}
        <button className="pagination-btn" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
          <i className="fas fa-chevron-right" />
        </button>
      </div>
    </div>
  )
}
