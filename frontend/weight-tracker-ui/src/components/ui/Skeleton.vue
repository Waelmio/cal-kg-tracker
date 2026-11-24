<template>
  <span class="skel" :style="{ width, height, borderRadius: radius }" aria-hidden="true" />
</template>

<script setup lang="ts">
withDefaults(defineProps<{ width?: string; height?: string; radius?: string }>(), { width: '100%', height: '16px', radius: '8px' })
</script>

<style scoped>
.skel { display: block; flex: none; background: linear-gradient(90deg, #f3f4f6 25%, #e5e7eb 37%, #f3f4f6 63%); background-size: 400% 100%; animation: appear 0.15s 0.12s both, shimmer 1.4s ease infinite; }
/* Most loads take a few milliseconds: only show the placeholders when one is slower than that. */
@keyframes appear { from { opacity: 0; } }
@keyframes shimmer { 0% { background-position: 100% 50%; } 100% { background-position: 0 50%; } }
@media (prefers-reduced-motion: reduce) { .skel { animation: none; } }
</style>
