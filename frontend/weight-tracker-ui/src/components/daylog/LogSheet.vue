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
        <button v-for="d in weekDays" :key="d.date" type="button" class="day" :class="{ plan: d.planned }" :style="d.style" :aria-pressed="d.date === date" @click="select(d.date)">
          <span class="dn">{{ d.name }}</span>
          <span v-if="ready" class="dk">{{ d.kcal }}</span><span v-else class="skel dk-skel" />
        </button>
        <button type="button" class="box" aria-label="Next week" @click="go(7)">›</button>
      </div>

      <ErrorNote v-if="loadError" :message="loadError" :retry="() => store.load(date)" />

      <!-- Nothing cached yet: placeholders with the shape of the form, shimmering -->
      <div v-if="!ready" class="cols" data-test="skeleton" aria-busy="true" aria-label="Loading the day">
        <div class="field">
          <div class="skel" style="height: 20px; width: 40%" />
          <div class="skel" style="height: 60px" />
          <div class="skel" style="height: 44px" />
          <div class="skel" style="height: 16px; width: 55%" />
        </div>
        <div class="right">
          <div class="field">
            <div class="skel" style="height: 20px; width: 40%" />
            <div class="skel" style="height: 60px" />
          </div>
          <div class="skel" style="height: 48px" />
          <div class="skel" style="height: 84px" />
        </div>
      </div>

      <div v-else class="cols">
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
            <input id="kcal" v-model="kcalText" inputmode="numeric" :placeholder="kcalPlaceholder" autocomplete="off" :aria-invalid="kcalError !== ''" @click="fillPlanned" />
            <button type="button" aria-label="50 kcal more" @click="addKcal(50)">+</button>
          </div>
          <div class="quick">
            <button v-for="n in [100, 250, 500]" :key="n" type="button" @click="addKcal(n)">+{{ n }}</button>
          </div>
          <p v-if="kcalError" class="invalid">{{ kcalError }}</p>
          <p v-else class="left" data-test="kcal-hint">{{ kcalHint }}</p>
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
              <input id="kg" v-model="kgText" inputmode="decimal" :placeholder="kgPlaceholder" autocomplete="off" :aria-invalid="kgError !== ''" @click="fillLastWeight" />
              <button type="button" :aria-label="`0.1 ${unit} more`" @click="stepKg(0.1)">+</button>
            </div>
            <p v-if="kgError" class="invalid">{{ kgError }}</p>
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
          <button type="button" class="save" :class="{ danger: deleting }" :disabled="saving || !canSave" @click="save">
            {{ saving ? 'Saving…' : deleting ? 'Delete entry' : 'Save day' }}
          </button>
        </div>
      </div>
    </template>
  </Modal>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import Modal from '../ui/Modal.vue'
import ErrorNote from '../ui/ErrorNote.vue'
import { useAppStore } from '../../stores/app'
import { useLogSheetStore } from '../../stores/logSheet'
import { useLogWeeksStore } from '../../stores/logWeeks'
import * as logApi from '../../api/dailyLog'
import { addDays, mondayOf } from '../../utils/dates'
import { CALENDAR_COLORS, relativeDay, shortDate, weekdayShort } from '../../utils/format'
import { displayWeight, toKg } from '../../utils/units'
import type { LogDay, LogWeek } from '../../types'

const app = useAppStore()
const sheet = useLogSheetStore()
const store = useLogWeeksStore()
const unit = computed(() => app.unit)

const date = ref(sheet.date ?? app.today)

// What the form was filled from. Kept while it is edited: a refresh in the background (or on coming back to
// the tab) makes the store reload, but never hides or overwrites what is being typed.
const filled = ref<{ date: string; week: LogWeek; day: LogDay } | null>(null)
const ready = computed(() => filled.value?.date === date.value)
const day = computed(() => filled.value?.day ?? null)
const loadError = computed(() => (ready.value ? '' : store.error ?? ''))
const saveError = ref('')
const saving = ref(false)

const kcalText = ref('')
const kgText = ref('')
const cheat = ref(false)
const note = ref('')
const kcalBackup = ref('')   // what "Undo" brings back
const kgBackup = ref('')

const shownWeight = (kg: number | null | undefined) => (kg == null ? '' : displayWeight(kg, unit.value).toFixed(1))

// ── What is typed ──────────────────────────────────────────────────────────

const kcalEmpty = computed(() => kcalText.value.trim() === '')
const kgEmpty = computed(() => kgText.value.trim() === '')

const kcalValue = computed<number | null>(() => {
  const n = Number(kcalText.value.trim())
  return !kcalEmpty.value && Number.isFinite(n) && n >= 0 && n <= 30000 ? Math.round(n) : null
})
const kgValue = computed<number | null>(() => {
  if (kgEmpty.value) return null
  // Not edited: keep the stored value, which can be finer than what is shown (one decimal, or lbs).
  if (day.value?.weightKg != null && kgText.value.trim() === shownWeight(day.value.weightKg)) return day.value.weightKg
  const kg = toKg(Number(kgText.value.trim().replace(',', '.')), unit.value)
  return Number.isFinite(kg) && kg >= 1 && kg <= 700 ? kg : null
})
// Something that is not a number must never be saved as "nothing".
const kcalError = computed(() => (!kcalEmpty.value && kcalValue.value == null ? 'Enter a number from 0 to 30000.' : ''))
const kgError = computed(() => (!kgEmpty.value && kgValue.value == null ? 'Enter a weight, like 83.8.' : ''))

const hadKcal = computed(() => day.value?.calories != null)
const hadKg = computed(() => day.value?.weightKg != null)
const kcalUndo = computed(() => kcalEmpty.value && kcalBackup.value !== '')
const kgUndo = computed(() => kgEmpty.value && kgBackup.value !== '')
const kcalNothing = computed(() => kcalEmpty.value && !kcalUndo.value)
const kgNothing = computed(() => kgEmpty.value && !kgUndo.value)

// Removing everything that was logged deletes the entry. A day with only a note or a cheat mark is a normal save.
const deleting = computed(() => ready.value && kcalEmpty.value && kgEmpty.value && (hadKcal.value || hadKg.value))
const canSave = computed(() => {
  if (!ready.value || kcalError.value || kgError.value) return false
  const d = day.value!
  return !kcalEmpty.value || !kgEmpty.value || hadKcal.value || hadKg.value
    || cheat.value !== d.isCheatDay || note.value.trim() !== (d.notes ?? '')
})

// ── Budget and hints ───────────────────────────────────────────────────────

const whenLabel = computed(() => {
  const r = relativeDay(date.value, app.today)
  return ['Today', 'Yesterday', 'Tomorrow'].includes(r) ? `${r}, ${weekdayShort(date.value)} ${shortDate(date.value)}` : r
})

const isToday = computed(() => date.value === app.today)
/** What switching the cheat day changes: the TDEE instead of the target (the target itself without a TDEE). */
const cheatExtra = computed(() => {
  const d = day.value
  return d?.calorieTarget == null ? 0 : (d.tdee ?? d.calorieTarget) - d.calorieTarget
})
const target = computed(() => (day.value?.calorieTarget == null ? null : day.value.calorieTarget + (cheat.value ? cheatExtra.value : 0)))
/** How far the switch moves the day's target from what is stored. */
const cheatChange = computed(() => (cheat.value ? cheatExtra.value : 0) - (day.value?.isCheatDay ? cheatExtra.value : 0))
/** The day's budget, following the cheat day switch. A future day that is logged has a target, not a budget. */
const budget = computed(() => {
  const d = day.value
  if (d?.planned == null || (hadKcal.value && !isToday.value)) return null
  return Math.max(0, d.planned + cheatChange.value)
})

// Same rule as the server's budget: today counts as its full budget until it is exceeded, and what is left of
// the pool is shared by the other free days.
const restOfWeek = computed<number | null>(() => {
  const w = filled.value?.week.thisWeek
  if (!isToday.value || !w || w.otherDays === 0 || budget.value == null || day.value == null) return null
  const eaten = kcalValue.value ?? 0
  const today = eaten > budget.value ? eaten : w.todayBudget + cheatChange.value
  return Math.max(0, Math.round((w.pool + cheatChange.value - today) / w.otherDays))
})

const kcalHint = computed(() => {
  const limit = budget.value ?? target.value
  if (kcalEmpty.value) {
    if (hadKcal.value) return 'Calories are removed when you save'
    return budget.value != null ? `Budget ${budget.value}` : ''
  }
  if (kcalValue.value === 0) return '0 kcal, saved as a fasting day'
  if (kcalValue.value == null || limit == null) return ''
  const gap = limit - kcalValue.value
  return gap >= 0 ? `${gap} left of ${limit}` : `${-gap} over ${limit}`
})
const kcalAfter = computed(() => {
  if (deleting.value) return ''
  if (!kcalEmpty.value) return restOfWeek.value != null ? `Rest of the week: ${restOfWeek.value} a day` : ''
  if (day.value?.calorieTarget != null && date.value >= app.today)
    return hadKcal.value ? `${isToday.value ? 'Today' : 'The day'} goes back to planned. Type 0 for a fasting day.` : 'Type 0 for a fasting day.'
  return ''
})

const kcalPlaceholder = computed(() => (budget.value != null && !kcalUndo.value ? String(budget.value) : 'Not logged'))
// The last weight itself, nothing else.
const kgPlaceholder = computed(() => shownWeight(day.value?.lastWeightKg) || 'Weight')
const cheatHint = computed(() => (day.value?.tdee ? `Uses ${day.value.tdee} kcal as ${isToday.value ? "today's" : "the day's"} target` : 'Uses your TDEE as the target'))
const cheatHintShort = computed(() => (day.value?.tdee ? `uses ${day.value.tdee} as the target` : 'uses your TDEE as the target'))

const weekDays = computed(() => {
  const monday = mondayOf(date.value)
  const days = filled.value?.week.monday === monday ? filled.value.week.days : []
  return Array.from({ length: 7 }, (_, i) => {
    const d = addDays(monday, i)
    const selected = d === date.value
    const stored = days.find((x) => x.date === d)
    const logged = selected ? kcalValue.value : stored?.calories ?? null
    const plan = restOfWeek.value != null && d > date.value ? restOfWeek.value : selected ? budget.value : stored?.planned ?? null
    const colors = !selected && stored?.status ? CALENDAR_COLORS.day[stored.status] : null
    return {
      date: d, name: `${weekdayShort(d)} ${Number(d.slice(8))}`,
      kcal: String(logged ?? plan ?? '–'),
      planned: !selected && logged == null && plan != null,
      style: colors ? { background: colors[0], color: colors[1], borderColor: 'transparent' } : undefined,
    }
  })
})

// ── Editing ────────────────────────────────────────────────────────────────

function addKcal(n: number) {
  kcalText.value = String(Math.max(0, (kcalValue.value ?? 0) + n))
  kcalBackup.value = ''
}

function stepKg(delta: number) {
  // The first step starts from the last weight.
  if (kgEmpty.value && kgPlaceholder.value !== 'Weight') kgText.value = kgPlaceholder.value
  else {
    const base = Number(kgText.value.replace(',', '.'))
    if (!Number.isFinite(base)) return
    kgText.value = Math.max(0, base + delta).toFixed(1)
  }
  kgBackup.value = ''
}

// The plan and the last weight are only placeholders. Clicking into an empty box fills it in as a starting
// point (selected, so typing replaces it); saving without touching it saves nothing.
function fillPlanned(e: Event) {
  if (!kcalEmpty.value || kcalBackup.value !== '' || budget.value == null) return
  kcalText.value = String(budget.value)
  selectAll(e)
}
function fillLastWeight(e: Event) {
  if (!kgEmpty.value || kgBackup.value !== '' || day.value?.lastWeightKg == null) return
  kgText.value = shownWeight(day.value.lastWeightKg)
  selectAll(e)
}
function selectAll(e: Event) {
  nextTick(() => (e.target as HTMLInputElement).select())
}

function toggleKcal() {
  if (kcalUndo.value) { kcalText.value = kcalBackup.value; kcalBackup.value = '' }
  else { kcalBackup.value = kcalText.value; kcalText.value = '' }
}
function toggleKg() {
  if (kgUndo.value) { kgText.value = kgBackup.value; kgBackup.value = '' }
  else { kgBackup.value = kgText.value; kgText.value = '' }
}

// ── Loading ────────────────────────────────────────────────────────────────

function fill() {
  const week = store.week(date.value)
  const d = store.day(date.value)
  if (!week || !d || ready.value) return
  filled.value = { date: date.value, week, day: d }
  kcalBackup.value = ''
  kgBackup.value = ''
  saveError.value = ''
  kgText.value = shownWeight(d.weightKg)
  cheat.value = d.isCheatDay
  note.value = d.notes ?? ''
  // A day with no calories starts at its target, unless it has a plan: that one is the placeholder.
  kcalText.value = d.calories != null ? String(d.calories) : budget.value == null && target.value != null ? String(target.value) : ''
  focusCalories()
}

async function focusCalories() {
  // On a phone this would open the keyboard over the sheet.
  if (!window.matchMedia?.('(min-width: 640px)').matches) return
  await nextTick()
  const inUse = document.activeElement?.closest('[role=dialog]') && document.activeElement.matches('input, textarea, button')
  if (!inUse) document.getElementById('kcal')?.focus()
}

async function load() {
  await store.load(date.value)
  fill()
  // Then the neighbours, so moving on is instant.
  store.load(addDays(date.value, -7))
  store.load(addDays(date.value, 7))
}
fill()      // the week is usually there already: the form is in the first render
watch(() => store.week(date.value), fill)
watch(date, () => { fill(); load() })
// The week was dropped because something changed. Only needed if this day is not on screen yet.
watch(() => app.version, () => { if (!ready.value) load() })
onMounted(load)

function go(days: number) { date.value = addDays(date.value, days) }
function select(d: string) { date.value = d }

// ── Saving ─────────────────────────────────────────────────────────────────

async function save() {
  if (saving.value || !canSave.value) return
  saving.value = true
  saveError.value = ''
  try {
    if (deleting.value) {
      // Both removed means "Delete entry": the whole day, note and cheat mark included.
      await logApi.deleteDay(date.value)
    } else {
      await logApi.saveDay(date.value, {
        weightKg: kgValue.value,
        caloriesKcal: kcalValue.value,
        notes: note.value.trim() === '' ? null : note.value.trim(),
        isCheatDay: cheat.value,
      })
    }
    sheet.close()
    app.refresh()
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
.day.plan { border: 1px dashed #6b7280; color: #4b5563; }
.day[aria-pressed='true'] { background: #0369a1; border-color: #0369a1; color: #fff; }
button:focus-visible, input:focus-visible, textarea:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; }
.cols { display: flex; flex-direction: column; gap: 12px; }
.skel { border-radius: 8px; background: linear-gradient(90deg, #f3f4f6 25%, #e5e7eb 37%, #f3f4f6 63%); background-size: 400% 100%; animation: appear 0.15s 0.12s both, shimmer 1.4s ease infinite; }
/* Most loads take a few milliseconds: only show the placeholders when one is slower than that. */
@keyframes appear { from { opacity: 0; } }
.dk-skel { display: block; width: 28px; height: 12px; margin-top: 3px; }
@keyframes shimmer { 0% { background-position: 100% 50%; } 100% { background-position: 0 50%; } }
@media (prefers-reduced-motion: reduce) { .skel { animation: none; } }
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
.left { margin: 0; font-weight: 600; min-height: 21px; }
.invalid { margin: 0; font-weight: 600; color: #c70036; }
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
