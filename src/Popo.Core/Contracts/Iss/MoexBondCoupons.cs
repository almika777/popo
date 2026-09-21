namespace Popo.Core.Contracts.Iss;

public class MoexBondization
{
    public List<MoexBondAmortizations> Amortizations { get; set; } = null!;
    public List<MoexBondCoupons> Coupons { get; set; } = null!;

    public class MoexBondCoupons
    {
        /// <summary>Идентификатор финансового инструмента</summary>
        public string? Isin { get; set; }
        public string? SecId { get; set; }
        
        /// <summary>Дата купона</summary>
        public DateOnly CouponDate { get; set; }

        /// <summary>Дата купонного периода, для этого купона</summary>
        public DateOnly StartDate { get; set; }

        /// <summary>Номинал на старте торгов</summary>
        public double InitialFaceValue { get; set; }

        /// <summary>Номинал на момент купона</summary>
        public double FaceValue { get; set; }
    
        /// <summary>Валюта, в которой начисляется купон</summary>
        public string FaceUnit { get; set; } = null!;

        /// <summary>Величина купона</summary>
        public double? Value { get; set; }    
    
        /// <summary>Величина купона в процентах</summary>
        public double? ValuePercent { get; set; }   
    
        /// <summary>Величина купона в рублях</summary>
        public double? ValueInRub { get; set; }   
    
        /// <summary>Приоритетный идентификатор режима торгов</summary>
        public string BoardId { get; set; } = null!;
    }
    public class MoexBondAmortizations
    {
        /// <summary>Идентификатор финансового инструмента</summary>
        public string? Isin { get; set; }        
        public string? SecId { get; set; }

        /// <summary>Дата амортизации</summary>
        public DateOnly AmortDate { get; set; }

        /// <summary>Номинал на старте торгов</summary>
        public double InitialFaceValue { get; set; }

        /// <summary>Номинал на момент купона</summary>
        public double FaceValue { get; set; }
    
        /// <summary>Валюта, в которой начисляется купон</summary>
        public string FaceUnit { get; set; } = null!;

        /// <summary>Величина купона</summary>
        public double? Value { get; set; }    
    
        /// <summary>Величина купона в процентах</summary>
        public double? ValuePercent { get; set; }   
    
        /// <summary>Величина купона в рублях</summary>
        public double? ValueInRub { get; set; }   
    
        /// <summary>Приоритетный идентификатор режима торгов</summary>
        public string BoardId { get; set; } = null!;
    }
}


