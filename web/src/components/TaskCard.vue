<script setup lang="ts">
import { computed } from 'vue'
import type { Task } from '../types'
import { STATUS_LABELS } from '../types'
import { formatDate, formatDue, isOverdue } from '../utils/datetime'

const props = defineProps<{ task: Task }>()
const emit = defineEmits<{
  open: [task: Task]
  edit: [task: Task]
  delete: [task: Task]
  'toggle-status': [task: Task]
  'toggle-pin': [task: Task]
}>()

const overdue = computed(() => isOverdue(props.task))
const nextStatusLabel = computed(() =>
  props.task.status === 'Todo' ? 'in progress' : props.task.status === 'InProgress' ? 'done' : 'to do',
)
// Optimistic (not-yet-persisted) rows get a temp id; disable actions on them.
const isTemp = computed(() => props.task.id.startsWith('temp-'))
const assignee = computed(() => props.task.assigneeEmail?.split('@')[0] ?? null)
</script>

<template>
  <article
    class="task"
    :class="{ 'task--done': task.status === 'Done', 'task--pending': isTemp }"
    :aria-label="`Task: ${task.title}`"
  >
    <div class="task__main">
      <button
        type="button"
        class="status-dot"
        :class="`status-dot--${task.status}`"
        :aria-label="`Status: ${STATUS_LABELS[task.status]}. Activate to mark as ${nextStatusLabel}.`"
        :disabled="isTemp"
        @click="emit('toggle-status', task)"
      >
        <span aria-hidden="true">{{ task.status === 'Done' ? '✓' : task.status === 'InProgress' ? '◐' : '○' }}</span>
      </button>

      <div class="task__body">
        <h3 class="task__title">
          <!-- Stretched button: opens the task in view mode; its ::after covers the whole card so a
               click anywhere (except the action buttons, which sit above it) opens it. Keyboard-
               accessible as a real button. -->
          <button type="button" class="task__open" :disabled="isTemp" @click="emit('open', task)">
            {{ task.title }}
          </button>
        </h3>
        <p v-if="task.notes" class="task__notes">{{ task.notes }}</p>
        <div class="task__meta">
          <span class="badge" :class="`badge--prio-${task.priority.toLowerCase()}`">{{ task.priority }} priority</span>
          <span class="badge badge--status">{{ STATUS_LABELS[task.status] }}</span>
          <span class="badge badge--team"><span aria-hidden="true">👥</span> {{ task.teamName }}</span>
          <span class="badge badge--assignee"><span aria-hidden="true">👤</span> {{ assignee ?? 'Unassigned' }}</span>
          <span v-if="task.dueAt" class="badge" :class="overdue ? 'badge--overdue' : 'badge--due'">
            <span aria-hidden="true">{{ overdue ? '⚠' : '🗓' }}</span>
            {{ overdue ? 'Overdue: ' : 'Due ' }}{{ formatDue(task.dueAt) }}
          </span>
          <span v-if="task.isPinned" class="badge badge--pinned"><span aria-hidden="true">📌</span> Pinned</span>
          <span class="task__created">Created {{ formatDate(task.createdAt) }}</span>
        </div>
      </div>
    </div>

    <div class="task__actions">
      <button
        type="button"
        class="icon-btn"
        :aria-pressed="task.isPinned"
        :aria-label="task.isPinned ? 'Unpin task' : 'Pin task'"
        :disabled="isTemp"
        @click="emit('toggle-pin', task)"
      >
        <span aria-hidden="true">📌</span>
      </button>
      <button type="button" class="btn btn--ghost btn--sm" :disabled="isTemp" @click="emit('edit', task)">Edit</button>
      <button type="button" class="btn btn--danger btn--sm" :disabled="isTemp" @click="emit('delete', task)">
        Delete
      </button>
    </div>
  </article>
</template>
