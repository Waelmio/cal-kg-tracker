<template>
  <div class="grid">
    <!-- Today -->
    <Tile title="Today">
      <template v-if="!s.budget">
        <p class="tile-sub">No calorie target</p>
        <p class="muted">Set one on the Goals page to see a daily budget.</p>
      </template>
      <template v-else>
        <div class="big" :style="{ color: over ? OVER : undefined }">
          {{ kcal(Math.abs(s.budget.todayLeft ?? s.budget.todayBudget)) }}
          <span class="unit">{{ over ? 'kcal over' : 'kcal left' }}</span>
        </div>
        <div class="bar" role="img" :aria-label="`${pct}% of today's budget eaten`"><div :style="{ width: pct + '%', background: over ? OVER_BG : UNDER_BG }" /></div>
        <TileRow label="Eaten" :value="s.budget.eatenToday == null ? '–' : kcal(s.budget.eatenToday)" />
        <TileRow>
          <template #label><InfoTip label="Budget" text="What you can eat today to stay on target on average." /></template>
          {{ kcal(s.budget.todayBudget) }}
        </TileRow>
      </template>
    </Tile>

    <!-- Budget this week -->
    <Tile title="Budget this week">
      <template v-if="!s.budget">
        <p class="tile-sub">No calorie target</p>
      </template>
      <template v-else>
        <div class="big">{{ kcal(s.budget.todayBudget) }} <span class="unit">kcal a day</span></div>
        <p class="tile-sub">Keeps your {{ s.averagingWeeks }}-week average at {{ kcal(s.calorieTarget) }}</p>
        <TileRow>
          <template #label><InfoTip label="Average so far" text="Logged days this week. Today isn't counted yet." /></template>
          <span :style="{ color: avgColor }">{{ kcal(s.budget.averageSoFar) }}</span>
        </TileRow>
        <TileRow>
          <template #label><InfoTip label="Expected average" text="This week's average if you eat your budget every day." /></template>
          {{ kcal(s.budget.expectedAverage) }}
        </TileRow>
      </template>
    </Tile>

    <!-- Weight -->
    <Tile title="Weight">
      <template v-if="s.weight.kg == null">
        <p class="tile-sub">No weigh-ins yet</p>
        <p class="muted">Log your weight to see your average and pace.</p>
      </template>
      <template v-else>
        <div class="big">{{ weight(s.weight.kg, unit) }} <span class="unit">{{ unit }}, {{ s.weight.isAverage ? '7-day average' : 'last weigh-in' + (s.weight.date ? ', ' + shortDate(s.weight.date) : '') }}</span></div>
        <p class="tile-sub">{{ s.weight.paceKgPerWeek == null ? 'Not enough weigh-ins' : `From ${s.weight.paceWeighIns} weigh-ins over 28 days` }}</p>
        <TileRow>
          <template #label><InfoTip label="Pace" text="Trend of your weigh-ins over the last 28 days." /></template>
          <span v-if="s.weight.paceKgPerWeek != null" :style="{ color: paceColor }">{{ weightChange(s.weight.paceKgPerWeek, unit, 2) }} {{ unit }} a week</span>
          <span v-else>–</span>
        </TileRow>
        <TileRow v-if="s.weight.bmi != null" label="BMI" :value="s.weight.bmi" />
      </template>
    </Tile>

    <!-- Streak -->
    <Tile>
      <template #title><InfoTip label="On target streak" text="The longest recent period that averages on target." /></template>
      <template v-if="!s.budget && s.streak.days === 0">
        <p class="tile-sub">Not enough logged days</p>
      </template>
      <template v-else>
        <div class="big" :style="{ color: s.streak.days > 0 ? UNDER : undefined }">{{ s.streak.days }} <span class="unit">{{ s.streak.days === 1 ? 'day' : 'days' }} on target on average</span></div>
        <p v-if="s.streak.nextGap != null" class="tile-sub">−{{ s.streak.nextGap }} kcal for a {{ s.streak.days + 1 }}-day streak</p>
        <div class="rows">
          <div v-for="r in s.streak.rows" :key="r.days" class="srow">
            <span class="muted">Last {{ r.days }} days</span>
            <strong class="r">{{ kcal(r.avgCalories) }}</strong>
            <strong class="r" :style="{ color: r.avgGap == null ? undefined : r.avgGap > 0 ? OVER : UNDER }">{{ r.avgGap == null ? '–' : signed(r.avgGap) }}</strong>
          </div>
        </div>
      </template>
    </Tile>

    <!-- Weekly deficit -->
    <Tile title="Weekly deficit">
      <template v-if="s.deficits.weekly.loggedDays === 0">
        <p class="tile-sub">No days logged this week yet</p>
        <TileRow v-if="s.deficits.weekly.expectedKcal != null">
          <template #label><InfoTip label="Expected by end of this week" text="If you eat your budget every day." /></template>
          {{ kcal(s.deficits.weekly.expectedKcal) }}
        </TileRow>
      </template>
      <template v-else>
        <div class="big" :style="{ color: s.deficits.weekly.kcal >= 0 ? UNDER : OVER }">{{ kcal(Math.abs(s.deficits.weekly.kcal)) }} <span class="unit"><InfoTip :label="s.deficits.weekly.kcal >= 0 ? 'kcal below TDEE' : 'kcal above TDEE'" text="What you burned minus what you ate." /></span></div>
        <p class="tile-sub">{{ s.deficits.weekly.loggedDays }} {{ s.deficits.weekly.loggedDays === 1 ? 'day' : 'days' }} logged</p>
        <TileRow>
          <template #label><InfoTip label="Worth about" text="7700 kcal is about 1 kg." /></template>
          {{ Math.abs(s.deficits.weekly.kg).toFixed(2) }} kg {{ s.deficits.weekly.kg >= 0 ? 'lost' : 'gained' }}
        </TileRow>
        <TileRow v-if="s.deficits.weekly.expectedKcal != null">
          <template #label><InfoTip label="Expected by end of this week" text="If you eat your budget every day." /></template>
          {{ kcal(s.deficits.weekly.expectedKcal) }}
        </TileRow>
        <TileRow>
          <template #label><InfoTip label="Against target" text="Compared with your calorie target." /></template>
          <span :style="{ color: s.week.gap > 0 ? OVER : UNDER }">{{ Math.abs(s.week.gap) }} {{ s.week.gap > 0 ? 'over' : 'under' }}</span>
        </TileRow>
      </template>
    </Tile>

    <!-- Overall deficit -->
    <Tile title="Overall deficit">
      <template v-if="o.loggedDays === 0">
        <p class="tile-sub">No logged days {{ o.sinceGoalStart ? 'since the goal started' : 'in the last four full weeks' }}</p>
      </template>
      <template v-else>
        <div class="big" :style="{ color: o.kcal >= 0 ? UNDER : OVER }">{{ kcal(Math.abs(o.kcal)) }} <span class="unit">{{ o.kcal >= 0 ? 'kcal below TDEE' : 'kcal above TDEE' }}</span></div>
        <p class="tile-sub">{{ o.sinceGoalStart ? `${o.loggedDays} / ${o.totalDays} days logged since the ${fullDate(o.since)}` : `${o.loggedDays} / ${o.totalDays} days logged in the last four full weeks` }}</p>
        <TileRow label="Worth about" :value="`${Math.abs(o.kg).toFixed(2)} kg ${o.kg >= 0 ? 'lost' : 'gained'}`" />
        <TileRow label="Against target">
          <span :style="{ color: o.gapKcal > 0 ? OVER : UNDER }">{{ Math.abs(o.gapKcal) }} {{ o.gapKcal > 0 ? 'over' : 'under' }}</span>
        </TileRow>
      </template>
    </Tile>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import Tile from '../ui/Tile.vue'
import TileRow from '../ui/TileRow.vue'
import InfoTip from '../ui/InfoTip.vue'
import { fullDate, kcal, shortDate, signed, weight, weightChange } from '../../utils/format'
import type { DashboardStats, WeightUnit } from '../../types'

const props = defineProps<{ stats: DashboardStats; unit: WeightUnit }>()
const s = computed(() => props.stats)
const o = computed(() => props.stats.deficits.overall)

const UNDER = '#007a55'
const OVER = '#c70036'
const UNDER_BG = '#00bc7d'
const OVER_BG = '#ff2056'

const over = computed(() => (s.value.budget?.todayLeft ?? 0) < 0)
const pct = computed(() => {
  const b = s.value.budget
  if (!b || b.eatenToday == null || b.todayBudget <= 0) return 0
  return Math.min(100, Math.round((b.eatenToday / b.todayBudget) * 100))
})
const avgColor = computed(() => {
  const b = s.value.budget
  if (!b || b.averageSoFar == null || b.avgTargetSoFar == null) return undefined
  return b.averageSoFar <= b.avgTargetSoFar ? UNDER : OVER
})
const paceColor = computed(() => {
  const g = s.value.goal
  const p = s.value.weight.paceKgPerWeek
  if (!g || p == null || g.currentKg == null) return undefined
  const losing = g.currentKg > g.targetWeightKg
  return (losing ? p < 0 : p > 0) ? UNDER : OVER
})
</script>

<style scoped>
.grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 290px), 1fr)); gap: 16px; }
.big { font-size: 30px; font-weight: 700; line-height: 1.1; }
.unit { font-size: 14px; font-weight: 400; color: #4b5563; margin-left: 6px; }
.tile-sub { margin: 0; color: #4b5563; }
.muted { color: #4b5563; margin: 0; }
.bar { height: 8px; background: #e5e7eb; border-radius: 4px; overflow: hidden; margin: 2px 0; }
.bar > div { height: 8px; }
.rows { display: flex; flex-direction: column; gap: 2px; }
.srow { display: grid; grid-template-columns: minmax(0, 1fr) 56px 64px; gap: 8px; }
.r { text-align: right; }
</style>
