using Hangfire;
using Popo.Core.Common;
using Popo.Jobs.Jobs;

namespace Popo.Jobs;

public static class RecurringJobsScheduler
{
    public static void ScheduleAll()
    {
        Schedule<IMoexBondUpdateJob>("long");
        Schedule<IMoexBondSecuritiesUpdateJob>("long");
        Schedule<IMoexHistoryPricesUpdateJob>("long");
        Schedule<IMoexAmortsAndCouponsUpdateJob>("long");
        Schedule<ISecUpdateJob>("fast");
        Schedule<IMoexEmitentUpdateJob>("fast");
        Schedule<IBondRatingUpdateJob>("fast");
        Schedule<ICbrCurrencyRatesUpdateJob>("long");
        RecurringJob.AddOrUpdate<IPositionRecommendationJob>(
            nameof(IPositionRecommendationJob),
            "fast",
            job => job.UpdateAsync(CancellationToken.None),
            "*/15 * * * *",
            new RecurringJobOptions { TimeZone = MoscowTime.Zone });
    }

    public static void RemoveAll()
    {
        RecurringJob.RemoveIfExists(nameof(IMoexBondUpdateJob));
        RecurringJob.RemoveIfExists(nameof(IMoexBondSecuritiesUpdateJob));
        RecurringJob.RemoveIfExists(nameof(IMoexHistoryPricesUpdateJob));
        RecurringJob.RemoveIfExists(nameof(IMoexAmortsAndCouponsUpdateJob));
        RecurringJob.RemoveIfExists(nameof(ISecUpdateJob));
        RecurringJob.RemoveIfExists(nameof(IMoexEmitentUpdateJob));
        RecurringJob.RemoveIfExists(nameof(IBondRatingUpdateJob));
        RecurringJob.RemoveIfExists(nameof(ICbrCurrencyRatesUpdateJob));
        RecurringJob.RemoveIfExists(nameof(IPositionRecommendationJob));
    }

    private static void Schedule<T>(string queue) where T : IHangfireJob => RecurringJob.AddOrUpdate<T>(
        typeof(T).Name,
        queue,
        job => job.UpdateAsync(CancellationToken.None),
        "0 9 * * *",
        new RecurringJobOptions { TimeZone = MoscowTime.Zone });
}
