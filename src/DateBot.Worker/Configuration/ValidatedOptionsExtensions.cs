using Microsoft.Extensions.Options;

namespace DateBot.Worker.Configuration;

internal static class ValidatedOptionsExtensions
{
    public static IServiceCollection AddValidatedOptions<T>(this IServiceCollection services, IConfiguration configuration, string section)
        where T : class
    {
        services.AddOptions<T>().Bind(configuration.GetSection(section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<T>, FluentValidateOptions<T>>();
        return services;
    }
}
