// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.AI;

/// <summary>Configuration options for Stability AI text-to-image services.</summary>
[Experimental("MEAI001")]
public sealed class StabilityAIOptions
{
    /// <summary>Gets or sets the API key for authenticating with Stability AI.</summary>
    [Required]
    public string? Key { get; set; }

    /// <summary>Gets or sets the model to use for image generation.</summary>
    /// <remarks>If not specified, defaults to "sd3-large".</remarks>
    public string? Model { get; set; }

    /// <summary>Gets or sets the endpoint URI for the Stability AI service.</summary>
    /// <remarks>If not specified, uses the default Stability AI endpoint.</remarks>
    public Uri? Endpoint { get; set; }
}
