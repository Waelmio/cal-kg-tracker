import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: () => import('../views/DashboardView.vue') },
    { path: '/history', component: () => import('../views/HistoryView.vue') },
    { path: '/goals', component: () => import('../views/GoalsView.vue') },
    { path: '/settings', component: () => import('../views/SettingsView.vue') },
    // Old addresses keep working.
    { path: '/log', redirect: '/history' },
    { path: '/goal', redirect: '/goals' },
  ],
})

export default router
