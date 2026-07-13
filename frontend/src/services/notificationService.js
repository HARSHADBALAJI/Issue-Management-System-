import api from './apiClient'

export const notificationService = {
  getAll: params => api.get('/notifications', { params }).then(r => r.data),
  markRead: id => api.put(`/notifications/${id}/read`),
  markAllRead: () => api.put('/notifications/read-all'),
}
