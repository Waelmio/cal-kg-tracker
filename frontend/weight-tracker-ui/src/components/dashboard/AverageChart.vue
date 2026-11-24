<template>
  <section class="card" aria-labelledby="avg-title">
    <div class="top">
      <div class="title">
        <h2 id="avg-title">Average calories</h2>
        <div class="seg" role="group" aria-label="Period">
          <button v-for="m in modes" :key="m.value" type="button" :aria-pressed="mode === m.value" @click="mode = m.value">{{ m.label }}</button>
        </div>
      </div>
      <ul class="legend">
        <li><i :style="{ background: STATUS_COLORS.under.solid }" />On or under target</li>
        <li><i :style="{ background: STATUS_COLORS.balanced.solid }" /><InfoTip :label="`Over, ${app.settings.averagingWeeks}-week average ok`" text="Over target, but fine averaged with the weeks around it." /></li>
        <li><i :style="{ background: STATUS_COLORS.over.solid }" />Over target</li>
        <li><i class="light" /><InfoTip label="Lighter" text="Outside your current goal." /></li>
      </ul>
    </div>

    <ErrorNote v-if="error" :message="error" :retry="reload" />
    <p v-else-if="!loading && bars.length === 0" class="empty">Nothing logged yet.</p>

    <div v-else class="scroll">
      <div class="bars" :style="{ minWidth: bars.length * 54 + 'px', gridTemplateColumns: `repeat(${bars.length}, minmax(0, 1fr))` }" role="list">
        <div v-for="b in bars" :key="b.from" class="col" role="listitem" :title="b.tip" :aria-label="b.tip">
          <div class="half up"><span class="lbl">{{ b.upLabel }}</span><div class="bar top" :style="{ height: b.up + 'px', background: b.color }" /></div>
          <div class="half down"><div class="bar bottom" :style="{ height: b.down + 'px', background: b.color }" /><span class="lbl">{{ b.downLabel }}</span></div>
          <div class="avg">{{ b.avg }}</div>
          <div class="name">{{ b.name }}</div>
          <div class="wt">{{ b.weight }}</div>
        </div>
      </div>
    </div>
    <p class="note">Bars show the gap to the target. Today and planned days are left out.</p>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import ErrorNote from '../ui/ErrorNote.vue'
import InfoTip from '../ui/InfoTip.vue'
import { useAppStore } from '../../stores/app'
import { useAsyncData } from '../../composables/useAsyncData'
import * as statsApi from '../../api/stats'
import { isoWeek } from '../../utils/dates'
import { kcal, monthTitle, STATUS_COLORS, weight, weightChange } from '../../utils/format'

const app = useAppStore()
const modes = [{ value: 'week', label: 'Weeks' }, { value: 'month', label: 'Months' }] as const
const mode = ref<'week' | 'month'>('week')

const { data, loading, error, reload } = useAsyncData(() => statsApi.getPeriods(mode.value, mode.value === 'week' ? 14 : 6), [mode])

const SCALE = 0.45   // px per kcal of gap

const bars = computed(() =>
  (data.value ?? [])
    .filter((p) => p.loggedDays > 0 && p.avgCalories != null && p.avgTarget != null)
    .map((p) => {
      const gap = p.avgCalories! - p.avgTarget!
      const h = Math.max(2, Math.round(Math.abs(gap) * (mode.value === 'week' ? SCALE : 0.34)))
      const status = p.status ?? 'under'
      const c = STATUS_COLORS[status]
      const sofar = p.to >= app.today && p.from <= app.today
      const name = mode.value === 'week' ? `W${isoWeek(p.from)}${sofar ? ' so far' : ''}` : monthTitle(p.from.slice(0, 7)).split(' ')[0].slice(0, 3) + (sofar ? ' so far' : '')
      return {
        from: p.from, color: p.inGoal ? c.solid : c.light,
        up: gap > 0 ? h : 0, down: gap > 0 ? 0 : h,
        upLabel: gap > 0 ? `+${gap}` : '', downLabel: gap > 0 ? '' : `−${Math.abs(gap)}`,
        avg: kcal(p.avgCalories), name,
        weight: p.avgWeightKg == null ? '' : `${weight(p.avgWeightKg, app.unit, true)}${p.weightChangeKg != null ? ' ' + weightChange(p.weightChangeKg, app.unit) : ''}`,
        tip: `${name}: average ${p.avgCalories}, target ${p.avgTarget}`,
      }
    }),
)
</script>

<style scoped>
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 18px; display: flex; flex-direction: column; gap: 14px; }
.top { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: baseline; gap: 4px 16px; }
.title { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 16px; }
h2 { margin: 0; font-size: 16px; font-weight: 600; }
.seg { display: flex; gap: 4px; }
.seg button { min-height: 44px; padding: 0 14px; border: 1px solid #d1d5db; background: #fff; border-radius: 8px; cursor: pointer; }
.seg button[aria-pressed='true'] { background: #0369a1; color: #fff; border-color: #0369a1; }
.legend { display: flex; flex-wrap: wrap; gap: 4px 16px; font-size: 13px; color: #4b5563; list-style: none; margin: 0; padding: 0; }
.legend li { display: inline-flex; align-items: center; gap: 6px; }
.legend i { width: 10px; height: 10px; border-radius: 2px; display: inline-block; }
.legend i.light { background: linear-gradient(90deg, #a4f4cf 50%, #ffccd3 50%); }
.scroll { overflow-x: auto; }
.bars { display: grid; }
.col { display: flex; flex-direction: column; align-items: center; text-align: center; }
.half { width: 100%; height: 96px; display: flex; flex-direction: column; align-items: center; }
.half.up { justify-content: flex-end; border-bottom: 1px solid #6b7280; }
.bar { width: 24px; }
.bar.top { border-radius: 4px 4px 0 0; }
.bar.bottom { border-radius: 0 0 4px 4px; }
.lbl { font-size: 12px; color: #374151; }
.avg { font-size: 16px; font-weight: 700; }
.name, .wt { font-size: 12px; color: #4b5563; }
.note { margin: 0; font-size: 13px; color: #4b5563; }
.empty { color: #4b5563; margin: 0; }
</style>
