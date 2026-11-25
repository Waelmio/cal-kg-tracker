<template>
  <span ref="root" class="info" @mouseenter="hover = true" @mouseleave="hover = false">
    <button
      type="button"
      class="label"
      :aria-describedby="open ? id : undefined"
      @pointerdown="beforeTap"
      @focus="pinned = true"
      @blur="pinned = false"
      @click.stop="afterTap"
      @keydown.esc="onEsc"
    >
      <slot>{{ label }}</slot>
    </button>
    <span v-if="open" :id="id" ref="tip" role="tooltip" class="tip" :style="{ left: `${shift}px` }">{{ text }}</span>
  </span>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'

defineProps<{ label?: string; text: string }>()

const id = useId()
const root = ref<HTMLElement | null>(null)
const tip = ref<HTMLElement | null>(null)

// Open while hovered, and while focused or tapped. A tap focuses the button first, so the click must not
// simply toggle: it would close what the focus just opened. It only closes what was open before the tap.
const hover = ref(false)
const pinned = ref(false)
let wasOpen = false
function beforeTap() { wasOpen = pinned.value }
function afterTap() { pinned.value = !wasOpen; wasOpen = false }
const open = computed(() => hover.value || pinned.value)

function onEsc(e: KeyboardEvent) {
  if (!open.value) return
  e.stopPropagation()      // closes the tooltip, not the dialog around it
  close()
}
function close() {
  hover.value = false
  pinned.value = false
}

// Keep the text inside the window: labels near the right edge would push it out.
const shift = ref(0)
watch(open, async (isOpen) => {
  shift.value = 0
  if (!isOpen) return
  await nextTick()
  const box = tip.value?.getBoundingClientRect()
  if (box) shift.value = Math.min(0, document.documentElement.clientWidth - 8 - box.right)
})

function onDocumentPointer(e: Event) {
  if (open.value && root.value && !root.value.contains(e.target as Node)) close()
}
function onDocumentKey(e: KeyboardEvent) {
  if (e.key === 'Escape' && open.value) close()
}

onMounted(() => {
  document.addEventListener('pointerdown', onDocumentPointer)
  document.addEventListener('keydown', onDocumentKey)
})
onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', onDocumentPointer)
  document.removeEventListener('keydown', onDocumentKey)
})
</script>

<style scoped>
.info { position: relative; display: inline-block; }
.label {
  all: unset; cursor: help; color: inherit;
  /* A 24 px target that takes no more room than the text around it. */
  min-height: 24px; margin-block: -4px; display: inline-flex; align-items: center;
}
.label:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; border-radius: 2px; }
.tip {
  position: absolute; z-index: 30; top: calc(100% + 4px); width: max-content; max-width: min(260px, 70vw);
  background: #1f2937; color: #fff; font-size: 13px; font-weight: 400; line-height: 1.35; text-align: left;
  padding: 8px 10px; border-radius: 8px; box-shadow: 0 4px 14px rgb(0 0 0 / 0.18);
}
</style>
