using Popo.Core.Recommendations;

namespace Popo.Core;

public interface ICashFlowProvider
{
    Task<BondYieldSchedule?> GetYieldSchedule(
        BondYieldRequest request,
        CancellationToken ct);
}
