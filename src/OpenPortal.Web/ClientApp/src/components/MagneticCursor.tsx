import { useEffect, useRef } from 'react'

/** Elements the ring snaps to. `data-magnetic` opts any other element in. */
const INTERACTIVE = 'a[href], button, input, textarea, select, label, [role="checkbox"], [data-magnetic]'
/** Candidates for the proximity glow. Labels are left out: their control is already a candidate. */
const NEARBY = 'a[href], button, input, textarea, select, [role="checkbox"], [data-magnetic]'

const RING_SIZE = 36
const RING_EASE = 0.2
/** How far (px) a snapped element is pulled toward the pointer, at most. */
const PULL_LIMIT = 7
const PULL_STRENGTH = 0.18
const PADDING = 6
/** Distance (px) from an element's edge at which it starts to glow. */
const NEAR_RANGE = 120

interface Pulled {
  element: HTMLElement
  x: number
  y: number
  previousTransition: string
}

function isDisabled(element: Element): boolean {
  return element.matches(':disabled, [aria-disabled="true"]')
}

/** Resolves the control the pointer is over. A label snaps to the control it labels, so the ring hugs one thing. */
function findTarget(source: EventTarget | null): HTMLElement | null {
  if (!(source instanceof Element)) {
    return null
  }

  const match = source.closest<HTMLElement>(INTERACTIVE)

  if (!match || isDisabled(match)) {
    return null
  }

  if (match instanceof HTMLLabelElement && match.control instanceof HTMLElement) {
    return match.control
  }

  return match
}

/** The nearest point of a rectangle to a point (the point itself when inside), and the distance to it. */
function closestPoint(rect: DOMRect, x: number, y: number) {
  const px = Math.min(Math.max(x, rect.left), rect.right)
  const py = Math.min(Math.max(y, rect.top), rect.bottom)

  return { x: px, y: py, distance: Math.hypot(x - px, y - py) }
}

/** The closest enabled interactive element within range, if any. */
function findNearest(x: number, y: number): HTMLElement | null {
  let best: HTMLElement | null = null
  let bestDistance = NEAR_RANGE

  for (const element of document.querySelectorAll<HTMLElement>(NEARBY)) {
    if (isDisabled(element)) {
      continue
    }

    const rect = element.getBoundingClientRect()

    if (rect.width === 0 || rect.height === 0) {
      continue
    }

    const { distance } = closestPoint(rect, x, y)

    if (distance <= bestDistance) {
      best = element
      bestDistance = distance
    }
  }

  return best
}

/**
 * A custom cursor: a dot that tracks the pointer exactly and a ring that trails it.
 *
 * - **Free**: dot plus a lagging ring.
 * - **Near** (within `NEAR_RANGE` of a control but not over it): a soft light spreads from the point where the
 *   two are closest. On the control it lights its edge around the nearest point; on the ring it lights the arc
 *   facing the control. Both spread further along their edges as the pointer gets closer.
 * - **Snapped** (over the control): the ring morphs to the control's box and corner radius, both are lit at
 *   full strength, and the control is pulled a few pixels toward the pointer like a magnet.
 *
 * The glow is a separate overlay positioned over the control, so it never touches the control's own styles
 * (focus rings included). All motion is written straight to the DOM from one requestAnimationFrame loop, so
 * React never re-renders per frame. It only exists for fine pointers (mouse, trackpad); touch devices keep
 * their normal behaviour, and the system cursor is hidden only while this component is mounted.
 */
export function MagneticCursor() {
  const ringRef = useRef<HTMLDivElement>(null)
  const dotRef = useRef<HTMLDivElement>(null)
  const glowRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const ring = ringRef.current
    const dot = dotRef.current
    const glow = glowRef.current

    if (!ring || !dot || !glow || !window.matchMedia('(hover: hover) and (pointer: fine)').matches) {
      return
    }

    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    const ease = reducedMotion ? 1 : RING_EASE

    const pointer = { x: -100, y: -100 }
    // The ring is drawn from its centre, size and radius, each eased toward its target.
    const shape = { x: -100, y: -100, width: RING_SIZE, height: RING_SIZE, radius: RING_SIZE / 2 }
    let target: HTMLElement | null = null
    let near: HTMLElement | null = null
    let glowElement: HTMLElement | null = null
    let glowStrength = 0
    // Where the control is lit (relative to its box) and which way the ring faces it; both glide, never jump.
    const lit = { x: 0, y: 0 }
    const facing = { x: 0, y: -1 }
    let hasLit = false
    let pressed = false
    let visible = false
    let frame = 0
    const pulled = new Map<HTMLElement, Pulled>()

    document.documentElement.classList.add('custom-cursor')

    const release = (element: HTMLElement) => {
      const entry = pulled.get(element)

      if (entry) {
        entry.element.style.translate = ''
        entry.element.style.transition = entry.previousTransition
        pulled.delete(element)
      }
    }

    /** Pulls an element toward the pointer, creating its bookkeeping on first use. */
    const pull = (element: HTMLElement) => {
      const box = element.getBoundingClientRect()
      let entry = pulled.get(element)

      if (!entry) {
        // Our own easing drives the movement; a CSS transition on top of it would only add lag.
        entry = { element, x: 0, y: 0, previousTransition: element.style.transition }
        element.style.transition = 'none'
        pulled.set(element, entry)
      }

      // Measure the unshifted box, otherwise the pull would chase the element it is moving.
      const centerX = box.left + box.width / 2 - entry.x
      const centerY = box.top + box.height / 2 - entry.y
      const wantX = Math.max(-PULL_LIMIT, Math.min(PULL_LIMIT, (pointer.x - centerX) * PULL_STRENGTH))
      const wantY = Math.max(-PULL_LIMIT, Math.min(PULL_LIMIT, (pointer.y - centerY) * PULL_STRENGTH))

      entry.x += (wantX - entry.x) * 0.2
      entry.y += (wantY - entry.y) * 0.2
      element.style.translate = `${entry.x}px ${entry.y}px`

      return box
    }

    const loop = () => {
      let goalX = pointer.x
      let goalY = pointer.y
      let goalWidth = RING_SIZE
      let goalHeight = RING_SIZE
      let goalRadius = RING_SIZE / 2
      let goalGlow = 0
      let nearest: { x: number; y: number } | null = null
      let nearestBox: DOMRect | null = null

      if (target?.isConnected) {
        const box = pull(target)
        const radius = parseFloat(getComputedStyle(target).borderTopLeftRadius) || 8

        goalX = box.left + box.width / 2
        goalY = box.top + box.height / 2
        goalWidth = box.width + PADDING * 2
        goalHeight = box.height + PADDING * 2
        goalRadius = Math.min(radius + PADDING, goalHeight / 2)
        goalGlow = 1
        glowElement = target
        nearest = closestPoint(box, pointer.x, pointer.y)
        nearestBox = box
      } else if (near?.isConnected) {
        const box = near.getBoundingClientRect()
        const closest = closestPoint(box, pointer.x, pointer.y)
        const distance = closest.distance

        if (distance <= NEAR_RANGE) {
          // 0 at the edge of the range, 1 right at the control, with a smooth start.
          const closeness = 1 - distance / NEAR_RANGE

          goalGlow = closeness * closeness * (3 - 2 * closeness)
          glowElement = near
          nearest = closest
          nearestBox = box
        }
      }

      // Elements the pointer has left ease back to rest, then drop their inline styles.
      for (const [element, entry] of pulled) {
        if (element === target) {
          continue
        }

        entry.x *= 0.78
        entry.y *= 0.78

        if (Math.abs(entry.x) < 0.1 && Math.abs(entry.y) < 0.1) {
          release(element)
        } else {
          element.style.translate = `${entry.x}px ${entry.y}px`
        }
      }

      shape.x += (goalX - shape.x) * ease
      shape.y += (goalY - shape.y) * ease
      shape.width += (goalWidth - shape.width) * ease
      shape.height += (goalHeight - shape.height) * ease
      shape.radius += (goalRadius - shape.radius) * ease
      glowStrength += (goalGlow - glowStrength) * 0.14

      if (nearest && nearestBox) {
        const dx = nearest.x - pointer.x
        const dy = nearest.y - pointer.y
        const length = Math.hypot(dx, dy)

        // Over the control the ring is lit all round, so the direction no longer matters; keep the last one.
        if (length > 1) {
          facing.x += (dx / length - facing.x) * 0.25
          facing.y += (dy / length - facing.y) * 0.25
        }

        const wantX = nearest.x - nearestBox.left
        const wantY = nearest.y - nearestBox.top

        if (!hasLit) {
          lit.x = wantX
          lit.y = wantY
          hasLit = true
        }

        lit.x += (wantX - lit.x) * 0.3
        lit.y += (wantY - lit.y) * 0.3
      }

      const scale = pressed ? 0.9 : 1

      ring.style.transform = `translate3d(${shape.x - shape.width / 2}px, ${shape.y - shape.height / 2}px, 0) scale(${scale})`
      ring.style.width = `${shape.width}px`
      ring.style.height = `${shape.height}px`
      ring.style.borderRadius = `${shape.radius}px`
      ring.style.opacity = visible ? '1' : '0'
      // Conic angles run clockwise from 12 o'clock. The lit arc widens from a sliver to the whole ring.
      ring.style.setProperty('--ra', `${(Math.atan2(facing.x, -facing.y) * 180) / Math.PI}deg`)
      ring.style.setProperty('--rs', `${15 + 165 * glowStrength}deg`)
      ring.style.setProperty('--rg', `${glowStrength}`)

      dot.style.transform = `translate3d(${pointer.x - 3}px, ${pointer.y - 3}px, 0)`
      dot.style.opacity = visible && !target ? '1' : '0'

      // The glow keeps tracking the last element it lit while it fades out.
      if (glowElement?.isConnected && glowStrength > 0.01) {
        const box = glowElement.getBoundingClientRect()
        const radius = parseFloat(getComputedStyle(glowElement).borderTopLeftRadius) || 8

        glow.style.transform = `translate3d(${box.left}px, ${box.top}px, 0)`
        glow.style.width = `${box.width}px`
        glow.style.height = `${box.height}px`
        glow.style.borderRadius = `${radius}px`
        glow.style.setProperty('--gx', `${lit.x}px`)
        glow.style.setProperty('--gy', `${lit.y}px`)
        // The lit patch spreads along the edge as the pointer closes in.
        glow.style.setProperty('--gr', `${30 + 190 * glowStrength}px`)
        glow.style.setProperty('--ga', `${glowStrength}`)
        glow.style.opacity = '1'
      } else {
        glow.style.opacity = '0'
        glowElement = null
      }

      frame = requestAnimationFrame(loop)
    }

    const onMove = (event: PointerEvent) => {
      if (!visible) {
        // First movement: appear where the pointer is instead of sweeping in from the corner.
        shape.x = event.clientX
        shape.y = event.clientY
        visible = true
      }

      pointer.x = event.clientX
      pointer.y = event.clientY
      target = findTarget(event.target)
      near = target ? null : findNearest(pointer.x, pointer.y)
    }

    const onLeave = () => {
      visible = false
      target = null
      near = null
    }
    const onDown = () => (pressed = true)
    const onUp = () => (pressed = false)
    const onVisibility = () => {
      if (document.hidden) {
        cancelAnimationFrame(frame)
        frame = 0
      } else if (frame === 0) {
        frame = requestAnimationFrame(loop)
      }
    }

    frame = requestAnimationFrame(loop)

    window.addEventListener('pointermove', onMove)
    document.documentElement.addEventListener('pointerleave', onLeave)
    window.addEventListener('pointerdown', onDown)
    window.addEventListener('pointerup', onUp)
    document.addEventListener('visibilitychange', onVisibility)

    return () => {
      cancelAnimationFrame(frame)
      document.documentElement.classList.remove('custom-cursor')
      for (const element of [...pulled.keys()]) {
        release(element)
      }
      window.removeEventListener('pointermove', onMove)
      document.documentElement.removeEventListener('pointerleave', onLeave)
      window.removeEventListener('pointerdown', onDown)
      window.removeEventListener('pointerup', onUp)
      document.removeEventListener('visibilitychange', onVisibility)
    }
  }, [])

  return (
    <>
      <div
        ref={glowRef}
        aria-hidden="true"
        className="magnetic-glow pointer-events-none fixed top-0 left-0 z-[98] opacity-0"
      >
        <span className="magnetic-glow__fill" />
        <span className="magnetic-glow__edge" />
      </div>
      <div
        ref={ringRef}
        aria-hidden="true"
        className="magnetic-ring border-foreground/70 pointer-events-none fixed top-0 left-0 z-[100] border opacity-0 transition-opacity duration-200"
      >
        <span className="magnetic-ring__edge" />
      </div>
      <div
        ref={dotRef}
        aria-hidden="true"
        className="bg-foreground pointer-events-none fixed top-0 left-0 z-[100] size-1.5 rounded-full opacity-0 transition-opacity duration-150"
      />
    </>
  )
}
