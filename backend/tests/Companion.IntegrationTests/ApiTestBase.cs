using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Companion.Application.Contracts;

namespace Companion.IntegrationTests;

[Collection(CompanionCollection.Name)]
public abstract class ApiTestBase(CompanionApiFactory factory)
{
    protected HttpClient Client { get; } = factory.CreateClient();
    protected static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected async Task<SessionResponse> CreateCampaign(string suffix = "")
    {
        var response = await Client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest($"Campanha {suffix}", $"Mestre {suffix}"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
    }

    protected static HttpRequestMessage Authenticated(HttpMethod method, string path, SessionResponse session, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Participant-Token", session.ParticipantToken);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }
}
