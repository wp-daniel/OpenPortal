import { useQuery } from '@tanstack/react-query'
import { Check, ChevronsUpDown, Loader2, MapPin, PenLine, X } from 'lucide-react'
import { useState, type ComponentProps } from 'react'
import { geoApi, geoKeys, type GeoPlace } from '@/api/geo'
import { GeoAttribution } from '@/components/GeoAttribution'
import { Button } from '@/components/ui/button'
import { Command, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { useDebounced } from '@/hooks/useDebounced'
import { useI18n } from '@/i18n/useI18n'
import { isCountryCode } from '@/lib/countries'
import { cn } from '@/lib/utils'

/**
 * City picker scoped to the chosen country. It offers, in order: the places behind the postal code already
 * entered, towns matching what is typed (looked up on the server), and the typed text itself, so a place the
 * providers do not know can still be entered. Picking a place that carries a postal code passes it along.
 *
 * Extra props (`id`, `aria-invalid`, `aria-describedby` from `FormField`) go to the trigger button.
 */
export function CityCombobox({
  value,
  onChange,
  country,
  postalCode,
  postalPlaces,
  ...trigger
}: {
  value: string
  onChange: (city: string, place?: GeoPlace) => void
  country: string
  postalCode: string
  /** Places sharing the entered postal code, offered first. */
  postalPlaces: readonly GeoPlace[]
} & Omit<ComponentProps<typeof Button>, 'value' | 'onChange'>) {
  const { t } = useI18n()
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const term = useDebounced(search.trim(), 300)
  const scoped = isCountryCode(country) ? country.toUpperCase() : null

  const cities = useQuery({
    queryKey: geoKeys.cities(scoped ?? '', term.toLowerCase()),
    queryFn: ({ signal }) => geoApi.cities(scoped!, term, signal),
    enabled: open && scoped !== null && term.length >= 2,
    staleTime: Infinity,
    // A suggestion that failed is simply not shown; the typed text can always be used instead.
    retry: false,
  })

  const needle = search.trim().toLocaleLowerCase()
  const fromPostalCode = postalPlaces.filter((place) => place.city.toLocaleLowerCase().includes(needle))
  const fromSearch = (cities.data?.places ?? []).filter(
    (place) => !fromPostalCode.some((other) => other.city === place.city && other.region === place.region),
  )
  const typed = search.trim()
  // A search for exactly what is typed has come back (not an earlier, shorter term still on screen).
  const searched = scoped !== null && typed.length >= 2 && term === typed && cities.data?.available === true

  function choose(city: string, place?: GeoPlace) {
    onChange(city, place)
    setSearch('')
    setOpen(false)
  }

  const item = (place: GeoPlace, key: string) => (
    <CommandItem key={key} value={key} onSelect={() => choose(place.city, place)}>
      <MapPin className="text-muted-foreground" />
      <div className="grid min-w-0">
        <span className="truncate">{place.city}</span>
        {(place.region || place.postalCode) && (
          <span className="text-muted-foreground truncate text-xs">
            {[place.region, place.postalCode].filter(Boolean).join(' · ')}
          </span>
        )}
      </div>
      <Check className={cn('ml-auto', value === place.city ? 'opacity-100' : 'opacity-0')} />
    </CommandItem>
  )

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          {...trigger}
          variant="outline"
          role="combobox"
          aria-expanded={open}
          className="w-full justify-between font-normal"
        >
          <span className={cn('truncate', !value && 'text-muted-foreground')}>
            {value || t('address.city.placeholder')}
          </span>
          <ChevronsUpDown className="opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-(--radix-popover-trigger-width) min-w-64 p-0" align="start">
        <Command shouldFilter={false}>
          <CommandInput value={search} onValueChange={setSearch} placeholder={t('address.city.search')} />
          <CommandList>
            {!scoped && <p className="text-muted-foreground px-3 py-2 text-xs">{t('address.city.pickCountry')}</p>}
            {cities.isFetching && (
              <div className="text-muted-foreground flex items-center gap-2 px-3 py-2 text-sm">
                <Loader2 className="size-4 animate-spin" />
                {t('common.loading')}
              </div>
            )}
            {cities.data?.available === false && (
              <p className="text-muted-foreground px-3 py-2 text-xs">{t('address.unavailable')}</p>
            )}
            {/* Say what to do rather than "nothing found" before anything has been searched. */}
            {scoped && typed.length < 2 && fromPostalCode.length === 0 && (
              <p className="text-muted-foreground px-3 py-2 text-xs">{t('address.city.typeToSearch')}</p>
            )}
            {searched && !cities.isFetching && fromSearch.length === 0 && fromPostalCode.length === 0 && (
              <p className="text-muted-foreground px-3 py-2 text-xs">{t('address.city.none')}</p>
            )}

            {fromPostalCode.length > 0 && (
              <CommandGroup heading={t('address.city.forPostalCode', { code: postalCode })}>
                {fromPostalCode.map((place) => item(place, `postal:${place.city}:${place.region ?? ''}`))}
              </CommandGroup>
            )}

            {fromSearch.length > 0 && (
              <CommandGroup heading={t('address.city.results')}>
                {fromSearch.map((place) => item(place, `search:${place.city}:${place.region ?? ''}`))}
              </CommandGroup>
            )}

            {(typed || value) && (
              <CommandGroup>
                {typed && (
                  <CommandItem value={`typed:${typed}`} onSelect={() => choose(typed)}>
                    <PenLine className="text-muted-foreground" />
                    <span className="truncate">{t('address.city.useTyped', { text: typed })}</span>
                  </CommandItem>
                )}
                {value && (
                  <CommandItem value="clear" onSelect={() => choose('')}>
                    <X className="text-muted-foreground" />
                    {t('address.city.clear')}
                  </CommandItem>
                )}
              </CommandGroup>
            )}
          </CommandList>
          <GeoAttribution
            sources={[
              ...(fromPostalCode.length > 0 ? (['geonames'] as const) : []),
              ...(fromSearch.length > 0 ? (['osm'] as const) : []),
            ]}
            className="border-t px-3 py-2"
          />
        </Command>
      </PopoverContent>
    </Popover>
  )
}
