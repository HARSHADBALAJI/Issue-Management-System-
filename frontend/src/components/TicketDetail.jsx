import { useState, useMemo, useEffect, useCallback, useRef } from 'react'
import { ticketService } from '../services/ticketService'
import { slaService } from '../services/slaService'
import { onEvent, offEvent } from '../services/signalRService'
import SpocSelector from './SpocSelector'

function getFileIcon(contentType) {
  if (!contentType) return 'fa-file-alt'
  if (contentType.startsWith('image/')) return 'fa-file-image'
  if (contentType.includes('pdf')) return 'fa-file-pdf'
  if (contentType.includes('word') || contentType.includes('document')) return 'fa-file-word'
  if (contentType.includes('excel') || contentType.includes('spreadsheet') || contentType.includes('xls')) return 'fa-file-excel'
  if (contentType.includes('powerpoint') || contentType.includes('presentation') || contentType.includes('ppt')) return 'fa-file-powerpoint'
  if (contentType.includes('zip') || contentType.includes('rar') || contentType.includes('7z')) return 'fa-file-archive'
  if (contentType.includes('text') || contentType.includes('txt')) return 'fa-file-alt'
  return 'fa-file-alt'
}

function formatDate(d) {
  if (!d) return ''
  const mo = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
  let h = d.getHours()
  const ampm = h >= 12 ? 'PM' : 'AM'
  h = h % 12 || 12
  const pad = n => String(n).padStart(2, '0')
  return `${mo[d.getMonth()]} ${pad(d.getDate())}, ${d.getFullYear()} ${pad(h)}:${pad(d.getMinutes())} ${ampm}`
}

function normalizeTicketDetail(t) {
  const statusMap = { 'Open': 'open', 'In Progress': 'in_progress', 'Waiting': 'waiting', 'Resolved': 'resolved', 'Closed': 'closed' }
  const status = statusMap[t.statusName] || (t.statusName || '').toLowerCase().replace(/\s+/g, '_')
  let sla = ''
  if (t.slaDeadline) {
    const deadline = new Date(t.slaDeadline)
    const now = new Date()
    const diff = deadline - now
    if (diff < 0) sla = `Overdue by ${Math.ceil(Math.abs(diff) / 3600000)}h`
    else { const h = Math.floor(diff / 3600000); const m = Math.floor((diff % 3600000) / 60000); sla = `${h}h ${m}m left` }
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
    assignedToUserId: t.assignedToId || null,
    sla,
    isSlaBreached: !!t.slaBreachedAt,
    slaDeadline: t.slaDeadline ? new Date(t.slaDeadline) : null,
    updated: t.updatedAt ? new Date(t.updatedAt) : new Date(),
    created: t.createdAt ? new Date(t.createdAt) : new Date(),
    messages: (t.messages || []).map(m => ({
      id: m.id,
      userName: m.createdByName || '',
      userEmail: m.createdByEmail || '',
      body: m.content || '',
      isInternal: m.isInternal || false,
      isFromRequester: m.isFromRequester || false,
      messageSourceType: m.messageSourceType || '',
      createdDate: m.createdAt ? new Date(m.createdAt) : new Date(),
      attachments: (m.attachments || []).map(a => ({
        id: a.id,
        fileName: a.fileName || a.name || '',
        fileSize: a.fileSize || 0,
        contentType: a.contentType || '',
      })),
    })),
    statusHistory: (t.statusHistory || []).map(sh => ({
      id: sh.id,
      fromStatusName: sh.fromStatus || 'Unknown',
      toStatusName: sh.toStatus || 'Unknown',
      remarks: sh.note || '',
      changedDate: sh.createdAt ? new Date(sh.createdAt) : new Date(),
    })),
    correctiveActions: (t.correctiveActions || []).map(ca => ({
      id: ca.id,
      description: ca.description || '',
      performedDate: ca.createdAt ? new Date(ca.createdAt) : new Date(),
    })),
  }
}

const statusFlow = ['open', 'in_progress', 'waiting', 'resolved', 'closed']

function isImageFile(contentType) {
  return contentType && contentType.startsWith('image/')
}

function formatFileSize(bytes) {
  if (!bytes) return ''
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export default function TicketDetail({ ticketId, onBack, onUpdate, usersList }) {
  const [ticket, setTicket] = useState(null)
  const [slaInfo, setSlaInfo] = useState(null)
  const [slaLoading, setSlaLoading] = useState(false)
  const [replyText, setReplyText] = useState('')
  const [correctiveActionText, setCorrectiveActionText] = useState('')
  const [sending, setSending] = useState(false)
  const [submittingCA, setSubmittingCA] = useState(false)
  const [changingStatus, setChangingStatus] = useState(false)
  const [reopening, setReopening] = useState(false)
  const [selectedFiles, setSelectedFiles] = useState([])
  const [previewAttachment, setPreviewAttachment] = useState(null)
  const [toastMsg, setToastMsg] = useState(null)
  const fileInputRef = useRef(null)
  const threadRef = useRef(null)

  useEffect(() => {
    if (toastMsg) {
      const t = setTimeout(() => setToastMsg(null), 3500)
      return () => clearTimeout(t)
    }
  }, [toastMsg])

  const fetchTicket = useCallback(async () => {
    if (!ticketId) return
    setSlaLoading(true)
    try {
      const t = await ticketService.getById(ticketId)
      setTicket(normalizeTicketDetail(t))
    } catch (err) {
      console.error('fetchTicket failed:', err)
    }
    try {
      const sla = await slaService.getTicketSla(ticketId)
      if (sla) {
        const deadline = sla.deadlineAt ? new Date(sla.deadlineAt) : null
        setSlaInfo({
          consumed: sla.remainingTime || 'N/A',
          pct: sla.remainingPercent || 0,
          breached: !!sla.breachedAt,
          deadline,
          status: sla.status,
          priority: sla.priority,
        })
      }
    } catch {}
    setSlaLoading(false)
  }, [ticketId])

  useEffect(() => { fetchTicket() }, [fetchTicket])

  useEffect(() => {
    if (!ticketId) return
    const handler = (data) => {
      if (data.id === ticketId) fetchTicket()
    }
    onEvent('TicketUpdated', handler)
    return () => offEvent('TicketUpdated')
  }, [ticketId, fetchTicket])

  const timeline = useMemo(() => {
    if (!ticket) return []
    const entries = []
    if (ticket.messages) {
      ticket.messages.forEach(m => {
        entries.push({
          id: `msg-${m.id}`,
          type: m.isInternal ? 'internal' : 'reply',
          author: {
            name: m.userName || 'Unknown',
            email: m.userEmail || '',
            role: m.isInternal ? 'SPOC' : (m.isFromRequester ? 'Requester' : (m.messageSourceType === 'System' ? 'System' : 'User')),
            initials: (m.userName || '?').split(' ').map(s => s[0]).join('').toUpperCase(),
            isAgent: !m.isFromRequester && m.messageSourceType !== 'Requester',
          },
          body: m.body || '',
          timestamp: m.createdDate ? new Date(m.createdDate) : new Date(),
          attachments: (m.attachments || []).map(a => ({
            id: a.id, name: a.fileName || a.name || '',
            size: formatFileSize(a.fileSize),
            contentType: a.contentType || '',
          })),
        })
      })
    }
    if (ticket.statusHistory) {
      ticket.statusHistory.forEach(sh => {
        entries.push({
          id: `sh-${sh.id || Math.random()}`,
          type: 'system', icon: 'fa-arrow-right', color: '#F59E0B',
          label: `Status changed from ${sh.fromStatusName || 'Unknown'} to ${sh.toStatusName || 'Unknown'}${sh.remarks ? ` — ${sh.remarks}` : ''}`,
          timestamp: sh.changedDate ? new Date(sh.changedDate) : new Date(),
        })
      })
    }
    if (ticket.correctiveActions) {
      ticket.correctiveActions.forEach(ca => {
        entries.push({
          id: `ca-${ca.id || Math.random()}`,
          type: 'system', icon: 'fa-wrench', color: '#10B981',
          label: `Corrective action: ${ca.description || ca.action || ''}`,
          timestamp: ca.performedDate ? new Date(ca.performedDate) : new Date(),
        })
      })
    }
    entries.sort((a, b) => (b.timestamp?.getTime() || 0) - (a.timestamp?.getTime() || 0))
    return entries
  }, [ticket])

  const conversationMessages = useMemo(() => timeline.filter(m => m.type !== 'system'), [timeline])

  useEffect(() => {
    if (threadRef.current) {
      threadRef.current.scrollTop = 0
    }
  }, [conversationMessages])

  const isClosed = ticket?.status === 'closed'

  function handleFileSelect(e) {
    const newFiles = Array.from(e.target.files || [])
    setSelectedFiles(prev => [...prev, ...newFiles].slice(0, 10))
    e.target.value = ''
  }

  function handleRemoveFile(index) {
    setSelectedFiles(prev => prev.filter((_, i) => i !== index))
  }

  async function handleSend() {
    if (!replyText.trim() || sending || isClosed) return
    setSending(true)
    try {
      await ticketService.addMessage(
        ticket.ticketId,
        { content: replyText, isInternal: false },
        selectedFiles.length > 0 ? selectedFiles : null
      )
      setReplyText('')
      setSelectedFiles([])
      await fetchTicket()
      await onUpdate?.()
      setToastMsg('Reply sent successfully')
    } catch { alert('Failed to send reply.') }
    setSending(false)
  }

  async function handleSubmitCA() {
    if (!correctiveActionText.trim() || submittingCA || isClosed) return
    setSubmittingCA(true)
    try {
      await ticketService.addCorrectiveAction(ticket.ticketId, { description: correctiveActionText, performedAt: new Date().toISOString() })
      setCorrectiveActionText('')
      await fetchTicket()
      await onUpdate?.()
      setToastMsg('Corrective action submitted successfully')
    } catch { alert('Failed to submit corrective action.') }
    setSubmittingCA(false)
  }

  async function handleChangeStatus(newStatus) {
    if (changingStatus || isClosed) return
    setChangingStatus(true)
    const statusMap = { 'open': 5, 'in_progress': 1, 'waiting': 2, 'resolved': 3, 'closed': 4 }
    try {
      await ticketService.updateStatus(ticket.ticketId, { statusId: statusMap[newStatus], remarks: `Status changed to ${newStatus}` })
      await fetchTicket()
      await onUpdate?.()
      setToastMsg(`Status changed to ${newStatus.replace(/_/g, ' ')}`)
    } catch (err) {
      const msg = err?.response?.data?.error || err?.message || 'Failed to change status.'
      alert(msg)
    }
    setChangingStatus(false)
  }

  async function handleAssign(userId) {
    if (!userId || isClosed) return
    try {
      await ticketService.assign(ticket.ticketId, { assignedToUserId: userId })
      await fetchTicket()
      await onUpdate?.()
      setToastMsg('SPOC assigned successfully')
    } catch (err) {
      const msg = err?.response?.data?.error || err?.message || 'Failed to assign SPOC.'
      alert(msg)
    }
  }

  async function handleReopen() {
    if (reopening) return
    setReopening(true)
    try {
      await ticketService.reopen(ticket.ticketId)
      await fetchTicket()
      await onUpdate?.()
      setToastMsg('Ticket reopened successfully')
    } catch (err) {
      const msg = err?.response?.data?.error || err?.message || 'Failed to reopen ticket.'
      alert(msg)
    }
    setReopening(false)
  }

  function handleAttachmentClick(attachment) {
    setPreviewAttachment(attachment)
  }

  function closePreview() {
    setPreviewAttachment(null)
  }

  function getPreviewUrl(attachment) {
    return ticketService.getAttachmentUrl(attachment.ticketId, attachment.id) + `?t=${Date.now()}`
  }

  if (!ticket) return <div className="page-loading"><i className="fas fa-spinner fa-spin" /> Loading ticket...</div>

  return (
    <div className="td">
      <div className="td-hdr">
        <div className="td-bread">
          <button className="td-back" onClick={onBack}><i className="fas fa-arrow-left" /></button>
          <span className="td-bread-link" onClick={onBack}>My Tickets</span>
          <i className="fas fa-chevron-right" />
          <span className="td-bread-current">{ticket.id}</span>
        </div>
        <div className="td-hdr-row">
          <h1 className="td-title">{ticket.id} &ndash; {ticket.subject}</h1>
          <div className="td-hdr-actions">
            {isClosed && (
              <button className="td-btn td-btn-primary" onClick={handleReopen} disabled={reopening}>
                {reopening ? <><i className="fas fa-spinner fa-spin" /> Reopening...</> : <><i className="fas fa-undo" /> Reopen Ticket</>}
              </button>
            )}
            <button className="td-btn"><i className="fas fa-print" /> Print</button>
          </div>
        </div>
      </div>

      {toastMsg && (
        <div className="td-toast"><i className="fas fa-check-circle" /> {toastMsg}</div>
      )}
      <div className="td-sumbar">
        <div className="td-sumcard"><span className="td-sumcard-label">Status</span><span className="td-sumcard-val">{(ticket.statusName || ticket.status || '').replace(/_/g, ' ').replace(/\b\w/g, l => l.toUpperCase())}</span></div>
        <div className="td-sumcard"><span className="td-sumcard-label">SPOC</span><span className="td-sumcard-val">{ticket.assignedTo || 'Unassigned'}</span></div>
        <div className="td-sumcard"><span className="td-sumcard-label">Application</span><span className="td-sumcard-val">{ticket.application}</span></div>
      </div>

      <div className="td-body">
        <div className="td-main">
          {!isClosed && (
            <div className="td-composer">
              <div className="td-comp-tbar">
                <button className="td-comp-btn" title="Attach file" onClick={() => fileInputRef.current?.click()}>
                  <i className="fas fa-paperclip" />
                </button>
                <input
                  ref={fileInputRef}
                  type="file"
                  multiple
                  style={{ display: 'none' }}
                  onChange={handleFileSelect}
                  accept="image/*,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv"
                />
                <button className="td-comp-btn" title="Mention user"><i className="fas fa-at" /></button>
              </div>
              <textarea className="td-comp-input" rows={3} placeholder="Type your reply here..." value={replyText} onChange={e => setReplyText(e.target.value)} />
              {selectedFiles.length > 0 && (
                <div className="td-comp-files">
                  {selectedFiles.map((f, i) => (
                    <div key={i} className="td-comp-file">
                      <i className={`fas ${isImageFile(f.type) ? 'fa-file-image' : 'fa-file-alt'}`} />
                      <span className="td-comp-file-name">{f.name}</span>
                      <span className="td-comp-file-size">({formatFileSize(f.size)})</span>
                      <button className="td-comp-file-remove" onClick={() => handleRemoveFile(i)} title="Remove">
                        <i className="fas fa-times" />
                      </button>
                    </div>
                  ))}
                </div>
              )}
              <div className="td-comp-footer">
                <button className="td-btn td-btn-primary" onClick={handleSend} disabled={!replyText.trim() || sending}>
                  {sending ? <><i className="fas fa-spinner fa-spin" /> Sending...</> : <><i className="fas fa-paper-plane" /> Send Reply</>}
                </button>
              </div>
            </div>
          )}
          {isClosed && (
            <div style={{ padding: '16px', marginBottom: '16px', background: '#FEF2F2', border: '1px solid #FECACA', borderRadius: '8px', color: '#991B1B', fontSize: '.85rem', textAlign: 'center' }}>
              <i className="fas fa-lock" style={{ marginRight: 8 }} />This ticket is closed and is read-only. Click "Reopen Ticket" to add new replies.
            </div>
          )}

          <div className="td-conv-heading"><i className="fas fa-comments" /> Conversation</div>
          <div className="td-thread" ref={threadRef}>
            {conversationMessages.map((m, i) => (
              <div key={m.id || i} className="td-msg">
                <div className="td-msg-avatar" style={{ background: m.author?.isAgent ? '#FEF3C7' : '#EFF6FF', color: m.author?.isAgent ? '#D97706' : '#2563EB' }}>
                  {m.author?.initials}
                </div>
                <div className="td-msg-bubble">
                  <div className="td-msg-hdr">
                    <span className="td-msg-name">{m.author?.name}</span>
                    <span className="td-msg-role">{m.author?.role}</span>
                    <span className="td-msg-time">{m.timestamp ? formatDate(m.timestamp) : ''}</span>
                  </div>
                  <div className="td-msg-body" style={{ whiteSpace: 'pre-wrap' }}>{m.body}</div>
                  {m.attachments?.length > 0 && (
                    <div className="td-msg-attach">
                      {m.attachments.map((a, j) => (
                        <div
                          key={j}
                          className={`td-msg-attach-item ${isImageFile(a.contentType) ? 'td-msg-attach-image-wrapper' : ''}`}
                          onClick={() => handleAttachmentClick({ ...a, ticketId: ticket.ticketId })}
                          title="Click to preview"
                        >
                          {isImageFile(a.contentType) ? (
                            <img
                              className="td-msg-attach-image"
                              src={`${ticketService.getAttachmentUrl(ticket.ticketId, a.id)}?t=${Date.now()}`}
                              alt={a.name}
                            />
                          ) : (
                            <div className="td-msg-attach-file">
                              <i className={`fas ${getFileIcon(a.contentType)}`} />
                              <span className="td-msg-attach-name">{a.name}</span>
                              <span className="td-msg-attach-size">({a.size})</span>
                            </div>
                          )}
                          {isImageFile(a.contentType) && (
                            <div className="td-msg-attach-image-name">{a.name}</div>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            ))}
            {conversationMessages.length === 0 && (
              <div style={{ textAlign: 'center', padding: '40px 16px', color: 'var(--text-muted)' }}>No messages yet.</div>
            )}
          </div>
        </div>

        <div className="td-side">
          <div className="td-side-section">
            <div className="td-side-hdr">
              <span>Ticket Details</span>
            </div>
            <div className="td-side-body">
              <div className="td-side-row">
                <label>Status</label>
                <select className="td-side-select" value={ticket.status} onChange={e => handleChangeStatus(e.target.value)} disabled={isClosed}>
                  {statusFlow.map(s => (
                    <option key={s} value={s}>{s.replace(/_/g, ' ').replace(/\b\w/g, l => l.toUpperCase())}</option>
                  ))}
                </select>
              </div>
              <div className="td-side-row">
                <label>SPOC</label>
                <div className="td-side-spoc-row">
                  <SpocSelector
                    users={usersList || []}
                    currentAssignedId={ticket.assignedToUserId}
                    assignedName={ticket.assignedTo}
                    disabled={isClosed}
                    loading={!usersList}
                    onAssign={handleAssign}
                    onClear={function(){}}
                  />
                </div>
              </div>
              <div className="td-side-row"><label>Application</label><span>{ticket.application}</span></div>
              <div className="td-side-row"><label>Raised By</label><span>{ticket.raisedBy}</span></div>
              <div className="td-side-row">
                <label>Corrective Actions Taken</label>
                {ticket.correctiveActions && ticket.correctiveActions.length > 0 && (
                  ticket.correctiveActions.map((ca, i) => (
                    <div key={ca.id || i} className="td-side-ca-display">{ca.description}</div>
                  ))
                )}
                {!isClosed ? (
                  <>
                    <textarea className="td-side-textarea" rows={3} placeholder="Describe corrective actions taken..." value={correctiveActionText} onChange={e => setCorrectiveActionText(e.target.value)} />
                    <div className="td-side-act-actions">
                      <button className="td-side-act-btn td-side-act-submit" onClick={handleSubmitCA} disabled={!correctiveActionText.trim() || submittingCA}>
                        {submittingCA ? 'Submitting...' : 'Submit'}
                      </button>
                    </div>
                  </>
                ) : (!ticket.correctiveActions || ticket.correctiveActions.length === 0) && (
                  <div style={{ color: 'var(--text-muted)', fontSize: '.82rem', padding: '4px 0' }}>No corrective actions recorded.</div>
                )}
              </div>
            </div>
          </div>

          <div className="td-side-section">
            <div className="td-side-hdr"><i className="fas fa-clock" style={{ color: slaInfo?.breached ? '#EF4444' : '#10B981', marginRight: 4 }} /> SLA Tracking</div>
            <div className="td-side-body">
              {slaLoading ? (
                <div style={{ color: 'var(--text-muted)', fontSize: '.85rem', padding: '8px 0' }}><i className="fas fa-spinner fa-spin" style={{ marginRight: 6 }} />Loading SLA...</div>
              ) : slaInfo ? (
                <>
                  {slaInfo?.breached ? (
                    <div className="td-side-sla-banner"><i className="fas fa-exclamation-triangle" /> SLA Breached — OVERDUE by {slaInfo?.consumed}</div>
                  ) : (
                    <div className="td-side-sla-row"><span className="td-side-sla-label">Time Left</span><span className="td-side-sla-val" style={{ color: (slaInfo?.pct || 0) < 20 ? '#F59E0B' : '#10B981', fontWeight: 700 }}>{slaInfo?.consumed}</span></div>
                  )}
                  <div className="td-side-sla-bar"><div className="td-side-sla-fill" style={{ width: `${Math.min(100, slaInfo?.pct || 0)}%`, background: slaInfo?.breached ? '#EF4444' : (slaInfo?.pct || 0) > 80 ? '#10B981' : (slaInfo?.pct || 0) > 20 ? '#F59E0B' : '#EF4444' }} /></div>
                  <div className="td-side-sla-row"><span className="td-side-sla-label">Remaining</span><span className="td-side-sla-val" style={{ color: slaInfo?.breached ? '#EF4444' : '#10B981', fontWeight: 700 }}>{slaInfo?.pct}%</span></div>
                  <div className="td-side-sla-row"><span className="td-side-sla-label">Deadline</span><span className="td-side-sla-val">{formatDate(slaInfo?.deadline)}</span></div>
                </>
              ) : (
                <div style={{ color: 'var(--text-muted)', fontSize: '.85rem', padding: '8px 0' }}>No SLA data available</div>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Attachment preview popup */}
      {previewAttachment && (
        <div className="td-preview-overlay" onClick={closePreview}>
          <div className="td-preview-modal" onClick={e => e.stopPropagation()}>
            <div className="td-preview-header">
              <span className="td-preview-name">
                <i className={`fas ${getFileIcon(previewAttachment.contentType)}`} style={{ marginRight: 8 }} />
                {previewAttachment.name}
              </span>
              <div className="td-preview-actions">
                <a
                  className="td-preview-download"
                  href={getPreviewUrl(previewAttachment)}
                  download={previewAttachment.name}
                  title="Download"
                >
                  <i className="fas fa-download" />
                </a>
                {isImageFile(previewAttachment.contentType) && (
                  <a
                    className="td-preview-download"
                    href={getPreviewUrl(previewAttachment)}
                    target="_blank"
                    rel="noopener noreferrer"
                    title="Open in new tab"
                  >
                    <i className="fas fa-external-link-alt" />
                  </a>
                )}
                <button className="td-preview-close" onClick={closePreview} title="Close">
                  <i className="fas fa-times" />
                </button>
              </div>
            </div>
            <div className="td-preview-body">
              {isImageFile(previewAttachment.contentType) ? (
                <img
                  src={getPreviewUrl(previewAttachment)}
                  alt={previewAttachment.name}
                  className="td-preview-img"
                />
              ) : (
                <div className="td-preview-file-info">
                  <div className="td-preview-file-icon">
                    <i className={`fas ${getFileIcon(previewAttachment.contentType)}`} />
                  </div>
                  <div className="td-preview-file-name">{previewAttachment.name}</div>
                  <div className="td-preview-file-type">{previewAttachment.contentType}</div>
                  <a
                    className="td-btn td-btn-primary"
                    href={getPreviewUrl(previewAttachment)}
                    download={previewAttachment.name}
                    style={{ marginTop: 16, display: 'inline-flex', alignItems: 'center', gap: 8 }}
                  >
                    <i className="fas fa-download" /> Download
                  </a>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}