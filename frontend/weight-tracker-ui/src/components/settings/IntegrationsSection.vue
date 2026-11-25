<template>
  <section class="sec" aria-labelledby="int-h">
    <h2 id="int-h">Integrations</h2>
    <div class="muted">Import calories and weight from another app.</div>

    <ErrorNote v-if="error" :message="error" :retry="reload" />
    <Skeleton v-else-if="!data" height="96px" />

    <div v-for="p in data ?? []" :key="p.id" class="box" :class="{ off: !p.configured }" :data-test="p.id">
      <div class="top">
        <h3>{{ p.name }}</h3>
        <StatusChip v-if="!p.configured">Disabled</StatusChip>
        <StatusChip v-else-if="p.status === 'Connected'" kind="under">Connected</StatusChip>
        <StatusChip v-else-if="p.status === 'NeedsReconnect'" kind="over">Connect again</StatusChip>
        <StatusChip v-else>Not connected</StatusChip>
      </div>

      <template v-if="!p.configured">
        <p class="muted">The server needs <template v-for="(s, i) in p.requiredSettings" :key="s"><template v-if="i"> and </template><code>{{ s }}</code></template>. Set what is missing, then restart it. The README explains where to find them.</p>
        <p v-if="p.status" class="muted">Your connection is kept, but nothing is imported meanwhile.</p>
        <div v-else class="buttons"><button type="button" class="go" disabled>Connect</button></div>
      </template>

      <template v-else>
        <p v-if="p.lastSyncAt" class="muted">Last sync {{ when(p.lastSyncAt) }}{{ p.lastSyncStatus === 'Failed' ? ', failed' : '' }}.</p>
        <p v-else-if="p.status" class="muted">Not synced yet.</p>
        <p v-if="p.lastError && p.lastError !== actionError" class="error">{{ p.lastError }}</p>
        <p v-if="p.status !== 'Connected'" class="muted">Values are imported every few hours, from 3 weeks back to 2 weeks ahead. What you logged yourself is never replaced, except calories set in advance.</p>

        <div class="buttons">
          <template v-if="p.status === 'Connected'">
            <button type="button" class="go" :disabled="!!busy" @click="syncNow(p)">{{ busy === 'sync' ? 'Syncing…' : 'Sync now' }}</button>
            <button type="button" class="plain" :disabled="!!busy" @click="period = p">Sync a period…</button>
          </template>
          <button v-else type="button" class="go" :disabled="!!busy" @click="connect(p)">{{ busy === 'connect' ? 'Connecting…' : p.status ? 'Connect again' : 'Connect' }}</button>
          <button v-if="p.status" type="button" class="plain" :disabled="!!busy" @click="leaving = p">Disconnect</button>
        </div>

        <template v-if="p.status !== 'Connected'">
          <button v-if="pinFor !== p.id" type="button" class="link" :disabled="!!busy" @click="askPin(p)">{{ p.name }} doesn't come back here? Use a code</button>
          <form v-else class="pin" @submit.prevent="verify(p)">
            <p class="muted">Approve on <a :href="pinUrl" target="_blank" rel="noopener noreferrer">{{ p.name }}</a>, then type the code it shows.</p>
            <div class="pin-row">
              <input v-model="pin" aria-label="Code" autocomplete="off" placeholder="Code" />
              <button type="submit" class="go" :disabled="!!busy || !pin.trim()">{{ busy === 'verify' ? 'Checking…' : 'Confirm' }}</button>
            </div>
          </form>
        </template>
      </template>

      <a v-if="p.attribution" class="credit" :href="p.attribution.url" target="_blank" rel="noopener noreferrer">{{ p.attribution.text }}</a>
    </div>

    <p v-if="actionError" role="alert" class="error">{{ actionError }}</p>
    <p v-if="message" role="status" class="ok">{{ message }}</p>

    <SyncPeriodModal v-if="period" :id="period.id" :name="period.name" @close="period = null" @done="onPeriodDone" />
    <Modal v-if="leaving" :title="`Disconnect ${leaving.name}?`" max-width="440px" @close="leaving = null">
      <p>Nothing is imported anymore. Values already imported stay.</p>
      <template #footer>
        <div class="dlg-row">
          <button type="button" class="plain" @click="leaving = null">Cancel</button>
          <button type="button" class="danger" @click="disconnect">Disconnect</button>
        </div>
      </template>
    </Modal>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ErrorNote from '../ui/ErrorNote.vue'
import Modal from '../ui/Modal.vue'
import Skeleton from '../ui/Skeleton.vue'
import StatusChip from '../ui/StatusChip.vue'
import SyncPeriodModal from './SyncPeriodModal.vue'
import { useAppStore } from '../../stores/app'
import { useAsyncData } from '../../composables/useAsyncData'
import * as api from '../../api/integrations'
import { relativeDay } from '../../utils/format'
import { toLocalDateString } from '../../utils/dates'
import { syncSummary } from '../../utils/syncReview'
import type { Integration } from '../../types'

const app = useAppStore()
const route = useRoute()
const router = useRouter()
const { data, error, reload } = useAsyncData(api.getAll)

const busy = ref<'' | 'connect' | 'verify' | 'sync' | 'disconnect'>('')
const actionError = ref('')
const message = ref('')
const period = ref<Integration | null>(null)
const leaving = ref<Integration | null>(null)
const pinFor = ref('')
const pinUrl = ref('')
const pin = ref('')

/** "Today, 14:05" */
function when(iso: string): string {
  const d = new Date(iso)
  const day = relativeDay(toLocalDateString(d), app.today)
  return `${day === 'Today' || day === 'Yesterday' ? day.toLowerCase() : day}, ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

async function run(what: typeof busy.value, action: () => Promise<void>) {
  busy.value = what
  actionError.value = ''
  message.value = ''
  try {
    await action()
  } catch (e) {
    actionError.value = (e as Error).message
  } finally {
    busy.value = ''
  }
}

const connect = (p: Integration) => run('connect', async () => {
  window.location.assign(await api.connect(p.id))
})

const askPin = (p: Integration) => run('connect', async () => {
  pinUrl.value = await api.connect(p.id, true)
  pin.value = ''
  pinFor.value = p.id
})

const verify = (p: Integration) => run('verify', async () => {
  await api.verifyPin(p.id, pin.value)
  pinFor.value = ''
  await doSync(p.id)
})

const syncNow = (p: Integration) => run('sync', () => doSync(p.id))

// Refreshes also after a failure: the failure itself is part of the status.
async function doSync(id: string) {
  busy.value = 'sync'
  try {
    message.value = syncSummary(await api.sync(id))
  } finally {
    app.refresh()
  }
}

function disconnect() {
  const p = leaving.value!
  leaving.value = null
  return run('disconnect', async () => {
    await api.disconnect(p.id)
    message.value = `${p.name} is disconnected.`
    app.refresh()
  })
}

function onPeriodDone(text: string) {
  period.value = null
  actionError.value = ''
  message.value = text
  app.refresh()
}

const RESULTS: Record<string, string> = {
  denied: 'The connection was not approved.',
  expired: 'That took too long, or the server restarted in between. Connect again.',
  failed: 'The connection failed. The server\'s log says why. Try again, or use a code.',
}

// Coming back from the provider: /settings?integration=fatsecret&result=connected. The first sync starts from here,
// because it needs the local date of this browser.
onMounted(() => {
  const { integration, result } = route.query
  if (typeof integration !== 'string' || typeof result !== 'string') return
  router.replace({ path: route.path })      // a reload must not do this again
  if (result === 'connected') run('sync', () => doSync(integration))
  else actionError.value = RESULTS[result] ?? RESULTS.failed
})
</script>

<style scoped>
.sec { display: flex; flex-direction: column; gap: 10px; }
h2 { margin: 0; font-size: 16px; font-weight: 600; }
h3 { margin: 0; font-size: 15px; font-weight: 600; }
.muted { color: #4b5563; }
p.muted { font-size: 14px; }
.box { border: 1px solid #e5e7eb; border-radius: 10px; padding: 14px 16px; display: flex; flex-direction: column; gap: 10px; }
.top { display: flex; justify-content: space-between; align-items: center; gap: 12px; }
code { font-size: 13px; background: #f3f4f6; border-radius: 4px; padding: 1px 5px; overflow-wrap: anywhere; }
.buttons { display: flex; flex-wrap: wrap; gap: 8px; }
.go { min-height: 44px; padding: 0 16px; border: 0; border-radius: 8px; background: #0284c7; color: #fff; font: inherit; font-weight: 600; }
.go:hover:not(:disabled) { background: #0369a1; }
.plain { min-height: 44px; padding: 0 16px; border: 1px solid #9ca3af; border-radius: 8px; background: #fff; color: #1f2937; font: inherit; font-weight: 600; }
.go:disabled, .plain:disabled { opacity: 0.6; }
.danger { min-height: 44px; padding: 0 16px; border: 0; border-radius: 8px; background: #c70036; color: #fff; font: inherit; font-weight: 600; }
.link { align-self: flex-start; min-height: 44px; padding: 0; border: 0; background: none; color: #0369a1; font: inherit; font-size: 14px; text-decoration: underline; text-align: left; }
.pin { display: flex; flex-direction: column; gap: 8px; }
.pin a, .credit { color: #0369a1; text-decoration: underline; }
.pin-row { display: flex; gap: 8px; }
.pin-row input { box-sizing: border-box; min-width: 0; flex: 1; height: 44px; border: 1px solid #9ca3af; border-radius: 8px; padding: 0 12px; font: inherit; color: #1f2937; }
.off { background: #f9fafb; }
.off h3 { color: #4b5563; }
.off code { background: #e5e7eb; }
.off .go { background: #d1d5db; color: #4b5563; opacity: 1; cursor: not-allowed; }
p { margin: 0; }
.credit { align-self: flex-start; font-size: 13px; }
.error { color: #a50036; background: #ffe4e6; padding: 10px 12px; border-radius: 8px; overflow-wrap: anywhere; }
.ok { color: #006045; }
.dlg-row { display: flex; justify-content: flex-end; gap: 8px; }
button:focus-visible, input:focus-visible, a:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; }
</style>
