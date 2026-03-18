<template>
  <div class="relative bg-white rounded-xl border border-gray-200 p-4 group">
    <p class="text-xs font-medium text-gray-400 uppercase tracking-wide mb-1">{{ label }}</p>
    <div class="flex items-center justify-between gap-2">
      <p class="text-2xl font-bold leading-none self-center" :class="valueClass">
        <span v-if="value !== null && value !== undefined">{{ value }}</span>
        <span v-else class="text-gray-200">—</span>
        <span v-if="unit && value !== null && value !== undefined" class="text-sm font-normal text-gray-400 ml-1">{{ unit }}</span>
      </p>
      <p v-if="trailingValue != null" class="text-base font-semibold leading-none shrink-0 self-center" :class="trailingClass">{{ trailingValue }}</p>
    </div>
    <p v-if="subHtml" class="text-xs mt-1" :class="subClass ?? 'text-gray-400'" v-html="subHtml" />
    <p v-else-if="sub" class="text-xs mt-1" :class="subClass ?? 'text-gray-400'">{{ sub }}</p>
    <div
      v-if="tooltip && (!onlyWhenEmpty || value === null || value === undefined)"
      class="pointer-events-none absolute bottom-full left-1/2 -translate-x-1/2 mb-2 w-52 rounded-lg bg-gray-800 px-3 py-2 text-xs text-white opacity-0 group-hover:opacity-100 transition-opacity z-10 text-center"
    >
      {{ tooltip }}
      <span class="absolute top-full left-1/2 -translate-x-1/2 border-4 border-transparent border-t-gray-800" />
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  label: string
  value: string | number | null | undefined
  unit?: string
  sub?: string
  subHtml?: string
  valueClass?: string
  subClass?: string
  tooltip?: string
  onlyWhenEmpty?: boolean
  trailingValue?: string | null
  trailingClass?: string
}>()
</script>
