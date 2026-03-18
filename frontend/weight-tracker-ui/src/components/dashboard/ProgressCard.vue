<template>
  <div class="bg-white rounded-xl border border-gray-200 p-4">
    <p class="text-xs font-medium text-gray-400 uppercase tracking-wide mb-3">Weight Goal</p>

    <div v-if="goal">
      <div class="flex justify-between items-end mb-1">
        <span class="text-sm text-gray-500">
          {{ goal.startingWeightKg != null ? formatWeight(goal.startingWeightKg, unit) : '—' }}
        </span>
        <span class="text-sm font-semibold text-primary-600">
          {{ currentWeightKg != null ? `${displayWeight(currentWeightKg, unit).toFixed(2)} ${unit}` : (progressPercent != null ? `${progressPercent}%` : '') }}
        </span>
        <span class="text-sm text-gray-500">{{ formatWeight(goal.targetWeightKg, unit) }}</span>
      </div>
      <div class="h-2.5 bg-gray-100 rounded-full overflow-hidden">
        <div class="h-full bg-primary-500 rounded-full transition-all duration-500"
          :style="{ width: `${progressPercent ?? 0}%` }" />
      </div>
      <div class="flex items-start text-xs mt-1.5">
        <span class="flex-1 text-gray-400">
          <template v-if="kgToGoal != null && kgToGoal > 0">{{ formatWeight(kgToGoal, unit) }} to go</template>
        </span>
        <span v-if="progressPercent != null" class="text-primary-600 font-medium">{{ progressPercent }}%</span>
        <span class="flex-1 text-gray-400 text-right">
          <template v-if="projectedDate">On track for {{ new Date(projectedDate).toLocaleDateString('en-GB') }}</template>
          <template v-else-if="kgToGoal === 0" class="text-emerald-500 font-medium">Goal reached!</template>
        </span>
      </div>
    </div>

    <p v-else class="text-sm text-gray-400">No active goal. Set one in the Goal tab.</p>
  </div>
</template>

<script setup lang="ts">
import type { Goal, WeightUnit } from '../../types'
import { formatWeight, displayWeight } from '../../utils/units'

defineProps<{
  goal: Goal | null
  progressPercent: number | null
  kgToGoal: number | null
  projectedDate: string | null
  unit: WeightUnit
  currentWeightKg: number | null
}>()
</script>
