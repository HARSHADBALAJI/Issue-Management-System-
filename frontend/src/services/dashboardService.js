import api from './apiClient'

export const dashboardService = {
  getStats: params => api.get('/dashboard/stats', { params }).then(r => r.data),
  getTrends: params => api.get('/dashboard/trends', { params }).then(r => r.data),
  getSla: () => api.get('/dashboard/sla').then(r => r.data),
  getAgentPerformance: () => api.get('/dashboard/agent-performance').then(r => r.data),
}
