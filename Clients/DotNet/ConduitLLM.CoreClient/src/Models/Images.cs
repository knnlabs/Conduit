using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.CoreClient.Models;

/// <summary>
/// Represents an image generation request.
/// </summary>
public class ImageGenerationRequest
{
    /// <summary>
    /// Gets or sets a text description of the desired image(s). 
    /// Maximum length is 1000 characters for dall-e-2 and 4000 characters for dall-e-3.
    /// </summary>
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model to use for image generation.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the number of images to generate. Must be between 1 and 10. 
    /// For dall-e-3, only n=1 is supported.
    /// </summary>
    [Range(1, 10)]
    public int? N { get; set; }

    /// <summary>
    /// Gets or sets the quality of the image that will be generated. 
    /// This parameter is only supported for dall-e-3.
    /// </summary>
    public ImageQuality? Quality { get; set; }

    /// <summary>
    /// Gets or sets the format in which the generated images are returned.
    /// </summary>
    public ImageResponseFormat? ResponseFormat { get; set; }

    /// <summary>
    /// Gets or sets the size of the generated images.
    /// </summary>
    public ImageSize? Size { get; set; }

    /// <summary>
    /// Gets or sets the style of the generated images. 
    /// This parameter is only supported for dall-e-3.
    /// </summary>
    public ImageStyle? Style { get; set; }

    /// <summary>
    /// Gets or sets a unique identifier representing your end-user.
    /// </summary>
    public string? User { get; set; }
}

/// <summary>
/// Represents image quality options.
/// </summary>
public enum ImageQuality
{
    /// <summary>
    /// Standard quality.
    /// </summary>
    Standard,

    /// <summary>
    /// High definition quality with finer details.
    /// </summary>
    Hd
}

/// <summary>
/// Represents image response format options.
/// </summary>
public enum ImageResponseFormat
{
    /// <summary>
    /// Return image as a URL.
    /// </summary>
    Url,

    /// <summary>
    /// Return image as base64-encoded JSON.
    /// </summary>
    B64Json
}

/// <summary>
/// Represents image size options.
/// </summary>
public enum ImageSize
{
    /// <summary>
    /// 256x256 pixels (dall-e-2 only).
    /// </summary>
    Size256x256,

    /// <summary>
    /// 512x512 pixels (dall-e-2 only).
    /// </summary>
    Size512x512,

    /// <summary>
    /// 1024x1024 pixels.
    /// </summary>
    Size1024x1024,

    /// <summary>
    /// 1792x1024 pixels (dall-e-3 only).
    /// </summary>
    Size1792x1024,

    /// <summary>
    /// 1024x1792 pixels (dall-e-3 only).
    /// </summary>
    Size1024x1792
}

/// <summary>
/// Represents image style options.
/// </summary>
public enum ImageStyle
{
    /// <summary>
    /// Vivid style - generates hyper-real and dramatic images.
    /// </summary>
    Vivid,

    /// <summary>
    /// Natural style - produces more natural, less hyper-real looking images.
    /// </summary>
    Natural
}

/// <summary>
/// Represents generated image data.
/// </summary>
public class ImageData
{
    /// <summary>
    /// Gets or sets the base64-encoded JSON of the generated image, if response_format is b64_json.
    /// </summary>
    public string? B64Json { get; set; }

    /// <summary>
    /// Gets or sets the URL of the generated image, if response_format is url (default).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the prompt that was used to generate the image, if there was any revision to the prompt.
    /// </summary>
    public string? RevisedPrompt { get; set; }
}

/// <summary>
/// Represents an image generation response.
/// </summary>
public class ImageGenerationResponse
{
    /// <summary>
    /// Gets or sets the Unix timestamp (in seconds) when the image was created.
    /// </summary>
    public long Created { get; set; }

    /// <summary>
    /// Gets or sets the list of generated images.
    /// </summary>
    public IEnumerable<ImageData> Data { get; set; } = new List<ImageData>();
}

/// <summary>
/// Represents an image edit request.
/// </summary>
public class ImageEditRequest
{
    /// <summary>
    /// Gets or sets the image to edit. Must be a valid PNG file, less than 4MB, and square.
    /// </summary>
    [Required]
    public Stream Image { get; set; } = Stream.Null;

    /// <summary>
    /// Gets or sets the filename for the image.
    /// </summary>
    public string ImageFileName { get; set; } = "image.png";

    /// <summary>
    /// Gets or sets a text description of the desired image(s). Maximum length is 1000 characters.
    /// </summary>
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an additional image whose fully transparent areas indicate where image should be edited.
    /// </summary>
    public Stream? Mask { get; set; }

    /// <summary>
    /// Gets or sets the filename for the mask image.
    /// </summary>
    public string? MaskFileName { get; set; }

    /// <summary>
    /// Gets or sets the model to use for image editing. Only dall-e-2 is supported at this time.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the number of images to generate. Must be between 1 and 10.
    /// </summary>
    [Range(1, 10)]
    public int? N { get; set; }

    /// <summary>
    /// Gets or sets the format in which the generated images are returned.
    /// </summary>
    public ImageResponseFormat? ResponseFormat { get; set; }

    /// <summary>
    /// Gets or sets the size of the generated images. Must be one of 256x256, 512x512, or 1024x1024.
    /// </summary>
    public ImageSize? Size { get; set; }

    /// <summary>
    /// Gets or sets a unique identifier representing your end-user.
    /// </summary>
    public string? User { get; set; }
}

/// <summary>
/// Represents an image variation request.
/// </summary>
public class ImageVariationRequest
{
    /// <summary>
    /// Gets or sets the image to use as the basis for the variation(s). 
    /// Must be a valid PNG file, less than 4MB, and square.
    /// </summary>
    [Required]
    public Stream Image { get; set; } = Stream.Null;

    /// <summary>
    /// Gets or sets the filename for the image.
    /// </summary>
    public string ImageFileName { get; set; } = "image.png";

    /// <summary>
    /// Gets or sets the model to use for image variation. Only dall-e-2 is supported at this time.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the number of images to generate. Must be between 1 and 10.
    /// </summary>
    [Range(1, 10)]
    public int? N { get; set; }

    /// <summary>
    /// Gets or sets the format in which the generated images are returned.
    /// </summary>
    public ImageResponseFormat? ResponseFormat { get; set; }

    /// <summary>
    /// Gets or sets the size of the generated images. Must be one of 256x256, 512x512, or 1024x1024.
    /// </summary>
    public ImageSize? Size { get; set; }

    /// <summary>
    /// Gets or sets a unique identifier representing your end-user.
    /// </summary>
    public string? User { get; set; }
}

/// <summary>
/// Supported image generation models.
/// </summary>
public static class ImageModels
{
    /// <summary>
    /// DALL-E 2 model.
    /// </summary>
    public const string DallE2 = "dall-e-2";

    /// <summary>
    /// DALL-E 3 model.
    /// </summary>
    public const string DallE3 = "dall-e-3";

    /// <summary>
    /// MiniMax image generation model.
    /// </summary>
    public const string MiniMaxImage = "minimax-image";
}

/// <summary>
/// Model-specific capabilities and constraints.
/// </summary>
public static class ImageModelCapabilities
{
    /// <summary>
    /// Gets the capabilities for DALL-E 2.
    /// </summary>
    public static readonly ImageModelCapability DallE2 = new()
    {
        MaxPromptLength = 1000,
        SupportedSizes = new[] { ImageSize.Size256x256, ImageSize.Size512x512, ImageSize.Size1024x1024 },
        SupportedQualities = new[] { ImageQuality.Standard },
        SupportedStyles = Array.Empty<ImageStyle>(),
        MaxImages = 10,
        SupportsEdit = true,
        SupportsVariation = true
    };

    /// <summary>
    /// Gets the capabilities for DALL-E 3.
    /// </summary>
    public static readonly ImageModelCapability DallE3 = new()
    {
        MaxPromptLength = 4000,
        SupportedSizes = new[] { ImageSize.Size1024x1024, ImageSize.Size1792x1024, ImageSize.Size1024x1792 },
        SupportedQualities = new[] { ImageQuality.Standard, ImageQuality.Hd },
        SupportedStyles = new[] { ImageStyle.Vivid, ImageStyle.Natural },
        MaxImages = 1,
        SupportsEdit = false,
        SupportsVariation = false
    };

    /// <summary>
    /// Gets the capabilities for MiniMax image model.
    /// </summary>
    public static readonly ImageModelCapability MiniMaxImage = new()
    {
        MaxPromptLength = 2000,
        SupportedSizes = new[] { ImageSize.Size1024x1024, ImageSize.Size1792x1024, ImageSize.Size1024x1792 },
        SupportedQualities = new[] { ImageQuality.Standard, ImageQuality.Hd },
        SupportedStyles = new[] { ImageStyle.Vivid, ImageStyle.Natural },
        MaxImages = 4,
        SupportsEdit = false,
        SupportsVariation = false
    };

    /// <summary>
    /// Gets the capabilities for a specific model.
    /// </summary>
    /// <param name="model">The model name.</param>
    /// <returns>The model capabilities, or null if not found.</returns>
    public static ImageModelCapability? GetCapabilities(string model)
    {
        return model switch
        {
            ImageModels.DallE2 => DallE2,
            ImageModels.DallE3 => DallE3,
            ImageModels.MiniMaxImage => MiniMaxImage,
            _ => null
        };
    }
}

/// <summary>
/// Represents the capabilities of an image generation model.
/// </summary>
public class ImageModelCapability
{
    /// <summary>
    /// Gets or sets the maximum prompt length.
    /// </summary>
    public int MaxPromptLength { get; set; }

    /// <summary>
    /// Gets or sets the supported image sizes.
    /// </summary>
    public IEnumerable<ImageSize> SupportedSizes { get; set; } = new List<ImageSize>();

    /// <summary>
    /// Gets or sets the supported image qualities.
    /// </summary>
    public IEnumerable<ImageQuality> SupportedQualities { get; set; } = new List<ImageQuality>();

    /// <summary>
    /// Gets or sets the supported image styles.
    /// </summary>
    public IEnumerable<ImageStyle> SupportedStyles { get; set; } = new List<ImageStyle>();

    /// <summary>
    /// Gets or sets the maximum number of images that can be generated in one request.
    /// </summary>
    public int MaxImages { get; set; }

    /// <summary>
    /// Gets or sets whether the model supports image editing.
    /// </summary>
    public bool SupportsEdit { get; set; }

    /// <summary>
    /// Gets or sets whether the model supports image variations.
    /// </summary>
    public bool SupportsVariation { get; set; }
}

/// <summary>
/// Default values for image generation requests.
/// </summary>
public static class ImageDefaults
{
    /// <summary>
    /// Default model.
    /// </summary>
    public const string Model = ImageModels.DallE3;

    /// <summary>
    /// Default number of images.
    /// </summary>
    public const int N = 1;

    /// <summary>
    /// Default quality.
    /// </summary>
    public const ImageQuality Quality = ImageQuality.Standard;

    /// <summary>
    /// Default response format.
    /// </summary>
    public const ImageResponseFormat ResponseFormat = ImageResponseFormat.Url;

    /// <summary>
    /// Default size.
    /// </summary>
    public const ImageSize Size = ImageSize.Size1024x1024;

    /// <summary>
    /// Default style.
    /// </summary>
    public const ImageStyle Style = ImageStyle.Vivid;
}