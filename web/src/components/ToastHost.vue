<script setup lang="ts">
import { useToastStore } from '../stores/ui'

const toasts = useToastStore()
</script>

<template>
  <!-- aria-live so success/failure is announced to screen readers, not just shown visually. -->
  <div class="toast-host" aria-live="polite" aria-atomic="false">
    <div
      v-for="t in toasts.toasts"
      :key="t.id"
      class="toast"
      :class="`toast--${t.type}`"
      role="status"
    >
      <span class="toast__icon" aria-hidden="true">{{ t.type === 'error' ? '⚠' : '✓' }}</span>
      <span class="toast__msg">{{ t.message }}</span>
      <button type="button" class="toast__close" aria-label="Dismiss notification" @click="toasts.dismiss(t.id)">
        ×
      </button>
    </div>
  </div>
</template>
