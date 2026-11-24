<template>
  <span ref="root" class="info" @mouseenter="open = true" @mouseleave="onLeave">
    <button
      type="button"
      class="label"
      :aria-describedby="open ? id : undefined"
      :aria-expanded="open"
      @focus="open = true"
      @blur="onBlur"
      @click.stop="open = !open"
      @keydown.esc="open = false"
    >
      <slot>{{ label }}</slot>
    </button>
    <span v-if="open" :id="id" role="tooltip" class="tip">{{ text }}</span>
  </span>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'

let counter = 0
defineProps<{ label?: string; text: string }>()

const id = `tip-${++counter}`
const open = ref(false)
const root = ref<HTMLElement | null>(null)
let pinned = false

// Hovering opens it; focusing or tapping opens it too. Leaving closes it unless the button still has focus.
function onLeave() {
  if (document.activeElement !== root.value?.querySelector('button')) open.value = false
}
function onBlur() {
  open.value = false
}

function onDocumentPointer(e: Event) {
  if (open.value && root.value && !root.value.contains(e.target as Node)) open.value = false
}
function onDocumentKey(e: KeyboardEvent) {
  if (e.key === 'Escape') open.value = false
}

onMounted(() => {
  document.addEventListener('pointerdown', onDocumentPointer)
  document.addEventListener('keydown', onDocumentKey)
})
onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', onDocumentPointer)
  document.removeEventListener('keydown', onDocumentKey)
  void pinned
})
</script>

<style scoped>
.info { position: relative; display: inline-block; }
.label {
  all: unset; cursor: help; color: inherit;
  text-decoration: underline dotted #9ca3af; text-underline-offset: 3px;
  min-height: 24px; display: inline-flex; align-items: center;
}
.label:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; border-radius: 2px; }
.tip {
  position: absolute; z-index: 30; left: 0; top: calc(100% + 4px); width: max-content; max-width: min(260px, 70vw);
  background: #1f2937; color: #fff; font-size: 13px; font-weight: 400; line-height: 1.35; text-align: left;
  padding: 8px 10px; border-radius: 8px; box-shadow: 0 4px 14px rgb(0 0 0 / 0.18);
}
</style>
