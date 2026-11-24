<template>
  <Teleport to="body">
    <div class="backdrop" @mousedown.self="$emit('close')">
      <div ref="dialog" class="dialog" :class="{ sheet }" role="dialog" aria-modal="true" :aria-labelledby="titleId" tabindex="-1" @keydown="onKey">
        <slot name="header" :title-id="titleId" :close="() => emit('close')">
          <header class="head">
            <h1 :id="titleId" class="title">{{ title }}</h1>
            <button type="button" class="close" aria-label="Close" @click="$emit('close')">×</button>
          </header>
        </slot>
        <div class="body"><slot /></div>
        <footer v-if="$slots.footer" class="foot"><slot name="footer" /></footer>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref } from 'vue'

let counter = 0
const props = withDefaults(defineProps<{ title: string; sheet?: boolean; maxWidth?: string }>(), { sheet: false, maxWidth: '520px' })
const emit = defineEmits<{ close: [] }>()

const titleId = `modal-title-${++counter}`
const dialog = ref<HTMLElement | null>(null)
let previouslyFocused: Element | null = null

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') {
    e.stopPropagation()
    emit('close')
    return
  }
  if (e.key !== 'Tab' || !dialog.value) return
  const items = Array.from(dialog.value.querySelectorAll<HTMLElement>(FOCUSABLE))
  if (items.length === 0) return
  const first = items[0]
  const last = items[items.length - 1]
  if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus() }
  else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus() }
}

onMounted(async () => {
  previouslyFocused = document.activeElement
  document.body.style.overflow = 'hidden'
  await nextTick()
  const first = dialog.value?.querySelector<HTMLElement>('[autofocus], input, textarea') ?? dialog.value
  first?.focus()
})
onBeforeUnmount(() => {
  document.body.style.overflow = ''
  if (previouslyFocused instanceof HTMLElement) previouslyFocused.focus()
})

void props
</script>

<style scoped>
.backdrop { position: fixed; inset: 0; z-index: 50; background: rgb(0 0 0 / 0.4); display: flex; align-items: center; justify-content: center; padding: 16px; }
.dialog { background: #fff; border-radius: 14px; width: 100%; max-width: v-bind(maxWidth); max-height: calc(100dvh - 32px); display: flex; flex-direction: column; box-shadow: 0 20px 50px rgb(0 0 0 / 0.25); outline: none; }
.head { display: flex; align-items: center; justify-content: space-between; padding: 24px 28px 8px; }
.title { margin: 0; font-size: 20px; font-weight: 700; }
.close { all: unset; cursor: pointer; width: 44px; height: 44px; display: grid; place-items: center; border-radius: 50%; background: #f3f4f6; font-size: 20px; color: #374151; }
.close:hover { background: #e5e7eb; }
.close:focus-visible { outline: 2px solid #0284c7; }
.body { padding: 12px 28px 20px; overflow-y: auto; }
.foot { padding: 18px 28px 24px; border-top: 1px solid #e5e7eb; }
/* Below 640 px a sheet fills the screen. */
@media (max-width: 639px) {
  .dialog.sheet { max-width: none; height: 100dvh; max-height: none; border-radius: 0; }
  .head { padding: 16px 20px 4px; }
  .body { padding: 8px 20px 16px; }
  .foot { padding: 12px 20px 20px; }
  .backdrop:has(.sheet) { padding: 0; }
}
</style>
