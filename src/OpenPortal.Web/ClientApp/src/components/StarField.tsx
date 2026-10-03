import { useEffect, useRef } from 'react'

const STAR_COUNT = 160
/** How far (px) the nearest stars shift against the pointer. */
const PARALLAX = 16

interface Star {
  /** Position as a 0..1 fraction of the canvas, so a resize keeps the layout. */
  x: number
  y: number
  /** 0..1 depth: deeper stars are smaller, dimmer and move less with the pointer. */
  depth: number
  phase: number
  speed: number
}

/**
 * A quiet starfield: tiny stars that twinkle and shift slightly against the pointer for depth.
 *
 * Colour comes from the theme's foreground token, so it is light on dark and dark on light. One
 * requestAnimationFrame loop, paused while the tab is hidden; a single still frame under reduced motion.
 */
export function StarField() {
  const canvasRef = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    const canvas = canvasRef.current
    const context = canvas?.getContext('2d')

    if (!canvas || !context) {
      return
    }

    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    const stars: Star[] = Array.from({ length: STAR_COUNT }, () => ({
      x: Math.random(),
      y: Math.random(),
      depth: Math.random(),
      phase: Math.random() * Math.PI * 2,
      speed: 0.4 + Math.random() * 1.2,
    }))
    // Pointer offset from the centre, -1..1; `smooth` eases toward it so the parallax never jumps.
    const pointer = { x: 0, y: 0 }
    const smooth = { x: 0, y: 0 }
    let width = 0
    let height = 0
    let frame = 0
    let ink = 'white'

    const readTheme = () => {
      ink = getComputedStyle(document.documentElement).getPropertyValue('--foreground').trim() || ink
    }

    const resize = () => {
      const ratio = window.devicePixelRatio || 1
      width = canvas.clientWidth
      height = canvas.clientHeight
      canvas.width = Math.round(width * ratio)
      canvas.height = Math.round(height * ratio)
      context.setTransform(ratio, 0, 0, ratio, 0, 0)
    }

    const draw = (time: number) => {
      smooth.x += (pointer.x - smooth.x) * 0.05
      smooth.y += (pointer.y - smooth.y) * 0.05

      context.clearRect(0, 0, width, height)
      context.fillStyle = ink

      const t = time / 1000

      for (const star of stars) {
        const shift = (1 - star.depth) * PARALLAX
        const x = star.x * width - smooth.x * shift
        const y = star.y * height - smooth.y * shift
        const twinkle = reducedMotion ? 0.7 : 0.55 + 0.45 * Math.sin(t * star.speed + star.phase)

        context.globalAlpha = (0.3 + (1 - star.depth) * 0.6) * twinkle
        context.beginPath()
        context.arc(x, y, 0.6 + (1 - star.depth) * 1.2, 0, Math.PI * 2)
        context.fill()
      }

      context.globalAlpha = 1
    }

    const loop = (time: number) => {
      draw(time)
      frame = requestAnimationFrame(loop)
    }

    const start = () => {
      if (!reducedMotion && frame === 0) {
        frame = requestAnimationFrame(loop)
      }
    }

    const stop = () => {
      cancelAnimationFrame(frame)
      frame = 0
    }

    const onPointer = (event: PointerEvent) => {
      pointer.x = (event.clientX / width) * 2 - 1
      pointer.y = (event.clientY / height) * 2 - 1
    }

    const onVisibility = () => (document.hidden ? stop() : start())

    readTheme()
    resize()
    draw(performance.now())
    start()

    const themeObserver = new MutationObserver(() => {
      readTheme()
      if (reducedMotion) {
        draw(performance.now())
      }
    })
    themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })

    // A ResizeObserver also fires for the first real size, which the canvas may not have at mount.
    const resizeObserver = new ResizeObserver(() => {
      resize()
      if (reducedMotion) {
        draw(performance.now())
      }
    })
    resizeObserver.observe(canvas)

    window.addEventListener('pointermove', onPointer)
    document.addEventListener('visibilitychange', onVisibility)

    return () => {
      stop()
      themeObserver.disconnect()
      resizeObserver.disconnect()
      window.removeEventListener('pointermove', onPointer)
      document.removeEventListener('visibilitychange', onVisibility)
    }
  }, [])

  return <canvas ref={canvasRef} aria-hidden="true" className="pointer-events-none absolute inset-0 size-full" />
}
