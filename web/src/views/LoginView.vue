<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { ApiError } from '../api/http'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const form = reactive({ email: '', password: '' })
const fieldErrors = ref<Record<string, string[]>>({})
const formError = ref('')
const submitting = ref(false)

async function submit() {
  submitting.value = true
  fieldErrors.value = {}
  formError.value = ''
  try {
    await auth.login(form.email, form.password)
    const redirect = (route.query.redirect as string) || '/tasks'
    await router.replace(redirect)
  } catch (e) {
    if (e instanceof ApiError) {
      if (e.status === 401) formError.value = 'Invalid email or password.'
      else if (e.status >= 500) formError.value = 'Something went wrong. Please try again.'
      else fieldErrors.value = e.fieldErrors
    } else {
      formError.value = 'Network error. Please try again.'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <section class="auth card">
    <h1>Log in</h1>
    <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>

    <form novalidate @submit.prevent="submit">
      <div class="field">
        <label for="email">Email</label>
        <input
          id="email"
          v-model="form.email"
          type="email"
          autocomplete="email"
          required
          :aria-invalid="!!fieldErrors.email"
          :aria-describedby="fieldErrors.email ? 'email-err' : undefined"
        />
        <p v-if="fieldErrors.email" id="email-err" class="field-error">{{ fieldErrors.email[0] }}</p>
      </div>

      <div class="field">
        <label for="password">Password</label>
        <input
          id="password"
          v-model="form.password"
          type="password"
          autocomplete="current-password"
          required
          :aria-invalid="!!fieldErrors.password"
          :aria-describedby="fieldErrors.password ? 'password-err' : undefined"
        />
        <p v-if="fieldErrors.password" id="password-err" class="field-error">{{ fieldErrors.password[0] }}</p>
      </div>

      <button type="submit" class="btn btn--primary btn--block" :disabled="submitting">
        {{ submitting ? 'Logging in…' : 'Log in' }}
      </button>
    </form>

    <p class="muted center">No account? <RouterLink :to="{ name: 'register' }">Create one</RouterLink></p>
  </section>
</template>
