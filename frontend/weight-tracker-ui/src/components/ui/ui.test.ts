import { describe, it, expect, afterEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import Tile from './Tile.vue'
import TileRow from './TileRow.vue'
import StatusChip from './StatusChip.vue'
import InfoTip from './InfoTip.vue'
import Modal from './Modal.vue'

afterEach(() => { document.body.innerHTML = '' })

describe('Tile', () => {
  it('shows title, value, unit, sub text and rows', () => {
    const w = mount(Tile, { props: { title: 'Weight', value: '84.1', unit: 'kg', sub: 'From 22 weigh-ins' }, slots: { default: '<span class="extra">row</span>' } })
    expect(w.get('h2').text()).toBe('Weight')
    expect(w.text()).toContain('84.1')
    expect(w.text()).toContain('kg')
    expect(w.text()).toContain('From 22 weigh-ins')
    expect(w.find('.extra').exists()).toBe(true)
  })

  it('shows a skeleton while loading, not stale values', () => {
    const w = mount(Tile, { props: { title: 'Weight', value: '84.1', loading: true } })
    expect(w.text()).not.toContain('84.1')
    expect(w.findAll('.skeleton').length).toBeGreaterThan(0)
  })

  it('never shares an id with another tile', () => {
    const w = mount({ components: { Tile }, template: '<div><Tile title="a" /><Tile title="b" /></div>' })
    const ids = w.findAll('h2').map((h) => h.attributes('id'))
    expect(new Set(ids).size).toBe(2)
  })

  it('is labelled by its title for screen readers', () => {
    const w = mount(Tile, { props: { title: 'Today' } })
    expect(w.get('section').attributes('aria-labelledby')).toBe(w.get('h2').attributes('id'))
  })
})

describe('TileRow', () => {
  it('renders label and value', () => {
    const w = mount(TileRow, { props: { label: 'Pace', value: '▼ 0.41 kg a week' } })
    expect(w.text()).toContain('Pace')
    expect(w.text()).toContain('0.41')
  })
})

describe('StatusChip', () => {
  it('uses a different palette per kind', () => {
    const a = mount(StatusChip, { props: { kind: 'under' }, slots: { default: 'ok' } }).get('span').attributes('style')
    const b = mount(StatusChip, { props: { kind: 'over' }, slots: { default: 'late' } }).get('span').attributes('style')
    expect(a).not.toEqual(b)
  })
})

describe('InfoTip', () => {
  it('opens on hover and focus, closes on Escape and on tapping elsewhere', async () => {
    const w = mount(InfoTip, { props: { label: 'Budget', text: 'What you can eat today.' }, attachTo: document.body })
    expect(w.find('[role=tooltip]').exists()).toBe(false)

    await w.trigger('mouseenter')
    expect(w.get('[role=tooltip]').text()).toBe('What you can eat today.')

    await w.trigger('mouseleave')
    expect(w.find('[role=tooltip]').exists()).toBe(false)

    await w.get('button').trigger('focus')
    expect(w.find('[role=tooltip]').exists()).toBe(true)
    await w.get('button').trigger('keydown', { key: 'Escape' })
    expect(w.find('[role=tooltip]').exists()).toBe(false)

    await w.get('button').trigger('click')
    expect(w.find('[role=tooltip]').exists()).toBe(true)
    document.body.dispatchEvent(new Event('pointerdown', { bubbles: true }))
    await nextTick()
    expect(w.find('[role=tooltip]').exists()).toBe(false)
    w.unmount()
  })

  it('a tap opens it (focus comes first) and a second tap closes it', async () => {
    const w = mount(InfoTip, { props: { label: 'Budget', text: 'x' }, attachTo: document.body })
    const tap = async () => {
      await w.get('button').trigger('pointerdown')
      if (document.activeElement !== w.get('button').element) await w.get('button').trigger('focus')
      await w.get('button').trigger('click')
    }
    await tap()
    expect(w.find('[role=tooltip]').exists()).toBe(true)
    ;(w.get('button').element as HTMLElement).focus()
    await tap()
    expect(w.find('[role=tooltip]').exists()).toBe(false)
    w.unmount()
  })

  it('links the tooltip to its button for assistive tech', async () => {
    const w = mount(InfoTip, { props: { label: 'Pace', text: 'Trend.' }, attachTo: document.body })
    await w.trigger('mouseenter')
    expect(w.get('button').attributes('aria-describedby')).toBe(w.get('[role=tooltip]').attributes('id'))
    w.unmount()
  })
})

describe('Modal', () => {
  it('renders a dialog with a title, emits close on Escape and on the × button', async () => {
    const w = mount(Modal, { props: { title: 'Log a day' }, slots: { default: '<input id="x" />' }, attachTo: document.body })
    const dialog = document.body.querySelector('[role=dialog]') as HTMLElement
    expect(dialog.getAttribute('aria-modal')).toBe('true')
    expect(document.body.textContent).toContain('Log a day')

    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
    expect(w.emitted('close')).toHaveLength(1)

    ;(document.body.querySelector('[aria-label=Close]') as HTMLElement).click()
    expect(w.emitted('close')).toHaveLength(2)
    w.unmount()
  })

  it('restores scrolling when it goes away', () => {
    const w = mount(Modal, { props: { title: 'x' }, attachTo: document.body })
    expect(document.body.style.overflow).toBe('hidden')
    w.unmount()
    expect(document.body.style.overflow).toBe('')
  })
})
