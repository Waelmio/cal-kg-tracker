<template>
  <span class="chip" :style="{ background: palette.bg, color: palette.fg }"><slot /></span>
</template>

<script setup lang="ts">
import { computed } from 'vue'

export type ChipKind = 'under' | 'balanced' | 'over' | 'neutral' | 'warn' | 'info'
const props = withDefaults(defineProps<{ kind?: ChipKind }>(), { kind: 'neutral' })

const PALETTE: Record<ChipKind, { bg: string; fg: string }> = {
  under: { bg: '#d0fae5', fg: '#006045' },
  balanced: { bg: '#fef3c6', fg: '#973c00' },
  over: { bg: '#ffe4e6', fg: '#a50036' },
  warn: { bg: '#ffedd5', fg: '#9a3412' },
  info: { bg: '#e0f2fe', fg: '#075985' },
  neutral: { bg: '#f3f4f6', fg: '#374151' },
}
const palette = computed(() => PALETTE[props.kind])
</script>

<style scoped>
.chip { display: inline-block; font-weight: 600; font-size: 13px; padding: 4px 10px; border-radius: 999px; white-space: nowrap; }
</style>
