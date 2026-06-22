import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import type { Ref } from 'vue'
import { tasksApi } from '../api/tasks'
import type { CreateTaskInput, Task, TaskQuery, UpdateTaskInput } from '../types'
import { useToastStore } from '../stores/ui'
import { ApiError } from '../api/http'

const TASKS_KEY = ['tasks'] as const

export function useTasksQuery(query: Ref<TaskQuery>) {
  // Reactive query key: changing any filter re-fetches automatically.
  return useQuery({
    queryKey: ['tasks', query],
    queryFn: () => tasksApi.list(query.value),
  })
}

type Snapshots = [readonly unknown[], Task[] | undefined][]

export function useTaskMutations() {
  const qc = useQueryClient()
  const toasts = useToastStore()

  // Snapshot every cached tasks list (one per filter combination) so onError can roll back exactly.
  async function snapshot(): Promise<Snapshots> {
    await qc.cancelQueries({ queryKey: TASKS_KEY })
    return qc.getQueriesData<Task[]>({ queryKey: TASKS_KEY })
  }
  function rollback(snaps: Snapshots | undefined) {
    snaps?.forEach(([key, data]) => qc.setQueryData(key, data))
  }
  function settle() {
    qc.invalidateQueries({ queryKey: TASKS_KEY })
  }
  function patchLists(fn: (tasks: Task[]) => Task[]) {
    qc.setQueriesData<Task[]>({ queryKey: TASKS_KEY }, (old) => (old ? fn(old) : old))
  }

  // Create is not optimistic: the new row's team name + server timestamps aren't known client-side,
  // so we just refetch on success (the modal closes immediately regardless).
  const create = useMutation({
    mutationFn: (input: CreateTaskInput) => tasksApi.create(input),
    onError: (e) => {
      // Validation errors (400) are shown inline in the form; don't also toast them.
      if (!(e instanceof ApiError && e.status === 400)) toasts.push('Could not create task.', 'error')
    },
    onSuccess: () => toasts.push('Task created.'),
    onSettled: settle,
  })

  const update = useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateTaskInput }) => tasksApi.update(id, input),
    onMutate: async ({ id, input }) => {
      const snaps = await snapshot()
      const now = new Date().toISOString()
      patchLists((tasks) =>
        tasks.map((t) =>
          t.id === id
            ? { ...t, ...input, updatedAt: now, completedAt: input.status === 'Done' ? (t.completedAt ?? now) : null }
            : t,
        ),
      )
      return { snaps }
    },
    onError: (e, _v, ctx) => {
      rollback(ctx?.snaps)
      if (!(e instanceof ApiError && e.status === 400)) toasts.push('Could not save changes.', 'error')
    },
    onSettled: settle,
  })

  const remove = useMutation({
    mutationFn: (id: string) => tasksApi.remove(id),
    onMutate: async (id) => {
      const snaps = await snapshot()
      patchLists((tasks) => tasks.filter((t) => t.id !== id))
      return { snaps }
    },
    onError: (_e, _v, ctx) => {
      rollback(ctx?.snaps)
      toasts.push('Could not delete task.', 'error')
    },
    onSuccess: () => toasts.push('Task deleted.'),
    onSettled: settle,
  })

  return { create, update, remove }
}
