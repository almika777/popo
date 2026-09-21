using Popo.HttpClients.Common;

namespace Popo.HttpClients.Contracts.Iss;

public class MoexBondDescription
{
    [MoexField("SECID")]
    public string SecId { get; set; } = string.Empty;

    [MoexField("ISSUENAME")]
    public string IssueName { get; set; } = string.Empty;

    [MoexField("NAME")]
    public string Name { get; set; } = string.Empty;

    [MoexField("SHORTNAME")]
    public string ShortName { get; set; } = string.Empty;

    [MoexField("REGNUMBER")]
    public string RegNumber { get; set; } = string.Empty;

    [MoexField("ISIN")]
    public string Isin { get; set; } = string.Empty;

    [MoexField("ISSUEDATE")]
    public DateOnly? IssueDate { get; set; }

    [MoexField("MATDATE")]
    public DateOnly? MatDate { get; set; }

    [MoexField("INITIALFACEVALUE")]
    public double? InitialFaceValue { get; set; }

    [MoexField("FACEVALUE")]
    public double FaceValue { get; set; }

    [MoexField("FACEUNIT")]
    public string FaceUnit { get; set; } = string.Empty;

    [MoexField("LATNAME")]
    public string LatName { get; set; } = string.Empty;

    [MoexField("STARTDATEMOEX")]
    public DateOnly? StartDateMoex { get; set; }

    [MoexField("HASPROSPECTUS")]
    public bool? HasProspectus { get; set; }

    [MoexField("DECISIONDATE")]
    public DateOnly? DecisionDate { get; set; }

    [MoexField("ISCONCESSIONAGREEMENT")]
    public bool? IsConcessionAgreement { get; set; }

    [MoexField("HASDEFAULT")]
    public bool? HasDefault { get; set; }

    [MoexField("HASTECHNICALDEFAULT")]
    public bool? HasTechnicalDefault { get; set; }

    [MoexField("PROGRAMREGISTRYNUMBER")]
    public string ProgramRegistryNumber { get; set; } = string.Empty;

    [MoexField("EMITENTMISMATCHCUR")]
    public int? EmitentMismatchCurrent { get; set; }

    [MoexField("LISTLEVEL")]
    public int? ListLevel { get; set; }

    [MoexField("DAYSTOREDEMPTION")]
    public int? DaysToRedemption { get; set; }

    [MoexField("ISSUESIZE")]
    public long IssueSize { get; set; }

    [MoexField("ISQUALIFIEDINVESTORS")]
    public bool? IsQualifiedInvestors { get; set; }

    [MoexField("COUPONFREQUENCY")]
    public int? CouponFrequency { get; set; }

    [MoexField("COUPONDATE")]
    public DateOnly? NextCouponDate { get; set; }

    [MoexField("COUPONPERCENT")]
    public double? CouponPercent { get; set; }

    [MoexField("COUPONVALUE")]
    public double CouponValue { get; set; }

    [MoexField("MORNINGSESSION")]
    public bool? MorningSession { get; set; }

    [MoexField("EVENINGSESSION")]
    public bool? EveningSession { get; set; }

    [MoexField("WEEKENDSESSION")]
    public bool? WeekendSession { get; set; }

    [MoexField("REGISTRY_DATE")]
    public DateOnly? RegistryDate { get; set; }

    [MoexField("BOND_TYPE")]
    public string BondType { get; set; } = string.Empty;

    [MoexField("BOND_SUBTYPE")]
    public string BondSubType { get; set; } = string.Empty;

    [MoexField("TYPENAME")]
    public string TypeName { get; set; } = string.Empty;

    [MoexField("GROUP")]
    public string Group { get; set; } = string.Empty;

    [MoexField("TYPE")]
    public string Type { get; set; } = string.Empty;

    [MoexField("GROUPNAME")]
    public string GroupName { get; set; } = string.Empty;

    [MoexField("EMITTER_ID")]
    public int? EmitterId { get; set; }
    
    public bool IsOfz => Type == "ofz_bond";

    public bool IsCorporateBond =>
        Type is "corporate_bond" or "exchange_bond";

    public bool IsMatured =>
        MatDate.HasValue && MatDate.Value < DateOnly.FromDateTime(DateTime.UtcNow);
}


