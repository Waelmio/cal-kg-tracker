<template>
  <Modal title="Log a day" sheet max-width="760px" @close="close">
    <form class="sheet" @submit.prevent="save" @keydown.enter.exact="onEnter">
      <!-- Phone: previous / next day -->
      <div class="nav" role="group" aria-label="Day">
        <button type="button" class="box" aria-label="Previous day" @click="go(-1)">‹</button>
        <strong class="when">{{ whenLabel }}</strong>
        <button type="button" class="box" aria-label="Next day" @click="go(1)">›</button>
      </div>

      <!-- Desktop: the week -->
      <div class="week" role="group" aria-label="Week">
        <button type="button" class="box" aria-label="Previous week" @click="go(-7)">‹</button>
        <button v-for="d in weekDays" :key="d.date" type="button" class="day" :aria-pressed="d.date === date" @click="select(d.date)">
          <span class="dn">{{ d.name }}</span>
          <span class="dk">{{ d.kcal }}</span>
        </button>
        <button type="button" class="box" aria-label="Next week" @click="go(7)">›</button>
      </div>

      <ErrorNote v-if="loadError" :message="loadError" :retry="load" />

      <div class="cols">
        <!-- Calories -->
        <section class="field">
          <div class="field-head">
            <label for="kcal">Calories</label>
            <button type="button" class="remove" :class="kcalUndo ? 'undo' : ''" :disabled="kcalNothing" :aria-label="kcalUndo ? 'Undo removing calories' : 'Remove calories'" @click="toggleKcal">
              {{ kcalUndo ? 'Undo' : 'Remove' }}
            </button>
          </div>
          <div class="stepper">
            <button type="button" aria-label="50 kcal less" @click="addKcal(-50)">−</button>
            <input id="kcal" v-model="kcalText" inputmode="numeric" placeholder="Not logged" autocomplete="off" />
            <button type="button" aria-label="50 kcal more" @click="addKcal(50)">+</button>
          </div>
          <div class="quick">
            <button v-for="n in [100, 250, 500]" :key="n" type="button" @click="addKcal(n)">+{{ n }}</button>
          </div>
          <p class="left" data-test="kcal-hint">{{ kcalHint }}</p>
          <p class="after">{{ kcalAfter }}</p>
        </section>

        <!-- Weight, cheat day (phone) and note -->
        <div class="right">
          <section class="field">
            <div class="field-head">
              <label for="kg">Weight <span class="opt">(optional)</span></label>
              <button type="button" class="remove" :class="kgUndo ? 'undo' : ''" :disabled="kgNothing" :aria-label="kgUndo ? 'Undo removing weight' : 'Remove weight'" @click="toggleKg">
                {{ kgUndo ? 'Undo' : 'Remove' }}
              </button>
            </div>
            <div class="stepper">
              <button type="button" :aria-label="`0.1 ${unit} less`" @click="stepKg(-0.1)">−</button>
              <input id="kg" v-model="kgText" inputmode="decimal" :placeholder="lastWeightHint" autocomplete="off" @click="fillLastWeight" />
              <button type="button" :aria-label="`0.1 ${unit} more`" @click="stepKg(0.1)">+</button>
            </div>
          </section>

          <label class="cheat-row" for="cheat">
            <span><strong>Cheat day</strong><br /><span class="muted">{{ cheatHint }}</span></span>
            <input id="cheat" v-model="cheat" type="checkbox" />
          </label>

          <section class="field note">
            <label for="note">Note <span class="opt">(optional)</span></label>
            <textarea id="note" v-model="note" rows="3" maxlength="500" placeholder="Travel, illness, dinner out" />
          </section>
        </div>
      </div>

      <p v-if="saveError" role="alert" class="error">{{ saveError }}</p>
      <p v-if="deleting" class="muted" data-test="delete-hint">Also deletes the note and cheat day.</p>
    </form>

    <template #footer>
      <div class="foot">
        <label class="cheat-foot" for="cheat2">
          <input id="cheat2" v-model="cheat" type="checkbox" />
          <span><strong>Cheat day</strong> <span class="muted">{{ cheatHintShort }}</span></span>
        </label>
        <div class="save-area">
          <span class="keys">Enter saves, Esc closes</span>
          <button type="button" class="save" :class="{ danger: deleting }" :disabled="saving || loading" @click="save">
            {{ saving ? 'Saving…' : deleting ? 'Delete entry' : 'Save day' }}
          </button>
        </div>
      </div>
    </template>
  </Modal>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import Modal from '../ui/Modal.vue'
import ErrorNote from '../ui/ErrorNote.vue'
import { useAppStore } from '../../stores/app'
import { useLogSheetStore } from '../../stores/logSheet'
import * as logApi from '../../api/dailyLog'
import * as statsApi from '../../api/stats'
import { addDays, mondayOf } from '../../utils/dates'
import { relativeDay, weekdayShort } from '../../utils/format'
import { displayWeight, toKg } from '../../utils/units'
import type { DailyLog } from '../../types'

const app = useAppStore()
const sheet = useLogSheetStore()
const unit = computed(() => app.unit)

const date = ref(sheet.date ?? app.today)
const loading = ref(false)
const loadError = ref('')
const saveError = ref('')
const saving = ref(false)

const kcalText = ref('')
const kgText = ref('')
const cheat = ref(false)
const note = ref('')
const kcalBackup = ref('')   // what "Undo" brings back
const kgBackup = ref('')
const weekLogs = ref<DailyLog[]>([])
const lastWeight = ref<number | null>(null)
const budget = ref<number | null>(null)
const target = ref<number | null>(null)
const week = ref<{ freeKcal: number; others: number; todayBudget: number } | null>(null)
const savedCheat = ref(false)

const kcalValue = computed<number | null>(() => {
  const t = kcalText.value.trim()
  if (t === '') return null
  const n = Number(t)
  return Number.isFinite(n) && n >= 0 ? Math.round(n) : null
})
const kgValue = computed<number | null>(() => {
  const t = kgText.value.trim().replace(',', '.')
  if (t === '') return null
  const n = Number(t)
  return Number.isFinite(n) && n > 0 ? toKg(n, unit.value) : null
})

const kcalEmpty = computed(() => kcalText.value.trim() === '')
const kgEmpty = computed(() => kgText.value.trim() === '')
const kcalUndo = computed(() => kcalEmpty.value && kcalBackup.value !== '')
const kgUndo = computed(() => kgEmpty.value && kgBackup.value !== '')
const kcalNothing = computed(() => kcalEmpty.value && !kcalUndo.value)
const kgNothing = computed(() => kgEmpty.value && !kgUndo.value)
const deleting = computed(() => kcalEmpty.value && kgEmpty.value)

const whenLabel = computed(() => {
  const r = relativeDay(date.value, app.today)
  return ['Today', 'Yesterday', 'Tomorrow'].includes(r) ? `${r}, ${dayLong(date.value)}` : r
})
function dayLong(d: string) {
  const [, m, dd] = d.split('-')
  return `${weekdayShort(d)} ${Number(dd)} ${['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'][Number(m) - 1]}`
}

const kcalHint = computed(() => {
  if (kcalEmpty.value) return 'Calories are removed when you save'
  if (kcalValue.value === 0) return '0 kcal, saved as a fasting day'
  const ref_ = budget.value ?? target.value
  if (kcalValue.value == null || ref_ == null) return ''
  const gap = ref_ - kcalValue.value
  return gap >= 0 ? `${gap} left of ${ref_}` : `${Math.abs(gap)} over ${ref_}`
})
const kcalAfter = computed(() => {
  if (deleting.value) return ''
  const w = week.value
  if (w && date.value === app.today && !kcalEmpty.value && kcalValue.value != null && w.others > 0) {
    // Same rule as the server's budget: what eaten today leaves of the pool is shared by the other free days.
    let pool = w.freeKcal
    if (cheat.value !== savedCheat.value && app.settings.tdeeKcal && target.value != null) {
      const diff = app.settings.tdeeKcal - target.value
      pool += cheat.value ? diff : -diff
    }
    const plan = kcalValue.value <= w.todayBudget && cheat.value === savedCheat.value
      ? Math.round((w.freeKcal - w.todayBudget) / w.others)
      : Math.max(0, Math.round((pool - kcalValue.value) / w.others))
    return `Rest of the week: ${plan} a day`
  }
  if (kcalEmpty.value && date.value >= app.today) return 'The day goes back to planned. Type 0 for a fasting day.'
  return ''
})

const lastWeightHint = computed(() => (lastWeight.value == null ? 'Weight' : `Last ${displayWeight(lastWeight.value, unit.value).toFixed(1)} ${unit.value}`))
const cheatHint = computed(() => (app.settings.tdeeKcal ? `Uses ${app.settings.tdeeKcal} kcal as today's target` : "Uses your TDEE as today's target"))
const cheatHintShort = computed(() => (app.settings.tdeeKcal ? `uses ${app.settings.tdeeKcal} as the target` : 'uses your TDEE as the target'))

const weekDays = computed(() => {
  const monday = mondayOf(date.value)
  return Array.from({ length: 7 }, (_, i) => {
    const d = addDays(monday, i)
    const log = weekLogs.value.find((l) => l.date === d)
    return { date: d, name: weekdayShort(d), kcal: log?.caloriesKcal != null ? String(log.caloriesKcal) : '–' }
  })
})

function addKcal(n: number) {
  const now = kcalValue.value ?? 0
  kcalText.value = String(Math.max(0, now + n))
  kcalBackup.value = ''
}

function stepKg(delta: number) {
  const base = kgText.value.trim() === '' ? (lastWeight.value != null ? displayWeight(lastWeight.value, unit.value) : 0) : Number(kgText.value.replace(',', '.'))
  if (!Number.isFinite(base)) return
  kgText.value = (Math.round((base + (kgText.value.trim() === '' && lastWeight.value != null ? 0 : delta)) * 10) / 10).toFixed(1)
  kgBackup.value = ''
}

// The last weight is only a placeholder. Clicking into an empty box fills it in as a starting point,
// so saving a day without a weight never saves one by accident.
function fillLastWeight() {
  if (kgText.value.trim() === '' && lastWeight.value != null && kgBackup.value === '') {
    kgText.value = displayWeight(lastWeight.value, unit.value).toFixed(1)
  }
}

function toggleKcal() {
  if (kcalUndo.value) { kcalText.value = kcalBackup.value; kcalBackup.value = '' }
  else { kcalBackup.value = kcalText.value; kcalText.value = '' }
}
function toggleKg() {
  if (kgUndo.value) { kgText.value = kgBackup.value; kgBackup.value = '' }
  else { kgBackup.value = kgText.value; kgText.value = '' }
}

async function load() {
  loading.value = true
  loadError.value = ''
  kcalBackup.value = ''
  kgBackup.value = ''
  saveError.value = ''
  try {
    const monday = mondayOf(date.value)
    const [logs, earlier, calendar] = await Promise.all([
      logApi.getAll({ from: monday, to: addDays(monday, 6) }),
      logApi.getAll({ from: addDays(date.value, -60), to: addDays(date.value, -1) }),
      statsApi.getCalendar(date.value, date.value),
    ])
    weekLogs.value = logs
    const log = logs.find((l) => l.date === date.value)
    // Only what is really stored is shown. Never a prefilled target or last weight.
    kcalText.value = log?.caloriesKcal != null ? String(log.caloriesKcal) : ''
    kgText.value = log?.weightKg != null ? displayWeight(log.weightKg, unit.value).toFixed(1) : ''
    cheat.value = log?.isCheatDay ?? false
    note.value = log?.notes ?? ''
    lastWeight.value = earlier.find((l) => l.weightKg != null)?.weightKg ?? null
    savedCheat.value = log?.isCheatDay ?? false
    week.value = null
    if (date.value === app.today) {
      const dash = await statsApi.getDashboard()
      const free = dash.budget?.days.filter((d) => !d.userLogged) ?? []
      if (dash.budget && free.length > 1) {
        week.value = { freeKcal: free.reduce((s, d) => s + d.kcal, 0), others: free.length - 1, todayBudget: dash.budget.todayBudget }
      }
    }
    const day = calendar.days[0]
    budget.value = day?.planned ?? null
    target.value = day?.target ?? null
    // A day with no calories and no planned value starts at the day's target (when there is one).
    if (log?.caloriesKcal == null && budget.value == null && target.value != null) kcalText.value = String(target.value)
  } catch (e) {
    loadError.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

function go(days: number) { date.value = addDays(date.value, days) }
function select(d: string) { date.value = d }

async function save() {
  if (saving.value) return
  saving.value = true
  saveError.value = ''
  try {
    if (deleting.value) {
      // Both empty means "Delete entry": the whole day, note and cheat mark included.
      if (weekLogs.value.some((l) => l.date === date.value)) await logApi.deleteDay(date.value)
    } else {
      await logApi.saveDay(date.value, {
        weightKg: kgValue.value,
        caloriesKcal: kcalValue.value,
        notes: note.value.trim() === '' ? null : note.value.trim(),
        isCheatDay: cheat.value,
      })
    }
    app.refresh()
    sheet.close()
  } catch (e) {
    saveError.value = (e as Error).message
  } finally {
    saving.value = false
  }
}

function onEnter(e: KeyboardEvent) {
  const t = e.target as HTMLElement
  if (t.tagName === 'TEXTAREA' || t.tagName === 'BUTTON') return
  e.preventDefault()
  save()
}

function close() { sheet.close() }

onMounted(load)
watch(date, load)
</script>

<style scoped>
.sheet { display: flex; flex-direction: column; gap: 12px; color: #1f2937; font-size: 15px; line-height: 1.4; font-variant-numeric: tabular-nums; }
.nav { display: grid; grid-template-columns: 48px minmax(0, 1fr) 48px; gap: 8px; align-items: center; }
.nav .box { height: 48px; }
.when { text-align: center; font-weight: 600; }
.week { display: none; grid-template-columns: 44px repeat(7, minmax(0, 1fr)) 44px; gap: 6px; }
.week .box { height: auto; min-height: 44px; font-size: 20px; }
.box { border: 1px solid #d1d5db; border-radius: 8px; background: #fff; font-size: 20px; color: #374151; }
.box:hover { background: #f3f4f6; }
.day { min-height: 62px; border-radius: 8px; border: 1px solid #e5e7eb; background: #fff; color: #1f2937; font: inherit; display: flex; flex-direction: column; align-items: center; justify-content: center; }
.day .dn { font-weight: 600; }
.day .dk { font-size: 13px; }
.day[aria-pressed='true'] { background: #0369a1; border-color: #0369a1; color: #fff; }
button:focus-visible, input:focus-visible, textarea:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; }
.cols { display: flex; flex-direction: column; gap: 12px; }
.right { display: flex; flex-direction: column; gap: 12px; }
.field { display: flex; flex-direction: column; gap: 8px; }
.field-head { display: flex; align-items: center; justify-content: space-between; }
label { font-weight: 600; }
.opt { font-weight: 400; color: #4b5563; }
.muted { color: #4b5563; font-weight: 400; }
.remove { min-height: 44px; padding: 0 4px; border: 0; background: transparent; font: inherit; font-weight: 600; color: #c70036; }
.remove.undo { color: #0369a1; }
.remove:disabled { color: #6b7280; }
.stepper { display: grid; grid-template-columns: 60px minmax(0, 1fr) 60px; gap: 8px; }
.stepper button { height: 60px; border: 1px solid #d1d5db; border-radius: 10px; background: #f9fafb; font-size: 22px; color: #1f2937; }
.stepper input { box-sizing: border-box; width: 100%; height: 60px; border: 1px solid #9ca3af; border-radius: 10px; padding: 0 14px; font: inherit; font-size: 24px; font-weight: 700; text-align: center; color: #1f2937; min-width: 0; }
.stepper input:focus { border: 2px solid #0369a1; outline: none; }
.quick { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; }
.quick button { height: 44px; border: 1px solid #d1d5db; border-radius: 8px; background: #f9fafb; font: inherit; font-weight: 600; color: #1f2937; }
.left { margin: 0; font-weight: 600; }
.after { margin: 0; color: #4b5563; min-height: 21px; }
.cheat-row { display: flex; align-items: center; justify-content: space-between; gap: 12px; min-height: 48px; border-top: 1px solid #e5e7eb; border-bottom: 1px solid #e5e7eb; padding: 6px 0; font-weight: 400; cursor: pointer; }
.cheat-row strong { font-weight: 600; }
.cheat-row input { width: 24px; height: 24px; accent-color: #0369a1; }
.cheat-foot { display: none; align-items: center; gap: 12px; min-height: 44px; font-weight: 400; cursor: pointer; }
.cheat-foot input { width: 22px; height: 22px; accent-color: #0369a1; }
.cheat-foot strong { font-weight: 600; }
.note textarea { box-sizing: border-box; width: 100%; height: 84px; resize: vertical; border: 1px solid #9ca3af; border-radius: 10px; padding: 10px 14px; font: inherit; color: #1f2937; }
.error { margin: 0; color: #a50036; background: #ffe4e6; padding: 10px 12px; border-radius: 8px; }
.foot { display: flex; align-items: center; justify-content: space-between; gap: 24px; }
.save-area { display: flex; align-items: center; gap: 14px; flex: 1; justify-content: flex-end; }
.keys { display: none; color: #4b5563; font-size: 13px; }
.save { min-height: 48px; padding: 0 28px; border: 0; border-radius: 10px; background: #0369a1; color: #fff; font: inherit; font-size: 16px; font-weight: 600; flex: 1; }
.save:hover:not(:disabled) { background: #075985; }
.save.danger { background: #c70036; }
.save:disabled { opacity: 0.5; }
@media (min-width: 640px) {
  .nav { display: none; }
  .week { display: grid; }
  .cols { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 32px; align-items: start; }
  .stepper { grid-template-columns: 56px minmax(0, 1fr) 56px; }
  .stepper button, .stepper input { height: 56px; }
  .cheat-row { display: none; }
  .cheat-foot { display: flex; }
  .note { margin-top: 8px; }
  .note textarea { height: 96px; }
  .keys { display: inline; }
  .save { flex: none; }
}
</style>
