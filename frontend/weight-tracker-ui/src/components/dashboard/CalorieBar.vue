<template>
  <div class="bg-white rounded-xl border border-gray-200 p-4">
    <p class="text-xs font-medium text-gray-400 uppercase tracking-wide mb-3">Today's Calories</p>

    <div v-if="target">
      <div class="flex justify-between items-end mb-1">
        <span class="text-2xl font-bold" :class="overBudget ? 'text-red-500' : 'text-gray-800'">
          {{ calories ?? 0 }}
        </span>
        <span class="text-sm text-gray-400">/ {{ target }} kcal</span>
      </div>
      <div class="h-2.5 bg-gray-100 rounded-full overflow-hidden">
        <div class="h-full rounded-full transition-all duration-500"
          :class="overBudget ? 'bg-red-400' : 'bg-emerald-500'"
          :style="{ width: `${Math.min(pct, 100)}%` }" />
      </div>
      <p class="text-xs mt-1.5" :class="overBudget ? 'text-red-400' : 'text-gray-400'">
        {{ overBudget ? `${Math.abs(remaining)} kcal over` : `${remaining} kcal remaining` }}
      </p>
    </div>

    <div v-else>
      <p class="text-2xl font-bold text-gray-800">{{ calories ?? '—' }}</p>
      <p class="text-xs text-gray-400 mt-1">Set a calorie target in Settings.</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ calories: number | null; target: number | null }>()

const pct = computed(() =>
  props.target && props.calories != null ? Math.round((props.calories / props.target) * 100) : 0
)
const remaining = computed(() => (props.target ?? 0) - (props.calories ?? 0))
const overBudget = computed(() => remaining.value < 0)
</script>
