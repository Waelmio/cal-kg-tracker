import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: () => import('../views/DashboardView.vue') },
    { path: '/log', component: () => import('../views/LogView.vue') },
{ path: '/goal', component: () => import('../views/GoalView.vue') },
  ],
})

export default router
