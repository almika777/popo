namespace Popo.Core.Portfolio.Position;

public sealed record OpenPositionCoupon(
    string SecId,
    DateOnly StartDate,
    DateOnly CouponDate,
    double? Value,
    string FaceUnit);
