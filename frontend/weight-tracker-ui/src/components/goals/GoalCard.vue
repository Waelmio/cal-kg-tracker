<template>
  <div class="bg-primary-50 border border-primary-100 rounded-xl p-5">
    <div class="flex justify-between items-start">
      <div>
        <p class="text-xs text-primary-600 font-medium uppercase tracking-wide">Active Goal</p>
        <p class="text-2xl font-bold text-gray-800 mt-1">{{ formatWeight(goal.targetWeightKg, unit) }}</p>
        <p class="text-sm text-gray-500 mt-1">by {{ new Date(goal.targetDate).toLocaleDateString('en-GB') }}</p>
      </div>
      <div class="text-right">
        <p class="text-xs text-gray-400">Started {{ new Date(goal.startDate).toLocaleDateString('en-GB') }}</p>
        <p v-if="goal.startingWeightKg" class="text-xs text-gray-400">
          from {{ formatWeight(goal.startingWeightKg, unit) }}
        </p>
      </div>
    </div>
    <p v-if="goal.notes" class="text-sm text-gray-500 mt-3 italic">{{ goal.notes }}</p>
    <button @click="$emit('delete', goal.id)"
      class="mt-3 text-xs text-red-400 hover:text-red-600 hover:underline">
      Remove goal
    </button>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { Goal } from '../../types'
import { useSettingsStore } from '../../stores/settings'
import { formatWeight } from '../../utils/units'

defineProps<{ goal: Goal }>()
defineEmits<{ delete: [number] }>()

const settingsStore = useSettingsStore()
const unit = computed(() => settingsStore.settings.preferredUnit)
</script>
