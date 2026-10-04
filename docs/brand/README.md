# OpenPortal brand

| File | Use |
| --- | --- |
| `openportal-logo.svg` | full logo (symbol + wordmark), black, transparent background — for light surfaces |
| `openportal-logo-white.svg` | the same in white — for dark surfaces |
| `openportal-logo.png`, `openportal-logo-white.png` | raster logo, 1800 × 448, on its own background |
| `openportal-symbol.svg`, `openportal-symbol-white.svg` | the symbol alone, black / white |
| `openportal-symbol.png` | raster symbol, 1024 px wide |
| `openportal-app-icon.png` | rounded square app icon, 512 × 512 |

The portal itself serves its favicon set (`favicon.ico`, `favicon.svg`, `apple-touch-icon.png`, `icon-192.png`,
`icon-512.png`, `site.webmanifest`) and the two symbol SVGs from `src/OpenPortal.Web/ClientApp/public`.
`favicon.svg` switches between black and white with the operating system's colour scheme; inside the app the
`BrandMark` component picks the symbol that matches the app's own theme.

The mark is monochrome on purpose: black on light, white on dark, no accent colour. Do not stretch, recolour or outline it.
