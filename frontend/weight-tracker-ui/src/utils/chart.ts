import { addDays, daysBetween } from './dates'

export interface SeriesPoint { date: string; kg: number; avg7: number }

export interface GoalChartInput {
  series: SeriesPoint[]
  today: string
  start: string
  /** Last day on the x axis: the target date, or the projected date if later. */
  end: string
  targetKg: number
  startKg: number | null
  /** Where the current pace ends: the date and weight of the dashed line. */
  projection: { date: string; kg: number } | null
  width?: number
  height?: number
}

export interface GoalChartGeometry {
  width: number
  height: number
  x: (date: string) => number
  y: (kg: number) => number
  yTicks: { kg: number; y: number }[]
  xTicks: { date: string; x: number }[]
  dots: { x: number; y: number }[]
  trend: string
  projection: string
  goalLine: string
  current: { x: number; y: number; kg: number } | null
}

const PAD = { left: 44, right: 80, top: 20, bottom: 34 }

/** Nice y ticks: a round step that gives 4 to 7 lines. */
export function niceTicks(min: number, max: number): number[] {
  const span = Math.max(max - min, 1)
  const step = [0.5, 1, 2, 5, 10].find((s) => span / s <= 7) ?? 20
  const first = Math.floor(min / step) * step
  const ticks: number[] = []
  for (let v = first; v <= max + step; v += step) ticks.push(Math.round(v * 100) / 100)
  return ticks
}

/** Time axis: x is proportional to the date, so slopes are true (a categorical axis bends them). */
export function buildGoalChart(input: GoalChartInput): GoalChartGeometry {
  const width = input.width ?? 900
  const height = input.height ?? 270
  const totalDays = Math.max(daysBetween(input.start, input.end), 1)
  const plotW = width - PAD.left - PAD.right
  const plotH = height - PAD.top - PAD.bottom

  const values = [input.targetKg, ...input.series.flatMap((p) => [p.kg, p.avg7])]
  if (input.startKg != null) values.push(input.startKg)
  if (input.projection) values.push(input.projection.kg)
  const lo = Math.min(...values)
  const hi = Math.max(...values)
  const ticks = niceTicks(lo, hi)
  const yMin = ticks[0]
  const yMax = ticks[ticks.length - 1]

  const x = (date: string) => PAD.left + (Math.min(Math.max(daysBetween(input.start, date), 0), totalDays) / totalDays) * plotW
  const y = (kg: number) => PAD.top + (1 - (kg - yMin) / Math.max(yMax - yMin, 0.001)) * plotH
  const f = (n: number) => Math.round(n * 10) / 10

  const xTicks: { date: string; x: number }[] = []
  // First of each month inside the range.
  for (let d = addDays(input.start, 0); daysBetween(d, input.end) >= 0; d = addDays(d, 1)) {
    if (d.endsWith('-01') && d !== input.start) xTicks.push({ date: d, x: f(x(d)) })
  }

  const trend = input.series.map((p, i) => `${i === 0 ? 'M' : 'L'}${f(x(p.date))} ${f(y(p.avg7))}`).join('')
  const last = input.series[input.series.length - 1]
  const projection =
    input.projection && last
      ? `M${f(x(input.today))} ${f(y(last.avg7))}L${f(x(input.projection.date))} ${f(y(input.projection.kg))}`
      : ''

  return {
    width, height, x, y,
    yTicks: ticks.map((kg) => ({ kg, y: f(y(kg)) })),
    xTicks,
    dots: input.series.map((p) => ({ x: f(x(p.date)), y: f(y(p.kg)) })),
    trend,
    projection,
    goalLine: `M${PAD.left} ${f(y(input.targetKg))}H${width - PAD.right}`,
    current: last ? { x: f(x(last.date)), y: f(y(last.avg7)), kg: last.avg7 } : null,
  }
}
