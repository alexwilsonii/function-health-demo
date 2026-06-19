import { api } from './http'
import type { Comment } from '../types'

export const commentsApi = {
  list: (taskId: string) => api<Comment[]>(`/tasks/${taskId}/comments`),
  add: (taskId: string, body: string) =>
    api<Comment>(`/tasks/${taskId}/comments`, { method: 'POST', body: JSON.stringify({ body }) }),
  remove: (taskId: string, commentId: string) =>
    api<void>(`/tasks/${taskId}/comments/${commentId}`, { method: 'DELETE' }),
}
