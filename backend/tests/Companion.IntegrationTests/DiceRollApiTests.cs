using System.Net;
using System.Net.Http.Json;
using Companion.Application.Contracts;

namespace Companion.IntegrationTests;

public sealed class DiceRollApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Repeated_idempotency_key_returns_same_roll()
    {
        var session = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var requestId = Guid.NewGuid();
        var first = await Roll(session, requestId, "2d6+3");
        var second = await Roll(session, requestId, "2d6+3");
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Total, second.Total);
    }

    [Fact]
    public async Task Invalid_expression_does_not_enter_history()
    {
        var session = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var request = Authenticated(HttpMethod.Post, $"/api/campaigns/{session.CampaignId}/dice-rolls", session, new RollDiceRequest("500d1"));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Repeated_compound_expression_returns_each_individual_result()
    {
        var session = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var roll = await Roll(session, Guid.NewGuid(), "3#2d3+4+2d8+3");

        Assert.Equal("3#2d3+2d8+7", roll.Expression);
        Assert.Equal(3, roll.Results.Count);
        Assert.All(roll.Results, result =>
        {
            Assert.Equal(2, result.Terms.Count);
            Assert.Equal(7, result.Modifier);
            Assert.Equal(result.Terms.Sum(x => x.Subtotal) + 7, result.Total);
        });
    }

    private async Task<DiceRollDto> Roll(SessionResponse session, Guid requestId, string expression)
    {
        var request = Authenticated(HttpMethod.Post, $"/api/campaigns/{session.CampaignId}/dice-rolls", session, new RollDiceRequest(expression));
        request.Headers.Add("Idempotency-Key", requestId.ToString());
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DiceRollDto>(JsonOptions))!;
    }
}
