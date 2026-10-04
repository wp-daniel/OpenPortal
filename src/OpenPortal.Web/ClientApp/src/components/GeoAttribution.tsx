import { useI18n } from '@/i18n/useI18n'
import { cn } from '@/lib/utils'

/** Where a suggestion comes from: postal codes from GeoNames (via Zippopotam.us), towns from OpenStreetMap (via Photon). */
export type GeoSource = 'geonames' | 'osm'

const SOURCES: Record<GeoSource, { label: string; href: string }> = {
  osm: { label: '© OpenStreetMap contributors', href: 'https://www.openstreetmap.org/copyright' },
  geonames: { label: 'GeoNames', href: 'https://www.geonames.org/' },
}

/**
 * The credit the address data's licenses ask for (OpenStreetMap: ODbL; GeoNames: CC BY 4.0). Shown wherever
 * suggestions from them are on screen, and only then, so a portal with lookups turned off shows nothing.
 * The source names are not translated: they are the attribution text the projects ask for.
 */
export function GeoAttribution({ sources, className }: { sources: readonly GeoSource[]; className?: string }) {
  const { t } = useI18n()

  if (sources.length === 0) {
    return null
  }

  return (
    <p className={cn('text-muted-foreground text-xs', className)}>
      {t('address.attribution')}{' '}
      {sources.map((source, index) => (
        <span key={source}>
          {index > 0 && ' · '}
          <a
            href={SOURCES[source].href}
            target="_blank"
            rel="noreferrer"
            className="hover:text-foreground underline underline-offset-2"
          >
            {SOURCES[source].label}
          </a>
        </span>
      ))}
    </p>
  )
}
