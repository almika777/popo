using Microsoft.Extensions.DependencyInjection;
using Popo.Core.Initialization;
using Popo.Jobs.Jobs;

namespace Popo.Jobs.Initialization;

public sealed class InitializationBootstrapService(
    IServiceScopeFactory scopeFactory,
    ILogger<InitializationBootstrapService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var setupScope = scopeFactory.CreateAsyncScope();
            var setupStore = setupScope.ServiceProvider.GetRequiredService<IInitializationStateStore>();
            await setupStore.EnsureCurrentBootstrapAsync(stoppingToken);
            await setupStore.RecoverUnfinishedJobsAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IInitializationStateStore>();

                if (await store.IsCompleteAsync(stoppingToken))
                {
                    RecurringJobsScheduler.ScheduleAll();
                    logger.LogInformation("Первичная загрузка данных завершена.");
                    return;
                }

                var jobKey = await store.GetNextPendingJobKeyAsync(stoppingToken);
                if (jobKey is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                if (!await store.StartJobAsync(jobKey, stoppingToken))
                {
                    continue;
                }

                var progressReporter = scope.ServiceProvider.GetRequiredService<IInitializationProgressReporter>();
                progressReporter.Begin(jobKey);
                try
                {
                    await ResolveJob(scope.ServiceProvider, jobKey).UpdateAsync(stoppingToken);
                    await store.MarkSucceededAsync(jobKey, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    await store.MarkFailedAsync(
                        jobKey,
                        "Не удалось выполнить первичную загрузку данных. Подробности доступны в журнале.",
                        stoppingToken);
                    await store.RetryFailedJobAutomaticallyAsync(jobKey, stoppingToken);
                    logger.LogError(exception, "Первичная Job {JobKey} завершилась ошибкой.", jobKey);
                }
                finally
                {
                    progressReporter.End();
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Первичная загрузка данных остановлена.");
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Координатор первичной загрузки данных неожиданно остановился.");
        }
    }

    private static IHangfireJob ResolveJob(IServiceProvider services, string jobKey) =>
        jobKey switch
        {
            "SecUpdateJob" => services.GetRequiredService<ISecUpdateJob>(),
            "MoexBondSecuritiesUpdateJob" => services.GetRequiredService<IMoexBondSecuritiesUpdateJob>(),
            "MoexBondUpdateJob" => services.GetRequiredService<IMoexBondUpdateJob>(),
            "MoexAmortsAndCouponsUpdateJob" => services.GetRequiredService<IMoexAmortsAndCouponsUpdateJob>(),
            "MoexHistoryPricesUpdateJob" => services.GetRequiredService<IMoexHistoryPricesUpdateJob>(),
            "MoexEmitentUpdateJob" => services.GetRequiredService<IMoexEmitentUpdateJob>(),
            "BondRatingUpdateJob" => services.GetRequiredService<IBondRatingUpdateJob>(),
            "CbrCurrencyRatesUpdateJob" => services.GetRequiredService<ICbrCurrencyRatesUpdateJob>(),
            _ => throw new InvalidOperationException($"Неизвестная Job первичной загрузки: {jobKey}")
        };
}
