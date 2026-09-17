# geoslayer.web

The admin interface — game management for GeoSlayer. See `stages/STAGE-18-admin-web.md`.

Built to mirror [`orbit.web`](https://github.com/Bregann/Orbit/tree/main/orbit.web):
Next.js App Router, Mantine, React Query, Tabler icons, and the same
`helpers/QueryKeys` + `helpers/mutations/*` + `interfaces/api/<feature>/` layout that
`geoslayer.app` already uses.

## Running it

```bash
npm install
npm run dev        # http://localhost:3000
npm run verify     # tsc --noEmit && eslint
```

The API is expected at `http://localhost:5199/api` in development, or `API_BASE_URL` in
production.

> **`npm run build` will not complete on the dev box** — it exhausts the available RAM, the
> same way `npx expo export` does for the app. `npm run verify` is the check that runs
> here; build on a machine with more headroom.

## How it hangs together

**Requests go through the Next proxy**, never straight to the API. `app/api/[...route]`
attaches the access token from an httpOnly cookie and streams the body through. That keeps
the token out of JavaScript — an XSS cannot read it — and means there is no CORS to
configure, because everything is same-origin from the browser's point of view.

**Login is its own route.** The API returns tokens in a response body; `app/auth/login`
exchanges that for httpOnly cookies rather than handing it to the client.

**Pages prefetch, components interact.** Each `page.tsx` is a server component that
prefetches into a `QueryClient`, dehydrates it into a `HydrationBoundary`, and renders a
client component from `components/pages/`. This is Orbit's pattern.

**Enums are mirrored, and a test keeps them honest.** `interfaces/api/admin/ItemEnums.ts`
holds ordered arrays whose *index is the wire value*. Reordering a C# enum would silently
change what a dropdown saves, so `WebEnumParityTests` on the server reads this file and
fails the build if it drifts.

## What is here

- **Items** — list, create, edit, delete, and upload images. Flags any item whose modifier
  nothing reads (DESIGN.md §4.3).
- **Audit trail** — read-only. Every admin mutation, newest first.

Everything else in Stage 18 (materials, recipes, encounters, rarity, player admin) is
planned but unbuilt.
