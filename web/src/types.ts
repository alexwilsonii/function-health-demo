export type TaskState = 'Todo' | 'InProgress' | 'Done'
export type TaskPriority = 'Low' | 'Medium' | 'High'

export interface User {
  id: string
  email: string
}

export interface Task {
  id: string
  title: string
  notes: string | null
  status: TaskState
  priority: TaskPriority
  dueAt: string | null
  isPinned: boolean
  createdAt: string
  updatedAt: string
  completedAt: string | null
}

export interface CreateTaskInput {
  title: string
  notes?: string | null
  status?: TaskState
  priority?: TaskPriority
  dueAt?: string | null
}

export interface UpdateTaskInput {
  title: string
  notes: string | null
  status: TaskState
  priority: TaskPriority
  dueAt: string | null
  isPinned: boolean
}

export interface Comment {
  id: string
  taskId: string
  body: string
  createdAt: string
}

export type SortOption = 'due' | 'priority' | 'created'

export interface TaskQuery {
  q: string
  status: TaskState | ''
  priority: TaskPriority | ''
  sort: SortOption
}

export const STATUSES: TaskState[] = ['Todo', 'InProgress', 'Done']
export const PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High']

export const STATUS_LABELS: Record<TaskState, string> = {
  Todo: 'To do',
  InProgress: 'In progress',
  Done: 'Done',
}
