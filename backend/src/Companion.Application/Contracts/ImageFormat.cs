namespace Companion.Application.Contracts;

/// <summary>Validates uploaded images by their magic bytes, not just the declared media type.</summary>
public static class ImageFormat
{
    public static bool IsValid(byte[] bytes, string mediaType) => mediaType switch
    {
        "image/png" => bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
        "image/jpeg" => bytes.Length >= 3 &&
            bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/webp" => bytes.Length >= 12 &&
            bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
}
