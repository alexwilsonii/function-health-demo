import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed, type Ref } from 'vue'
import { commentsApi } from '../api/comments'
import type { Comment } from '../types'
import { useToastStore } from '../stores/ui'
import { useAuthStore } from '../stores/auth'
import { ApiError } from '../api/http'

/**
 * Comments for a single task. Lazy: only fetches when `enabled` (i.e. the view modal is open) and
 * the task is real (not an optimistic temp row). Add/delete are optimistic with rollback.
 */
export function useComments(taskId: Ref<string | null>, enabled: Ref<boolean>) {
  const qc = useQueryClient()
  const toasts = useToastStore()
  const auth = useAuthStore()
  const keyFor = (id: string | null) => ['comments', id]

  const list = useQuery({
    queryKey: ['comments', taskId],
    queryFn: () => commentsApi.list(taskId.value as string),
    enabled: computed(() => enabled.value && !!taskId.value && !taskId.value!.startsWith('temp-')),
  })

  const add = useMutation({
    mutationFn: (body: string) => commentsApi.add(taskId.value as string, body),
    onMutate: async (body) => {
      const key = keyFor(taskId.value)
      await qc.cancelQueries({ queryKey: key })
      const prev = qc.getQueryData<Comment[]>(key)
      const optimistic: Comment = {
        id: `temp-${crypto.randomUUID()}`,
        taskId: taskId.value as string,
        body,
        authorEmail: auth.user?.email ?? 'You',
        createdAt: new Date().toISOString(),
      }
      qc.setQueryData<Comment[]>(key, (old) => [optimistic, ...(old ?? [])])
      return { prev, key }
    },
    onError: (e, _body, ctx) => {
      if (ctx) qc.setQueryData(ctx.key, ctx.prev)
      // 400 (empty body) is shown inline in the form; don't also toast it.
      if (!(e instanceof ApiError && e.status === 400)) toasts.push('Could not add comment.', 'error')
    },
    onSettled: () => qc.invalidateQueries({ queryKey: keyFor(taskId.value) }),
  })

  const remove = useMutation({
    mutationFn: (commentId: string) => commentsApi.remove(taskId.value as string, commentId),
    onMutate: async (commentId) => {
      const key = keyFor(taskId.value)
      await qc.cancelQueries({ queryKey: key })
      const prev = qc.getQueryData<Comment[]>(key)
      qc.setQueryData<Comment[]>(key, (old) => old?.filter((c) => c.id !== commentId))
      return { prev, key }
    },
    onError: (_e, _id, ctx) => {
      if (ctx) qc.setQueryData(ctx.key, ctx.prev)
      toasts.push('Could not delete comment.', 'error')
    },
    onSettled: () => qc.invalidateQueries({ queryKey: keyFor(taskId.value) }),
  })

  return { list, add, remove }
}
