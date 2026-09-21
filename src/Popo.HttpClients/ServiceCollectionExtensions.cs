using Microsoft.Extensions.DependencyInjection;
using Popo.Core.HttpClients;

namespace Popo.HttpClients;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPopoHttpClients(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient("MoexHttpClient", client =>
        {
            client.BaseAddress = new Uri("https://iss.moex.com/iss/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddScoped<MoexIssClient>();
        services.AddScoped<IMoexHttpClient, MoexHttpClient>();
        services.AddHttpClient<ICbondClient, CbondClient>(client =>
        {
            client.BaseAddress = new Uri("https://corpbonds.ru/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddHttpClient<ICbrHttpClient, CbrHttpClient>(client =>
        {
            client.BaseAddress = new Uri("https://www.cbr.ru/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/xml");
        });
        return services;
    }
}
