export type TaskState = 'Todo' | 'InProgress' | 'Done'
export type TaskPriority = 'Low' | 'Medium' | 'High'

export interface User {
  id: string
  email: string
}

export interface Team {
  id: string
  name: string
  isPersonal: boolean
  memberCount: number
}

export interface Member {
  userId: string
  email: string
  joinedAt: string
}

export interface TeamDetail {
  id: string
  name: string
  isPersonal: boolean
  members: Member[]
}

export interface Task {
  id: string
  teamId: string
  teamName: string
  title: string
  notes: string | null
  status: TaskState
  priority: TaskPriority
  dueAt: string | null
  isPinned: boolean
  createdByUserId: string
  createdByEmail: string
  assigneeUserId: string | null
  assigneeEmail: string | null
  createdAt: string
  updatedAt: string
  completedAt: string | null
}

export interface CreateTaskInput {
  teamId: string
  title: string
  notes?: string | null
  status?: TaskState
  priority?: TaskPriority
  dueAt?: string | null
  assigneeUserId?: string | null
}

export interface UpdateTaskInput {
  title: string
  notes: string | null
  status: TaskState
  priority: TaskPriority
  dueAt: string | null
  isPinned: boolean
  assigneeUserId: string | null
}

export interface Comment {
  id: string
  taskId: string
  body: string
  authorEmail: string
  createdAt: string
}

export type SortOption = 'due' | 'priority' | 'created'

export interface TaskQuery {
  q: string
  status: TaskState | ''
  priority: TaskPriority | ''
  sort: SortOption
  teamId: string | '' // '' = all my teams
  assignee: string // '' | 'me' | 'unassigned' | a user id
}

export const STATUSES: TaskState[] = ['Todo', 'InProgress', 'Done']
export const PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High']

export const STATUS_LABELS: Record<TaskState, string> = {
  Todo: 'To do',
  InProgress: 'In progress',
  Done: 'Done',
}
