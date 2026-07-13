import api from './apiClient'

export const userService = {
  getAll: params => api.get('/users', { params }).then(r => r.data),
  getById: id => api.get(`/users/${id}`).then(r => r.data),
  getLookup: params => api.get('/users/lookup', { params }).then(r => r.data),
  create: data => api.post('/users', data).then(r => r.data),
  update: (id, data) => api.put(`/users/${id}`, data).then(r => r.data),
  delete: id => api.delete(`/users/${id}`),
  assignApplications: (id, data) => api.put(`/users/${id}/applications`, data),
  resetPassword: id => api.post(`/users/${id}/reset-password`),
}
