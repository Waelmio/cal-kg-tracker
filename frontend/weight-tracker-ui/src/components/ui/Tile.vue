<template>
  <!-- A tile spans five rows of the parent grid (title and value, subtitle, three detail rows) as a subgrid,
       so the detail rows of neighbouring tiles line up. Outside a grid it is a plain stack. -->
  <section class="tile" :aria-labelledby="id">
    <header class="head">
      <h2 :id="id" class="tile-title"><slot name="title">{{ title }}</slot></h2>
      <template v-if="loading">
        <div class="skeleton" style="height: 34px; width: 60%" />
      </template>
      <div v-else-if="$slots.value || value != null" class="tile-value" :style="color ? { color } : undefined">
        <slot name="value">{{ value }}</slot>
        <span v-if="unit" class="tile-unit">{{ unit }}</span>
      </div>
    </header>
    <div class="sub">
      <div v-if="loading" class="skeleton" style="height: 14px; width: 80%" />
      <slot v-else name="sub"><p v-if="sub" class="tile-sub">{{ sub }}</p></slot>
    </div>
    <slot v-if="!loading" />
  </section>
</template>

<script setup lang="ts">
let counter = 0
defineProps<{ title?: string; value?: string | number | null; unit?: string; sub?: string; color?: string; loading?: boolean }>()
const id = `tile-${++counter}`
</script>

<style scoped>
.tile {
  background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 14px 16px; min-width: 0; margin-bottom: 16px;
  display: grid; grid-row: span 5; grid-template-rows: subgrid; row-gap: 0; align-content: start;
}
.head { display: flex; flex-direction: column; gap: 4px; margin-bottom: 2px; }
.tile-title { margin: 0; font-size: 13px; font-weight: 600; color: #4b5563; }
.tile-value { font-size: 30px; font-weight: 700; line-height: 1.1; }
.tile-unit { font-size: 14px; font-weight: 400; color: #4b5563; margin-left: 6px; }
.sub { min-width: 0; margin-bottom: 10px; }
.tile-sub { margin: 0; color: #4b5563; font-size: 14px; line-height: 1.3; }
.skeleton { background: #f3f4f6; border-radius: 6px; }
</style>
