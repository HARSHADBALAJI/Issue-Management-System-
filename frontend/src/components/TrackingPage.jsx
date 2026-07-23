import { useState, useEffect } from 'react'
import api from '../services/apiClient'

const statusColors = {
  open: '#0D6EFD', in_progress: '#FFC107', waiting: '#FD7E14',
  resolved: '#198754', closed: '#6C757D'
}

const priorityColors = {
  critical: '#DC3545', high: '#FD7E14', medium: '#FFC107', low: '#0D6EFD'
}

function timeSince(d) {
  if (!d) return ''
  const date = new Date(d)
  const s = Math.round((Date.now() - date.getTime()) / 1000)
  if (s < 60) return `${s}s ago`
  if (s < 3600) return `${Math.floor(s / 60)}m ago`
  if (s < 86400) return `${Math.floor(s / 3600)}h ago`
  return `${Math.floor(s / 86400)}d ago`
}

function formatDate(d) {
  if (!d) return '-'
  return new Date(d).toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export default function TrackingPage({ ticketId, token }) {
  const [ticket, setTicket] = useState(null)
  const [myTickets, setMyTickets] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [selectedTicketId, setSelectedTicketId] = useState(null)

  useEffect(() => {
    if (!ticketId || !token) {
      setError('Invalid tracking link. No ticket ID or token provided.')
      setLoading(false)
      return
    }
    setLoading(true)
    setError(null)
    api.get(`/tracking/validate/${ticketId}`, { params: { token } })
      .then(res => {
        const data = res.data
        if (data.isValid) {
          setTicket(data)
          setSelectedTicketId(data.ticketId)
          api.get(`/tracking/my-tickets/${encodeURIComponent(data.requesterEmail)}`, { params: { ticketId: data.ticketId } })
            .then(r => setMyTickets(r.data.tickets || []))
            .catch(() => {})
        } else {
          setError(data.errorMessage || 'Access Link Invalid or Expired')
        }
      })
      .catch(() => setError('Access Link Invalid or Expired'))
      .finally(() => setLoading(false))
  }, [ticketId, token])

  if (loading) {
    return (
      <div style={{ minHeight: '100vh', background: '#F4F5F7', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: "'Segoe UI',Arial,sans-serif" }}>
        <div style={{ textAlign: 'center', color: '#666' }}>
          <div style={{ fontSize: 32, marginBottom: 12 }}><i className="fas fa-spinner fa-spin" /></div>
          <div style={{ fontSize: 16 }}>Validating your access link...</div>
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <div style={{ minHeight: '100vh', background: '#F4F5F7', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: "'Segoe UI',Arial,sans-serif" }}>
        <div style={{ maxWidth: 480, background: '#fff', borderRadius: 12, padding: '48px 40px', textAlign: 'center', boxShadow: '0 2px 12px rgba(0,0,0,0.08)' }}>
          <div style={{ width: 64, height: 64, borderRadius: '50%', background: '#FEE2E2', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 20px' }}>
            <i className="fas fa-link" style={{ fontSize: 28, color: '#DC3545' }} />
          </div>
          <h2 style={{ margin: '0 0 8px', color: '#1F2937', fontSize: 20 }}>Access Link Invalid or Expired</h2>
          <p style={{ margin: '0 0 24px', color: '#6B7280', lineHeight: 1.6 }}>{error}</p>
          <p style={{ margin: 0, color: '#9CA3AF', fontSize: 13 }}>Please check your email for a valid tracking link or contact support.</p>
        </div>
      </div>
    )
  }

  if (!ticket) return null

  const stColor = ticket.statusColor || statusColors[ticket.statusName?.toLowerCase()] || '#6C757D'

  return (
    <div style={{ minHeight: '100vh', background: '#F4F5F7', fontFamily: "'Segoe UI',Arial,sans-serif" }}>
      <header style={{ background: '#005BAC', padding: '0 32px', height: 56, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ color: '#fff', fontSize: 18, fontWeight: 600 }}>Issue Management System</div>
        <div style={{ color: 'rgba(255,255,255,0.8)', fontSize: 13 }}>Ticket Tracking</div>
      </header>

      <div style={{ maxWidth: 1100, margin: '0 auto', padding: '24px 24px 48px' }}>
        <div style={{ display: 'flex', gap: 24 }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            {selectedTicketId && myTickets.length > 1 && (
              <div style={{ marginBottom: 20 }}>
                <div style={{ background: '#fff', borderRadius: 10, border: '1px solid #E5E7EB', padding: '16px 20px' }}>
                  <div style={{ fontSize: 13, fontWeight: 600, color: '#374151', marginBottom: 10 }}>My Tickets ({myTickets.length})</div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                    {myTickets.map(mt => (
                      <div key={mt.ticketId} onClick={() => { if (mt.ticketId !== selectedTicketId) { setLoading(true); setError(null); setTicket(null); window.history.pushState({}, '', `/track/${mt.ticketId}?token=${token}`); window.location.reload() } }}
                        style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '8px 12px', borderRadius: 6, cursor: 'pointer', background: mt.ticketId === selectedTicketId ? '#EFF6FF' : 'transparent', border: mt.ticketId === selectedTicketId ? '1px solid #BFDBFE' : '1px solid transparent', transition: 'all .15s' }}>
                        <span style={{ fontSize: 13, fontWeight: 600, color: '#005BAC', minWidth: 80 }}>{mt.ticketNumber}</span>
                        <span style={{ flex: 1, fontSize: 13, color: '#374151', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{mt.subject}</span>
                        <span style={{ fontSize: 11, padding: '2px 8px', borderRadius: 10, background: (statusColors[mt.statusName?.toLowerCase()] || '#6C757D') + '20', color: statusColors[mt.statusName?.toLowerCase()] || '#6C757D', fontWeight: 600 }}>{mt.statusName}</span>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            )}

            <div style={{ background: '#fff', borderRadius: 10, border: '1px solid #E5E7EB', overflow: 'hidden' }}>
              <div style={{ padding: '20px 24px', borderBottom: '1px solid #E5E7EB', display: 'flex', alignItems: 'center', gap: 12 }}>
                <span style={{ fontSize: 20, fontWeight: 700, color: '#111827' }}>{ticket.ticketNumber}</span>
                <span style={{ fontSize: 13, padding: '3px 10px', borderRadius: 12, background: stColor + '20', color: stColor, fontWeight: 600 }}>{ticket.statusName}</span>
                <span style={{ fontSize: 12, padding: '2px 8px', borderRadius: 10, background: (priorityColors[ticket.priority] || '#6C757D') + '20', color: priorityColors[ticket.priority] || '#6C757D', fontWeight: 600, textTransform: 'capitalize' }}>{ticket.priority}</span>
              </div>

              <div style={{ padding: '20px 24px' }}>
                <h3 style={{ margin: '0 0 16px', fontSize: 16, color: '#111827' }}>{ticket.subject}</h3>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px 24px', marginBottom: 20 }}>
                  <div><span style={{ fontSize: 12, color: '#9CA3AF' }}>Application</span><div style={{ fontSize: 14, color: '#374151' }}>{ticket.applicationName}</div></div>
                  <div><span style={{ fontSize: 12, color: '#9CA3AF' }}>Assigned To</span><div style={{ fontSize: 14, color: '#374151' }}>{ticket.assignedToName || 'Unassigned'}</div></div>
                  <div><span style={{ fontSize: 12, color: '#9CA3AF' }}>Raised By</span><div style={{ fontSize: 14, color: '#374151' }}>{ticket.requesterName}</div></div>
                  <div><span style={{ fontSize: 12, color: '#9CA3AF' }}>Created</span><div style={{ fontSize: 14, color: '#374151' }}>{formatDate(ticket.createdAt)}</div></div>
                  {ticket.resolvedAt && <div><span style={{ fontSize: 12, color: '#9CA3AF' }}>Resolved</span><div style={{ fontSize: 14, color: '#198754' }}>{formatDate(ticket.resolvedAt)}</div></div>}
                  {ticket.closedAt && <div><span style={{ fontSize: 12, color: '#9CA3AF' }}>Closed</span><div style={{ fontSize: 14, color: '#6C757D' }}>{formatDate(ticket.closedAt)}</div></div>}
                </div>

                {ticket.description && (
                  <div style={{ marginBottom: 24 }}>
                    <div style={{ fontSize: 11, textTransform: 'uppercase', color: '#9CA3AF', letterSpacing: 0.5, marginBottom: 8 }}>Description</div>
                    <div style={{ background: '#F9FAFB', border: '1px solid #E5E7EB', borderRadius: 8, padding: 16, fontSize: 14, color: '#374151', lineHeight: 1.6, whiteSpace: 'pre-wrap' }}>{ticket.description}</div>
                  </div>
                )}

                <div style={{ fontSize: 11, textTransform: 'uppercase', color: '#9CA3AF', letterSpacing: 0.5, marginBottom: 12 }}>Conversation</div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                  {(ticket.messages || []).map(msg => (
                    <div key={msg.id} style={{ border: '1px solid #E5E7EB', borderRadius: 8, overflow: 'hidden' }}>
                      <div style={{ background: '#F9FAFB', padding: '10px 16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid #E5E7EB' }}>
                        <div>
                          <span style={{ fontSize: 13, fontWeight: 600, color: '#374151' }}>{msg.senderName}</span>
                          <span style={{ fontSize: 11, color: '#9CA3AF', marginLeft: 8, padding: '1px 6px', borderRadius: 4, background: msg.senderType === 'Requester' ? '#DBEAFE' : msg.senderType === 'System' ? '#F3F4F6' : '#D1FAE5', color: msg.senderType === 'Requester' ? '#1D4ED8' : msg.senderType === 'System' ? '#6B7280' : '#065F46' }}>{msg.senderType}</span>
                        </div>
                        <span style={{ fontSize: 12, color: '#9CA3AF' }}>{formatDate(msg.createdAt)}</span>
                      </div>
                      <div style={{ padding: '12px 16px', fontSize: 14, color: '#374151', lineHeight: 1.6, whiteSpace: 'pre-wrap' }}>{msg.content}</div>
                      {msg.attachments && msg.attachments.length > 0 && (
                        <div style={{ padding: '0 16px 12px', display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                          {msg.attachments.map(att => (
                            <div key={att.id} style={{ display: 'flex', alignItems: 'center', gap: 6, padding: '4px 10px', background: '#F3F4F6', borderRadius: 4, fontSize: 12, color: '#6B7280' }}>
                              <i className="fas fa-paperclip" style={{ fontSize: 10 }} /> {att.fileName}
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>

          <div style={{ width: 300, flexShrink: 0 }}>
            <div style={{ background: '#fff', borderRadius: 10, border: '1px solid #E5E7EB', padding: '20px', marginBottom: 16 }}>
              <div style={{ fontSize: 13, fontWeight: 600, color: '#374151', marginBottom: 16 }}>Status Timeline</div>
              <div style={{ position: 'relative', paddingLeft: 20 }}>
                <div style={{ position: 'absolute', left: 6, top: 4, bottom: 4, width: 2, background: '#E5E7EB' }} />
                {(ticket.statusHistory || []).map((sh, i) => (
                  <div key={i} style={{ position: 'relative', marginBottom: 16 }}>
                    <div style={{ position: 'absolute', left: -17, top: 4, width: 10, height: 10, borderRadius: '50%', background: '#005BAC', border: '2px solid #fff', boxShadow: '0 0 0 2px #005BAC' }} />
                    <div style={{ fontSize: 13, color: '#111827', fontWeight: 500 }}>{sh.fromStatus} → {sh.toStatus}</div>
                    {sh.changedBy && <div style={{ fontSize: 12, color: '#6B7280', marginTop: 2 }}>by {sh.changedBy}</div>}
                    {sh.remarks && <div style={{ fontSize: 12, color: '#9CA3AF', marginTop: 2 }}>{sh.remarks}</div>}
                    <div style={{ fontSize: 11, color: '#9CA3AF', marginTop: 2 }}>{formatDate(sh.createdAt)}</div>
                  </div>
                ))}
                {(!ticket.statusHistory || ticket.statusHistory.length === 0) && (
                  <div style={{ fontSize: 13, color: '#9CA3AF' }}>No status changes yet</div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>

      <footer style={{ background: '#F8F9FA', borderTop: '1px solid #E5E7EB', padding: '16px 32px', textAlign: 'center', fontSize: 11, color: '#9CA3AF' }}>
        This is an automated notification generated by the Issue Management System.
      </footer>
    </div>
  )
}
