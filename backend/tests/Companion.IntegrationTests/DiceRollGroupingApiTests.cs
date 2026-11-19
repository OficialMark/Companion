using System.Net.Http.Json;
using System.Text.Json;
using Companion.Application.Contracts;

namespace Companion.IntegrationTests;

/// <summary>Spec 011 (T010): vantagem, desvantagem e repetição com colchetes pela API.</summary>
public sealed class DiceRollGroupingApiTests(CompanionApiFactory factory) : ApiTestBase(factory)
{
    private readonly CompanionApiFactory _factory = factory;

    [Fact]
    public async Task Advantage_returns_both_executions_and_keeps_the_highest()
    {
        var session = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        _factory.Dice.Enqueue(4, 17);

        var (roll, _) = await Roll(session, "2#(d20+(FOR)3) MAIOR");

        Assert.Equal("2#(d20 + (FOR) 3) maior", roll.Expression);
        Assert.Equal(20, roll.Total);
        var result = Assert.Single(roll.Results);
        Assert.Equal("maior", result.Selection);
        Assert.Equal(1, result.SelectedExecution);
        Assert.Equal([7, 20], result.Executions!.Select(x => x.Total));
        Assert.Equal([17], result.Terms.SelectMany(x => x.Values));
        Assert.Equal([new DiceModifierDto("FOR", 3)], result.Modifiers);
    }

    [Fact]
    public async Task Snapshot_history_keeps_the_executions_of_each_repetition()
    {
        var session = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        _factory.Dice.Enqueue(12, 3, 5, 9);

        var (roll, _) = await Roll(session, "2#[2#(d20) menor]");

        var response = await Client.SendAsync(Authenticated(HttpMethod.Get, $"/api/campaigns/{session.CampaignId}/snapshot", session));
        response.EnsureSuccessStatusCode();
        var snapshot = (await response.Content.ReadFromJsonAsync<CampaignSnapshot>(JsonOptions))!;
        var stored = Assert.Single(snapshot.DiceRolls, x => x.Id == roll.Id);
        Assert.Equal("2#[2#(d20) menor]", stored.Expression);
        Assert.Equal(3, stored.Total);
        Assert.Equal(2, stored.Results.Count);
        Assert.All(stored.Results, x =>
        {
            Assert.Equal("menor", x.Selection);
            Assert.Equal(2, x.Executions!.Count);
        });
        Assert.Equal([12, 3], stored.Results[0].Executions!.Select(x => x.Total));
        Assert.Equal([5, 9], stored.Results[1].Executions!.Select(x => x.Total));
        Assert.Equal([1, 0], stored.Results.Select(x => x.SelectedExecution!.Value));
    }

    [Fact]
    public async Task Legacy_expression_keeps_the_previous_json_shape()
    {
        var session = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        _factory.Dice.Enqueue(1, 2, 3);

        var (roll, json) = await Roll(session, "3#d20+5");

        Assert.Equal("3#1d20+5", roll.Expression);
        using var document = JsonDocument.Parse(json);
        var results = document.RootElement.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(3, results.Length);
        Assert.All(results, result => Assert.Equal(
            ["sequence", "terms", "modifier", "total"],
            result.EnumerateObject().Select(x => x.Name)));
        Assert.Equal([6, 7, 8], results.Select(x => x.GetProperty("total").GetInt32()));
        Assert.Equal(
            """{"expression":"1d20","values":[1],"subtotal":1}""",
            results[0].GetProperty("terms")[0].GetRawText());
    }

    [Fact]
    public async Task Expression_with_200_characters_is_stored()
    {
        var session = await CreateCampaign(Guid.NewGuid().ToString("N")[..6]);
        var labels = Enumerable.Range(1, 9).Select(x => $"Modificador {x:D4}").ToArray();
        var expression = "2#(d20" + string.Concat(labels.Select(x => $" + ({x}) 1")) + ") maior";
        Assert.InRange(expression.Length, 200, 256);

        var (roll, _) = await Roll(session, expression);

        Assert.Equal(expression, roll.Expression);
    }

    private async Task<(DiceRollDto Roll, string Json)> Roll(SessionResponse session, string expression)
    {
        var request = Authenticated(HttpMethod.Post, $"/api/campaigns/{session.CampaignId}/dice-rolls", session, new RollDiceRequest(expression));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return (JsonSerializer.Deserialize<DiceRollDto>(json, JsonOptions)!, json);
    }
}
