export function timeAgo(d) {
  const n = Date.now()
  const t = d.getTime()
  const s = Math.round((n - t) / 1000)
  if (s < 60) return `${s}s ago`
  if (s < 3600) return `${Math.floor(s / 60)}m ago`
  if (s < 86400) return `${Math.floor(s / 3600)}h ago`
  return `${Math.floor(s / 86400)}d ago`
}

export function getStatusMeta(s) {
  const map = {
    open: { label: 'Open', cls: 'open' },
    in_progress: { label: 'In Progress', cls: 'in_progress' },
    waiting: { label: 'Waiting', cls: 'waiting' },
    resolved: { label: 'Resolved', cls: 'resolved' },
    closed: { label: 'Closed', cls: 'closed' },
  }
  return map[s] || { label: s, cls: s }
}

export function slaDot(sla) {
  if (!sla) return 'none'
  if (sla.startsWith('Overdue')) return 'critical'
  const dm = sla.match(/(\d+)d/)
  const hm = sla.match(/(\d+)h/)
  const mm = sla.match(/(\d+)m/)
  if (dm) {
    const d = parseInt(dm[1])
    if (d <= 1 && hm && parseInt(hm[1]) <= 4) return 'warn'
    return 'ok'
  }
  if (hm) {
    const h = parseInt(hm[1])
    if (h <= 2) return 'warn'
    return 'ok'
  }
  if (mm) return 'warn'
  return 'ok'
}

export function extractError(e, fallback) {
  if (!e) return fallback
  const data = e?.response?.data
  if (typeof data === 'string') return data
  if (data?.detail) return data.detail
  if (data?.title) return data.title
  if (data?.message) return data.message
  return e?.message || fallback
}

export function exportToCSV(rows, filename) {
  const header = Object.keys(rows[0])
  const csv = [
    header.join(','),
    ...rows.map(r =>
      header.map(k => `"${String(r[k]).replace(/"/g, '""')}"`).join(',')
    ),
  ].join('\n')
  const blob = new Blob([csv], { type: 'text/csv' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = filename
  a.click()
  URL.revokeObjectURL(a.href)
}
