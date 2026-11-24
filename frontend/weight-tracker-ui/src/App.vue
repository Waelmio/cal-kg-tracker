<template>
  <div class="min-h-screen bg-gray-50">
    <AppNav />
    <RouterView />
    <LogSheet v-if="sheet.isOpen" :key="sheet.date ?? ''" />
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted } from 'vue'
import AppNav from './components/layout/AppNav.vue'
import LogSheet from './components/daylog/LogSheet.vue'
import { useAppStore } from './stores/app'
import { useLogSheetStore } from './stores/logSheet'

const app = useAppStore()
const sheet = useLogSheetStore()

// The tab may stay open overnight: when it becomes visible again, re-read the local date and refetch.
function onVisible() {
  if (document.visibilityState === 'visible') app.refresh()
}

onMounted(async () => {
  document.addEventListener('visibilitychange', onVisible)
  await app.loadSettings().catch(() => {})
  app.refresh()
})
onBeforeUnmount(() => document.removeEventListener('visibilitychange', onVisible))
</script>

