<template>
  <div class="bg-white rounded-xl border border-gray-200 p-4">
    <p class="text-xs font-medium text-gray-400 uppercase tracking-wide mb-3">Weight Goal</p>

    <div v-if="goal">
      <div class="flex justify-between items-end mb-1">
        <span class="text-sm text-gray-500">
          {{ goal.startingWeightKg != null ? formatWeight(goal.startingWeightKg, unit) : '—' }}
        </span>
        <span class="text-sm font-semibold text-primary-600">
          {{ progressPercent != null ? `${progressPercent}%` : '' }}
        </span>
        <span class="text-sm text-gray-500">{{ formatWeight(goal.targetWeightKg, unit) }}</span>
      </div>
      <div class="h-2.5 bg-gray-100 rounded-full overflow-hidden">
        <div class="h-full bg-primary-500 rounded-full transition-all duration-500"
          :style="{ width: `${progressPercent ?? 0}%` }" />
      </div>
      <div class="flex justify-between text-xs text-gray-400 mt-1.5">
        <span v-if="kgToGoal != null">{{ formatWeight(kgToGoal, unit) }} to go</span>
        <span v-if="projectedDate">On track for {{ new Date(projectedDate).toLocaleDateString('en-GB') }}</span>
        <span v-else-if="kgToGoal === 0" class="text-emerald-500 font-medium">Goal reached!</span>
      </div>
    </div>

    <p v-else class="text-sm text-gray-400">No active goal. Set one in the Goal tab.</p>
  </div>
</template>

<script setup lang="ts">
import type { Goal, WeightUnit } from '../../types'
import { formatWeight } from '../../utils/units'

defineProps<{
  goal: Goal | null
  progressPercent: number | null
  kgToGoal: number | null
  projectedDate: string | null
  unit: WeightUnit
}>()
</script>
