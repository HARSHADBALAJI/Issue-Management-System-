import api, { API_BASE_URL } from './apiClient'

export const ticketService = {
  getAll: params => api.get('/tickets', { params }).then(r => r.data),
  getById: id => api.get(`/tickets/${id}`).then(r => r.data),
  create: data => api.post('/tickets', data).then(r => r.data),
  addMessage: (id, data, files) => {
    if (files && files.length > 0) {
      const formData = new FormData()
      formData.append('content', data.content)
      formData.append('isInternal', data.isInternal || false)
      files.forEach(f => formData.append('files', f))
      return api.post(`/tickets/${id}/messages`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      }).then(r => r.data)
    }
    return api.post(`/tickets/${id}/messages`, data).then(r => r.data)
  },
  addCorrectiveAction: (id, data) => api.post(`/tickets/${id}/corrective-actions`, data).then(r => r.data),
  assign: (id, data) => api.put(`/tickets/${id}/assign`, data),
  updateStatus: (id, data) => api.put(`/tickets/${id}/status`, data),
  getStats: params => api.get('/tickets/stats', { params }).then(r => r.data),
  getSlaSummary: () => api.get('/tickets/sla-summary').then(r => r.data),
  bulkAssign: data => api.post('/tickets/bulk-assign', data).then(r => r.data),
  bulkUpdateStatus: data => api.post('/tickets/bulk-status', data).then(r => r.data),
  reopen: id => api.post(`/tickets/${id}/reopen`).then(r => r.data),
  getAttachmentUrl: (ticketId, attachmentId) => `${API_BASE_URL}/tickets/${ticketId}/attachments/${attachmentId}`,
}
