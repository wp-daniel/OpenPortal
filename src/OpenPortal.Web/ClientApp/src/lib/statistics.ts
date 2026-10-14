import { useQuery } from '@tanstack/react-query'
import { statisticsApi, statisticsKeys } from '@/api/statistics'

/** The periods offered, in days; the server accepts 1 to 90. */
export const STATISTICS_PERIODS = [7, 30, 90] as const
export type StatisticsPeriod = (typeof STATISTICS_PERIODS)[number]

export function useStatistics(days: number) {
  return useQuery({
    queryKey: statisticsKeys.period(days),
    queryFn: ({ signal }) => statisticsApi.get(days, signal),
    placeholderData: (previous) => previous,
    staleTime: 60_000,
  })
}
