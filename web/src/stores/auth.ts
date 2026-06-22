import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { authApi } from '../api/auth'
import type { User } from '../types'
import { useRecentStore } from './ui'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const initialized = ref(false)

  const isAuthenticated = computed(() => user.value !== null)

  /** Restore the session from the cookie on first load. Safe to call repeatedly. */
  async function init() {
    if (initialized.value) return
    try {
      user.value = await authApi.me()
    } catch {
      user.value = null
    } finally {
      initialized.value = true
    }
  }

  async function login(email: string, password: string) {
    user.value = await authApi.login(email, password)
    initialized.value = true
    useRecentStore().clear() // don't carry per-user UI state across accounts
  }

  async function register(email: string, password: string) {
    user.value = await authApi.register(email, password)
    initialized.value = true
    useRecentStore().clear()
  }

  async function logout() {
    try {
      await authApi.logout()
    } finally {
      user.value = null
      useRecentStore().clear()
    }
  }

  function clear() {
    user.value = null
    useRecentStore().clear()
  }

  return { user, initialized, isAuthenticated, init, login, register, logout, clear }
})
