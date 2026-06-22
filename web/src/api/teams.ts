import { api } from './http'
import type { Member, Team, TeamDetail } from '../types'

export const teamsApi = {
  list: () => api<Team[]>('/teams'),
  create: (name: string) => api<Team>('/teams', { method: 'POST', body: JSON.stringify({ name }) }),
  detail: (id: string) => api<TeamDetail>(`/teams/${id}`),
  addMember: (id: string, email: string) =>
    api<Member>(`/teams/${id}/members`, { method: 'POST', body: JSON.stringify({ email }) }),
  leave: (id: string) => api<void>(`/teams/${id}/members/me`, { method: 'DELETE' }),
  remove: (id: string) => api<void>(`/teams/${id}`, { method: 'DELETE' }),
}
