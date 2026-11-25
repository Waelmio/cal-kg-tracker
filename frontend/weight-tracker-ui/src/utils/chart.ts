import { addDays, daysBetween } from './dates'

export interface SeriesPoint { date: string; kg: number; avg7: number }

export interface GoalChartInput {
  series: SeriesPoint[]
  today: string
  start: string
  /** Last day on the x axis: the target date (today, once it has passed). */
  end: string
  targetKg: number
  /** Where the goal line ends. Defaults to the end of the axis. */
  targetDate?: string | null
  startKg: number | null
  /** Today's displayed weight: where the 7-day average line ends. */
  currentKg: number | null
  /** Kg per day. The dashed line runs from today to the end of the axis, or until it meets the target. */
  pacePerDay: number | null
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
  projectionEnd: { x: number; y: number; kg: number; reached: boolean } | null
  goalLine: string
  current: { x: number; y: number; kg: number } | null
}

const PAD = { left: 44, right: 92, top: 20, bottom: 34 }

/** Nice y ticks: a round step that gives 4 to 7 lines. */
export function niceTicks(min: number, max: number): number[] {
  const span = Math.max(max - min, 1)
  const step = [0.5, 1, 2, 5, 10].find((s) => span / s <= 7) ?? 20
  const first = Math.floor(min / step) * step
  const ticks: number[] = []
  for (let v = first; v <= max + step; v += step) ticks.push(Math.round(v * 100) / 100)
  return ticks
}

function projectionEnd(input: GoalChartInput, current: number | null) {
  const { pacePerDay: pace, targetKg } = input
  const left = daysBetween(input.today, input.end)
  if (pace == null || pace === 0 || current == null || left <= 0) return null
  const daysToTarget = (targetKg - current) / pace
  if (daysToTarget > 0 && daysToTarget <= left) return { date: addDays(input.today, Math.round(daysToTarget)), kg: targetKg, reached: true }
  return { date: input.end, kg: current + pace * left, reached: false }
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
  const last = input.series[input.series.length - 1]
  const current = input.currentKg ?? last?.avg7 ?? null
  const atPace = projectionEnd(input, current)
  if (atPace) values.push(atPace.kg)
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

  const line = input.series.map((p) => ({ date: p.date, kg: p.avg7 }))
  if (current != null && last?.date !== input.today) line.push({ date: input.today, kg: current })
  const trend = line.map((p, i) => `${i === 0 ? 'M' : 'L'}${f(x(p.date))} ${f(y(p.kg))}`).join('')

  return {
    width, height, x, y,
    yTicks: ticks.map((kg) => ({ kg, y: f(y(kg)) })),
    xTicks,
    dots: input.series.map((p) => ({ x: f(x(p.date)), y: f(y(p.kg)) })),
    trend,
    projection: atPace && current != null ? `M${f(x(input.today))} ${f(y(current))}L${f(x(atPace.date))} ${f(y(atPace.kg))}` : '',
    projectionEnd: atPace ? { x: f(x(atPace.date)), y: f(y(atPace.kg)), kg: atPace.kg, reached: atPace.reached } : null,
    // From the starting weight to the target: being above this line is being behind.
    goalLine: input.startKg == null
      ? `M${PAD.left} ${f(y(input.targetKg))}H${width - PAD.right}`
      : `M${f(x(input.start))} ${f(y(input.startKg))}L${f(x(input.targetDate ?? input.end))} ${f(y(input.targetKg))}`,
    current: current == null ? null : { x: f(x(input.today)), y: f(y(current)), kg: current },
  }
}
