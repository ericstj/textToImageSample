// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering <see cref="IChatClient"/> as a singleton in the services provided by the <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireOpenAIClientBuilderResponseChatClientExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="IChatClient"/> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">An <see cref="AspireOpenAIClientBuilder" />.</param>
    /// <param name="deploymentName">Optionally specifies which model deployment to use. If not specified, a value will be taken from the connection string.</param>
    /// <returns>A <see cref="ChatClientBuilder"/> that can be used to build a pipeline around the inner <see cref="IChatClient"/>.</returns>
    public static ChatClientBuilder AddResponseChatClient(
        this AspireOpenAIClientBuilder builder,
        string? deploymentName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.HostBuilder.Services.AddChatClient(
            services => CreateInnerChatClient(services, builder, deploymentName));
    }

    /// <summary>
    /// Registers a keyed singleton <see cref="IChatClient"/> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">An <see cref="AspireOpenAIClientBuilder" />.</param>
    /// <param name="serviceKey">The service key with which the <see cref="IChatClient"/> will be registered.</param>
    /// <param name="deploymentName">Optionally specifies which model deployment to use. If not specified, a value will be taken from the connection string.</param>
    /// <returns>A <see cref="ChatClientBuilder"/> that can be used to build a pipeline around the inner <see cref="IChatClient"/>.</returns>
    public static ChatClientBuilder AddKeyedResponseChatClient(
        this AspireOpenAIClientBuilder builder,
        string serviceKey,
        string? deploymentName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(serviceKey);

        return builder.HostBuilder.Services.AddKeyedChatClient(
            serviceKey,
            services => CreateInnerChatClient(services, builder, deploymentName));
    }

    internal static string GetRequiredDeploymentName(
        this AspireOpenAIClientBuilder builder)
    {
        const string DeploymentKey = "Deployment";
        const string ModelKey = "Model";
        string? deploymentName = null;

        var configuration = builder.HostBuilder.Configuration;
        if (configuration.GetConnectionString(builder.ConnectionName) is string connectionString)
        {
            // The reason we accept either 'Deployment' or 'Model' as the key is because OpenAI's terminology
            // is 'Model' and Azure OpenAI's terminology is 'Deployment'. It may seem awkward if we picked just
            // one of these, as it might not match the usage scenario. We could restrict it based on which backend
            // you're using, but that adds an unnecessary failure case for no clear benefit.
            var connectionBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            var deploymentValue = ConnectionStringValue(connectionBuilder, DeploymentKey);
            var modelValue = ConnectionStringValue(connectionBuilder, ModelKey);
            if (deploymentValue is not null && modelValue is not null)
            {
                throw new InvalidOperationException(
                    $"The connection string '{builder.ConnectionName}' contains both '{DeploymentKey}' and '{ModelKey}' keys. Either of these may be specified, but not both.");
            }

            deploymentName = deploymentValue ?? modelValue;
        }

        if (string.IsNullOrEmpty(deploymentName))
        {
            var configSection = configuration.GetSection(builder.ConfigurationSectionName);
            deploymentName = configSection[DeploymentKey];
        }

        if (string.IsNullOrEmpty(deploymentName))
        {
            throw new InvalidOperationException($"The deployment could not be determined. Ensure a '{DeploymentKey}' or '{ModelKey}' value is provided in 'ConnectionStrings:{builder.ConnectionName}', or specify a '{DeploymentKey}' in the '{builder.ConfigurationSectionName}' configuration section, or specify a '{nameof(deploymentName)}' in the call.");
        }

        return deploymentName;
    }

    private static string? ConnectionStringValue(DbConnectionStringBuilder connectionString, string key)
        => connectionString.TryGetValue(key, out var value) ? value as string : null;

    /// <summary>
    /// Wrap the <see cref="OpenAIClient"/> in a telemetry client if tracing is enabled.
    /// Note that this doesn't use ".UseOpenTelemetry()" because the order of the clients would be incorrect.
    /// We want the telemetry client to be the innermost client, right next to the inner <see cref="OpenAIClient"/>.
    /// </summary>
    private static IChatClient CreateInnerChatClient(
        IServiceProvider services,
        AspireOpenAIClientBuilder builder,
        string? deploymentName)
    {
        var openAiClient = builder.ServiceKey is null
            ? services.GetRequiredService<OpenAIClient>()
            : services.GetRequiredKeyedService<OpenAIClient>(builder.ServiceKey);

        deploymentName ??= builder.GetRequiredDeploymentName();
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var result = openAiClient.GetOpenAIResponseClient(deploymentName).AsIChatClient();
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        if (builder.DisableTracing)
        {
            return result;
        }

        var loggerFactory = services.GetService<ILoggerFactory>();
        return new OpenTelemetryChatClient(result, loggerFactory?.CreateLogger(typeof(OpenTelemetryChatClient)));
    }


}