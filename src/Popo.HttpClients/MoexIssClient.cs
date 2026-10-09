using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Popo.Core.Common;
using Popo.HttpClients.Contracts.Iss;
using Popo.HttpClients.Common;

using Popo.HttpClients.Serialization.Iss;

namespace Popo.HttpClients
{
    public sealed class MoexIssClient
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan MoneyMarketFundCacheDuration = TimeSpan.FromHours(1);
        private static readonly DateOnly LqdtIndexFirstCalculationDate = new(2022, 7, 22);
        private static readonly DateOnly LqdtFirstTradingDate = new(2022, 7, 22);
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> CacheLocks = new();

        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        public MoexIssClient(IHttpClientFactory client, IMemoryCache cache)
        {
            _httpClient = client.CreateClient("MoexHttpClient");
            _httpClient.BaseAddress = new Uri("https://iss.moex.com/iss/");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _cache = cache;
        }

        private async Task<T> GetOrCreateWithLockAsync<T>(
            string cacheKey, Func<Task<T>> factory, CancellationToken ct,
            TimeSpan? cacheDuration = null) where T : class
        {
            if (_cache.TryGetValue<T>(cacheKey, out var existing) && existing is not null)
            {
                return existing;
            }

            var semaphore = CacheLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(ct);
            try
            {
                if (_cache.TryGetValue<T>(cacheKey, out var recheck) && recheck is not null)
                {
                    return recheck;
                }

                var created = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = cacheDuration ?? CacheDuration;
                    return await factory();
                });
                return created ?? throw new InvalidOperationException($"Фабрика кеша вернула пустой результат для {cacheKey}.");
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<Dictionary<string, List<MoexBond>>> GetActiveBondsSecuritiesAsync(CancellationToken ct)
        {
            return await GetOrCreateWithLockAsync(nameof(GetActiveBondsSecuritiesAsync), async () =>
            {
                return await GetBoardDictionaryAsync<MoexBond>(
                    RelationHelper.BoardCurrency.Keys,
                    boardId =>
                        $"engines/stock/markets/bonds/boards/{boardId}/securities.json?iss.only=securities",
                    "securities", ct);
            }, ct);
        }

        public async Task<Dictionary<string, List<MoexMarketdataYields>>> GetActiveBondsMarketdataYieldsAsync(
            CancellationToken ct)
        {
            return (await GetOrCreateWithLockAsync(nameof(GetActiveBondsMarketdataYieldsAsync), async () =>
            {
                const string columns = "SECID,BOARDID,PRICE,WAPRICE,TRADEMOMENT,EFFECTIVEYIELD";

                return await GetBoardDictionaryAsync<MoexMarketdataYields>(
                    RelationHelper.BoardCurrency.Keys,
                    boardId =>
                        $"engines/stock/markets/bonds/boards/{boardId}/securities.json?iss.only=marketdata_yields&columns={columns}",
                    "marketdata_yields", ct);
            }, ct))!;
        }

        public async Task<Dictionary<string, List<MoexMarketdata>>> GetActiveBondsMarketdataAsync(CancellationToken ct)
        {
            return (await GetOrCreateWithLockAsync(nameof(GetActiveBondsMarketdataAsync), async () =>
            {
                const string columns =
                    "SECID,BOARDID,YIELD,LAST,LCURRENTPRICE,MARKETPRICE2,MARKETPRICE,DURATION,LCLOSEPRICE,CLOSEPRICE,SYSTIME";

                return await GetBoardDictionaryAsync<MoexMarketdata>(
                    RelationHelper.BoardCurrency.Keys,
                    boardId =>
                        $"engines/stock/markets/bonds/boards/{boardId}/securities.json?iss.only=marketdata&columns={columns}",
                    "marketdata", ct);
            }, ct))!;
        }

        public async Task<List<MoexHistoryYields>> GetBondHistoryAsync(DateTimeOffset date, int start, int limit,
            CancellationToken ct)
        {
            var url = $"history/engines/stock/markets/bonds/securities.json" +
                      $"?date={date:yyyy-MM-dd}" +
                      $"&start={start}" +
                      $"&limit={limit}";

            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseMoexResult<MoexHistoryYields>(json, "history");
        }

        public Task<MoexMarketdata> GetMoneyMarketFundMarketdataAsync(
            string secId,
            string boardId,
            CancellationToken ct)
        {
            var cacheKey = $"money-market-fund-marketdata:{secId}:{boardId}";
            return GetOrCreateWithLockAsync(cacheKey, async () =>
            {
                var encodedSecId = Uri.EscapeDataString(secId);
                var encodedBoardId = Uri.EscapeDataString(boardId);
                const string columns =
                    "SECID,BOARDID,LAST,LCURRENTPRICE,MARKETPRICE2,MARKETPRICE,LCLOSEPRICE,CLOSEPRICE,SYSTIME";
                var url = $"engines/stock/markets/shares/boards/{encodedBoardId}/securities/{encodedSecId}.json" +
                          $"?iss.meta=off&iss.only=marketdata&marketdata.columns={columns}";
                var quote = (await GetCollectionAsync<MoexMarketdata>(url, "marketdata", ct)).FirstOrDefault();
                return quote ?? throw new InvalidOperationException($"MOEX не вернул котировку для {secId}/{boardId}.");
            }, ct, MoneyMarketFundCacheDuration);
        }

        public async Task<List<MoexIndexHistory>> GetLqdtIndexHistoryAsync(CancellationToken ct)
        {
            const int pageSize = 100;
            var start = 0;
            var result = new List<MoexIndexHistory>();

            while (true)
            {
                var url =
                    "history/engines/stock/markets/index/securities/LQDTM.json" +
                    $"?iss.meta=off&iss.only=history,history.cursor&from={LqdtIndexFirstCalculationDate:yyyy-MM-dd}" +
                    $"&history.columns=TRADEDATE,SECID,CLOSE&start={start}&limit={pageSize}";

                using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var history = ParseMoexResult<MoexIndexHistory>(json, "history");
                var cursor = ParseMoexResult<MoexHistoryCursor>(json, "history.cursor").FirstOrDefault();

                if (history.Count == 0)
                    break;

                result.AddRange(history);

                var nextStart = GetNextHistoryStart(
                    start,
                    history.Count,
                    pageSize,
                    cursor,
                    "MOEX вернул некорректный курсор истории LQDTM.");
                if (nextStart is null)
                    break;

                start = nextStart.Value;
            }

            result.AddRange(await GetLqdtDailyPriceHistoryAsync(ct));
            return result
                .OrderBy(value => value.SecId)
                .ThenBy(value => value.TradeDate)
                .ToList();
        }

        private async Task<List<MoexIndexHistory>> GetLqdtDailyPriceHistoryAsync(CancellationToken ct)
        {
            const int pageSize = 500;
            var start = 0;
            var result = new List<MoexIndexHistory>();

            while (true)
            {
                var url =
                    "engines/stock/markets/shares/securities/LQDT/candles.json" +
                    $"?iss.meta=off&iss.only=candles,candles.cursor&interval=24&from={LqdtFirstTradingDate:yyyy-MM-dd}" +
                    $"&start={start}&limit={pageSize}";

                using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var candles = ParseMoexResult<MoexDailyCandle>(json, "candles");
                var cursor = ParseMoexResult<MoexHistoryCursor>(json, "candles.cursor").FirstOrDefault();
                result.AddRange(candles
                    .Where(candle => candle.Begin is not null && candle.Close is > 0)
                    .Select(candle => new MoexIndexHistory
                    {
                        SecId = "LQDT",
                        TradeDate = candle.Begin,
                        Close = candle.Close
                    }));

                if (candles.Count == 0)
                    break;

                var nextStart = GetNextHistoryStart(
                    start,
                    candles.Count,
                    pageSize,
                    cursor,
                    "MOEX вернул некорректный курсор дневных цен LQDT.");
                if (nextStart is null)
                    break;

                start = nextStart.Value;
            }

            return result;
        }

        private static int? GetNextHistoryStart(
            int start,
            int pageCount,
            int requestedPageSize,
            MoexHistoryCursor? cursor,
            string invalidCursorMessage)
        {
            if (pageCount == 0)
                return null;

            if (cursor is null)
                return pageCount < requestedPageSize ? null : start + pageCount;

            var actualPageSize = cursor.PageSize > 0 ? cursor.PageSize : pageCount;
            var nextStart = cursor.Index + actualPageSize;
            if (nextStart >= cursor.Total)
                return null;
            if (nextStart <= start)
                throw new InvalidOperationException(invalidCursorMessage);

            return nextStart;
        }

        public async Task<MoexBondization> GetFutureAmortsAndCoupons(string secId, CancellationToken ct)
        {
            const int pageSize = 100;
            var start = 0;
            var encodedSecId = Uri.EscapeDataString(secId);
            var coupons = new List<MoexBondization.MoexBondCoupons>();
            var amorts = new List<MoexBondization.MoexBondAmortizations>();

            while (true)
            {
                var url = $"statistics/engines/stock/markets/bonds/bondization/{encodedSecId}.json" +
                          $"?iss.meta=off&iss.only=coupons,coupons.cursor,amortizations,amortizations.cursor" +
                          $"&start={start}&limit={pageSize}";

                using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var couponPage = ParseMoexResult<MoexBondization.MoexBondCoupons>(json, "coupons");
                var amortizationPage = ParseMoexResult<MoexBondization.MoexBondAmortizations>(json, "amortizations");
                coupons.AddRange(couponPage);
                amorts.AddRange(amortizationPage);

                var couponCursor = ParseMoexResult<MoexHistoryCursor>(json, "coupons.cursor").FirstOrDefault();
                var amortizationCursor = ParseMoexResult<MoexHistoryCursor>(json, "amortizations.cursor")
                    .FirstOrDefault();
                var total = Math.Max(couponCursor?.Total ?? 0, amortizationCursor?.Total ?? 0);
                if ((couponPage.Count == 0 && amortizationPage.Count == 0)
                    || couponCursor is null && amortizationCursor is null
                    || start + pageSize >= total)
                    break;

                start += pageSize;
            }

            return new MoexBondization
            {
                Amortizations = amorts,
                Coupons = coupons
            };
        }

        public async Task<List<MoexHistoryYields>> GetBondHistoryPageAsync(
            string secId,
            string boardId,
            DateOnly? from = null,
            CancellationToken ct = default)
        {
            var start = 0;
            var encodedSecId = Uri.EscapeDataString(secId);
            var encodedBoardId = Uri.EscapeDataString(boardId);

            var res = new List<MoexHistoryYields>();

            while (true)
            {
                var url =
                    $"history/engines/stock/markets/bonds/boards/{encodedBoardId}/securities/{encodedSecId}.json" +
                    $"?iss.meta=off&iss.only=history,history.cursor&start={start}&limit=100" +
                    (from.HasValue ? $"&from={from.Value:yyyy-MM-dd}" : "");

                using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var history = ParseMoexResult<MoexHistoryYields>(json, "history");
                var cursor = ParseMoexResult<MoexHistoryCursor>(json, "history.cursor").FirstOrDefault();

                if (history.Count == 0 || cursor is null || start >= cursor.Total)
                    break;

                start += 100;
                res.AddRange(history);
            }

            return res;
        }

        public async Task<List<MoexTrade>> GetBondTradesAsync(string secId, string boardId, CancellationToken ct)
        {
            const int limit = 100;
            var start = 0;
            var encodedSecId = Uri.EscapeDataString(secId);
            var encodedBoardId = Uri.EscapeDataString(boardId);

            var result = new List<MoexTrade>();

            while (true)
            {
                var url = $"engines/stock/markets/bonds/boards/{encodedBoardId}/securities/{encodedSecId}/trades.json" +
                          $"?iss.meta=off&iss.only=trades,trades.cursor&start={start}&limit={limit}";

                using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var trades = ParseMoexResult<MoexTrade>(json, "trades");
                var cursor = ParseMoexResult<MoexHistoryCursor>(json, "trades.cursor").FirstOrDefault();

                if (trades.Count == 0)
                    break;

                result.AddRange(trades);

                if (cursor is null || start + limit >= cursor.Total)
                    break;

                start += limit;
            }

            return result;
        }

        public async Task<MoexBondDescription> GetSecurityDescriptionAsync(string secId, CancellationToken ct)
        {
            var encodedSecId = Uri.EscapeDataString(secId);
            var url = $"securities/{encodedSecId}.json";

            var properties = await GetCollectionAsync<MoexSingleRow>(url, "description", ct);
            return MoexMapper.Map<MoexBondDescription>(properties.ToDictionary(x => x.Name, x => x.Value));
        }

        public async Task<List<MoexEmitentDescription>> GetAllEmitentDescriptionAsync(CancellationToken ct)
        {
            var start = 0;
            var result = new List<MoexEmitentDescription>();
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var url =
                    $"securities.json?group_by=group&group_by_filter=stock_bonds&iss.meta=off&iss.only=securities&start={start}";

                var descriptions = await GetCollectionAsync<MoexEmitentDescription>(url, "securities", ct);

                if (descriptions.Count == 0 || descriptions.All(x => x.IsTraded != 1))
                    break;

                result.AddRange(descriptions.Where(x => x.IsTraded == 1));
                start += 100;
            }

            return result.DistinctBy(x => x.Id).ToList();
        }

        public async Task<List<SecDescription>> GetActiveSecAsync(CancellationToken ct)
        {
            var start = 0;
            var result = new List<SecDescription>();
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var url =
                    $"securities.json?group_by=group&group_by_filter=stock_bonds&iss.meta=off&iss.only=securities&start={start}";

                var descriptions = await GetCollectionAsync<SecDescription>(url, "securities", ct);

                if (descriptions.Count == 0 || descriptions.All(x => x.IsTraded != 1))
                    break;

                result.AddRange(descriptions.Where(x => x.IsTraded == 1));
                start += 100;
            }

            return result;
        }

        private async Task<Dictionary<string, List<T>>> GetBoardDictionaryAsync<T>(
            IEnumerable<string> boardIds,
            Func<string, string> urlFactory,
            string rootElement,
            CancellationToken ct) where T : class, new()
        {
            var result = new ConcurrentDictionary<string, List<T>>();

            await Parallel.ForEachAsync(boardIds, ct, async (boardId, innerCt) =>
            {
                var payload = await GetCollectionAsync<T>(urlFactory(boardId), rootElement, innerCt);
                if (payload.Count == 0)
                    return;

                result.TryAdd(boardId, payload);
            });

            return result.ToDictionary();
        }

        private async Task<List<T>> GetCollectionAsync<T>(
            string url,
            string rootElement,
            CancellationToken ct) where T : class, new()
        {
            using var response = await _httpClient
                .GetAsync(url, ct)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseMoexResult<T>(json, rootElement);
        }

        private List<T> ParseMoexResult<T>(string json, string rootElement) where T : class, new()
        {
            using var jsonDocument = JsonDocument.Parse(json);
            if (!jsonDocument.RootElement.TryGetProperty(rootElement, out var securitiesBlock))
                return [];

            var options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new DateOnlyConvertor());

            var moexResponse = JsonSerializer.Deserialize<MoexResponse<T>>(securitiesBlock.GetRawText(), options);
            return moexResponse is { Data: not null }
                ? moexResponse.GetTypedData(options).ToList()
                : [];
        }
    }
}
