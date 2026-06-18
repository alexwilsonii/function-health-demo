import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { VueQueryPlugin } from '@tanstack/vue-query'
import App from './App.vue'
import router from './router'
import { setUnauthorizedHandler } from './api/http'
import { useAuthStore } from './stores/auth'
import './assets/main.css'

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.use(VueQueryPlugin)

// Global 401 handling: an unexpected 401 (expired session) clears auth and bounces to login.
setUnauthorizedHandler(() => {
  const auth = useAuthStore()
  auth.clear()
  if (router.currentRoute.value.name !== 'login') {
    router.replace({ name: 'login' })
  }
})

app.mount('#app')
