import api from './apiClient'

export const applicationService = {
  getAll: params => api.get('/applications', { params }).then(r => r.data),
  getById: id => api.get(`/applications/${id}`).then(r => r.data),
  getLookup: () => api.get('/applications/lookup').then(r => r.data),
  create: data => api.post('/applications', data).then(r => r.data),
  update: (id, data) => api.put(`/applications/${id}`, data).then(r => r.data),
  delete: id => api.delete(`/applications/${id}`),
  assignUsers: (id, data) => api.put(`/applications/${id}/users`, data),
}
