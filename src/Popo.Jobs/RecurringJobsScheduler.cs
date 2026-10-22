using Hangfire;
using Popo.Core.Common;
using Popo.Core.Portfolio.ClosedSessions;
using Popo.Jobs.Jobs;

namespace Popo.Jobs;

public static class RecurringJobsScheduler
{
    private const string RetiredPositionRecommendationJobId = "IPositionRecommendationJob";

    public static void ScheduleAll()
    {
        RecurringJob.AddOrUpdate<IClosedPositionSessionsJob>(
            nameof(IClosedPositionSessionsJob),
            "fast",
            job => job.UpdateAsync(CancellationToken.None),
            "*/15 * * * *",
            new RecurringJobOptions { TimeZone = MoscowTime.Zone });
        Schedule<IMoexBondUpdateJob>("long");
        Schedule<IMoexBondSecuritiesUpdateJob>("long");
        Schedule<IMoexHistoryPricesUpdateJob>("long");
        Schedule<IMoexIndexHistoryUpdateJob>("long");
        Schedule<IMoexAmortsAndCouponsUpdateJob>("long");
        Schedule<ISecUpdateJob>("fast");
        Schedule<IMoexEmitentUpdateJob>("fast");
        Schedule<IBondRatingUpdateJob>("fast");
        Schedule<ICbrCurrencyRatesUpdateJob>("long");
    }

    public static void RemoveAll()
    {
        RecurringJob.RemoveIfExists(nameof(IClosedPositionSessionsJob));
        RecurringJob.RemoveIfExists(nameof(IMoexBondUpdateJob));
        RecurringJob.RemoveIfExists(nameof(IMoexBondSecuritiesUpdateJob));
        RecurringJob.RemoveIfExists(nameof(IMoexHistoryPricesUpdateJob));
        RecurringJob.RemoveIfExists(nameof(IMoexIndexHistoryUpdateJob));
        RecurringJob.RemoveIfExists(nameof(IMoexAmortsAndCouponsUpdateJob));
        RecurringJob.RemoveIfExists(nameof(ISecUpdateJob));
        RecurringJob.RemoveIfExists(nameof(IMoexEmitentUpdateJob));
        RecurringJob.RemoveIfExists(nameof(IBondRatingUpdateJob));
        RecurringJob.RemoveIfExists(nameof(ICbrCurrencyRatesUpdateJob));
        RecurringJob.RemoveIfExists(RetiredPositionRecommendationJobId);
    }

    private static void Schedule<T>(string queue) where T : IHangfireJob => RecurringJob.AddOrUpdate<T>(
        typeof(T).Name,
        queue,
        job => job.UpdateAsync(CancellationToken.None),
        "0 9 * * *",
        new RecurringJobOptions { TimeZone = MoscowTime.Zone });
}
