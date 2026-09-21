namespace Popo.Core.Contracts.Iss;

public class MoexBondDescription
{
    public string SecId { get; set; } = string.Empty;
    public string IssueName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string RegNumber { get; set; } = string.Empty;
    public string Isin { get; set; } = string.Empty;
    public DateOnly? IssueDate { get; set; }
    public DateOnly? MatDate { get; set; }
    public double? InitialFaceValue { get; set; }
    public double FaceValue { get; set; }
    public string FaceUnit { get; set; } = string.Empty;
    public string LatName { get; set; } = string.Empty;
    public DateOnly? StartDateMoex { get; set; }
    public bool? HasProspectus { get; set; }
    public DateOnly? DecisionDate { get; set; }
    public bool? IsConcessionAgreement { get; set; }
    public bool? HasDefault { get; set; }
    public bool? HasTechnicalDefault { get; set; }
    public string ProgramRegistryNumber { get; set; } = string.Empty;
    public int? EmitentMismatchCurrent { get; set; }
    public int? ListLevel { get; set; }
    public int? DaysToRedemption { get; set; }
    public long IssueSize { get; set; }
    public bool? IsQualifiedInvestors { get; set; }
    public int? CouponFrequency { get; set; }
    public DateOnly? NextCouponDate { get; set; }
    public double? CouponPercent { get; set; }
    public double CouponValue { get; set; }
    public bool? MorningSession { get; set; }
    public bool? EveningSession { get; set; }
    public bool? WeekendSession { get; set; }
    public DateOnly? RegistryDate { get; set; }
    public string BondType { get; set; } = string.Empty;
    public string BondSubType { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int? EmitterId { get; set; }
    
    public bool IsOfz => Type == "ofz_bond";

    public bool IsCorporateBond =>
        Type is "corporate_bond" or "exchange_bond";

    public bool IsMatured =>
        MatDate.HasValue && MatDate.Value < DateOnly.FromDateTime(DateTime.UtcNow);
}


