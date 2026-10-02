namespace Popo.Api.Services.BrokerReports;

public interface IBrokerReportPdfParser
{
    ParsedBrokerReport Parse(Stream pdfStream);
}
