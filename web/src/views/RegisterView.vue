<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter, RouterLink } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { ApiError } from '../api/http'

const auth = useAuthStore()
const router = useRouter()

const form = reactive({ email: '', password: '' })
const fieldErrors = ref<Record<string, string[]>>({})
const formError = ref('')
const submitting = ref(false)

async function submit() {
  submitting.value = true
  fieldErrors.value = {}
  formError.value = ''
  try {
    await auth.register(form.email, form.password)
    await router.replace('/tasks')
  } catch (e) {
    if (e instanceof ApiError) {
      if (e.status >= 500) formError.value = 'Something went wrong. Please try again.'
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
    <h1>Create account</h1>
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
          autocomplete="new-password"
          required
          aria-describedby="password-hint password-err"
          :aria-invalid="!!fieldErrors.password"
        />
        <p id="password-hint" class="field-hint">At least 8 characters.</p>
        <p v-if="fieldErrors.password" id="password-err" class="field-error">{{ fieldErrors.password[0] }}</p>
      </div>

      <button type="submit" class="btn btn--primary btn--block" :disabled="submitting">
        {{ submitting ? 'Creating…' : 'Create account' }}
      </button>
    </form>

    <p class="muted center">Already have an account? <RouterLink :to="{ name: 'login' }">Log in</RouterLink></p>
  </section>
</template>
