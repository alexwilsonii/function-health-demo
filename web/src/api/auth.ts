import { api } from './http'
import type { User } from '../types'

export const authApi = {
  me: () => api<User>('/auth/me'),
  login: (email: string, password: string) =>
    api<User>('/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),
  register: (email: string, password: string) =>
    api<User>('/auth/register', { method: 'POST', body: JSON.stringify({ email, password }) }),
  logout: () => api<void>('/auth/logout', { method: 'POST' }),
}
