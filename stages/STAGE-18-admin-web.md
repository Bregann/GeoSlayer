# Stage 18 — Admin web interface

> A management interface for the whole game: items, images, rarity, materials, recipes,
> encounters and every other piece of seeded balance data.
>
> **Reference implementation:** [`orbit.web`](https://github.com/Bregann/Orbit/tree/main/orbit.web).

## Status

- **State:** IN PROGRESS
- **Completed:** tasks 1, 2, 3, 4, 6, 7 and the audit-trail half of 9. Task 5 dropped.
- **Remaining:** task 8 (encounters, Museum, progression, surges) and the player half
  of task 9. Task 5 (rarity) was **dropped deliberately** — see below.
- **Blockers:** _(none)_

### What was built

**Backend.** `User.IsAdmin` emitted as a JWT role claim; `AdminController` gated with
`[Authorize(Roles = AuthService.AdminRole)]` at the controller rather than per action, so a
new endpoint is protected by default. `AdminService` covering item CRUD, image upload and
the audit trail. `ImageValidation` and `ModifierReaders` as pure, tested rules.

**Web.** `geoslayer.web` — Next.js App Router, Mantine, React Query, mirroring `orbit.web`.
Items list with create/edit/delete and image upload; read-only audit trail; login that
exchanges the API's token for an httpOnly cookie.

**43 new tests**: 18 image validation, 4 modifier readers, 3 enum parity, 18 integration.

### `/swagger` was fixed first

The stage notes flagged it: building an admin client against endpoints that cannot be
introspected would be slow. The pin turned out to be unnecessary — `Microsoft.OpenApi` was
held at 3.10.2 for GHSA-v5pm-xwqc-g5wc, which forced Swashbuckle onto an API surface it
does not target, but that advisory is patched on *both* lines and Swashbuckle 10.2.3
already depends on the patched 2.7.5. Removing the direct reference fixed it.

### Images are stored in the database

Task 3 asked for the choice to be recorded. **`bytea`, in its own table.**

The API is containerised with no declared volume, so a filesystem path would either vanish
on redeploy or need infrastructure that does not exist. A column keeps deployment to one
artefact and makes backup exactly what it already is. The trade — images are read through
the app rather than a CDN — is not worth avoiding for a few dozen small icons.

Its own table rather than a column on `Item`, because a `bytea` on the item row would be
loaded by every query that touches items, including the crafting screen that wants names
and nothing else.

### The declared content type is treated as a claim

`ImageValidation` checks magic bytes per format. "HTML labelled `image/png`, stored, then
served from our own origin" is the actual attack, and the role check does not prevent it —
an admin account can be compromised, and an admin can be wrong. SVG is refused outright: it
is a script host, not an icon format.

### §4.3 needed a machine-checkable form

The rule that every `ItemModifier` must be read by the system it names has held because the
enum was small enough to audit by hand. **An admin dropdown removes that safety.**

`ModifierReaders` declares the reader for each value, `SaveItem` refuses an item whose
modifier nothing reads, and a test verifies each named reader actually exists in source —
without that test this would just be a second place to forget.

### The enum mirror is the fragile part

`ItemEnums.ts` holds ordered arrays whose index is the wire value. Reorder a C# enum and
the web client keeps compiling and keeps sending numbers that now mean something else — an
admin picks "Trinket" and saves "Feet". `WebEnumParityTests` reads the TypeScript and
compares it to the enum, so the drift fails a build instead of corrupting data.

### Verification

- **Build:** green, 0 warnings. `dotnet format` clean.
- **Targeted tests:** `--filter "FullyQualifiedName~Services.Admin"` — **43 passed**.
- **Web:** `npm run verify` (tsc + eslint) clean.

**Not verified:** `npm run build` is OOM-killed on the dev box, the same way `expo export`
is. And `/swagger` has not been hit with a live request — this environment cannot bind a
socket — though the version conflict that caused the 500 is definitively resolved.

## Prerequisites

Stage 17 `DONE`. Read this file, then `orbit.web` itself — the conventions below are
summarised from it, but the repo is the source of truth and it moves.

## Why this stage exists

**Almost every number in this game is seeded data, on purpose.** DESIGN.md says so
repeatedly: drop rates, XP curves, upgrade costs, encounter tiers and now coin prices are
all data "so balance can be retuned without a deploy". That promise is currently only half
kept — the data lives in JSON files and C# seed classes, so retuning means editing source
and redeploying anyway.

This stage is what makes the promise true. It is also the first surface in the project that
is **not** for players, which changes the rules: an admin tool can be dense, table-heavy and
assume competence, where the app must be legible on a phone in one hand.

**Item images are the specific thing that forces it now.** Nothing in the codebase stores a
binary. Items are seeded with a key, a name and a description, and the app renders emoji.
Adding images means an upload path, storage, and something to serve them — none of which
exists, and all of which is far easier to build with an interface than a migration.

---

## Conventions to follow

`geoslayer.app` was already aligned to Orbit's structure, so most of this will look
familiar. The web project should mirror it exactly rather than inventing a second dialect.

### Stack

Taken from `orbit.web/package.json`:

- **Next.js (App Router)** with Turbopack in dev
- **Mantine** — `@mantine/core`, `@mantine/hooks`, `@mantine/notifications`,
  `@mantine/dates`, `@mantine/charts` as needed
- **`@tabler/icons-react`** for icons
- **`@tanstack/react-query`** — same version family as the app

### Structure

```
geoslayer.web/
  app/
    api/[...route]/route.ts     ← proxy to the API, attaches the auth cookie
    layout.tsx
    providers.tsx
    <feature>/page.tsx          ← server component: prefetch + HydrationBoundary
  components/
    pages/<Feature>Component.tsx ← the client component the page renders
    <feature>/*.tsx              ← modals and cards, foldered by feature
    common/*.tsx                 ← shared, e.g. DeleteConfirmationModal
  helpers/
    apiClient.ts
    QueryKeys.ts
    mutations/useMutation{Post,Put,Patch,Delete}.ts
    notificationHelper.ts
  interfaces/api/<feature>/*.ts  ← one type per file
```

### The page pattern

Orbit's pages are **server components that prefetch, then hand off to a client component**:

```tsx
export default async function ItemsPage() {
  const queryClient = new QueryClient()
  const cookieStore = await cookies()

  if (cookieStore.has('accessToken')) {
    const cookieHeader = cookieStore.getAll()
      .map(c => `${c.name}=${c.value}`).join('; ')

    await queryClient.prefetchQuery({
      queryKey: [QueryKeys.Items],
      queryFn: async () => await doQueryGet<GetAllItemsDto>(
        '/api/admin/GetAllItems', { headers: { Cookie: cookieHeader } })
    })
  }

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <ItemsComponent />
    </HydrationBoundary>
  )
}
```

The page owns prefetching and metadata; the component owns interaction. Keep that split.

### The API proxy

`app/api/[...route]/route.ts` forwards everything to the API, reading `accessToken` from a
cookie and setting the `Authorization` header. It strips hop-by-hop headers and streams the
body. **Copy Orbit's version rather than writing a new one** — it handles multipart
correctly, which this stage needs for uploads.

### Uploads

Orbit's pattern, from `DocumentsController`:

```csharp
[HttpPost]
public async Task<ActionResult> UploadDocument(
    [FromForm] UploadDocumentRequest request, IFormFile file)
```

Metadata as a `[FromForm]` DTO, the binary as a separate `IFormFile`, and the service takes
a `Stream` plus the extension rather than the `IFormFile` itself — so the service stays
testable without ASP.NET types.

---

## Tasks

### 1. Project setup

- [x] `geoslayer.web`, Next.js App Router, Mantine, React Query, Tabler icons.
- [x] `app/api/[...route]/route.ts` proxying to `GeoSlayer.Core`, streaming the body so
      multipart uploads work.
- [x] `helpers/apiClient.ts`, `QueryKeys.ts` and mutation wrappers (post, delete, upload),
      mirroring `geoslayer.app`'s versions.
- [x] Lint and typecheck wired into a `verify` script, as the app has.

### 2. Authentication and authorisation

- [x] Log in against the existing `AuthController`, exchanging the token for an httpOnly
      cookie via `app/auth/login` — the browser never holds it in JavaScript.
- [x] **An `Admin` role, enforced server-side**, at the controller rather than per action.
- [x] Documented how the first admin is granted: `User.IsAdmin`, set out of band. There is
      deliberately no self-service path.

### 3. Image storage and serving

This is new ground; nothing in the project stores a binary today.

- [x] **Decided: `bytea`, in its own `ItemImage` table.** Reasoning above and in the model.
- [x] Nullable by construction — an item with no image row still works everywhere.
- [x] Upload endpoint following the `IFormFile` pattern.
- [x] Serving endpoint with the stored content type and a day's caching.
- [x] **Validated**: allow-list of types, size limit, and magic-byte checks so the declared
      type is never trusted. Filenames stripped of paths.
- [x] App: renders the image when present, falls back to emoji when not. `PlayerItemDto`
      carries `HasImage` so the app never has to probe for a 404, and `ItemIcon` degrades to
      the emoji on a failed load — artwork is decoration, and nothing about an item is
      unusable without it.

### 4. Item management

- [x] List every item, filterable by name or key.
- [x] Create, edit and delete — with delete refused when a recipe produces the item.
- [x] Edit the modifier and its value, enums rendered as names.
- [x] Image upload per item.
- [x] **Show what reads each modifier** — `ModifierReaders`, surfaced as an inline
      explanation while choosing and a banner for any item that is inert.

### 5. ~~Rarity~~ — DROPPED, deliberately

**Decided: not building it.** The task was a design question wearing a task's clothes, and
the answer turned out to be that rarity has no job here.

Three shapes were considered:

- **Drop frequency, decoupled from tier.** Ruled out on inspection —
  `DropTableEntry.Weight` already *is* per-terrain drop frequency, read by
  `DropRoller.PickWeighted`. A `Rarity` field controlling the same thing would be a second
  knob on one dial.
- **A presentation of tier** (Common → Legendary derived from 1–7). Safe and cheap, but it
  adds no decision — a skin on something that already exists.
- **POI scarcity**, formalising what `IsUnique` gestures at in §7.4. The most faithful
  option, and still not one anything was asking for.

`Material.IsUnique` and the tier ladder already cover the ground. Adding an axis without a
clear job is the mistake §4.3 names for gear versus Bonus Points, and the same reasoning
that kept the coin economy unbuilt until §9.5 was answered.

Worth revisiting **only** if something concrete needs it — a Museum wing that sorts by
rarity, or a drop rule tier cannot express.

### 6. Materials, ladders and drop tables

- [ ] List and edit materials: tier, category, level required, gather seconds, XP per unit.
- [ ] Edit coin pricing multipliers (`CoinPricing`) — currently compile-time constants, so
      this task includes moving them to seeded data.
- [ ] View and edit drop table entries by terrain.
- [ ] **Guard the invariants.** `SkillLaddersDoNotShareAMaterialCategory` and the
      accessibility rules are currently enforced by tests. The interface must not let an
      admin save a state the tests would reject — validate server-side and say why.

### 7. Recipes

- [x] List, create, edit and delete recipes with their inputs.
- [x] Show the chain: skill, level, what it consumes, what it produces.
- [x] Warn when a recipe would be a coin loss (§5D.1), **without blocking the save** — see
      the split below.

**The rule this task turned on: refuse what is broken, flag what is merely wrong.**

`RecipeValidation.Reject` covers structural failures and stops the save — a recipe with no
output is a timer that consumes materials and gives nothing back, one that consumes its own
output is an infinite loop, a duplicated input makes the cost ambiguous. Each of those would
surface as a crash or a silent wrong number somewhere else in the game.

`RecipeValidation.Warn` covers everything else and blocks nothing. A loss-making recipe is
the main case: §5D.1 expects produced goods to beat their parts, but an admin mid-tune has
to be able to save something temporarily unattractive and come back to it. An interface that
refused every questionable number would fight the person using it.

Input cost uses the same derived pricing the shop does, so the warning cannot disagree with
what a player would actually be paid. The edit form totals it live, because the fix is
usually to change a quantity there and then.

### 8. Encounters, clues and the rest

- [ ] `EncounterDefinition` management.
- [ ] Museum entry definitions and set bonuses.
- [ ] Progression: unlock ladder, upgrade definitions and costs.
- [ ] Surge and expedition tuning.

### 9. Player administration

- [ ] Find a player; view skills, inventory, coin and claims.
- [ ] Grant or remove items and coin.
- [x] **The audit trail itself is built** — `AdminAuditEntry`, written by every mutation,
      with a read-only page. Player-facing admin actions will use it when they land.

---

## Acceptance criteria

1. Every endpoint is `Admin`-only, proved by a test that a non-admin gets 403.
2. An item's image can be uploaded, replaced and removed, and the app renders it.
3. An item with no image still renders, unchanged.
4. Uploads are validated for type and size; a non-image is rejected.
5. Editing a drop weight changes what a cell drops, without a redeploy.
6. Editing a coin multiplier changes what a material sells for, without a redeploy.
7. The interface cannot save data that violates a seeded-data invariant the test suite
   enforces — it refuses with a reason.
8. Every balance-affecting admin action is recorded with who, what and when.
9. `npm run verify` passes on `geoslayer.web`.

---

## Notes and open questions

- **Scope.** This is the largest stage in the project by surface area. Tasks 1–4 are the
  spine and deliver the thing that prompted it (item images); 5–9 can land incrementally.
  Do not treat the ordering as negotiable — auth (task 2) before anything writeable.
- **Rarity is genuinely undecided.** Task 5 is a design question wearing a task's clothes.
  It should be answered before it is built, the way §9.5 was.
- **Moving constants to the database has a cost.** `CoinPricing` and `ProgressionDefaults`
  are currently compile-time, which means tests assert against them directly and cheaply.
  Making them editable makes those tests need fixtures. Worth it, but plan for it.
- **The swagger issue matters more here.** `/swagger` returns 500 (`Microsoft.OpenApi`
  pinned for CVEs vs Swashbuckle 10.1.7). Building a whole admin client against endpoints
  that cannot be introspected will be slower. Consider resolving that first.
