namespace Popo.Core.Contracts.Iss;

public class MoexEmitentDescription
{
    public string SecId { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Inn { get; set; } = string.Empty;    
    public string Okpo { get; set; } = string.Empty;   
    public byte IsTraded { get; set; }
}


