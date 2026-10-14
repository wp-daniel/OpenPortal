import { useState } from 'react'
import { PageHeader } from '@/components/PageHeader'
import { PeriodPicker, StatisticsOverview } from '@/components/StatisticsOverview'
import type { StatisticsPeriod } from '@/lib/statistics'
import { useI18n } from '@/i18n/useI18n'

/**
 * The administrators' overview in full: key figures, sign-ins per day, what needs attention, the most used
 * applications and where failed sign-ins come from. Computed from the audit log, so it covers what the log keeps.
 */
export function AdminStatisticsPage() {
  const { t } = useI18n()
  const [days, setDays] = useState<StatisticsPeriod>(30)

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('statistics.title')}
        description={t('statistics.description')}
        actions={<PeriodPicker value={days} onChange={setDays} />}
      />

      <StatisticsOverview days={days} />
    </div>
  )
}
