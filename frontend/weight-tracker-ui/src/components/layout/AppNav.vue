<template>
  <nav class="bg-white border-b border-gray-200 sticky top-0 z-40">
    <div class="max-w-3xl mx-auto px-4 flex items-center justify-between h-14">
      <RouterLink to="/" class="font-semibold text-gray-800 text-lg tracking-tight">
        WeightTracker
      </RouterLink>
      <div class="flex items-center gap-5 text-sm font-medium">
        <RouterLink to="/" class="nav-link" active-class="nav-link-active" exact>Dashboard</RouterLink>
        <RouterLink to="/log" class="nav-link" active-class="nav-link-active">Log</RouterLink>
<RouterLink to="/goal" class="nav-link" active-class="nav-link-active">Goal</RouterLink>
        <button @click="$emit('open-settings')" class="nav-link">⚙</button>
      </div>
    </div>
  </nav>
</template>

<script setup lang="ts">
defineEmits<{ 'open-settings': [] }>()
</script>

<style scoped>
@reference "../../style.css";
.nav-link { @apply text-gray-400 hover:text-gray-900 transition-colors cursor-pointer; }
.nav-link-active { @apply text-primary-600 font-semibold; }
</style>
