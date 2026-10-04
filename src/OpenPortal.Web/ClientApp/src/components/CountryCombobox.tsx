import { Check, ChevronsUpDown, Globe } from 'lucide-react'
import { useMemo, useState, type ComponentProps } from 'react'
import { Button } from '@/components/ui/button'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { useI18n } from '@/i18n/useI18n'
import { COUNTRY_CODES, countryName, flagUrl, isCountryCode } from '@/lib/countries'
import { cn } from '@/lib/utils'

/** A small flag; decorative, since the country's name is always written next to it. */
export function CountryFlag({ code, className }: { code: string; className?: string }) {
  return (
    <img
      src={flagUrl(code)}
      alt=""
      loading="lazy"
      className={cn('h-3.5 w-5 shrink-0 rounded-[2px] object-cover ring-1 ring-black/10', className)}
    />
  )
}

/**
 * Country picker with flags, searchable by name (in the UI language) or code. The value is the ISO 3166
 * alpha-2 code; an older free-text value is still shown until another country is picked.
 *
 * Extra props (`id`, `aria-invalid`, `aria-describedby` from `FormField`) go to the trigger button.
 */
export function CountryCombobox({
  value,
  onChange,
  ...trigger
}: {
  value: string
  onChange: (code: string) => void
} & Omit<ComponentProps<typeof Button>, 'value' | 'onChange'>) {
  const { t, language } = useI18n()
  const [open, setOpen] = useState(false)

  const countries = useMemo(() => {
    const collator = new Intl.Collator(language)

    return COUNTRY_CODES.map((code) => ({ code, name: countryName(code, language) })).sort((a, b) =>
      collator.compare(a.name, b.name),
    )
  }, [language])

  const selected = isCountryCode(value) ? value.toUpperCase() : null

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
          <span className="flex min-w-0 items-center gap-2">
            {selected ? <CountryFlag code={selected} /> : <Globe className="text-muted-foreground" />}
            <span className={cn('truncate', !value && 'text-muted-foreground')}>
              {selected ? countryName(selected, language) : value || t('address.country.placeholder')}
            </span>
          </span>
          <ChevronsUpDown className="opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-(--radix-popover-trigger-width) min-w-64 p-0" align="start">
        <Command>
          <CommandInput placeholder={t('address.country.search')} />
          <CommandList>
            <CommandEmpty>{t('address.country.none')}</CommandEmpty>
            <CommandGroup>
              {countries.map((country) => (
                <CommandItem
                  key={country.code}
                  value={country.code}
                  keywords={[country.name]}
                  onSelect={() => {
                    onChange(country.code)
                    setOpen(false)
                  }}
                >
                  <CountryFlag code={country.code} />
                  <span className="truncate">{country.name}</span>
                  <span className="text-muted-foreground ml-auto font-mono text-xs">{country.code}</span>
                  <Check className={cn(selected === country.code ? 'opacity-100' : 'opacity-0')} />
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  )
}
