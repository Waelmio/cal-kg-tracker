<template>
  <div class="min-h-screen bg-gray-50">
    <AppNav />
    <RouterView />
    <LogSheet v-if="sheet.isOpen" :key="sheet.date ?? ''" />
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, watch } from 'vue'
import AppNav from './components/layout/AppNav.vue'
import LogSheet from './components/daylog/LogSheet.vue'
import { useAppStore } from './stores/app'
import { useLogSheetStore } from './stores/logSheet'
import { useLogWeeksStore } from './stores/logWeeks'

const app = useAppStore()
const sheet = useLogSheetStore()
const logWeeks = useLogWeeksStore()

// The tab may stay open overnight: when it becomes visible again, re-read the local date and refetch.
function onVisible() {
  if (document.visibilityState === 'visible') app.refresh()
}

onMounted(async () => {
  document.addEventListener('visibilitychange', onVisible)
  await app.loadSettings().catch(() => {})
})
// Keep this week ready, so the log sheet opens with its data already there.
watch(() => app.version, () => { logWeeks.load(app.today) }, { immediate: true, flush: 'post' })
onBeforeUnmount(() => document.removeEventListener('visibilitychange', onVisible))
</script>

