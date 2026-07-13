import api from './apiClient'

export const authService = {
  login: (email, password) => api.post('/auth/login', { email, password }).then(r => r.data),
  refresh: refreshToken => api.post('/auth/refresh', { refreshToken }).then(r => r.data),
  logout: () => api.post('/auth/logout'),
  changePassword: data => api.post('/auth/change-password', data).then(r => r.data),
}
