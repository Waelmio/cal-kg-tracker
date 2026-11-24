<template>
  <div role="alert" class="err">
    <span>{{ message }}</span>
    <button v-if="retry" type="button" class="btn-secondary" @click="retry">Try again</button>
  </div>
</template>

<script setup lang="ts">
defineProps<{ message: string; retry?: () => void }>()
</script>

<style scoped>
.err { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px 16px; background: #ffe4e6; color: #a50036; border-radius: 10px; padding: 12px 14px; }
</style>
