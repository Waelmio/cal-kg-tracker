<template>
  <div class="min-h-screen bg-gray-50">
    <AppNav @open-settings="showSettings = true" />
    <RouterView />
    <SettingsModal v-if="showSettings" @close="showSettings = false" />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import AppNav from './components/layout/AppNav.vue'
import SettingsModal from './components/layout/SettingsModal.vue'

const showSettings = ref(false)
</script>
