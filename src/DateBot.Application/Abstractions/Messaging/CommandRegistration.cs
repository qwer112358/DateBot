using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DateBot.Application.Abstractions.Messaging;

public static class CommandRegistration
{
    public static IServiceCollection AddCommandHandler<TCommand, TResult, THandler>(this IServiceCollection services)
        where TCommand : ICommand<TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        services.AddScoped<THandler>();
        services.AddScoped<ICommandHandler<TCommand, TResult>>(sp => new ValidatingCommandHandler<TCommand, TResult>(
            sp.GetRequiredService<THandler>(), sp.GetServices<IValidator<TCommand>>()));
        return services;
    }
}
