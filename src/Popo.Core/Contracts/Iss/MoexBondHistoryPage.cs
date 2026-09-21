namespace Popo.Core.Contracts.Iss;

public class MoexBondHistoryPage
{
    public IReadOnlyList<MoexHistoryYields> History { get; set; } = [];

    public int Index { get; set; }

    public int Total { get; set; }

    public int PageSize { get; set; }
}



