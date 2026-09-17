# Stage 18 — Admin web interface

> A management interface for the whole game: items, images, rarity, materials, recipes,
> encounters and every other piece of seeded balance data.
>
> **Reference implementation:** [`orbit.web`](https://github.com/Bregann/Orbit/tree/main/orbit.web).

## Status

- **State:** NOT STARTED
- **Completed:** none
- **Remaining:** all tasks.
- **Blockers:** _(none)_

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

- [ ] `geoslayer.web`, Next.js App Router, Mantine, React Query, Tabler icons.
- [ ] `app/api/[...route]/route.ts` proxying to `GeoSlayer.Core`.
- [ ] `helpers/apiClient.ts`, `QueryKeys.ts` and the four mutation wrappers, mirroring
      `geoslayer.app`'s versions.
- [ ] Lint and typecheck wired into a `verify` script, as the app has.

### 2. Authentication and authorisation

- [ ] Log in against the existing `AuthController`, storing the token as `accessToken`.
- [ ] **An `Admin` role, enforced server-side.** Every endpoint in this stage must be
      `[Authorize(Roles = "Admin")]` — a management interface that any logged-in player can
      reach is a way to give yourself a Royal Charter.
- [ ] Seed or document how the first admin is granted.

### 3. Image storage and serving

This is new ground; nothing in the project stores a binary today.

- [ ] Decide storage: filesystem path, or a `bytea` column. Filesystem is simpler to serve
      and back up; a column keeps deployment to one artefact. **Record the choice and why.**
- [ ] `Item.ImagePath` (or equivalent), nullable — every existing item has no image and must
      keep working.
- [ ] Upload endpoint following the `IFormFile` pattern above.
- [ ] A serving endpoint with correct content types and caching.
- [ ] **Validate what is uploaded.** Content type, magnitude, and dimensions. An admin tool
      is still an upload path.
- [ ] App: render the image when present, fall back to the existing emoji when not.

### 4. Item management

- [ ] List every item, filterable by kind, slot and modifier.
- [ ] Create, edit and delete.
- [ ] Edit the modifier and its value — with the enum rendered as names, never ordinals.
- [ ] Image upload per item.
- [ ] **Show what reads each modifier.** §4.3's rule is that an item changing no behaviour
      is a bug; an admin adding a modifier should be able to see it is wired to something.

### 5. Rarity

- [ ] **Rarity does not exist yet.** `Material.IsUnique` is the closest thing, and tiers
      carry most of what rarity would mean. Decide whether rarity is a new axis or a
      presentation of tier before building it — a second axis that duplicates tier is the
      same mistake §4.3 warns about for gear versus Bonus Points.
- [ ] If it is new: a `Rarity` enum, seeded, with drop weights reading it.
- [ ] Manage rarity per item and per material.

### 6. Materials, ladders and drop tables

- [ ] List and edit materials: tier, category, level required, gather seconds, XP per unit.
- [ ] Edit coin pricing multipliers (`CoinPricing`) — currently compile-time constants, so
      this task includes moving them to seeded data.
- [ ] View and edit drop table entries by terrain.
- [ ] **Guard the invariants.** `SkillLaddersDoNotShareAMaterialCategory` and the
      accessibility rules are currently enforced by tests. The interface must not let an
      admin save a state the tests would reject — validate server-side and say why.

### 7. Recipes

- [ ] List, create, edit and delete recipes with their inputs.
- [ ] Show the chain: which skill, which tier, what it consumes, what it produces.
- [ ] Warn when a recipe would be a coin loss (§5D.1's produced-goods rule).

### 8. Encounters, clues and the rest

- [ ] `EncounterDefinition` management.
- [ ] Museum entry definitions and set bonuses.
- [ ] Progression: unlock ladder, upgrade definitions and costs.
- [ ] Surge and expedition tuning.

### 9. Player administration

- [ ] Find a player; view skills, inventory, coin and claims.
- [ ] Grant or remove items and coin, **with an audit trail** — an admin action that changes
      a player's balance and leaves no record is indistinguishable from a bug.

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
