import api from './apiClient'

export const slaService = {
  getSettings: () => api.get('/sla/settings').then(r => r.data),
  updateSettings: data => api.put('/sla/settings', data).then(r => r.data),

  getHolidays: () => api.get('/sla/holidays').then(r => r.data),
  createHoliday: data => api.post('/sla/holidays', data).then(r => r.data),
  updateHoliday: (id, data) => api.put(`/sla/holidays/${id}`, data).then(r => r.data),
  deleteHoliday: id => api.delete(`/sla/holidays/${id}`),

  getWeeklyRules: () => api.get('/sla/weekly-rules').then(r => r.data),
  createWeeklyRule: data => api.post('/sla/weekly-rules', data).then(r => r.data),
  deleteWeeklyRule: id => api.delete(`/sla/weekly-rules/${id}`),

  getPolicies: () => api.get('/sla/policies').then(r => r.data),
  updatePolicy: (id, data) => api.put(`/sla/policies/${id}`, data).then(r => r.data),

  getTicketSla: ticketId => api.get(`/sla/tickets/${ticketId}`).then(r => r.data),
  getSlaAudit: ticketId => api.get(`/sla/tickets/${ticketId}/audit`).then(r => r.data),
}
