using Popo.Core.Contracts.Iss;

namespace Popo.Core.HttpClients;

public interface IMoexHttpClient
{
    Task<Dictionary<string, List<MoexBond>>> GetActiveBondsSecuritiesAsync(CancellationToken ct);

    Task<MoexBondization> GetFutureAmortsAndCoupons(string secId, CancellationToken ct);

    Task<List<MoexHistoryYields>> GetBondHistoryPageAsync(
        string secId,
        string boardId,
        DateOnly? from = null,
        CancellationToken ct = default);

    Task<Dictionary<string, List<MoexMarketdataYields>>> GetActiveBondsMarketdataYieldsAsync(CancellationToken ct);
    Task<Dictionary<string, List<MoexMarketdata>>> GetActiveBondsMarketdataAsync(CancellationToken ct);
    Task<MoexMarketdata> GetMoneyMarketFundMarketdataAsync(string secId, string boardId, CancellationToken ct);
    Task<List<MoexTrade>> GetBondTradesAsync(string secId, string boardId, CancellationToken ct);
    Task<List<SecDescription>> GetActiveSecAsync(CancellationToken ct);
    Task<MoexBondDescription> GetSecurityDescriptionAsync(string secId, CancellationToken ct);
    Task<List<MoexEmitentDescription>> GetAllEmitentDescriptionAsync(CancellationToken ct);
}