using Mapster;

namespace Popo.Jobs.Jobs.Mappings;

public static class MapConfig
{
    public static void Register()
    {
        TypeAdapterConfig<DateTimeOffset?, DateOnly?>.NewConfig()
            .MapWith(src => src.HasValue ? DateOnly.FromDateTime(src.Value.Date) : null);

        TypeAdapterConfig<DateTime?, DateOnly?>.NewConfig()
            .MapWith(src => src.HasValue ? DateOnly.FromDateTime(src.Value.Date) : null);
    }
}

