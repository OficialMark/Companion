using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.SqliteMigrations
{
    /// <inheritdoc />
    public partial class InitialSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSheets",
                columns: table => new
                {
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false, collation: "BINARY"),
                    Data = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSheets", x => x.ParticipantId);
                });

            migrationBuilder.CreateTable(
                name: "SeedVersions",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AppliedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeedVersions", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "CampaignAccessCodes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 4, nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAccessCodes", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CampaignAccessCodes_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignKnowledgeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SharedDescription = table.Column<string>(type: "TEXT", maxLength: 12000, nullable: true),
                    IsPlayerVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ImageContent = table.Column<byte[]>(type: "BLOB", nullable: true),
                    ImageMediaType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ImageFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    PdfContent = table.Column<byte[]>(type: "BLOB", nullable: true),
                    PdfFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignKnowledgeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignKnowledgeEntries_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    NameEn = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Subcategory = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Rarity = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    RequiresAttunement = table.Column<bool>(type: "INTEGER", nullable: false),
                    Cost = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Template = table.Column<string>(type: "TEXT", nullable: false),
                    SearchKey = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    SearchText = table.Column<string>(type: "TEXT", nullable: false),
                    IsRemovedFromCatalog = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresBaseItem = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogItems_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogItems_CatalogItems_BaseId",
                        column: x => x.BaseId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CatalogSpells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    NameEn = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    School = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Ritual = table.Column<bool>(type: "INTEGER", nullable: false),
                    Concentration = table.Column<bool>(type: "INTEGER", nullable: false),
                    Classes = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    HigherLevels = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Template = table.Column<string>(type: "TEXT", nullable: false),
                    SearchKey = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    SearchText = table.Column<string>(type: "TEXT", nullable: false),
                    IsRemovedFromCatalog = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogSpells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogSpells_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogSpells_CatalogSpells_BaseId",
                        column: x => x.BaseId,
                        principalTable: "CatalogSpells",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Creatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BaseCreatureId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    NameEn = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    SearchKey = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    SourceBook = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Size = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Alignment = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ArmorClass = table.Column<int>(type: "INTEGER", nullable: false),
                    ArmorClassNote = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    HitPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    HitDice = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Speed = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Initiative = table.Column<int>(type: "INTEGER", nullable: true),
                    Strength = table.Column<int>(type: "INTEGER", nullable: false),
                    Dexterity = table.Column<int>(type: "INTEGER", nullable: false),
                    Constitution = table.Column<int>(type: "INTEGER", nullable: false),
                    Intelligence = table.Column<int>(type: "INTEGER", nullable: false),
                    Wisdom = table.Column<int>(type: "INTEGER", nullable: false),
                    Charisma = table.Column<int>(type: "INTEGER", nullable: false),
                    SavingThrows = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Skills = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DamageVulnerabilities = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DamageResistances = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DamageImmunities = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ConditionImmunities = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Senses = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Languages = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ChallengeRating = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ChallengeValue = table.Column<decimal>(type: "TEXT", precision: 6, scale: 3, nullable: false),
                    ExperiencePoints = table.Column<int>(type: "INTEGER", nullable: true),
                    ProficiencyBonus = table.Column<int>(type: "INTEGER", nullable: true),
                    Traits = table.Column<string>(type: "TEXT", nullable: false),
                    Actions = table.Column<string>(type: "TEXT", nullable: false),
                    BonusActions = table.Column<string>(type: "TEXT", nullable: false),
                    Reactions = table.Column<string>(type: "TEXT", nullable: false),
                    LegendaryActions = table.Column<string>(type: "TEXT", nullable: false),
                    LegendaryActionsIntro = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ImageSourceUrl = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    ImageRevision = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsRemovedFromCompendium = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Creatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Creatures_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Creatures_Creatures_BaseCreatureId",
                        column: x => x.BaseCreatureId,
                        principalTable: "Creatures",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Encounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentTurnIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    EndedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Encounters_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    NameKey = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true, collation: "BINARY"),
                    Role = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "BLOB", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    InitiativeModifier = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrentHitPoints = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxHitPoints = table.Column<int>(type: "INTEGER", nullable: true),
                    AvatarRevision = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsHiddenFromPanel = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Participants_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterFeatures_CharacterSheets_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "CharacterSheets",
                        principalColumn: "ParticipantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSpells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CatalogSpellId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Data = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSpells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterSpells_CharacterSheets_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "CharacterSheets",
                        principalColumn: "ParticipantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Data = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_CharacterSheets_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "CharacterSheets",
                        principalColumn: "ParticipantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedRolls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedRolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedRolls_CharacterSheets_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "CharacterSheets",
                        principalColumn: "ParticipantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignKnowledgeContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    CreatedByParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignKnowledgeContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignKnowledgeContributions_CampaignKnowledgeEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "CampaignKnowledgeEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignKnowledgeMasterNotes",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 12000, nullable: false),
                    UpdatedByParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignKnowledgeMasterNotes", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_CampaignKnowledgeMasterNotes_CampaignKnowledgeEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "CampaignKnowledgeEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignMapPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MapEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    X = table.Column<decimal>(type: "TEXT", precision: 8, scale: 7, nullable: false),
                    Y = table.Column<decimal>(type: "TEXT", precision: 8, scale: 7, nullable: false),
                    CreatedByParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMapPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignMapPoints_CampaignKnowledgeEntries_MapEntryId",
                        column: x => x.MapEntryId,
                        principalTable: "CampaignKnowledgeEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatureImages",
                columns: table => new
                {
                    CreatureId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Revision = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromSource = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureImages", x => x.CreatureId);
                    table.ForeignKey(
                        name: "FK_CreatureImages_Creatures_CreatureId",
                        column: x => x.CreatureId,
                        principalTable: "Creatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Battlemaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EncounterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImageContent = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ImageMediaType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ImageFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ImageRevision = table.Column<Guid>(type: "TEXT", nullable: false),
                    Columns = table.Column<int>(type: "INTEGER", nullable: false),
                    Rows = table.Column<int>(type: "INTEGER", nullable: false),
                    IsGridVisible = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", nullable: false),
                    FogMask = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Battlemaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Battlemaps_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Combatants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EncounterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    InitiativeModifier = table.Column<int>(type: "INTEGER", nullable: false),
                    Initiative = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrentHitPoints = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxHitPoints = table.Column<int>(type: "INTEGER", nullable: true),
                    CreationOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsHitPointsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatureId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Combatants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Combatants_Creatures_CreatureId",
                        column: x => x.CreatureId,
                        principalTable: "Creatures",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Combatants_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Combatants_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DiceRolls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Expression = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ValuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    Modifier = table.Column<int>(type: "INTEGER", nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    RevealedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiceRolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiceRolls_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiceRolls_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParticipantAvatars",
                columns: table => new
                {
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Revision = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantAvatars", x => x.ParticipantId);
                    table.ForeignKey(
                        name: "FK_ParticipantAvatars_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParticipantSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "BLOB", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipantSessions_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BattlemapMarkers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BattlemapId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattlemapMarkers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BattlemapMarkers_Battlemaps_BattlemapId",
                        column: x => x.BattlemapId,
                        principalTable: "Battlemaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BattlemapMarkers_Participants_OwnerParticipantId",
                        column: x => x.OwnerParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CombatAttacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EncounterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthorParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiceRollId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Damage = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombatAttacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CombatAttacks_DiceRolls_DiceRollId",
                        column: x => x.DiceRollId,
                        principalTable: "DiceRolls",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CombatAttacks_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CombatAttacks_Participants_AuthorParticipantId",
                        column: x => x.AuthorParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AttackAreas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BattlemapId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CombatantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MarkerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Shape = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Radius = table.Column<int>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    DirectionDegrees = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttackAreas", x => x.Id);
                    table.CheckConstraint("CK_AttackAreas_Owner", "(CASE WHEN [ParticipantId] IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN [CombatantId] IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN [MarkerId] IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_AttackAreas_BattlemapMarkers_MarkerId",
                        column: x => x.MarkerId,
                        principalTable: "BattlemapMarkers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AttackAreas_Battlemaps_BattlemapId",
                        column: x => x.BattlemapId,
                        principalTable: "Battlemaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttackAreas_Combatants_CombatantId",
                        column: x => x.CombatantId,
                        principalTable: "Combatants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AttackAreas_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BattlemapTokenAppearances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BattlemapId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CombatantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MarkerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FootprintSize = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusConditions = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", nullable: false),
                    LastRollCount = table.Column<int>(type: "INTEGER", nullable: true),
                    LastRollDie = table.Column<int>(type: "INTEGER", nullable: true),
                    LastRollAbility = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    LastRollBonus = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattlemapTokenAppearances", x => x.Id);
                    table.CheckConstraint("CK_BattlemapTokenAppearances_Owner", "(CASE WHEN [ParticipantId] IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN [CombatantId] IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN [MarkerId] IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_BattlemapTokenAppearances_BattlemapMarkers_MarkerId",
                        column: x => x.MarkerId,
                        principalTable: "BattlemapMarkers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BattlemapTokenAppearances_Battlemaps_BattlemapId",
                        column: x => x.BattlemapId,
                        principalTable: "Battlemaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BattlemapTokenAppearances_Combatants_CombatantId",
                        column: x => x.CombatantId,
                        principalTable: "Combatants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BattlemapTokenAppearances_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BattlemapTokenPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BattlemapId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CombatantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MarkerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Column = table.Column<int>(type: "INTEGER", nullable: false),
                    Row = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattlemapTokenPositions", x => x.Id);
                    table.CheckConstraint("CK_BattlemapTokenPositions_Owner", "(CASE WHEN [ParticipantId] IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN [CombatantId] IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN [MarkerId] IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_BattlemapTokenPositions_BattlemapMarkers_MarkerId",
                        column: x => x.MarkerId,
                        principalTable: "BattlemapMarkers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BattlemapTokenPositions_Battlemaps_BattlemapId",
                        column: x => x.BattlemapId,
                        principalTable: "Battlemaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BattlemapTokenPositions_Combatants_CombatantId",
                        column: x => x.CombatantId,
                        principalTable: "Combatants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BattlemapTokenPositions_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CombatAttackTargets",
                columns: table => new
                {
                    AttackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CombatantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DamageApplied = table.Column<int>(type: "INTEGER", nullable: false),
                    HitPointsBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    HitPointsAfter = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombatAttackTargets", x => new { x.AttackId, x.CombatantId });
                    table.ForeignKey(
                        name: "FK_CombatAttackTargets_CombatAttacks_AttackId",
                        column: x => x.AttackId,
                        principalTable: "CombatAttacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CombatAttackTargets_Combatants_CombatantId",
                        column: x => x.CombatantId,
                        principalTable: "Combatants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_BattlemapId",
                table: "AttackAreas",
                column: "BattlemapId");

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_BattlemapId_CombatantId",
                table: "AttackAreas",
                columns: new[] { "BattlemapId", "CombatantId" },
                unique: true,
                filter: "[CombatantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_BattlemapId_MarkerId",
                table: "AttackAreas",
                columns: new[] { "BattlemapId", "MarkerId" },
                unique: true,
                filter: "[MarkerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_BattlemapId_ParticipantId",
                table: "AttackAreas",
                columns: new[] { "BattlemapId", "ParticipantId" },
                unique: true,
                filter: "[ParticipantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_CombatantId",
                table: "AttackAreas",
                column: "CombatantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_MarkerId",
                table: "AttackAreas",
                column: "MarkerId");

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_ParticipantId",
                table: "AttackAreas",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapMarkers_BattlemapId",
                table: "BattlemapMarkers",
                column: "BattlemapId");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapMarkers_OwnerParticipantId",
                table: "BattlemapMarkers",
                column: "OwnerParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Battlemaps_EncounterId",
                table: "Battlemaps",
                column: "EncounterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenAppearances_BattlemapId_CombatantId",
                table: "BattlemapTokenAppearances",
                columns: new[] { "BattlemapId", "CombatantId" },
                unique: true,
                filter: "[CombatantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenAppearances_BattlemapId_MarkerId",
                table: "BattlemapTokenAppearances",
                columns: new[] { "BattlemapId", "MarkerId" },
                unique: true,
                filter: "[MarkerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenAppearances_BattlemapId_ParticipantId",
                table: "BattlemapTokenAppearances",
                columns: new[] { "BattlemapId", "ParticipantId" },
                unique: true,
                filter: "[ParticipantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenAppearances_CombatantId",
                table: "BattlemapTokenAppearances",
                column: "CombatantId");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenAppearances_MarkerId",
                table: "BattlemapTokenAppearances",
                column: "MarkerId");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenAppearances_ParticipantId",
                table: "BattlemapTokenAppearances",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId",
                table: "BattlemapTokenPositions",
                column: "BattlemapId");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId_CombatantId",
                table: "BattlemapTokenPositions",
                columns: new[] { "BattlemapId", "CombatantId" },
                unique: true,
                filter: "[CombatantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId_MarkerId",
                table: "BattlemapTokenPositions",
                columns: new[] { "BattlemapId", "MarkerId" },
                unique: true,
                filter: "[MarkerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId_ParticipantId",
                table: "BattlemapTokenPositions",
                columns: new[] { "BattlemapId", "ParticipantId" },
                unique: true,
                filter: "[ParticipantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_CombatantId",
                table: "BattlemapTokenPositions",
                column: "CombatantId");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_MarkerId",
                table: "BattlemapTokenPositions",
                column: "MarkerId");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_ParticipantId",
                table: "BattlemapTokenPositions",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAccessCodes_CampaignId_Role",
                table: "CampaignAccessCodes",
                columns: new[] { "CampaignId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignKnowledgeContributions_EntryId_CreatedAt",
                table: "CampaignKnowledgeContributions",
                columns: new[] { "EntryId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignKnowledgeEntries_CampaignId_Kind_IsPlayerVisible_Title",
                table: "CampaignKnowledgeEntries",
                columns: new[] { "CampaignId", "Kind", "IsPlayerVisible", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapPoints_MapEntryId_CreatedAt",
                table: "CampaignMapPoints",
                columns: new[] { "MapEntryId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_BaseId",
                table: "CatalogItems",
                column: "BaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_CampaignId",
                table: "CatalogItems",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_CampaignId_BaseId",
                table: "CatalogItems",
                columns: new[] { "CampaignId", "BaseId" },
                unique: true,
                filter: "[CampaignId] IS NOT NULL AND [BaseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_IsRemovedFromCatalog_Category",
                table: "CatalogItems",
                columns: new[] { "IsRemovedFromCatalog", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_Slug",
                table: "CatalogItems",
                column: "Slug",
                unique: true,
                filter: "[CampaignId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_BaseId",
                table: "CatalogSpells",
                column: "BaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_CampaignId",
                table: "CatalogSpells",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_CampaignId_BaseId",
                table: "CatalogSpells",
                columns: new[] { "CampaignId", "BaseId" },
                unique: true,
                filter: "[CampaignId] IS NOT NULL AND [BaseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_IsRemovedFromCatalog_Level",
                table: "CatalogSpells",
                columns: new[] { "IsRemovedFromCatalog", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_Slug",
                table: "CatalogSpells",
                column: "Slug",
                unique: true,
                filter: "[CampaignId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterFeatures_ParticipantId_SortOrder",
                table: "CharacterFeatures",
                columns: new[] { "ParticipantId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_OwnerKey",
                table: "CharacterSheets",
                column: "OwnerKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSpells_ParticipantId_SortOrder",
                table: "CharacterSpells",
                columns: new[] { "ParticipantId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Combatants_CreatureId",
                table: "Combatants",
                column: "CreatureId");

            migrationBuilder.CreateIndex(
                name: "IX_Combatants_EncounterId_CreationOrder",
                table: "Combatants",
                columns: new[] { "EncounterId", "CreationOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Combatants_EncounterId_ParticipantId",
                table: "Combatants",
                columns: new[] { "EncounterId", "ParticipantId" },
                unique: true,
                filter: "[ParticipantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Combatants_ParticipantId",
                table: "Combatants",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_CombatAttacks_AuthorParticipantId_ClientRequestId",
                table: "CombatAttacks",
                columns: new[] { "AuthorParticipantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CombatAttacks_DiceRollId",
                table: "CombatAttacks",
                column: "DiceRollId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CombatAttacks_EncounterId",
                table: "CombatAttacks",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_CombatAttackTargets_CombatantId",
                table: "CombatAttackTargets",
                column: "CombatantId");

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_BaseCreatureId",
                table: "Creatures",
                column: "BaseCreatureId");

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_CampaignId_BaseCreatureId",
                table: "Creatures",
                columns: new[] { "CampaignId", "BaseCreatureId" },
                unique: true,
                filter: "[CampaignId] IS NOT NULL AND [BaseCreatureId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_CampaignId_SearchKey",
                table: "Creatures",
                columns: new[] { "CampaignId", "SearchKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_Slug",
                table: "Creatures",
                column: "Slug",
                unique: true,
                filter: "[CampaignId] IS NULL AND [Slug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiceRolls_CampaignId_CreatedAt",
                table: "DiceRolls",
                columns: new[] { "CampaignId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DiceRolls_ParticipantId_ClientRequestId",
                table: "DiceRolls",
                columns: new[] { "ParticipantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_CampaignId_CreatedAt",
                table: "Encounters",
                columns: new[] { "CampaignId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ParticipantId_SortOrder",
                table: "InventoryItems",
                columns: new[] { "ParticipantId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Participants_CampaignId_DisplayName",
                table: "Participants",
                columns: new[] { "CampaignId", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "IX_Participants_CampaignId_Role_NameKey",
                table: "Participants",
                columns: new[] { "CampaignId", "Role", "NameKey" },
                unique: true,
                filter: "[NameKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_TokenHash",
                table: "Participants",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantSessions_ParticipantId",
                table: "ParticipantSessions",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantSessions_TokenHash",
                table: "ParticipantSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedRolls_ParticipantId_SortOrder",
                table: "SavedRolls",
                columns: new[] { "ParticipantId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttackAreas");

            migrationBuilder.DropTable(
                name: "BattlemapTokenAppearances");

            migrationBuilder.DropTable(
                name: "BattlemapTokenPositions");

            migrationBuilder.DropTable(
                name: "CampaignAccessCodes");

            migrationBuilder.DropTable(
                name: "CampaignKnowledgeContributions");

            migrationBuilder.DropTable(
                name: "CampaignKnowledgeMasterNotes");

            migrationBuilder.DropTable(
                name: "CampaignMapPoints");

            migrationBuilder.DropTable(
                name: "CatalogItems");

            migrationBuilder.DropTable(
                name: "CatalogSpells");

            migrationBuilder.DropTable(
                name: "CharacterFeatures");

            migrationBuilder.DropTable(
                name: "CharacterSpells");

            migrationBuilder.DropTable(
                name: "CombatAttackTargets");

            migrationBuilder.DropTable(
                name: "CreatureImages");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "ParticipantAvatars");

            migrationBuilder.DropTable(
                name: "ParticipantSessions");

            migrationBuilder.DropTable(
                name: "SavedRolls");

            migrationBuilder.DropTable(
                name: "SeedVersions");

            migrationBuilder.DropTable(
                name: "BattlemapMarkers");

            migrationBuilder.DropTable(
                name: "CampaignKnowledgeEntries");

            migrationBuilder.DropTable(
                name: "CombatAttacks");

            migrationBuilder.DropTable(
                name: "Combatants");

            migrationBuilder.DropTable(
                name: "CharacterSheets");

            migrationBuilder.DropTable(
                name: "Battlemaps");

            migrationBuilder.DropTable(
                name: "DiceRolls");

            migrationBuilder.DropTable(
                name: "Creatures");

            migrationBuilder.DropTable(
                name: "Encounters");

            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropTable(
                name: "Campaigns");
        }
    }
}
