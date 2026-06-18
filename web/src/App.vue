<script setup lang="ts">
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useAuthStore } from './stores/auth'
import ToastHost from './components/ToastHost.vue'

const auth = useAuthStore()
const router = useRouter()

async function logout() {
  await auth.logout()
  router.replace({ name: 'login' })
}
</script>

<template>
  <a class="skip-link" href="#main">Skip to content</a>

  <header class="app-header">
    <div class="app-header__inner">
      <RouterLink class="brand" :to="{ name: 'tasks' }">Task&nbsp;Manager</RouterLink>
      <div v-if="auth.isAuthenticated" class="app-header__user">
        <span class="muted">{{ auth.user?.email }}</span>
        <button type="button" class="btn btn--ghost" @click="logout">Log out</button>
      </div>
    </div>
  </header>

  <main id="main" class="app-main">
    <RouterView />
  </main>

  <ToastHost />
</template>
