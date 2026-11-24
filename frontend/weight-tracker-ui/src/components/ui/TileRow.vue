<template>
  <div class="row">
    <span class="label"><slot name="label">{{ label }}</slot></span>
    <span class="val" :style="color ? { color } : undefined"><slot>{{ value }}</slot></span>
  </div>
</template>

<script setup lang="ts">
defineProps<{ label?: string; value?: string | number | null; color?: string }>()
</script>

<style scoped>
.row { display: flex; justify-content: space-between; gap: 12px; align-items: baseline; font-size: 14px; line-height: 1.3; min-height: 22px; }
.label { color: #4b5563; white-space: nowrap; }
.val { font-weight: 600; text-align: right; white-space: nowrap; }
</style>
