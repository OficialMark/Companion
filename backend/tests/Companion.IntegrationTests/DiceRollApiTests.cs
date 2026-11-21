using System.Net;
using System.Net.Http.Json;
using Companion.Application.Contracts;

namespace Companion.IntegrationTests;

public sealed class DiceRollApiTests : ApiTestBase
{
    private readonly CompanionApiFactory _factory;

    public DiceRollApiTests(CompanionApiFactory factory) : base(factory) => _factory = factory;
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

    [Fact]
    public async Task Prepared_roll_enters_history_only_after_reveal()
    {
        var session = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var request = Authenticated(HttpMethod.Post, $"/api/campaigns/{session.CampaignId}/dice-rolls", session,
            new RollDiceRequest("d20", false));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var roll = (await response.Content.ReadFromJsonAsync<DiceRollDto>(JsonOptions))!;

        var beforeReveal = await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{session.CampaignId}/snapshot", session));
        var hiddenSnapshot = (await beforeReveal.Content.ReadFromJsonAsync<CampaignSnapshot>(JsonOptions))!;
        Assert.DoesNotContain(hiddenSnapshot.DiceRolls, item => item.Id == roll.Id);

        var reveal = await Client.SendAsync(Authenticated(HttpMethod.Post,
            $"/api/campaigns/{session.CampaignId}/dice-rolls/{roll.Id}/reveal", session));
        reveal.EnsureSuccessStatusCode();
        var afterReveal = await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{session.CampaignId}/snapshot", session));
        var visibleSnapshot = (await afterReveal.Content.ReadFromJsonAsync<CampaignSnapshot>(JsonOptions))!;
        Assert.Contains(visibleSnapshot.DiceRolls, item => item.Id == roll.Id);
    }

    [Fact]
    public async Task Typed_damage_subtotals_survive_history_round_trip()
    {
        var session = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        _factory.Dice.Enqueue(5, 3);
        var breakdown = new DiceDamageBreakdownRequest("cortante", 1,
            [new DiceDamageComponentRequest("Encantamento", "cold", 1)]);
        var request = Authenticated(HttpMethod.Post, $"/api/campaigns/{session.CampaignId}/dice-rolls", session,
            new RollDiceRequest("d8+d4+2", true, null, breakdown));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var rolled = (await response.Content.ReadFromJsonAsync<DiceRollDto>(JsonOptions))!;
        var snapshotResponse = await Client.SendAsync(Authenticated(HttpMethod.Get,
            $"/api/campaigns/{session.CampaignId}/snapshot", session));
        var snapshot = (await snapshotResponse.Content.ReadFromJsonAsync<CampaignSnapshot>(JsonOptions))!;
        var historical = Assert.Single(snapshot.DiceRolls, item => item.Id == rolled.Id);

        var detail = historical.Results[0].DamageBreakdown!;
        Assert.Equal(7, detail.BaseSubtotal);
        var additional = Assert.Single(detail.Components);
        Assert.Equal("gélido", additional.DamageType);
        Assert.Equal(3, additional.Subtotal);
        Assert.Equal(10, historical.Total);
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
