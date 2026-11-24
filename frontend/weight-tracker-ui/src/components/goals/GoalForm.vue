<template>
  <form class="form" @submit.prevent="submit">
    <h3>{{ goal ? 'Edit goal' : 'New weight goal' }}</h3>
    <div class="grid">
      <div class="f">
        <label for="gt">Target weight ({{ unit }})</label>
        <input id="gt" v-model="target" class="input" inputmode="decimal" required />
      </div>
      <div class="f">
        <label for="gd">Target date</label>
        <input id="gd" v-model="targetDate" class="input" type="date" :min="addDays(startDate, 1)" required />
      </div>
      <div class="f">
        <label for="gs">Start date</label>
        <input id="gs" v-model="startDate" class="input" type="date" :max="app.today" />
      </div>
      <div class="f">
        <label for="gw">Starting weight ({{ unit }})</label>
        <input id="gw" v-model="startWeight" class="input" inputmode="decimal" :placeholder="goal ? '' : 'From your weigh-ins'" />
        <p class="hint">{{ startWeight.trim() === '' ? 'From your weigh-ins on the start date' : 'Typed by you' }}</p>
      </div>
    </div>
    <div class="f">
      <label for="gn">Note <span class="opt">(optional)</span></label>
      <textarea id="gn" v-model="notes" rows="2" maxlength="500" class="input" />
    </div>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <div class="row">
      <button type="submit" class="btn-primary" :disabled="saving">{{ saving ? 'Saving…' : goal ? 'Save goal' : 'Set goal' }}</button>
      <button type="button" class="btn-secondary" @click="$emit('cancel')">Cancel</button>
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
.form { display: flex; flex-direction: column; gap: 12px; border-top: 1px solid #e5e7eb; padding-top: 14px; }
h3 { margin: 0; font-size: 15px; }
.grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; }
.f { display: flex; flex-direction: column; gap: 4px; }
label { font-weight: 600; }
.opt { font-weight: 400; color: #6b7280; }
.input { min-height: 44px; }
.hint { margin: 0; color: #4b5563; font-size: 13px; }
.row { display: flex; gap: 8px; flex-wrap: wrap; }
.row button { min-height: 44px; }
.error { margin: 0; color: #a50036; background: #ffe4e6; padding: 10px 12px; border-radius: 8px; }
</style>
