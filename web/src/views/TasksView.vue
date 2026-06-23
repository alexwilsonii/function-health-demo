<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useTaskMutations, useTasksQuery } from '../composables/useTasks'
import { useTeamsQuery } from '../composables/useTeams'
import type { CreateTaskInput, Task, TaskQuery, UpdateTaskInput } from '../types'
import { PRIORITIES, STATUSES, STATUS_LABELS } from '../types'
import TaskCard from '../components/TaskCard.vue'
import TaskDetailModal from '../components/TaskDetailModal.vue'
import { useRecentStore, useToastStore } from '../stores/ui'
import { tasksApi } from '../api/tasks'

const filters = reactive<TaskQuery>({ q: '', status: '', priority: '', sort: 'due', teamId: '', assignee: '' })
const queryRef = computed<TaskQuery>(() => ({ ...filters }))
const { data: tasks, isPending, isError, refetch } = useTasksQuery(queryRef)
const { create, update, remove } = useTaskMutations()
const { data: teams } = useTeamsQuery()
const recent = useRecentStore()
const toasts = useToastStore()

// Debounce search input into the (reactive) query.
const searchInput = ref('')
let searchTimer: number | undefined
watch(searchInput, (val) => {
  window.clearTimeout(searchTimer)
  searchTimer = window.setTimeout(() => {
    filters.q = val.trim()
  }, 300)
})

const modalOpen = ref(false)
const editing = ref<Task | null>(null)
const modalMode = ref<'view' | 'edit'>('view')

function openTask(task: Task, mode: 'view' | 'edit') {
  editing.value = task
  modalMode.value = mode
  recent.visit({ id: task.id, title: task.title })
  modalOpen.value = true
}
function openView(task: Task) {
  openTask(task, 'view')
}
function openEdit(task: Task) {
  openTask(task, 'edit')
}
function openCreate() {
  editing.value = null
  modalMode.value = 'edit'
  modalOpen.value = true
}
function closeModal() {
  modalOpen.value = false
  editing.value = null
}

async function handleSubmit(payload: CreateTaskInput | UpdateTaskInput) {
  if (editing.value) {
    await update.mutateAsync({ id: editing.value.id, input: payload as UpdateTaskInput })
  } else {
    await create.mutateAsync(payload as CreateTaskInput)
  }
}

function toUpdateInput(task: Task, patch: Partial<UpdateTaskInput>): UpdateTaskInput {
  return {
    title: task.title,
    notes: task.notes,
    status: task.status,
    priority: task.priority,
    dueAt: task.dueAt,
    isPinned: task.isPinned,
    assigneeUserId: task.assigneeUserId,
    ...patch,
  }
}
function cycleStatus(task: Task) {
  const next = task.status === 'Todo' ? 'InProgress' : task.status === 'InProgress' ? 'Done' : 'Todo'
  update.mutate({ id: task.id, input: toUpdateInput(task, { status: next }) })
}
function togglePin(task: Task) {
  update.mutate({ id: task.id, input: toUpdateInput(task, { isPinned: !task.isPinned }) })
}
function deleteTask(task: Task) {
  if (window.confirm(`Delete “${task.title}”? This cannot be undone.`)) {
    remove.mutate(task.id)
    recent.remove(task.id)
  }
}
async function openRecent(id: string) {
  // Open even when the task is hidden by the current filters: prefer the loaded copy, else fetch by id.
  const found = tasks.value?.find((t) => t.id === id)
  if (found) {
    openView(found)
    return
  }
  try {
    openView(await tasksApi.get(id))
  } catch {
    recent.remove(id)
    toasts.push('That task is no longer available.', 'error')
  }
}

const hasActiveFilters = computed(
  () => !!filters.q || !!filters.status || !!filters.priority || !!filters.teamId || !!filters.assignee,
)
function clearFilters() {
  filters.q = ''
  filters.status = ''
  filters.priority = ''
  filters.teamId = ''
  filters.assignee = ''
  searchInput.value = ''
}
</script>

<template>
  <div class="tasks">
    <div class="tasks__head">
      <h1>Tasks</h1>
      <button type="button" class="btn btn--primary" @click="openCreate">+ New task</button>
    </div>

    <nav v-if="recent.items.length" class="recent" aria-label="Recently viewed tasks">
      <span class="recent__label">Recently viewed:</span>
      <button v-for="item in recent.items" :key="item.id" type="button" class="chip" @click="openRecent(item.id)">
        {{ item.title }}
      </button>
    </nav>

    <form class="filters" role="search" @submit.prevent>
      <div class="field">
        <label for="f-search">Search</label>
        <input id="f-search" v-model="searchInput" type="search" placeholder="Title or notes" />
      </div>
      <div class="field">
        <label for="f-team">Team</label>
        <select id="f-team" v-model="filters.teamId">
          <option value="">All teams</option>
          <option v-for="t in teams" :key="t.id" :value="t.id">{{ t.name }}</option>
        </select>
      </div>
      <div class="field">
        <label for="f-assignee">Assignee</label>
        <select id="f-assignee" v-model="filters.assignee">
          <option value="">Anyone</option>
          <option value="me">Assigned to me</option>
          <option value="unassigned">Unassigned</option>
        </select>
      </div>
      <div class="field">
        <label for="f-status">Status</label>
        <select id="f-status" v-model="filters.status">
          <option value="">All</option>
          <option v-for="s in STATUSES" :key="s" :value="s">{{ STATUS_LABELS[s] }}</option>
        </select>
      </div>
      <div class="field">
        <label for="f-priority">Priority</label>
        <select id="f-priority" v-model="filters.priority">
          <option value="">All</option>
          <option v-for="p in PRIORITIES" :key="p" :value="p">{{ p }}</option>
        </select>
      </div>
      <div class="field">
        <label for="f-sort">Sort by</label>
        <select id="f-sort" v-model="filters.sort">
          <option value="due">Due date</option>
          <option value="priority">Priority</option>
          <option value="created">Recently created</option>
        </select>
      </div>
      <button v-if="hasActiveFilters" type="button" class="btn btn--ghost btn--sm" @click="clearFilters">Clear</button>
    </form>

    <p v-if="isPending" class="state" role="status">Loading tasks…</p>
    <div v-else-if="isError" class="state state--error" role="alert">
      Could not load tasks.
      <button type="button" class="btn btn--ghost btn--sm" @click="() => refetch()">Retry</button>
    </div>
    <p v-else-if="!tasks || tasks.length === 0" class="state">
      {{ hasActiveFilters ? 'No tasks match your filters.' : 'No tasks yet — create your first one.' }}
    </p>

    <ul v-else class="task-list">
      <li v-for="task in tasks" :key="task.id">
        <TaskCard
          :task="task"
          @open="openView"
          @edit="openEdit"
          @delete="deleteTask"
          @toggle-status="cycleStatus"
          @toggle-pin="togglePin"
        />
      </li>
    </ul>

    <TaskDetailModal
      v-if="modalOpen"
      :task="editing"
      :mode="modalMode"
      :submit="handleSubmit"
      @close="closeModal"
    />
  </div>
</template>
