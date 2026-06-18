import { defineStore } from 'pinia'
import { ref } from 'vue'

export type ToastType = 'success' | 'error'
export interface Toast {
  id: number
  message: string
  type: ToastType
}

export const useToastStore = defineStore('toasts', () => {
  const toasts = ref<Toast[]>([])
  let seq = 0

  function push(message: string, type: ToastType = 'success') {
    const id = ++seq
    toasts.value.push({ id, message, type })
    window.setTimeout(() => dismiss(id), 5000)
  }

  function dismiss(id: number) {
    toasts.value = toasts.value.filter((t) => t.id !== id)
  }

  return { toasts, push, dismiss }
})

export interface RecentItem {
  id: string
  title: string
}

/** "Recently viewed" — pure frontend state (no backend), capped at 5 and de-duplicated. */
export const useRecentStore = defineStore('recent', () => {
  const items = ref<RecentItem[]>([])

  function visit(item: RecentItem) {
    items.value = [item, ...items.value.filter((x) => x.id !== item.id)].slice(0, 5)
  }

  function remove(id: string) {
    items.value = items.value.filter((x) => x.id !== id)
  }

  return { items, visit, remove }
})
