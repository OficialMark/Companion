using System.Net.Http.Headers;
using Companion.Application.Creatures;
using Microsoft.Extensions.Logging;

namespace Companion.Infrastructure.Creatures;

/// <summary>
/// Fetches compendium token images from their source (pocketdm.com.br) on first use
/// (spec 008, research R5). Bounded: HTTPS to the allowed host only, 5 s timeout, 1 MB cap.
/// Never throws for network problems; the caller degrades to initials.
/// </summary>
public sealed class HttpCreatureImageSource(HttpClient client, ILogger<HttpCreatureImageSource> logger) : ICreatureImageSource
{
    public const string AllowedHost = "pocketdm.com.br";
    private const int MaxBytes = 1024 * 1024;

    public async Task<(byte[] Content, string MediaType)?> FetchAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !(uri.Host.Equals(AllowedHost, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + AllowedHost, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning("Creature image URL rejected: {Url}", url);
            return null;
        }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CompanionImporter", "1.0"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > MaxBytes)
            {
                logger.LogInformation("Creature image unavailable ({Status}): {Url}", (int)response.StatusCode, url);
                return null;
            }
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                if (buffer.Length + read > MaxBytes) return null;
                buffer.Write(chunk, 0, read);
            }
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return (buffer.ToArray(), mediaType);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            logger.LogInformation(exception, "Creature image fetch failed: {Url}", url);
            return null;
        }
    }
}
