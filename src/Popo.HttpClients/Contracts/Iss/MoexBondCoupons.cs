using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public class MoexBondization
{
    public List<MoexBondAmortizations> Amortizations { get; set; } = null!;
    public List<MoexBondCoupons> Coupons { get; set; } = null!;

    public class MoexBondCoupons
    {
        /// <summary>Идентификатор финансового инструмента</summary>
        [JsonPropertyName("isin")]
        public string? Isin { get; set; }
        
        [JsonPropertyName("secid")]
        public string? SecId { get; set; }
        
        /// <summary>Дата купона</summary>
        [JsonPropertyName("coupondate")]
        public DateOnly CouponDate { get; set; }

        /// <summary>Дата купонного периода, для этого купона</summary>
        [JsonPropertyName("startdate")]
        public DateOnly StartDate { get; set; }

        /// <summary>Номинал на старте торгов</summary>
        [JsonPropertyName("initialfacevalue")]
        public double InitialFaceValue { get; set; }

        /// <summary>Номинал на момент купона</summary>
        [JsonPropertyName("facevalue")]
        public double FaceValue { get; set; }
    
        /// <summary>Валюта, в которой начисляется купон</summary>
        [JsonPropertyName("faceunit")]
        public string FaceUnit { get; set; } = null!;

        /// <summary>Величина купона</summary>
        [JsonPropertyName("value")]
        public double? Value { get; set; }    
    
        /// <summary>Величина купона в процентах</summary>
        [JsonPropertyName("valueprc")]
        public double? ValuePercent { get; set; }   
    
        /// <summary>Величина купона в рублях</summary>
        [JsonPropertyName("value_rub")]
        public double? ValueInRub { get; set; }   
    
        /// <summary>Приоритетный идентификатор режима торгов</summary>
        [JsonPropertyName("primary_boardid")]
        public string BoardId { get; set; } = null!;
    }
    public class MoexBondAmortizations
    {
        /// <summary>Идентификатор финансового инструмента</summary>
        [JsonPropertyName("isin")]
        public string? Isin { get; set; }        
        
        [JsonPropertyName("secid")]
        public string? SecId { get; set; }

        /// <summary>Дата амортизации</summary>
        [JsonPropertyName("amortdate")]
        public DateOnly AmortDate { get; set; }

        /// <summary>Номинал на старте торгов</summary>
        [JsonPropertyName("initialfacevalue")]
        public double InitialFaceValue { get; set; }

        /// <summary>Номинал на момент купона</summary>
        [JsonPropertyName("facevalue")]
        public double FaceValue { get; set; }
    
        /// <summary>Валюта, в которой начисляется купон</summary>
        [JsonPropertyName("faceunit")]
        public string FaceUnit { get; set; } = null!;

        /// <summary>Величина купона</summary>
        [JsonPropertyName("value")]
        public double? Value { get; set; }    
    
        /// <summary>Величина купона в процентах</summary>
        [JsonPropertyName("valueprc")]
        public double? ValuePercent { get; set; }   
    
        /// <summary>Величина купона в рублях</summary>
        [JsonPropertyName("value_rub")]
        public double? ValueInRub { get; set; }   
    
        /// <summary>Приоритетный идентификатор режима торгов</summary>
        [JsonPropertyName("primary_boardid")]
        public string BoardId { get; set; } = null!;
    }
}


