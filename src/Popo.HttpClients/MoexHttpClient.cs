using Popo.Core.Contracts.Iss;
using Popo.Core.HttpClients;
using Popo.HttpClients.Common;

namespace Popo.HttpClients;

public sealed class MoexHttpClient(MoexIssClient client) : IMoexHttpClient
{
    public async Task<Dictionary<string, List<MoexBond>>> GetActiveBondsSecuritiesAsync(CancellationToken ct)
    {
        return (await client.GetActiveBondsSecuritiesAsync(ct))
            .ToDictionary(x => x.Key, x => x.Value.Select(value => value.ToContract()).ToList());
    }

    public async Task<Dictionary<string, List<MoexMarketdataYields>>> GetActiveBondsMarketdataYieldsAsync(CancellationToken ct) =>
        (await client.GetActiveBondsMarketdataYieldsAsync(ct)).ToDictionary(x => x.Key, x => x.Value.Select(value => value.ToContract()).ToList());

    public async Task<Dictionary<string, List<MoexMarketdata>>> GetActiveBondsMarketdataAsync(CancellationToken ct)
    {
        return (await client.GetActiveBondsMarketdataAsync(ct))
            .ToDictionary(x => x.Key, x => x.Value.Select(value => value.ToContract()).ToList());
    }

    public async Task<MoexBondization> GetFutureAmortsAndCoupons(string secId, CancellationToken ct) =>
        (await client.GetFutureAmortsAndCoupons(secId, ct)).ToContract();

    public async Task<List<MoexHistoryYields>> GetBondHistoryPageAsync(string secId, string boardId, DateOnly? from = null, CancellationToken ct = default) =>
        (await client.GetBondHistoryPageAsync(secId, boardId, from, ct)).Select(value => value.ToContract()).ToList();

    public async Task<MoexMarketdata> GetMoneyMarketFundMarketdataAsync(string secId, string boardId, CancellationToken ct) =>
        (await client.GetMoneyMarketFundMarketdataAsync(secId, boardId, ct)).ToContract();

    public async Task<List<MoexTrade>> GetBondTradesAsync(string secId, string boardId, CancellationToken ct) =>
        (await client.GetBondTradesAsync(secId, boardId, ct)).Select(value => value.ToContract()).ToList();

    public async Task<List<SecDescription>> GetActiveSecAsync(CancellationToken ct) =>
        (await client.GetActiveSecAsync(ct)).Select(value => value.ToContract()).ToList();

    public async Task<MoexBondDescription> GetSecurityDescriptionAsync(string secId, CancellationToken ct) =>
        (await client.GetSecurityDescriptionAsync(secId, ct)).ToContract();

    public async Task<List<MoexEmitentDescription>> GetAllEmitentDescriptionAsync(CancellationToken ct) =>
        (await client.GetAllEmitentDescriptionAsync(ct)).Select(value => value.ToContract()).ToList();

}
