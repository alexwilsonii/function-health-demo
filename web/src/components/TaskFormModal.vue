<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import type { CreateTaskInput, Task, TaskPriority, TaskState, UpdateTaskInput } from '../types'
import { PRIORITIES, STATUSES, STATUS_LABELS } from '../types'
import { isoToLocalInput, localInputToIso } from '../utils/datetime'
import { ApiError } from '../api/http'

const props = defineProps<{
  task: Task | null
  submit: (payload: CreateTaskInput | UpdateTaskInput) => Promise<void>
}>()
const emit = defineEmits<{ close: [] }>()

const isEdit = computed(() => props.task !== null)

const form = reactive({
  title: props.task?.title ?? '',
  notes: props.task?.notes ?? '',
  status: (props.task?.status ?? 'Todo') as TaskState,
  priority: (props.task?.priority ?? 'Medium') as TaskPriority,
  dueAt: isoToLocalInput(props.task?.dueAt ?? null),
  isPinned: props.task?.isPinned ?? false,
})

const fieldErrors = ref<Record<string, string[]>>({})
const formError = ref('')
const submitting = ref(false)

const dialogRef = ref<HTMLElement | null>(null)
const titleInput = ref<HTMLInputElement | null>(null)
let previouslyFocused: HTMLElement | null = null

onMounted(() => {
  previouslyFocused = document.activeElement as HTMLElement | null
  titleInput.value?.focus()
  document.addEventListener('keydown', onKeydown)
})
onBeforeUnmount(() => {
  document.removeEventListener('keydown', onKeydown)
  previouslyFocused?.focus?.()
})

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') {
    e.preventDefault()
    emit('close')
  } else if (e.key === 'Tab') {
    trapFocus(e)
  }
}

function trapFocus(e: KeyboardEvent) {
  const root = dialogRef.value
  if (!root) return
  const focusables = Array.from(
    root.querySelectorAll<HTMLElement>(
      'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled])',
    ),
  )
  if (focusables.length === 0) return
  const first = focusables[0]
  const last = focusables[focusables.length - 1]
  if (e.shiftKey && document.activeElement === first) {
    e.preventDefault()
    last.focus()
  } else if (!e.shiftKey && document.activeElement === last) {
    e.preventDefault()
    first.focus()
  }
}

async function onSubmit() {
  submitting.value = true
  fieldErrors.value = {}
  formError.value = ''
  const dueAtIso = localInputToIso(form.dueAt)
  const notes = form.notes.trim() ? form.notes : null

  try {
    if (isEdit.value) {
      const payload: UpdateTaskInput = {
        title: form.title,
        notes,
        status: form.status,
        priority: form.priority,
        dueAt: dueAtIso,
        isPinned: form.isPinned,
      }
      await props.submit(payload)
    } else {
      const payload: CreateTaskInput = {
        title: form.title,
        notes,
        status: form.status,
        priority: form.priority,
        dueAt: dueAtIso,
      }
      await props.submit(payload)
    }
    emit('close')
  } catch (e) {
    if (e instanceof ApiError && e.status === 400) {
      fieldErrors.value = e.fieldErrors
      if (fieldErrors.value.request) formError.value = fieldErrors.value.request[0]
    } else {
      formError.value = 'Could not save. Please try again.'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="modal-overlay" @click.self="emit('close')">
    <div ref="dialogRef" class="modal card" role="dialog" aria-modal="true" aria-labelledby="modal-title">
      <h2 id="modal-title">{{ isEdit ? 'Edit task' : 'New task' }}</h2>
      <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>

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
            :aria-invalid="!!fieldErrors.notes"
            :aria-describedby="fieldErrors.notes ? 't-notes-err' : undefined"
          ></textarea>
          <p v-if="fieldErrors.notes" id="t-notes-err" class="field-error">{{ fieldErrors.notes[0] }}</p>
        </div>

        <div class="field-row">
          <div class="field">
            <label for="t-status">Status</label>
            <select id="t-status" v-model="form.status">
              <option v-for="s in STATUSES" :key="s" :value="s">{{ STATUS_LABELS[s] }}</option>
            </select>
          </div>
          <div class="field">
            <label for="t-priority">Priority</label>
            <select id="t-priority" v-model="form.priority">
              <option v-for="p in PRIORITIES" :key="p" :value="p">{{ p }}</option>
            </select>
          </div>
        </div>

        <div class="field">
          <label for="t-due">Due date</label>
          <input id="t-due" v-model="form.dueAt" type="datetime-local" />
          <p class="field-hint">Stored in UTC, shown in your local time.</p>
        </div>

        <div v-if="isEdit" class="field field--check">
          <input id="t-pin" v-model="form.isPinned" type="checkbox" />
          <label for="t-pin">Pinned (keep at top of the list)</label>
        </div>

        <div class="modal__actions">
          <button type="button" class="btn btn--ghost" @click="emit('close')">Cancel</button>
          <button type="submit" class="btn btn--primary" :disabled="submitting">
            {{ submitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create task' }}
          </button>
        </div>
      </form>
    </div>
  </div>
</template>
