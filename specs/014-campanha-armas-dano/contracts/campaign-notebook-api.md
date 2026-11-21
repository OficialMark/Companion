# Contract: Campaign Notebook API

All requests are scoped to the campaign in the route and authenticated with the existing participant session token. The server checks campaign membership and role on every request. JSON uses camelCase. Dates are UTC ISO 8601. Private fields and hidden records are omitted from every player response, including errors and search metadata.

## Read entries

`GET /api/campaigns/{campaignId}/knowledge?kind={Npc|Map|Clue|Journal}&q={text}&page={n}&pageSize={n}`

Returns `200 { items: KnowledgeSummary[], total, page, pageSize }`. A player sees only visible records. A master sees all records. Summaries omit map image bytes and master notes. `total` counts only records visible to the caller.

## Create and edit an entry

`POST /api/campaigns/{campaignId}/knowledge`

Body: `{ kind, title, summary?, sharedDescription?, isPlayerVisible?, image? }`. Only masters may set hidden visibility. New NPC/map/clue entries default visible; Journal is always visible. Returns `201 KnowledgeDetail` after persistence.

`PUT /api/campaigns/{campaignId}/knowledge/{entryId}`

Body includes `{ title, summary?, sharedDescription?, expectedVersion, isPlayerVisible? }`. Players may edit shared fields only when the record is visible; the master may edit all records and visibility. Returns `200 KnowledgeDetail`, `404` for an absent or undiscoverable record to players, `409` for stale versions, and `422` for invalid content.

## Delete an entry

`DELETE /api/campaigns/{campaignId}/knowledge/{entryId}`

Master only. Cascades to contributions, map points, notes and image. Returns `204` or `404`.

## Add a shared contribution

`POST /api/campaigns/{campaignId}/knowledge/{entryId}/contributions`

Body: `{ body }`. Any campaign participant may contribute to a visible record. Returns `201 Contribution`. `PUT` and `DELETE` on a contribution are limited to its author or master. Players receive `404` for hidden records.

## Read/write master notes

`PUT /api/campaigns/{campaignId}/knowledge/{entryId}/master-note`

Master only. Body: `{ body, expectedVersion? }`. Returns `200 { body, version, updatedAt }`. Players receive `404`; a player never gets note fields through any other route.

## Map images and points

`GET /api/campaigns/{campaignId}/knowledge/{entryId}/image` returns raw image bytes with validated content type and cache headers, only when the map is visible to the caller or the caller is master.

`POST /api/campaigns/{campaignId}/knowledge/{entryId}/points` body: `{ title, description?, x, y }`; `PUT .../points/{pointId}` updates; `DELETE .../points/{pointId}` removes. Coordinates are normalized decimals from 0 to 1. Participants may create/edit their own points and the master may manage all points. A point is not independently addressable unless its parent map is visible.

## Realtime

After a successful transaction, notify the campaign group with `CampaignNotebookInvalidated` and no record identifier or private data. Clients refetch through the role-filtered REST API. Reconnect uses the same API snapshot. A failed transaction emits no event.
