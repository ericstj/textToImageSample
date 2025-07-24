// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering <see cref="ITextToImageClient"/> as a singleton in the services provided by the <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireOpenAIClientBuilderTextToImageClientExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="ITextToImageClient"/> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">An <see cref="AspireOpenAIClientBuilder" />.</param>
    /// <param name="modelName">Optionally specifies which model deployment to use. If not specified, a value will be taken from the connection string.</param>
    /// <returns>A <see cref="TextToImageClientBuilder"/> that can be used to build a pipeline around the inner <see cref="ITextToImageClient"/>.</returns>
    public static TextToImageClientBuilder AddTextToImageClient(
        this AspireOpenAIClientBuilder builder,
        string? modelName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.HostBuilder.Services.AddTextToImageClient(
            services => CreateInnerTextToImageClient(services, builder, modelName));
    }

    /// <summary>
    /// Registers a keyed singleton <see cref="ITextToImageClient"/> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">An <see cref="AspireOpenAIClientBuilder" />.</param>
    /// <param name="serviceKey">The service key with which the <see cref="ITextToImageClient"/> will be registered.</param>
    /// <param name="modelName">Optionally specifies which model deployment to use. If not specified, a value will be taken from the connection string.</param>
    /// <returns>A <see cref="TextToImageClientBuilder"/> that can be used to build a pipeline around the inner <see cref="ITextToImageClient"/>.</returns>
    public static TextToImageClientBuilder AddKeyedTextToImageClient(
        this AspireOpenAIClientBuilder builder,
        string serviceKey,
        string? modelName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(serviceKey);

        return builder.HostBuilder.Services.AddKeyedTextToImageClient(
            serviceKey,
            services => CreateInnerTextToImageClient(services, builder, modelName));
    }

    /// <summary>
    /// Wrap the <see cref="OpenAIClient"/> in a telemetry client if tracing is enabled.
    /// Note that this doesn't use ".UseOpenTelemetry()" because the order of the clients would be incorrect.
    /// We want the telemetry client to be the innermost client, right next to the inner <see cref="OpenAIClient"/>.
    /// </summary>
    private static ITextToImageClient CreateInnerTextToImageClient(
        IServiceProvider services,
        AspireOpenAIClientBuilder builder,
        string? modelName)
    {
        // can we somehow deduce the model name from the connection string?
        // modelName ??= builder.ConnectionString?.GetModelName()
        //    ?? throw new InvalidOperationException("Model name must be specified either in the connection string or as a parameter.");

        var openAiClient = builder.ServiceKey is null
            ? services.GetRequiredService<OpenAIClient>()
            : services.GetRequiredKeyedService<OpenAIClient>(builder.ServiceKey);

        var result = openAiClient.GetImageClient(modelName).AsITextToImageClient();

        if (builder.DisableTracing)
        {
            return result;
        }

        var loggerFactory = services.GetService<ILoggerFactory>();
        // should this be OpenTelemetryTextToImageClient?
        return new LoggingTextToImageClient(
            result,
            loggerFactory?.CreateLogger(typeof(LoggingTextToImageClient)));
    }
}