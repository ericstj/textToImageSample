// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Shared.Diagnostics;
using System.Net.Http.Headers;

namespace Microsoft.Extensions.AI;

/// <summary>Represents an <see cref="ITextToImageClient"/> for Stability AI.</summary>
internal sealed class StabilityAITextToImageClient : ITextToImageClient
{
    /// <summary>The default Stability AI API endpoint.</summary>
    public static readonly Uri DefaultBaseEndpoint = new Uri("https://api.stability.ai/v2beta/stable-image/generate/");

    /// <summary>
    /// Gets the default model to use for image generation if none is specified.
    /// </summary>
    public static string DefaultModel => "sd3-large";

    /// <summary>Metadata about the client.</summary>
    private readonly TextToImageClientMetadata _metadata;

    /// <summary>The underlying <see cref="HttpClient" />.</summary>
    private readonly HttpClient _httpClient;

    /// <summary>The API key for authentication.</summary>
    private readonly string _apiKey;

    /// <summary>The model to use for image generation.</summary>
    private readonly string _model;

    /// <summary>Whether this instance owns the HttpClient and should dispose it.</summary>
    private readonly bool _disposeHttpClient;

    /// <summary>Initializes a new instance of the <see cref="StabilityAITextToImageClient"/> class.</summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="model">The model to use for image generation. Defaults to "sd3-large".</param>
    /// <param name="endpoint">The API endpoint. Defaults to the standard Stability AI endpoint.</param>
    /// <param name="disposeHttpClient">Whether to dispose the HttpClient when this instance is disposed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> or <paramref name="apiKey"/> is <see langword="null"/>.</exception>
    public StabilityAITextToImageClient(
        HttpClient httpClient, 
        string apiKey, 
        string? model = null, 
        Uri? endpoint = null,
        bool disposeHttpClient = false)
    {
        _httpClient = Throw.IfNull(httpClient);
        _apiKey = Throw.IfNullOrWhitespace(apiKey);
        _model = model ?? DefaultModel;
        _disposeHttpClient = disposeHttpClient;

        endpoint ??= GetDefaultEndpointForModel(_model);
        _metadata = new("stability-ai", endpoint, _model);
    }

    /// <inheritdoc />
    public async Task<TextToImageResponse> GenerateImagesAsync(string prompt, TextToImageOptions? options = null, CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(prompt);

        var properties = CreateRequestProperties(prompt, options);

        var content = CreateRequestContent(properties);

        return await SendRequestAsync(content, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TextToImageResponse> EditImagesAsync(
        IEnumerable<AIContent> originalImages, string prompt, TextToImageOptions? options = null, CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(originalImages);
        _ = Throw.IfNull(prompt);

        var properties = CreateRequestProperties(prompt, options);

        // we can use the image as a starting point for the generation and set strength to 0.85
        if (!properties.ContainsKey("strength"))
        {
            properties["strength"] = "0.85"; // Default strength for image editing
        }
        properties["mode"] = "image-to-image";

        var content = CreateRequestContent(properties);

        ByteArrayContent? imageContent = null;
        foreach (AIContent originalImage in originalImages)
        {
            if (imageContent is not null)
            {
                // We only support a single image for editing.
                Throw.ArgumentException(
                    "Only a single image can be provided for editing.",
                    nameof(originalImages));
            }

            if (originalImage is DataContent dataContent)
            {
                
                imageContent = new ByteArrayContent(dataContent.Data.ToArray());
                imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(dataContent.MediaType);
                string name = dataContent.Name ?? dataContent.MediaType switch
                {
                    "image/png" => "image.png",
                    "image/jpeg" => "image.jpg",
                    "image/webp" => "image.webp",
                    _ => "image"
                };
                content.Add(imageContent, "\"image\"", $"\"{name}\"");
            }
            else
            {
                // We might be able to handle UriContent by downloading the image, but need to plumb an HttpClient for that.
                // For now, we only support DataContent for image editing as OpenAI's API expects image data in a stream.
                Throw.ArgumentException(
                    "The original image must be a DataContent instance containing image data.",
                    nameof(originalImages));
            }
        }

        return await SendRequestAsync(content, cancellationToken);
    }

    private async Task<TextToImageResponse> SendRequestAsync(MultipartFormDataContent content, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _metadata.ProviderUri)
        {
            Content = content
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorContent = await httpResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Stability AI API request failed with status {httpResponse.StatusCode}: {errorContent}");
        }

        var responseBytes = await httpResponse.Content.ReadAsByteArrayAsync();
        var contentType = httpResponse.Content.Headers.ContentType?.MediaType ?? "image/png";

        var dataContent = new DataContent(responseBytes, contentType);

        return new TextToImageResponse([dataContent]);    
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType is null ? throw new ArgumentNullException(nameof(serviceType)) :
        serviceKey is not null ? null :
        serviceType == typeof(TextToImageClientMetadata) ? _metadata :
        serviceType == typeof(HttpClient) ? _httpClient :
        serviceType.IsInstanceOfType(this) ? this :
        null;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>Creates a generation request from the prompt and options.</summary>
    private Dictionary<string, string?> CreateRequestProperties(string prompt, TextToImageOptions? options)
    {
        Dictionary<string, string?> properties = options?.RawRepresentationFactory?.Invoke(this) as Dictionary<string, string?> ?? new();

        properties["prompt"] = prompt;

        if (_model.StartsWith("sd3", StringComparison.Ordinal) && _model != DefaultModel && !properties.ContainsKey("model"))
        {
            properties["model"] = _model;
        }

        if (!properties.ContainsKey("output_format") && !string.IsNullOrEmpty(options?.MediaType))
        {
            properties["output_format"] = options.MediaType switch
            {
                "image/png" => "png",
                "image/jpeg" => "jpeg",
                "image/webp" => "webp",
                _ => null
            };
        }

        if (!properties.ContainsKey("style_preset") && !string.IsNullOrEmpty(options?.Style))
        {
            properties["style_preset"] = options.Style;
        }

        return properties;
    }

    private MultipartFormDataContent CreateRequestContent(Dictionary<string, string?> properties)
    {
        MultipartFormDataContent content = new();
        foreach ((var key, var value) in properties)
        {
            if (value is not null)
            {
                content.Add(new StringContent(value), $"\"{key}\"");
            }
        }
        return content;
    }

    private Uri GetDefaultEndpointForModel(string model)
    {
        _ = Throw.IfNullOrWhitespace(model);

        if (model.StartsWith("sd3", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(DefaultBaseEndpoint, "sd3");
        }

        return new Uri(DefaultBaseEndpoint, model);
    }
}
