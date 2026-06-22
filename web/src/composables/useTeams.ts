import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed, type Ref } from 'vue'
import { teamsApi } from '../api/teams'
import { useToastStore } from '../stores/ui'
import { ApiError } from '../api/http'

export function useTeamsQuery() {
  return useQuery({ queryKey: ['teams'], queryFn: () => teamsApi.list() })
}

export function useTeamDetail(id: Ref<string | null>) {
  return useQuery({
    queryKey: ['team', id],
    queryFn: () => teamsApi.detail(id.value as string),
    enabled: computed(() => !!id.value),
  })
}

export function useTeamMutations() {
  const qc = useQueryClient()
  const toasts = useToastStore()
  const invalidateTeams = () => qc.invalidateQueries({ queryKey: ['teams'] })
  const problem = (e: unknown, fallback: string) =>
    e instanceof ApiError ? (e.problem?.title ?? fallback) : fallback

  const create = useMutation({
    mutationFn: (name: string) => teamsApi.create(name),
    onSuccess: () => {
      invalidateTeams()
      toasts.push('Team created.')
    },
    onError: (e) => {
      // 400 (e.g. blank name) is shown inline in the form.
      if (!(e instanceof ApiError && e.status === 400)) toasts.push('Could not create team.', 'error')
    },
  })

  const addMember = useMutation({
    mutationFn: ({ id, email }: { id: string; email: string }) => teamsApi.addMember(id, email),
    onSuccess: (_d, vars) => {
      qc.invalidateQueries({ queryKey: ['team', vars.id] })
      invalidateTeams()
      toasts.push('Member added.')
    },
    onError: (e) => {
      if (!(e instanceof ApiError && e.status === 400)) toasts.push('Could not add member.', 'error')
    },
  })

  const leave = useMutation({
    mutationFn: (id: string) => teamsApi.leave(id),
    onSuccess: () => {
      invalidateTeams()
      toasts.push('You left the team.')
    },
    onError: (e) => toasts.push(problem(e, 'Could not leave the team.'), 'error'),
  })

  const remove = useMutation({
    mutationFn: (id: string) => teamsApi.remove(id),
    onSuccess: () => {
      invalidateTeams()
      toasts.push('Team deleted.')
    },
    onError: (e) => toasts.push(problem(e, 'Could not delete the team.'), 'error'),
  })

  return { create, addMember, leave, remove }
}
