<template>
  <header class="bar">
    <div class="inner">
      <RouterLink to="/" class="brand">WeightTracker</RouterLink>
      <nav aria-label="Main">
        <RouterLink to="/" class="link" exact-active-class="active">Dashboard</RouterLink>
        <RouterLink to="/history" class="link" active-class="active">History</RouterLink>
        <RouterLink to="/goals" class="link" active-class="active">Goals</RouterLink>
        <RouterLink to="/settings" class="link" active-class="active">Settings</RouterLink>
        <button type="button" class="log" @click="sheet.open(app.today)">Log today</button>
      </nav>
    </div>
  </header>
</template>

<script setup lang="ts">
import { useAppStore } from '../../stores/app'
import { useLogSheetStore } from '../../stores/logSheet'

const app = useAppStore()
const sheet = useLogSheetStore()
</script>

<style scoped>
.bar { background: #fff; border-bottom: 1px solid #e5e7eb; }
.inner { max-width: 960px; margin: 0 auto; padding: 10px 16px; display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px 20px; }
.brand { font-size: 18px; font-weight: 600; color: #1f2937; text-decoration: none; }
nav { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 18px; }
.link { color: #4b5563; text-decoration: none; padding: 12px 0; }
.link:hover { color: #111827; }
.link.active { color: #0369a1; font-weight: 600; }
.link:focus-visible, .log:focus-visible, .brand:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; border-radius: 4px; }
.log { background: #0369a1; color: #fff; font-weight: 600; border: 0; padding: 12px 18px; border-radius: 8px; cursor: pointer; min-height: 44px; }
.log:hover { background: #075985; }
</style>
