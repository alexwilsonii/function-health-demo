import { api } from './http'
import type { CreateTaskInput, Task, TaskQuery, UpdateTaskInput } from '../types'

export const tasksApi = {
  list: (query: TaskQuery) => {
    const params = new URLSearchParams()
    if (query.q) params.set('q', query.q)
    if (query.status) params.set('status', query.status)
    if (query.priority) params.set('priority', query.priority)
    if (query.sort) params.set('sort', query.sort)
    if (query.teamId) params.set('teamId', query.teamId)
    if (query.assignee) params.set('assignee', query.assignee)
    const qs = params.toString()
    return api<Task[]>(`/tasks${qs ? `?${qs}` : ''}`)
  },
  get: (id: string) => api<Task>(`/tasks/${id}`),
  create: (input: CreateTaskInput) =>
    api<Task>('/tasks', { method: 'POST', body: JSON.stringify(input) }),
  update: (id: string, input: UpdateTaskInput) =>
    api<Task>(`/tasks/${id}`, { method: 'PATCH', body: JSON.stringify(input) }),
  remove: (id: string) => api<void>(`/tasks/${id}`, { method: 'DELETE' }),
}
