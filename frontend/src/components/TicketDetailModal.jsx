import { useState, useEffect, useCallback, useRef } from 'react'
import { ticketService } from '../services/ticketService'
import { slaService } from '../services/slaService'
import { getStatusMeta } from '../utils/helpers'
import { onEvent, offEvent } from '../services/signalRService'
import SpocSelector from './SpocSelector'

function formatDate(d) {
  if (!d) return ''
  const mo = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
  let h = d.getHours()
  const ampm = h >= 12 ? 'PM' : 'AM'
  h = h % 12 || 12
  const pad = n => String(n).padStart(2, '0')
  return `${mo[d.getMonth()]} ${pad(d.getDate())}, ${d.getFullYear()} ${pad(h)}:${pad(d.getMinutes())} ${ampm}`
}

function isImageFile(contentType) {
  return contentType && contentType.startsWith('image/')
}

function formatFileSize(bytes) {
  if (!bytes) return ''
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

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
    priority: t.priority || 'Normal',
    updated: t.updatedAt ? new Date(t.updatedAt) : new Date(),
    created: t.createdAt ? new Date(t.createdAt) : new Date(),
    messages: (t.messages || []).map(m => ({
      id: m.id, userName: m.createdByName || '', userEmail: m.createdByEmail || '',
      body: m.content || '', isInternal: m.isInternal || false,
      isFromRequester: m.isFromRequester || false,
      messageSourceType: m.messageSourceType || '',
      createdDate: m.createdAt ? new Date(m.createdAt) : new Date(),
      attachments: (m.attachments || []).map(a => ({ id: a.id, fileName: a.fileName || a.name || '', fileSize: a.fileSize || 0, contentType: a.contentType || '' })),
    })),
    statusHistory: (t.statusHistory || []).map(sh => ({
      id: sh.id, fromStatusName: sh.fromStatus || 'Unknown', toStatusName: sh.toStatus || 'Unknown',
      remarks: sh.note || '', changedDate: sh.createdAt ? new Date(sh.createdAt) : new Date(),
    })),
    correctiveActions: (t.correctiveActions || []).map(ca => ({
      id: ca.id, description: ca.description || '', performedDate: ca.createdAt ? new Date(ca.createdAt) : new Date(),
    })),
  }
}

const statusFlow = ['open', 'in_progress', 'waiting', 'resolved', 'closed']
const statusMap = { 'open': 5, 'in_progress': 1, 'waiting': 2, 'resolved': 3, 'closed': 4 }

export default function TicketDetailModal({ ticketId, onClose, onUpdate, usersList, simple }) {
  const [ticket, setTicket] = useState(null)
  const [slaInfo, setSlaInfo] = useState(null)
  const [slaLoading, setSlaLoading] = useState(false)
  const [activeTab, setActiveTab] = useState('details')
  const [replying, setReplying] = useState(false)
  const [replyText, setReplyText] = useState('')
  const [sending, setSending] = useState(false)
  const [reopening, setReopening] = useState(false)
  const [selectedFiles, setSelectedFiles] = useState([])
  const [previewAttachment, setPreviewAttachment] = useState(null)
  const [toastMsg, setToastMsg] = useState(null)
  const modalRef = useRef(null)

  useEffect(() => {
    if (toastMsg) {
      const t = setTimeout(() => setToastMsg(null), 3500)
      return () => clearTimeout(t)
    }
  }, [toastMsg])
  const fileInputRef = useRef(null)

  const fetchTicket = useCallback(async () => {
    if (!ticketId) return
    setSlaLoading(true)
    try {
      const t = await ticketService.getById(ticketId)
      setTicket(normalizeTicketDetail(t))
    } catch {}
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

  const isClosed = ticket?.status === 'closed'

  async function handleReopen() {
    if (reopening) return
    setReopening(true)
    try {
      await ticketService.reopen(ticket.ticketId)
      await fetchTicket()
      await onUpdate?.()
    } catch (err) {
      const msg = err?.response?.data?.error || err?.message || 'Failed to reopen ticket.'
      alert(msg)
    }
    setReopening(false)
  }

  useEffect(() => {
    const handler = e => { if (e.key === 'Escape') { onClose(); setPreviewAttachment(null) } }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [onClose])

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
      setReplying(false)
      await fetchTicket()
      await onUpdate?.()
      setToastMsg('Reply sent successfully')
    } catch { alert('Failed to send reply.') }
    setSending(false)
  }

  async function handleChangeStatus(newStatus) {
    if (!newStatus || newStatus === ticket.status || isClosed) return
    try {
      await ticketService.updateStatus(ticket.ticketId, { statusId: statusMap[newStatus], remarks: `Status changed to ${newStatus}` })
      await fetchTicket()
      await onUpdate?.()
      setToastMsg(`Status changed to ${newStatus.replace(/_/g, ' ')}`)
    } catch (err) {
      const msg = err?.response?.data?.error || err?.message || 'Failed to change status.'
      alert(msg)
    }
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

  function handleClose() {
    onClose()
  }

  function getPreviewUrl(attachment) {
    const base = ticketService.getAttachmentUrl(attachment.ticketId, attachment.id)
    return `${base}?t=${Date.now()}`
  }

  function handleAttachmentClick(attachment) {
    setPreviewAttachment(attachment)
  }

  function closePreview() {
    setPreviewAttachment(null)
  }

  if (!ticket) return (
    <div className="modal-backdrop open" onClick={onClose}>
      <div className="modal tdm open" ref={modalRef} onClick={e => e.stopPropagation()}>
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', padding: 80, color: 'var(--text-muted)' }}>
          <i className="fas fa-spinner fa-spin" style={{ marginRight: 8 }} /> Loading...
        </div>
      </div>
    </div>
  )

  const sm = getStatusMeta(ticket.status)
  const tabs = [
    { key: 'details', label: 'Details', icon: 'fa-info-circle' },
    { key: 'activity', label: 'Activity', icon: 'fa-clock' },
    { key: 'comments', label: 'Comments', icon: 'fa-comments' },
    { key: 'history', label: 'History', icon: 'fa-history' },
  ]

  const timeline = (() => {
    const entries = []
    ticket.messages?.forEach(m => {
      const role = m.isInternal ? 'SPOC' : (m.isFromRequester ? 'Requester' : (m.messageSourceType === 'System' ? 'System' : 'User'))
      entries.push({ id: `msg-${m.id}`, type: m.isInternal ? 'internal' : 'reply',
        author: { name: m.userName || 'Unknown', role,
          initials: (m.userName || '?').split(' ').map(s => s[0]).join('').toUpperCase(), isAgent: !m.isFromRequester && m.messageSourceType !== 'Requester' },
        body: m.body || '', timestamp: m.createdDate,
        attachments: m.attachments?.map(a => ({
          id: a.id, name: a.fileName, size: formatFileSize(a.fileSize), contentType: a.contentType,
        })) || [],
      })
    })
    ticket.statusHistory?.forEach(sh => {
      entries.push({ id: `sh-${sh.id}`, type: 'system', icon: 'fa-arrow-right', color: '#F59E0B', label: `Status changed from ${sh.fromStatusName} to ${sh.toStatusName}${sh.remarks ? ` — ${sh.remarks}` : ''}`, timestamp: sh.changedDate })
    })
    ticket.correctiveActions?.forEach(ca => {
      entries.push({ id: `ca-${ca.id}`, type: 'system', icon: 'fa-wrench', color: '#10B981', label: `Corrective action: ${ca.description}`, timestamp: ca.performedDate })
    })
    entries.sort((a, b) => (b.timestamp?.getTime() || 0) - (a.timestamp?.getTime() || 0))
    return entries
  })()

  const conversationMessages = timeline.filter(m => m.type !== 'system')

  return (
    <div className="modal-backdrop open tdm-backdrop" onClick={onClose}>
      <div className="modal tdm open" ref={modalRef} onClick={e => e.stopPropagation()}>
        {toastMsg && (
          <div className="td-toast"><i className="fas fa-check-circle" /> {toastMsg}</div>
        )}
        <div className="tdm-header">
          <div className="tdm-header-left">
            <span className="tdm-id">{ticket.id}</span>
            <span className={`status-pill status-${sm.cls}`}><span className="dot" />{sm.label}</span>
            {slaInfo?.breached && <span className="tdm-sla-badge"><i className="fas fa-exclamation-triangle" /> SLA Breached</span>}
          </div>
          {isClosed && (
            <button className="btn btn-primary btn-sm" onClick={handleReopen} disabled={reopening} style={{ marginRight: 8 }}>
              {reopening ? <><i className="fas fa-spinner fa-spin" /> Reopening...</> : <><i className="fas fa-undo" /> Reopen</>}
            </button>
          )}
          <button className="modal-close tdm-close" onClick={onClose}><i className="fas fa-times" /></button>
        </div>

        {simple ? (
          <div className="tdm-body tdm-body-simple">
            <div className="tdm-section">
              <div className="tdm-section-title">Actions</div>
              <div className="tdm-actions">
                <div className="tdm-action-row">
                  <label>Current SPOC</label>
                  <span style={{ fontWeight: 500, color: 'var(--text)' }}>{ticket.assignedTo || 'Unassigned'}</span>
                </div>
                <div className="tdm-action-row">
                  <label>Change Status</label>
                  <select className="tdm-select" value={ticket.status} onChange={e => handleChangeStatus(e.target.value)} disabled={isClosed}>
                    {statusFlow.map(s => (
                      <option key={s} value={s}>{s.replace(/_/g, ' ').replace(/\b\w/g, l => l.toUpperCase())}</option>
                    ))}
                  </select>
                </div>
                <div className="tdm-action-row">
                  <label>Assign SPOC</label>
                  <SpocSelector
                    users={usersList || []}
                    currentAssignedId={ticket.assignedToUserId}
                    assignedName={ticket.assignedTo}
                    disabled={isClosed}
                    loading={!usersList}
                    onAssign={(userId) => handleAssign(userId)}
                    onClear={function(){}}
                  />
                </div>
              </div>
            </div>
          </div>
        ) : (
          <>
            {isClosed && (
              <div style={{ padding: '12px', margin: '12px 16px 0', background: '#FEF2F2', border: '1px solid #FECACA', borderRadius: '8px', color: '#991B1B', fontSize: '.82rem', textAlign: 'center' }}>
                <i className="fas fa-lock" style={{ marginRight: 6 }} />This ticket is closed. Click "Reopen" to enable editing.
              </div>
            )}
            <div className="tdm-tabs">
              {tabs.map(tab => (
                <button key={tab.key} className={`tdm-tab${activeTab === tab.key ? ' active' : ''}`} onClick={() => setActiveTab(tab.key)}>
                  <i className={`fas ${tab.icon}`} /> {tab.label}
                </button>
              ))}
            </div>

            <div className="tdm-body">
              {activeTab === 'details' && (
                <>
                  <div className="tdm-section">
                    <div className="tdm-section-title">Ticket Information</div>
                    <div className="tdm-grid">
                      <div className="tdm-grid-item"><label>Ticket ID</label><span>{ticket.id}</span></div>
                      <div className="tdm-grid-item"><label>Application</label><span>{ticket.application}</span></div>
                      <div className="tdm-grid-item"><label>Subject</label><span>{ticket.subject}</span></div>
                      <div className="tdm-grid-item"><label>Status</label><span className={`status-pill status-${sm.cls}`} style={{ fontSize: '.72rem', padding: '2px 8px', display: 'inline-flex', width: 'auto' }}><span className="dot" />{sm.label}</span></div>
                      <div className="tdm-grid-item"><label>Priority</label><span>{ticket.priority}</span></div>
                      <div className="tdm-grid-item"><label>SLA</label><span style={{ color: slaInfo?.breached ? '#DC2626' : slaInfo?.pct > 80 ? '#F59E0B' : '#10B981' }}>{ticket.sla || 'N/A'}</span></div>
                      <div className="tdm-grid-item"><label>Created Date</label><span>{formatDate(ticket.created)}</span></div>
                      <div className="tdm-grid-item"><label>Updated Date</label><span>{formatDate(ticket.updated)}</span></div>
                      <div className="tdm-grid-item"><label>Raised By</label><span>{ticket.raisedBy || 'N/A'}</span></div>
                      <div className="tdm-grid-item"><label>Assigned SPOC</label><span>{ticket.assignedTo || 'Unassigned'}</span></div>
                    </div>
                  </div>

                  <div className="tdm-section">
                    <div className="tdm-section-title">Description</div>
                    <div className="tdm-desc-modal">{ticket.description || 'No description provided.'}</div>
                  </div>

                  {ticket.assignedTo && (
                    <div className="tdm-section">
                      <div className="tdm-section-title">Assignment</div>
                      <div className="tdm-spoc">
                        <div className="tdm-spoc-avatar">{(ticket.assignedTo.split(' ').map(s => s[0]).join('') || '?').toUpperCase().slice(0, 2)}</div>
                        <div className="tdm-spoc-info">
                          <span className="tdm-spoc-name">{ticket.assignedTo}</span>
                          <span className="tdm-spoc-role">SPOC</span>
                        </div>
                      </div>
                    </div>
                  )}

                  <div className="tdm-section">
                    <div className="tdm-section-title">Actions</div>
                    <div className="tdm-actions">
                      <div className="tdm-action-row">
                        <label>Current SPOC</label>
                        <span style={{ fontWeight: 500, color: 'var(--text)' }}>{ticket.assignedTo || 'Unassigned'}</span>
                      </div>
                      <div className="tdm-action-row">
                        <label>Change Status</label>
                        <select className="tdm-select" value={ticket.status} onChange={e => handleChangeStatus(e.target.value)} disabled={isClosed}>
                          {statusFlow.map(s => (
                            <option key={s} value={s}>{s.replace(/_/g, ' ').replace(/\b\w/g, l => l.toUpperCase())}</option>
                          ))}
                        </select>
                      </div>
                      <div className="tdm-action-row">
                        <label>Assign SPOC</label>
                        <SpocSelector
                          users={usersList || []}
                          currentAssignedId={ticket.assignedToUserId}
                          assignedName={ticket.assignedTo}
                          disabled={isClosed}
                          loading={!usersList}
                          onAssign={(userId) => handleAssign(userId)}
                          onClear={function(){}}
                        />
                      </div>
                    </div>
                  </div>

                  {slaInfo && (
                    <div className="tdm-section">
                      <div className="tdm-section-title">SLA Tracking</div>
                      <div className="tdm-sla">
                        {slaInfo.breached ? (
                          <div className="tdm-sla-banner"><i className="fas fa-exclamation-triangle" /> SLA Breached — OVERDUE by {slaInfo.consumed}</div>
                        ) : (
                          <div className="tdm-sla-row"><span>Time Left</span><span style={{ color: slaInfo.pct < 20 ? '#F59E0B' : '#10B981', fontWeight: 700 }}>{slaInfo.consumed}</span></div>
                        )}
                        <div className="tdm-sla-bar"><div className="tdm-sla-fill" style={{ width: `${slaInfo.pct}%`, background: slaInfo.breached ? '#DC2626' : slaInfo.pct > 80 ? '#10B981' : slaInfo.pct > 20 ? '#F59E0B' : '#EF4444' }} /></div>
                        <div className="tdm-sla-row"><span>Remaining</span><span style={{ color: slaInfo.breached ? '#DC2626' : '#10B981', fontWeight: 700 }}>{slaInfo.pct}%</span></div>
                        <div className="tdm-sla-row"><span>Deadline</span><span>{formatDate(slaInfo.deadline)}</span></div>
                      </div>
                    </div>
                  )}
                </>
              )}

              {activeTab === 'activity' && (
                <div className="tdm-timeline">
                  {timeline.length === 0 && <div style={{ textAlign: 'center', padding: 40, color: 'var(--text-muted)' }}>No activity yet.</div>}
                  {timeline.map((entry, i) => (
                    <div key={entry.id || i} className="tdm-tl-item">
                      <div className="tdm-tl-dot" style={entry.type === 'system' ? { background: entry.color } : undefined}>
                        {entry.type !== 'system' ? <i className="fas fa-user" /> : <i className={`fas ${entry.icon}`} />}
                      </div>
                      <div className="tdm-tl-content">
                        {entry.type !== 'system' ? (
                          <>
                            <div className="tdm-tl-header">
                              <span className="tdm-tl-name">{entry.author?.name}</span>
                              <span className="tdm-tl-role">{entry.author?.role}</span>
                              <span className="tdm-tl-time">{entry.timestamp ? formatDate(entry.timestamp) : ''}</span>
                            </div>
                            <div className="tdm-tl-body">{entry.body}</div>
                            {entry.attachments?.length > 0 && (
                              <div className="tdm-tl-attach">
                                {entry.attachments.map((a, j) => (
                                  <div key={j} className="td-msg-attach" style={{ display: 'inline-flex', gap: 6, flexWrap: 'wrap' }}>
                                    <div
                                      className={`td-msg-attach-item ${isImageFile(a.contentType) ? 'td-msg-attach-image-wrapper' : ''}`}
                                      onClick={() => handleAttachmentClick({ ...a, ticketId: ticket.ticketId })}
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
                                  </div>
                                ))}
                              </div>
                            )}
                          </>
                        ) : (
                          <div className="tdm-tl-system">
                            <i className={`fas ${entry.icon}`} style={{ color: entry.color }} />
                            <span>{entry.label}</span>
                            <span className="tdm-tl-time">{entry.timestamp ? formatDate(entry.timestamp) : ''}</span>
                          </div>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}

              {activeTab === 'comments' && (
                <div className="tdm-comments">
                  {isClosed && (
                    <div style={{ padding: '10px', background: '#FEF2F2', border: '1px solid #FECACA', borderRadius: '8px', color: '#991B1B', fontSize: '.8rem', textAlign: 'center', marginBottom: 12 }}>
                      <i className="fas fa-lock" style={{ marginRight: 4 }} />Ticket is closed — replies disabled.
                    </div>
                  )}
                  <div className="tdm-comp-wrap">
                    <div className="tdm-comp-tbar">
                      <button className="tdm-comp-btn" title="Attach file" onClick={() => fileInputRef.current?.click()} disabled={isClosed}>
                        <i className="fas fa-paperclip" />
                      </button>
                      <button className="tdm-comp-btn" title="Mention user" disabled={isClosed}><i className="fas fa-at" /></button>
                      <input
                        ref={fileInputRef}
                        type="file"
                        multiple
                        style={{ display: 'none' }}
                        onChange={handleFileSelect}
                        accept="image/*,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv"
                      />
                    </div>
                    <textarea className="tdm-comp-input" rows={3} placeholder="Type your reply..." value={replyText} onChange={e => { setReplyText(e.target.value); setReplying(true) }} disabled={isClosed} />
                    {selectedFiles.length > 0 && (
                      <div className="tdm-comp-files">
                        {selectedFiles.map((f, i) => (
                          <div key={i} className="tdm-comp-file">
                            <i className={`fas ${isImageFile(f.type) ? 'fa-file-image' : 'fa-file-alt'}`} />
                            <span className="tdm-comp-file-name">{f.name}</span>
                            <span className="tdm-comp-file-size">({formatFileSize(f.size)})</span>
                            <button className="tdm-comp-file-remove" onClick={() => handleRemoveFile(i)} title="Remove">
                              <i className="fas fa-times" />
                            </button>
                          </div>
                        ))}
                      </div>
                    )}
                    <div className="tdm-comp-footer">
                      <button className="btn btn-primary btn-sm" onClick={handleSend} disabled={!replyText.trim() || sending || isClosed}>
                        {sending ? <><i className="fas fa-spinner fa-spin" /> Sending...</> : <><i className="fas fa-paper-plane" /> Send Reply</>}
                      </button>
                    </div>
                  </div>
                  {conversationMessages.length === 0 && <div style={{ textAlign: 'center', padding: 40, color: 'var(--text-muted)' }}>No messages yet.</div>}
                  {conversationMessages.map((m, i) => (
                    <div key={m.id || i} className="tdm-comment">
                      <div className="tdm-comment-avatar" style={{ background: m.author?.isAgent ? '#FEF3C7' : '#EFF6FF', color: m.author?.isAgent ? '#D97706' : '#2563EB' }}>{m.author?.initials}</div>
                      <div className="tdm-comment-bubble">
                        <div className="tdm-comment-hdr">
                          <span className="tdm-comment-name">{m.author?.name}</span>
                          <span className="tdm-comment-role">{m.author?.role}</span>
                          <span className="tdm-comment-time">{m.timestamp ? formatDate(m.timestamp) : ''}</span>
                        </div>
                        <div className="tdm-comment-body">{m.body}</div>
                        {m.attachments?.length > 0 && (
                          <div className="tdm-tl-attach" style={{ padding: '6px 0 0', display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                            {m.attachments.map((a, j) => (
                              <div
                                key={j}
                                className={`td-msg-attach-item ${isImageFile(a.contentType) ? 'td-msg-attach-image-wrapper' : ''}`}
                                onClick={() => handleAttachmentClick({ ...a, ticketId: ticket.ticketId })}
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
                                    <span className="td-msg-attach-name">{a.fileName}</span>
                                    <span className="td-msg-attach-size">({a.fileSize ? `${(a.fileSize / 1024).toFixed(1)} KB` : '?'})</span>
                                  </div>
                                )}
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}

              {activeTab === 'history' && (
                <div className="tdm-history">
                  {(!ticket.statusHistory || ticket.statusHistory.length === 0) && <div style={{ textAlign: 'center', padding: 40, color: 'var(--text-muted)' }}>No status changes recorded.</div>}
                  {ticket.statusHistory?.map((sh, i) => (
                    <div key={sh.id || i} className="tdm-hist-item">
                      <div className="tdm-hist-dot" />
                      <div className="tdm-hist-content">
                        <span className="tdm-hist-label">Status changed from <strong>{sh.fromStatusName}</strong> to <strong>{sh.toStatusName}</strong></span>
                        {sh.remarks && <span className="tdm-hist-remarks">{sh.remarks}</span>}
                        <span className="tdm-hist-time">{formatDate(sh.changedDate)}</span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </>
        )}
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
                {isImageFile(previewAttachment.contentType) && (
                  <a
                    className="td-preview-download"
                    href={getPreviewUrl(previewAttachment)}
                    download={previewAttachment.name}
                    title="Open in new tab"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    <i className="fas fa-external-link-alt" />
                  </a>
                )}
                <a
                  className="td-preview-download"
                  href={getPreviewUrl(previewAttachment)}
                  download={previewAttachment.name}
                  title="Download"
                >
                  <i className="fas fa-download" />
                </a>
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
                    className="btn btn-primary btn-sm"
                    style={{ marginTop: 12 }}
                    href={getPreviewUrl(previewAttachment)}
                    download={previewAttachment.name}
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