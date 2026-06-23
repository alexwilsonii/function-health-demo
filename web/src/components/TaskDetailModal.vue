<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import type { CreateTaskInput, Task, TaskPriority, TaskState, UpdateTaskInput } from '../types'
import { PRIORITIES, STATUSES, STATUS_LABELS } from '../types'
import { formatDate, formatDue, isoToLocalInput, localInputToIso } from '../utils/datetime'
import { ApiError } from '../api/http'
import { useComments } from '../composables/useComments'
import { useTeamDetail, useTeamsQuery } from '../composables/useTeams'

const props = defineProps<{
  task: Task | null
  mode: 'view' | 'edit'
  submit: (payload: CreateTaskInput | UpdateTaskInput) => Promise<void>
}>()
const emit = defineEmits<{ close: [] }>()

const currentMode = ref<'view' | 'edit'>(props.mode)
const isView = computed(() => currentMode.value === 'view')
const isCreate = computed(() => props.task === null)
const openedInView = props.mode === 'view'

const form = reactive({
  title: '',
  notes: '',
  status: 'Todo' as TaskState,
  priority: 'Medium' as TaskPriority,
  dueAt: '',
  isPinned: false,
  teamId: '',
  assigneeUserId: '',
})
function resetForm() {
  form.title = props.task?.title ?? ''
  form.notes = props.task?.notes ?? ''
  form.status = props.task?.status ?? 'Todo'
  form.priority = props.task?.priority ?? 'Medium'
  form.dueAt = isoToLocalInput(props.task?.dueAt ?? null)
  form.isPinned = props.task?.isPinned ?? false
  form.teamId = props.task?.teamId ?? ''
  form.assigneeUserId = props.task?.assigneeUserId ?? ''
}
resetForm()

// --- Teams (create: pick a team) + members (assignee picker) ---
const teamsQuery = useTeamsQuery()
const teams = computed(() => teamsQuery.data.value ?? [])
const activeTeamId = computed(() => (isCreate.value ? form.teamId || null : (props.task?.teamId ?? null)))
const teamDetail = useTeamDetail(activeTeamId)
const members = computed(() => teamDetail.data.value?.members ?? [])

// Default a new task to the first team; clear the assignee if the team changes.
watch(
  () => teamsQuery.data.value,
  (list) => {
    if (isCreate.value && !form.teamId && list?.length) form.teamId = list[0].id
  },
  { immediate: true },
)
watch(
  () => form.teamId,
  () => {
    if (isCreate.value) form.assigneeUserId = ''
  },
)

const fieldErrors = ref<Record<string, string[]>>({})
const formError = ref('')
const submitting = ref(false)

// --- Comments (existing task, view mode, lazy) ---
const taskId = computed(() => props.task?.id ?? null)
const showComments = computed(() => isView.value && !isCreate.value)
const { list: commentsQuery, add: addComment, remove: removeComment } = useComments(taskId, showComments)
const comments = computed(() => commentsQuery.data.value ?? [])
const commentsLoading = computed(() => commentsQuery.isLoading.value)
const newComment = ref('')
const commentError = ref('')

// --- Focus management ---
const dialogRef = ref<HTMLElement | null>(null)
const titleInput = ref<HTMLInputElement | null>(null)
const editButton = ref<HTMLButtonElement | null>(null)
let previouslyFocused: HTMLElement | null = null

onMounted(() => {
  previouslyFocused = document.activeElement as HTMLElement | null
  focusInitial()
  document.addEventListener('keydown', onKeydown)
})
onBeforeUnmount(() => {
  document.removeEventListener('keydown', onKeydown)
  previouslyFocused?.focus?.() // restore focus to whatever opened the dialog
})

function focusInitial() {
  nextTick(() => {
    if (isView.value) editButton.value?.focus()
    else titleInput.value?.focus()
  })
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') {
    e.preventDefault()
    emit('close')
  } else if (e.key === 'Tab') {
    trapFocus(e)
  }
}
// Keep Tab focus inside the dialog: wrap from the last focusable back to the first (and Shift+Tab the
// other way) so keyboard focus can't escape the modal while it's open.
function trapFocus(e: KeyboardEvent) {
  const root = dialogRef.value
  if (!root) return
  const f = Array.from(
    root.querySelectorAll<HTMLElement>(
      'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled])',
    ),
  )
  if (f.length === 0) return
  const first = f[0]
  const last = f[f.length - 1]
  if (e.shiftKey && document.activeElement === first) {
    e.preventDefault()
    last.focus()
  } else if (!e.shiftKey && document.activeElement === last) {
    e.preventDefault()
    first.focus()
  }
}

function startEdit() {
  currentMode.value = 'edit'
  nextTick(() => titleInput.value?.focus())
}
function cancelEdit() {
  if (!isCreate.value && openedInView) {
    resetForm()
    fieldErrors.value = {}
    formError.value = ''
    currentMode.value = 'view'
    focusInitial()
  } else {
    emit('close')
  }
}

async function onSubmit() {
  submitting.value = true
  fieldErrors.value = {}
  formError.value = ''
  const dueAtIso = localInputToIso(form.dueAt)
  const notes = form.notes.trim() ? form.notes : null
  const assigneeUserId = form.assigneeUserId || null
  try {
    if (isCreate.value) {
      await props.submit({
        teamId: form.teamId,
        title: form.title,
        notes,
        status: form.status,
        priority: form.priority,
        dueAt: dueAtIso,
        assigneeUserId,
      } satisfies CreateTaskInput)
      emit('close')
    } else {
      await props.submit({
        title: form.title,
        notes,
        status: form.status,
        priority: form.priority,
        dueAt: dueAtIso,
        isPinned: form.isPinned,
        assigneeUserId,
      } satisfies UpdateTaskInput)
      if (openedInView) {
        currentMode.value = 'view'
        focusInitial()
      } else {
        emit('close')
      }
    }
  } catch (e) {
    if (e instanceof ApiError && e.status === 400) {
      fieldErrors.value = e.fieldErrors
      formError.value = fieldErrors.value.request?.[0] ?? fieldErrors.value.teamId?.[0] ?? fieldErrors.value.assigneeUserId?.[0] ?? ''
    } else {
      formError.value = 'Could not save. Please try again.'
    }
  } finally {
    submitting.value = false
  }
}

async function submitComment() {
  if (!newComment.value.trim()) {
    commentError.value = 'Comment cannot be empty.'
    return
  }
  commentError.value = ''
  try {
    await addComment.mutateAsync(newComment.value)
    newComment.value = ''
  } catch (e) {
    commentError.value =
      e instanceof ApiError && e.status === 400
        ? (e.fieldErrors.body?.[0] ?? 'Invalid comment.')
        : 'Could not add comment.'
  }
}
function deleteComment(id: string) {
  removeComment.mutate(id)
}

const heading = computed(() => (isCreate.value ? 'New task' : isView.value ? 'Task details' : 'Edit task'))
</script>

<template>
  <div class="modal-overlay" @click.self="emit('close')">
    <div ref="dialogRef" class="modal card" role="dialog" aria-modal="true" aria-labelledby="modal-title">
      <div class="modal__head">
        <h2 id="modal-title">{{ heading }}</h2>
        <div class="modal__head-actions">
          <button
            v-if="isView && !isCreate"
            ref="editButton"
            type="button"
            class="icon-btn"
            aria-label="Edit task"
            @click="startEdit"
          >
            <span aria-hidden="true">✏️</span>
          </button>
          <button type="button" class="icon-btn" aria-label="Close" @click="emit('close')">
            <span aria-hidden="true">✕</span>
          </button>
        </div>
      </div>

      <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>

      <div v-if="isView && !isCreate" class="modal__meta">
        <span>Team: <strong>{{ task?.teamName }}</strong></span>
        <span>Created by {{ task?.createdByEmail }}</span>
        <span>Assigned to {{ task?.assigneeEmail ?? 'no one' }}</span>
        <span>Created {{ formatDate(task?.createdAt ?? null) }} · Updated {{ formatDate(task?.updatedAt ?? null) }}</span>
      </div>

      <form novalidate @submit.prevent="onSubmit">
        <div class="field">
          <label for="t-title">Title</label>
          <input
            id="t-title"
            ref="titleInput"
            v-model="form.title"
            type="text"
            required
            maxlength="200"
            :disabled="isView"
            :aria-invalid="!!fieldErrors.title"
            :aria-describedby="fieldErrors.title ? 't-title-err' : undefined"
          />
          <p v-if="fieldErrors.title" id="t-title-err" class="field-error">{{ fieldErrors.title[0] }}</p>
        </div>

        <div class="field">
          <label for="t-notes">Notes</label>
          <textarea
            id="t-notes"
            v-model="form.notes"
            rows="3"
            :disabled="isView"
            :aria-invalid="!!fieldErrors.notes"
            :aria-describedby="fieldErrors.notes ? 't-notes-err' : undefined"
          ></textarea>
          <p v-if="fieldErrors.notes" id="t-notes-err" class="field-error">{{ fieldErrors.notes[0] }}</p>
        </div>

        <!-- Team + assignee (hidden in view, where they appear in the meta line above) -->
        <div v-if="!isView" class="field-row">
          <div class="field">
            <label for="t-team">Team</label>
            <select v-if="isCreate" id="t-team" v-model="form.teamId">
              <option v-for="t in teams" :key="t.id" :value="t.id">{{ t.name }}</option>
            </select>
            <input v-else id="t-team" type="text" :value="task?.teamName" disabled />
          </div>
          <div class="field">
            <label for="t-assignee">Assignee</label>
            <select id="t-assignee" v-model="form.assigneeUserId">
              <option value="">Unassigned</option>
              <option v-for="m in members" :key="m.userId" :value="m.userId">{{ m.email }}</option>
            </select>
          </div>
        </div>

        <div class="field-row">
          <div class="field">
            <label for="t-status">Status</label>
            <select id="t-status" v-model="form.status" :disabled="isView">
              <option v-for="s in STATUSES" :key="s" :value="s">{{ STATUS_LABELS[s] }}</option>
            </select>
          </div>
          <div class="field">
            <label for="t-priority">Priority</label>
            <select id="t-priority" v-model="form.priority" :disabled="isView">
              <option v-for="p in PRIORITIES" :key="p" :value="p">{{ p }}</option>
            </select>
          </div>
        </div>

        <div class="field">
          <label for="t-due">Due date</label>
          <input id="t-due" v-model="form.dueAt" type="datetime-local" :disabled="isView" />
          <p v-if="!isView" class="field-hint">Stored in UTC, shown in your local time.</p>
        </div>

        <div v-if="!isCreate" class="field field--check">
          <input id="t-pin" v-model="form.isPinned" type="checkbox" :disabled="isView" />
          <label for="t-pin">Pinned (keep at top of the list)</label>
        </div>

        <div class="modal__actions">
          <template v-if="isView">
            <button type="button" class="btn btn--ghost" @click="emit('close')">Close</button>
          </template>
          <template v-else>
            <button type="button" class="btn btn--ghost" @click="cancelEdit">Cancel</button>
            <button type="submit" class="btn btn--primary" :disabled="submitting">
              {{ submitting ? 'Saving…' : isCreate ? 'Create task' : 'Save changes' }}
            </button>
          </template>
        </div>
      </form>

      <!-- Comments: view mode only, lazily loaded for the open task -->
      <section v-if="showComments" class="comments" aria-labelledby="comments-heading">
        <h3 id="comments-heading">Comments</h3>

        <form class="comment-add" @submit.prevent="submitComment">
          <label class="sr-only" for="c-body">Add a comment</label>
          <textarea
            id="c-body"
            v-model="newComment"
            rows="2"
            placeholder="Add a comment…"
            :aria-invalid="!!commentError"
            :aria-describedby="commentError ? 'c-body-err' : undefined"
          ></textarea>
          <p v-if="commentError" id="c-body-err" class="field-error">{{ commentError }}</p>
          <div class="comment-add__actions">
            <button type="submit" class="btn btn--primary btn--sm" :disabled="addComment.isPending.value">Add</button>
          </div>
        </form>

        <p v-if="commentsLoading" class="muted">Loading comments…</p>
        <p v-else-if="comments.length === 0" class="muted">No comments yet.</p>
        <ul v-else class="comment-list">
          <li v-for="c in comments" :key="c.id" class="comment" :class="{ 'comment--pending': c.id.startsWith('temp-') }">
            <div class="comment__body">{{ c.body }}</div>
            <div class="comment__foot">
              <span>{{ c.authorEmail }} · <time :datetime="c.createdAt">{{ formatDue(c.createdAt) }}</time></span>
              <button
                type="button"
                class="comment__del"
                aria-label="Delete comment"
                :disabled="c.id.startsWith('temp-')"
                @click="deleteComment(c.id)"
              >
                ×
              </button>
            </div>
          </li>
        </ul>
      </section>
    </div>
  </div>
</template>
