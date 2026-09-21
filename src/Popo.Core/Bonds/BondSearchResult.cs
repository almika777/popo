namespace Popo.Core.Bonds;

public sealed record BondSearchResult(
    string SecId,
    string BoardId,
    string Currency,
    string ShortName,
    double AccruedInterest,
    double FaceValue);
