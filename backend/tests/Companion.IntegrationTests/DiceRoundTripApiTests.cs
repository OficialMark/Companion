using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Companion.Application.Contracts;

namespace Companion.IntegrationTests;

/// <summary>
/// Spec 011, US5 / FR-029 / SC-005: every expression the player's panel can generate (cases marked
/// roundTrip in backend/tests/Fixtures/dice-expressions.cases.json, the same file the frontend
/// generator is tested against) is accepted by the real API and comes back identical. The screen
/// and the server can therefore never show different expressions.
/// </summary>
public sealed class DiceRoundTripApiTests(CompanionApiFactory factory) : CharacterTestBase(factory)
{
    [Fact]
    public async Task Every_generated_expression_round_trips_through_the_api_unchanged()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SharedFixtures", "dice-expressions.cases.json");
        var cases = JsonNode.Parse(await File.ReadAllTextAsync(path))!["cases"]!.AsArray()
            .Where(x => x!["roundTrip"]?.GetValue<bool>() == true)
            .Select(x => x!["canonical"]!.GetValue<string>())
            .ToArray();
        Assert.True(cases.Length >= 30, $"Esperava pelo menos 30 casos geráveis, há {cases.Length}.");

        var (_, player) = await Table();
        foreach (var expression in cases)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/campaigns/{player.CampaignId}/dice-rolls");
            request.Headers.Add("X-Participant-Token", player.ParticipantToken);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            request.Content = JsonContent.Create(new RollDiceRequest(expression, false, "Teste de ida e volta"));
            var response = await Client.SendAsync(request);
            Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
                $"{expression}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            var roll = (await response.Content.ReadFromJsonAsync<DiceRollDto>(JsonOptions))!;
            Assert.Equal(expression, roll.Expression);
        }
    }
}
