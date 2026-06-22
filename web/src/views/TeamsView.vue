<script setup lang="ts">
import { computed, ref } from 'vue'
import { useTeamDetail, useTeamMutations, useTeamsQuery } from '../composables/useTeams'
import { ApiError } from '../api/http'

const { data: teams, isPending } = useTeamsQuery()
const { create, addMember, leave, remove } = useTeamMutations()

const newTeamName = ref('')
const createError = ref('')
async function createTeam() {
  if (!newTeamName.value.trim()) {
    createError.value = 'Team name is required.'
    return
  }
  createError.value = ''
  try {
    await create.mutateAsync(newTeamName.value.trim())
    newTeamName.value = ''
  } catch (e) {
    createError.value =
      e instanceof ApiError && e.status === 400 ? (e.fieldErrors.name?.[0] ?? 'Invalid name.') : 'Could not create team.'
  }
}

const selectedId = ref<string | null>(null)
const { data: detail, isPending: detailLoading } = useTeamDetail(selectedId)
function toggle(id: string) {
  selectedId.value = selectedId.value === id ? null : id
  memberError.value = ''
  newMemberEmail.value = ''
}

const newMemberEmail = ref('')
const memberError = ref('')
async function addMemberToSelected() {
  if (!selectedId.value) return
  if (!newMemberEmail.value.trim()) {
    memberError.value = 'Email is required.'
    return
  }
  memberError.value = ''
  try {
    await addMember.mutateAsync({ id: selectedId.value, email: newMemberEmail.value.trim() })
    newMemberEmail.value = ''
  } catch (e) {
    memberError.value =
      e instanceof ApiError && e.status === 400 ? (e.fieldErrors.email?.[0] ?? 'Could not add member.') : 'Could not add member.'
  }
}

async function leaveTeam(id: string) {
  if (!window.confirm('Leave this team? You will lose access to its tasks.')) return
  try {
    await leave.mutateAsync(id)
    if (selectedId.value === id) selectedId.value = null
  } catch {
    /* toast shown by the mutation */
  }
}
async function deleteTeam(id: string) {
  if (!window.confirm('Delete this team? Its tasks will be permanently removed.')) return
  try {
    await remove.mutateAsync(id)
    if (selectedId.value === id) selectedId.value = null
  } catch {
    /* toast shown by the mutation */
  }
}

const hasTeams = computed(() => (teams.value?.length ?? 0) > 0)
</script>

<template>
  <div class="teams">
    <div class="tasks__head">
      <h1>Teams</h1>
    </div>

    <form class="create-team" @submit.prevent="createTeam">
      <div class="field">
        <label for="team-name">New team</label>
        <div class="inline-form">
          <input
            id="team-name"
            v-model="newTeamName"
            type="text"
            maxlength="100"
            placeholder="Team name"
            :aria-invalid="!!createError"
            :aria-describedby="createError ? 'team-name-err' : undefined"
          />
          <button type="submit" class="btn btn--primary" :disabled="create.isPending.value">Create</button>
        </div>
        <p v-if="createError" id="team-name-err" class="field-error">{{ createError }}</p>
      </div>
    </form>

    <p v-if="isPending" class="state" role="status">Loading teams…</p>
    <p v-else-if="!hasTeams" class="state">No teams yet.</p>
    <ul v-else class="team-list">
      <li v-for="team in teams" :key="team.id">
        <div class="team" :class="{ 'team--selected': team.id === selectedId }">
          <button type="button" class="team__head" :aria-expanded="team.id === selectedId" @click="toggle(team.id)">
            <span class="team__name">{{ team.name }}</span>
            <span v-if="team.isPersonal" class="badge">Private</span>
            <span class="muted team__count">{{ team.memberCount }} member{{ team.memberCount === 1 ? '' : 's' }}</span>
          </button>

          <div v-if="team.id === selectedId" class="team__detail">
            <p v-if="team.isPersonal" class="muted">Your private space — tasks here are visible only to you.</p>

            <template v-else>
              <p v-if="detailLoading" class="muted">Loading members…</p>
              <ul v-else class="member-list">
                <li v-for="m in detail?.members" :key="m.userId" class="member">{{ m.email }}</li>
              </ul>

              <form class="inline-form add-member" @submit.prevent="addMemberToSelected">
                <label class="sr-only" for="member-email">Add member by email</label>
                <input
                  id="member-email"
                  v-model="newMemberEmail"
                  type="email"
                  placeholder="teammate@example.com"
                  :aria-invalid="!!memberError"
                  :aria-describedby="memberError ? 'member-err' : undefined"
                />
                <button type="submit" class="btn btn--primary btn--sm" :disabled="addMember.isPending.value">Add</button>
              </form>
              <p v-if="memberError" id="member-err" class="field-error">{{ memberError }}</p>

              <div class="team__actions">
                <button
                  v-if="team.memberCount > 1"
                  type="button"
                  class="btn btn--ghost btn--sm"
                  @click="leaveTeam(team.id)"
                >
                  Leave team
                </button>
                <button v-else type="button" class="btn btn--danger btn--sm" @click="deleteTeam(team.id)">
                  Delete team
                </button>
              </div>
            </template>
          </div>
        </div>
      </li>
    </ul>
  </div>
</template>
