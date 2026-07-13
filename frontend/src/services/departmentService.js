import api from './apiClient'

export const departmentService = {
  getAll: params => api.get('/departments', { params }).then(r => r.data),
  getById: id => api.get(`/departments/${id}`).then(r => r.data),
  getLookup: () => api.get('/departments/lookup').then(r => r.data),
  create: data => api.post('/departments', data).then(r => r.data),
  update: (id, data) => api.put(`/departments/${id}`, data).then(r => r.data),
  delete: id => api.delete(`/departments/${id}`),
}
