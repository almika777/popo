using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Popo.Core.Contracts;
using Popo.Core.HttpClients;

namespace Popo.HttpClients;

public sealed class CbrHttpClient(HttpClient httpClient) : ICbrHttpClient
{
    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    static CbrHttpClient()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<IReadOnlyList<CbrCurrencyDefinitionDto>> GetCurrenciesAsync(
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("scripts/XML_daily.asp", cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.Root?.Elements("Valute")
                   .Select(x => new CbrCurrencyDefinitionDto(
                       x.Attribute("ID")?.Value ?? string.Empty,
                       GetRequiredValue(x, "CharCode"),
                       GetRequiredValue(x, "Name")))
                   .Where(x => x.CbrCode.Length > 0 && x.CurrencyCode.Length > 0)
                   .ToArray()
               ?? [];
    }

    public async Task<IReadOnlyList<CbrCurrencyRateDto>> GetCurrencyRatesAsync(
        CbrCurrencyDefinitionDto currency,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var query = $"scripts/XML_dynamic.asp?date_req1={FormatDate(from)}&date_req2={FormatDate(to)}&VAL_NM_RQ={Uri.EscapeDataString(currency.CbrCode)}";
        using var response = await httpClient.GetAsync(query, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.Root?.Elements("Record")
                   .Select(x =>
                   {
                       var nominal = ParseDouble(GetRequiredValue(x, "Nominal"));
                       var value = ParseDouble(GetRequiredValue(x, "Value"));
                       var unitRate = x.Element("VunitRate") is { } unitRateElement
                           ? ParseDouble(unitRateElement.Value)
                           : value / nominal;
                       return new CbrCurrencyRateDto(
                           DateOnly.ParseExact(x.Attribute("Date")?.Value ?? string.Empty, "dd.MM.yyyy", RussianCulture),
                           currency.CurrencyCode,
                           currency.Name,
                           nominal,
                           value,
                           unitRate);
                   })
                   .ToArray()
               ?? [];
    }

    private static string FormatDate(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string GetRequiredValue(XElement element, string name) =>
        element.Element(name)?.Value.Trim()
        ?? throw new InvalidOperationException($"В ответе ЦБ отсутствует поле '{name}'.");

    private static double ParseDouble(string value) =>
        double.Parse(value.Replace(',', '.'), CultureInfo.InvariantCulture);
}
