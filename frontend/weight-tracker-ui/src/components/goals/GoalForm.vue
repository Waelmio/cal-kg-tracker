<template>
  <form class="form" @submit.prevent="submit">
    <div class="f"><label for="gt">Target weight ({{ unit }})</label><input id="gt" v-model="target" inputmode="decimal" required /></div>
    <div class="f"><label for="gd">Target date</label><input id="gd" v-model="targetDate" type="date" :min="addDays(startDate, 1)" required /></div>
    <div class="f"><label for="gs">Start date</label><input id="gs" v-model="startDate" type="date" :max="app.today" /></div>
    <div class="f">
      <label for="gw">Starting weight ({{ unit }})</label>
      <input id="gw" v-model="startWeight" inputmode="decimal" />
      <span class="hint">{{ startWeight.trim() === '' ? 'From your weigh-ins on the start date' : 'Typed by you' }}</span>
    </div>
    <div class="f wide"><label for="gn">Note</label><textarea id="gn" v-model="notes" rows="2" maxlength="500" /></div>
    <p v-if="error" role="alert" class="error wide">{{ error }}</p>
    <div class="wide row">
      <button type="submit" class="primary" :disabled="saving">{{ saving ? 'Saving…' : goal ? 'Save goal' : 'Set goal' }}</button>
      <button type="button" class="cancel" @click="$emit('cancel')">Cancel</button>
    </div>
  </form>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useAppStore } from '../../stores/app'
import * as goalsApi from '../../api/goals'
import { addDays } from '../../utils/dates'
import { displayWeight, toKg } from '../../utils/units'
import type { Goal } from '../../types'

const props = defineProps<{ goal?: Goal | null }>()
const emit = defineEmits<{ saved: []; cancel: [] }>()

const app = useAppStore()
const unit = computed(() => app.unit)
const fmt = (kg: number | null | undefined) => (kg == null ? '' : String(Math.round(displayWeight(kg, unit.value) * 10) / 10))

const target = ref(fmt(props.goal?.targetWeightKg))
const targetDate = ref(props.goal?.targetDate ?? '')
const startDate = ref(props.goal?.startDate ?? app.today)
const startWeight = ref(fmt(props.goal?.startingWeightKg))
const notes = ref(props.goal?.notes ?? '')
const saving = ref(false)
const error = ref('')

const num = (t: string) => Number(t.trim().replace(',', '.'))

async function submit() {
  error.value = ''
  if (!Number.isFinite(num(target.value)) || num(target.value) <= 0) { error.value = 'Enter a target weight.'; return }
  if (!targetDate.value) { error.value = 'Pick a target date.'; return }
  saving.value = true
  try {
    const startingWeightKg = startWeight.value.trim() === '' ? undefined : toKg(num(startWeight.value), unit.value)
    if (props.goal) {
      await goalsApi.update(props.goal.id, {
        targetWeightKg: toKg(num(target.value), unit.value), targetDate: targetDate.value, startDate: startDate.value,
        startingWeightKg: startingWeightKg ?? props.goal.startingWeightKg, notes: notes.value.trim() || null,
      })
    } else {
      await goalsApi.create({
        targetWeightKg: toKg(num(target.value), unit.value), targetDate: targetDate.value, startDate: startDate.value,
        startingWeightKg, notes: notes.value.trim() || undefined,
      })
    }
    app.refresh()
    emit('saved')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.form { background: #fff; border: 1px solid #bae6fd; border-radius: 10px; padding: 16px; display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 190px), 1fr)); gap: 14px; }
.f { display: flex; flex-direction: column; gap: 6px; }
.wide { grid-column: 1 / -1; }
label { font-weight: 600; }
input, textarea { box-sizing: border-box; width: 100%; height: 46px; border: 1px solid #9ca3af; border-radius: 8px; padding: 0 12px; font: inherit; }
textarea { height: auto; padding: 10px 12px; }
.hint { font-size: 13px; color: #4b5563; }
.row { display: flex; gap: 10px; }
.primary { min-height: 44px; padding: 0 18px; border: 0; border-radius: 8px; background: #0369a1; color: #fff; font: inherit; font-weight: 600; }
.cancel { min-height: 44px; padding: 0 18px; border: 1px solid #9ca3af; border-radius: 8px; background: #fff; color: #1f2937; font: inherit; font-weight: 600; }
.error { margin: 0; color: #a50036; background: #ffe4e6; padding: 10px 12px; border-radius: 8px; }
</style>
