// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Options;
using Microsoft.Shared.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.AI;

/// <summary>Provides extension methods for working with Stability AI services.</summary>
[Experimental("MEAI001")]
public static class StabilityAIExtensions
{
    /// <summary>Creates an <see cref="ITextToImageClient"/> for the Stability AI service.</summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for HTTP requests. If null, a default HttpClient will be used.</param>
    /// <param name="apiKey">The API key to authenticate with the Stability AI service.</param>
    /// <param name="model">The model to use for image generation. If not specified, defaults to "sd3-large".</param>
    /// <param name="endpoint">The endpoint URI for the Stability AI service. If not specified, uses the default endpoint.</param>
    /// <returns>An <see cref="ITextToImageClient"/> configured for Stability AI.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="apiKey"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> is empty or whitespace.</exception>
    public static IImageClient AsStabilityAITextToImageClient(
        this HttpClient? httpClient,
        string apiKey,
        string? model = null,
        Uri? endpoint = null)
    {
        _ = Throw.IfNullOrWhitespace(apiKey);

        if (httpClient is null)
        {
            httpClient = new HttpClient();
            return new StabilityAITextToImageClient(httpClient, apiKey, model, endpoint, disposeHttpClient: true);
        }

        return new StabilityAITextToImageClient(httpClient, apiKey, model, endpoint, disposeHttpClient: false);
    }

    /// <summary>Creates an <see cref="ITextToImageClient"/> for the Stability AI service using a new HttpClient.</summary>
    /// <param name="apiKey">The API key to authenticate with the Stability AI service.</param>
    /// <param name="model">The model to use for image generation. If not specified, defaults to "sd3-large".</param>
    /// <param name="endpoint">The endpoint URI for the Stability AI service. If not specified, uses the default endpoint.</param>
    /// <returns>An <see cref="ITextToImageClient"/> configured for Stability AI.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="apiKey"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> is empty or whitespace.</exception>
    public static IImageClient CreateStabilityAITextToImageClient(
        string apiKey,
        string? model = null,
        Uri? endpoint = null)
    {
        _ = Throw.IfNullOrWhitespace(apiKey);

        var httpClient = new HttpClient();
        return new StabilityAITextToImageClient(httpClient, apiKey, model, endpoint, disposeHttpClient: true);
    }

    /// <summary>Adds a Stability AI text-to-image client to the service collection.</summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the client to.</param>
    /// <param name="configure">An optional callback to configure the <see cref="StabilityAIOptions"/>.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>A <see cref="TextToImageClientBuilder"/> that can be used to build a pipeline around the client.</returns>
    public static ImageClientBuilder AddStabilityAITextToImageClient(
        this IServiceCollection services,
        Action<StabilityAIOptions>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        _ = Throw.IfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services.AddImageClient(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<StabilityAIOptions>>().Value;
            var httpClient = serviceProvider.GetRequiredService<HttpClient>();
            
            if (string.IsNullOrWhiteSpace(options.Key))
            {
                throw new InvalidOperationException("StabilityAI API key is required. Configure it via StabilityAIOptions.ApiKey.");
            }

            return new StabilityAITextToImageClient(httpClient, options.Key, options.Model, options.Endpoint, disposeHttpClient: false);
        }, lifetime);
    }

    /// <summary>Adds a Stability AI text-to-image client to the service collection with configuration from a configuration section.</summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the client to.</param>
    /// <param name="configurationSection">The configuration section containing Stability AI options.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>A <see cref="TextToImageClientBuilder"/> that can be used to build a pipeline around the client.</returns>
    public static ImageClientBuilder AddStabilityAITextToImageClient(
        this IServiceCollection services,
        IConfigurationSection configurationSection,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        _ = Throw.IfNull(services);
        _ = Throw.IfNull(configurationSection);

        services.Configure<StabilityAIOptions>(configurationSection);

        return services.AddImageClient(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<StabilityAIOptions>>().Value;
            var httpClient = serviceProvider.GetRequiredService<HttpClient>();
            
            if (string.IsNullOrWhiteSpace(options.Key))
            {
                throw new InvalidOperationException("StabilityAI API key is required. Configure it via StabilityAIOptions.ApiKey or the configuration section.");
            }

            return new StabilityAITextToImageClient(httpClient, options.Key, options.Model, options.Endpoint, disposeHttpClient: false);
        }, lifetime);
    }

    /// <summary>Adds a keyed Stability AI text-to-image client to the service collection.</summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the client to.</param>
    /// <param name="serviceKey">The key with which to associate the client.</param>
    /// <param name="configure">An optional callback to configure the <see cref="StabilityAIOptions"/>.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>A <see cref="TextToImageClientBuilder"/> that can be used to build a pipeline around the client.</returns>
    public static ImageClientBuilder AddKeyedStabilityAITextToImageClient(
        this IServiceCollection services,
        object serviceKey,
        Action<StabilityAIOptions>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        _ = Throw.IfNull(services);
        _ = Throw.IfNull(serviceKey);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services.AddKeyedImageClient(serviceKey, serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<StabilityAIOptions>>().Value;
            var httpClient = serviceProvider.GetRequiredService<HttpClient>();
            
            if (string.IsNullOrWhiteSpace(options.Key))
            {
                throw new InvalidOperationException("StabilityAI API key is required. Configure it via StabilityAIOptions.ApiKey.");
            }

            return new StabilityAITextToImageClient(httpClient, options.Key, options.Model, options.Endpoint, disposeHttpClient: false);
        }, lifetime);
    }

    /// <summary>Adds a Stability AI text-to-image client to the host application builder.</summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to add the client to.</param>
    /// <param name="connectionName">The name of the connection string or configuration section containing Stability AI options.</param>
    /// <param name="configureOptions">An optional callback to configure the <see cref="StabilityAIOptions"/>.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>A <see cref="TextToImageClientBuilder"/> that can be used to build a pipeline around the client.</returns>
    public static ImageClientBuilder AddStabilityAITextToImageClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<StabilityAIOptions>? configureOptions = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        _ = Throw.IfNull(builder);
        _ = Throw.IfNullOrWhitespace(connectionName);

        // First try to get it as a connection string, then as a configuration section
        var connectionString = builder.Configuration.GetConnectionString(connectionName);
        if (!string.IsNullOrEmpty(connectionString))
        {
            // If it's a connection string, parse it as the API key
            builder.Services.Configure<StabilityAIOptions>(options =>
            {
                options.Key = connectionString;
                configureOptions?.Invoke(options);
            });
        }
        else
        {
            // Otherwise, bind from configuration section
            var configurationSection = builder.Configuration.GetSection(connectionName);
            builder.Services.Configure<StabilityAIOptions>(configurationSection);
            
            if (configureOptions is not null)
            {
                builder.Services.Configure(configureOptions);
            }
        }

        return builder.Services.AddImageClient(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<StabilityAIOptions>>().Value;
            var httpClient = serviceProvider.GetRequiredService<HttpClient>();
            
            if (string.IsNullOrWhiteSpace(options.Key))
            {
                throw new InvalidOperationException($"StabilityAI API key is required. Configure it via connection string '{connectionName}' or configuration section '{connectionName}:ApiKey'.");
            }

            return new StabilityAITextToImageClient(httpClient, options.Key, options.Model, options.Endpoint, disposeHttpClient: false);
        }, lifetime);
    }
}
