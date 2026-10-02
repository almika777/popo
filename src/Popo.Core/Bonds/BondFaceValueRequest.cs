namespace Popo.Core.Bonds;

public sealed record BondFaceValueRequest(string SecId, string BoardId, DateOnly TradeDate);

public sealed record BondFaceValueResult(string SecId, string BoardId, DateOnly TradeDate, double? FaceValue);
