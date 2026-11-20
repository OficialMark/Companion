using System.Net;
using System.Net.Http.Json;
using Companion.Application.Characters;
using Companion.Application.Contracts;
using Companion.Domain.Characters;

namespace Companion.IntegrationTests;

/// <summary>Helpers for the spec 011 character API tests.</summary>
public abstract class CharacterTestBase(CompanionApiFactory factory) : ApiTestBase(factory)
{
    protected static string Suffix() => Guid.NewGuid().ToString("N")[..6];

    protected async Task<SessionResponse> Join(string code, string name)
    {
        var response = await Client.PostAsJsonAsync("/api/campaigns/join", new JoinCampaignRequest(code, name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!;
    }

    protected static string Base(SessionResponse session) => $"/api/campaigns/{session.CampaignId}/participants/me/character";

    protected async Task<HttpResponseMessage> Send(HttpMethod method, string path, SessionResponse session, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Participant-Token", session.ParticipantToken);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        return await Client.SendAsync(request);
    }

    protected async Task<CharacterDto> Ok(HttpMethod method, string path, SessionResponse session, object? body = null)
    {
        var response = await Send(method, path, session, body);
        if (response.StatusCode != HttpStatusCode.OK)
            Assert.Fail($"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<CharacterDto>(JsonOptions))!;
    }

    protected Task<CharacterDto> Get(SessionResponse session) => Ok(HttpMethod.Get, Base(session), session);

    protected Task<CharacterDto> SaveSheet(SessionResponse session, CharacterDto current, Func<CharacterSheetData, CharacterSheetData> change) =>
        Ok(HttpMethod.Put, Base(session), session, new SaveSheetRequest(change(current.Sheet), current.Version));

    protected Task<CharacterDto> AddItem(SessionResponse session, InventoryItemData item) =>
        Ok(HttpMethod.Post, $"{Base(session)}/items", session, new SaveItemRequest(item, null));

    protected Task<CharacterDto> Equip(SessionResponse session, Guid itemId, bool equipped = true) =>
        Ok(HttpMethod.Post, $"{Base(session)}/items/{itemId}/equip", session, new EquipItemRequest(equipped));

    protected async Task<(SessionResponse Master, SessionResponse Player)> Table(string? playerName = null)
    {
        var master = await CreateCampaign(Suffix());
        // Sheets are global per player name (2026-11-20): every test gets its own name, so its own sheet.
        var player = await Join(master.PlayerCode, playerName ?? $"Loren {Suffix()}");
        return (master, player);
    }

    protected static InventoryItemData Armor(string name, int baseAc, ArmorType type, bool proficient = true) =>
        new(name, ItemCategory.Armor, 1, null, null, null, false, proficient, 0, null, new ArmorData(baseAc, type, null, false), null, []);

    protected static InventoryItemData Shield(string name, int magicBonus = 0) =>
        new(name, ItemCategory.Shield, 1, null, null, null, false, true, magicBonus, null, null, new ShieldData(2), []);

    protected static InventoryItemData Weapon(string name, string dice = "1d8", int magicBonus = 0, bool proficient = true) =>
        new(name, ItemCategory.Weapon, 1, null, null, null, false, proficient, magicBonus,
            new WeaponData(dice, "cortante", null, WeaponKind.Melee, WeaponClass.Martial, [], null, null, null, null), null, null, []);

    protected static InventoryItemData Gear(string name, params ItemEffect[] effects) =>
        new(name, ItemCategory.Gear, 1, null, null, null, false, false, 0, null, null, null, effects);
}
