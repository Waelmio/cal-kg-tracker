<template>
  <section class="tile" :aria-labelledby="id">
    <h2 :id="id" class="tile-title"><slot name="title">{{ title }}</slot></h2>
    <template v-if="loading">
      <div class="skeleton" style="height: 34px; width: 60%" />
      <div class="skeleton" style="height: 14px; width: 80%" />
    </template>
    <template v-else>
      <div v-if="$slots.value || value != null" class="tile-value" :style="color ? { color } : undefined">
        <slot name="value">{{ value }}</slot>
        <span v-if="unit" class="tile-unit">{{ unit }}</span>
      </div>
      <p v-if="sub" class="tile-sub">{{ sub }}</p>
      <slot />
    </template>
  </section>
</template>

<script setup lang="ts">
let counter = 0
defineProps<{ title?: string; value?: string | number | null; unit?: string; sub?: string; color?: string; loading?: boolean }>()
const id = `tile-${++counter}`
</script>

<style scoped>
.tile { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 18px; display: flex; flex-direction: column; gap: 8px; min-width: 0; }
.tile-title { margin: 0; font-size: 13px; font-weight: 600; color: #4b5563; }
.tile-value { font-size: 30px; font-weight: 700; line-height: 1.1; }
.tile-unit { font-size: 14px; font-weight: 400; color: #4b5563; margin-left: 6px; }
.tile-sub { margin: 0; color: #4b5563; }
.skeleton { background: #f3f4f6; border-radius: 6px; }
</style>
