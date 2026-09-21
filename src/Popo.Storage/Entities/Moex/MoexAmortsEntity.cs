namespace Popo.Storage.Entities.Moex;

public class MoexAmortsEntity
{
    public string SecId { get; set; } = null!;
    public string Isin { get; set; } = null!;
    public DateOnly AmortDate { get; set; }
    public double InitialFaceValue { get; set; }
    public double FaceValue { get; set; }
    public string FaceUnit { get; set; } = null!;
    public double? Value { get; set; }
    public double? ValuePercent { get; set; }
    public double? ValueInRub { get; set; }
    public string BoardId { get; set; } = null!;
}

