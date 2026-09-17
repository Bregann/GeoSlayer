# Stage 17 — The coin economy

> Selling at shops, banking, and the end of stack caps. See `DESIGN.md` §5D.

## Status

- **State:** DONE
- **Completed:** all tasks
- **Remaining:** the coin *sink* is deliberately unbuilt — see below.

### What was built

`Player.Coin` / `CoinDeposited` / `InterestSettledUtc`; pure rules in `CoinPricing` and
`BankingInterest`; an `EconomyService` and controller covering sell, sell-junk, deposit and
withdraw; shop and bank screens reached from the POI modal. Stack caps were removed
throughout, and the three modifiers that existed only to raise them were repointed at sell
price.

**43 new tests** (12 pricing, 13 interest, 18 integration).

### The question this stage actually answered

`DESIGN.md` §9.5 asked whether to add a currency and what it would be for. Reading the code
first changed the question, and that is the most useful thing recorded here.

**Dust was already the second currency with no job.** It was seeded, produced by two
different sources — stack overflow and `TerrainType.Open` cells — and `DustKey` appeared in
exactly one place in the codebase: the code that *creates* it. Nothing consumed it. No
recipe took it. Its stack cap was 1,000,000, which is the tell: it was built as a pressure
valve, not as money.

So the real choice was never "should we add a currency" but "what do we do about the one
already here". That reframing is what made the rest obvious.

**Dust also meant two opposite things.** Overflow Dust scales with how *well* you are
doing — full stacks mean a lot of it. Open-terrain Dust scales with how *bad* your
geography is. The same resource was simultaneously "I have too much of everything" and "I
have nothing worth having", which is an awkward foundation for money and a good reason not
to simply rename it.

### Stack caps were removed, reversing §7.4

This is the largest single reversal in the project and it is recorded in `DESIGN.md` §7.4
rather than left implicit.

The original rule capped each material, converting overflow to Dust so that filling up
created pressure to return. In practice:

- **Overflow was a deletion dressed as a conversion.** Dust bought nothing, so a good
  night's gathering became a number that did nothing.
- **The pressure was punitive.** "Come back or lose value" is a chore. "Come back and sell
  this" is an errand, and it is the same walk.

The offline *time* cap already does the pacing work — §9.3 says as much when rejecting
stamina. Caps-as-pressure was always the weaker lever and the only one generating busywork.

`PlayerMaterial.Quantity` was already `long`, so no schema change was needed on the holding
side; the migration only drops the two columns describing the old rule, and no player state
is touched.

### Three modifiers had to move, not die

Storehouse, the Museum Skills wing bonus, and the Banking→stack-cap synergy (built one pass
earlier) all existed solely to raise caps. §4.3 is explicit that a modifier nothing reads is
a bug, so leaving them would have been worse than deleting them.

All three were repointed to **sell price** instead — `StackCapPercent` became
`SellPricePercent`. The acquisition routes and the feel are unchanged: each still rewards
the player who gathers more than they immediately need. Banking raising what a haul fetches
is a better thematic fit than Banking raising storage ever was.

The Storehouse became the **Counting House** to match.

### Banking's ladder collided with real currency

Banking's seven tiers were Copper Coin → Royal Charter, in `MaterialCategory.Coin`. Once
`Player.Coin` existed, "sell 40 Gold Coins for 800 coin" was going to read as a bug rather
than a trade.

Reflavoured to valuables: **Copper Token, Silver Bar, Gold Bullion**, the rest unchanged.
They are the highest-priced category, which gives a bank a reason to be visited before
deposits are unlocked at all.

### The architectural guard caught this stage too

`NoServiceCode_BranchesOnASpecificSkill` failed on `EconomyService`, on a single line that
only chose an *error message*:

```csharp
required == SkillType.Trading ? "…does not buy anything" : "…is not a bank."
```

Arguably trivial. But the guard is right that this is where per-skill copy-paste starts, and
the fix was better than an exemption: `EconomySeedData.Venue` carries the skill *and* its
refusal text together, so the message cannot drift from the rule and a third kind of venue
is a row rather than a branch.

Same outcome as Stage 16's encounter with the same guard. It has now caught two real design
smells, which is a good argument for keeping it.

### Interest is bounded twice, deliberately

Interest on an uncapped balance is how an idle economy runs away from every other income
source. Two bounds, both tested:

- **Accrues only over the offline-cap window**, matching `OfflineAccrual.BaseOfflineCapHours`
  exactly. A month away pays what one window pays. A test asserts the two constants match,
  so they cannot drift into two rules a player has to learn separately.
- **Simple, not compounding.**

The rate is 0.1%/hour — small on purpose. `FrequentSyncing_NeverOutEarnsWaiting` exists
because the app polls constantly: if each tiny settlement rounded up, a client polling every
ten seconds would out-earn one that waited.

### Verification

- **Build:** green, 0 warnings.
- **Targeted tests:** `--filter "FullyQualifiedName~Economy|…BranchesOnASpecificSkill"` —
  **44 passed, 0 failed**.
- **App:** `npm run verify` (tsc + expo lint) clean.
- **Migration:** `Stage17CoinEconomy` scaffolded; drops `StackCap` and `DustPerOverflow`,
  adds three columns to `Players`.

**Not verified:** the full suite has not been run to completion since the guard fix. The dev
box cannot take repeated Testcontainers runs — see the note in `STATUS.md`.

- **Blockers:** _(none)_

## Prerequisites

Stage 16 `DONE`. Read `DESIGN.md` §5D, §7.4 and §9.5.

## Goal

Give coin a job before it exists, then build it. Selling is a **reason to walk somewhere** —
a location mechanic, not a menu — which is the only form a currency can take in a game whose
premise is going outside.

**The rule that must not break:** shops are dense even where terrain is thin, so selling
must never become a geography gate. A rural player walks further to sell, the same way they
walk further for everything, and is never locked out (§4.1a).

---

## Tasks

### 1. Coin state and pricing

- [x] `Player.Coin`, `CoinDeposited`, `InterestSettledUtc`.
- [x] `CoinPricing`: `tier²` × per-category multiplier, derived rather than enumerated.
- [x] Every material worth ≥ 1 coin, so a walk across featureless ground still pays.
- [x] Coin per gathering second rises with tier — advancing must never pay worse (§4.1a).
- [x] Produced goods outvalue their inputs, or crafting loses money.

### 2. Remove stack caps

- [x] Drop `Material.StackCap` and `Material.DustPerOverflow`.
- [x] `GrantMaterials` accepts everything; no overflow path remains.
- [x] `StackCapPercent` → `SellPricePercent`, with all three sources repointed.
- [x] App: inventory shows value rather than fullness; overflow toasts removed.

### 3. Selling at shops

- [x] `EconomyService.Sell` and `SellJunk`, at Trading POIs only.
- [x] Position validated server-side against the last verified fix (§7.2).
- [x] Selling more than held sells what is held — a stale screen must not fail a sale.
- [x] Junk shortcut reaches low tiers only, and never Relics.

### 4. Banking

- [x] Deposit and withdraw at Banking POIs.
- [x] Interest bounded by the offline cap, simple not compounding, rounded down.
- [x] Deposited coin is not spendable until withdrawn.
- [x] Banking ladder reflavoured off currency names.

### 5. App surfaces

- [x] `app/shop.tsx` and `app/bank.tsx`, reached from the POI modal in range.
- [x] `helpers/economy.ts` for presentation logic.

---

## Acceptance criteria

1. ✅ Coin is earned only while standing at a Trading POI — `SellingFromOutOfRange_IsRejected`,
   `SellingWithNoVerifiedPosition_IsRejected`, `SellingAtANonShop_IsRejected`.
2. ✅ Every seeded material is worth at least one coin —
   `EverySeededMaterial_HasAPriceOfAtLeastOne`.
3. ✅ Every category is priced deliberately, not by fallback — `EveryCategory_IsPricedDeliberately`.
4. ✅ Advancing never pays worse — `HigherTiers_PayMorePerGatherSecond`.
5. ✅ Crafting is never a loss — `ProducedGoods_OutvalueRawOnesOfTheSameTier`.
6. ✅ Nothing is ever lost to a cap — `NothingIsEverConvertedToDustOnGather`,
   `RepeatedGathering_AccumulatesWithoutBound`.
7. ✅ The junk shortcut never takes a Relic — `SellJunk_NeverSellsARelic`.
8. ✅ Interest is bounded by the offline cap — `InterestStopsAtTheOfflineCap`.
9. ✅ Polling never out-earns patience — `FrequentSyncing_NeverOutEarnsWaiting`.
10. ✅ Deposited coin is not spendable — `DepositedCoin_IsNotSpendable`.
11. ✅ Interest is never paid twice for the same hours — `InterestIsNotPaidTwiceForTheSameTime`.
12. ✅ Sell-price sources are read — `ACountingHouse_MeasurablyRaisesSellPrice`,
    `BankingLevel_RaisesWhatASaleFetches`.

---

## Not done, and recorded rather than ticked

- **The coin sink.** Upkeep is still paid in food alone, and nothing else in the game has a
  coin price. Coin currently stores value and makes a full satchel worth something, which is
  a real job — but what it *buys* is the next design question, and it should be designed
  rather than accreted. `DESIGN.md` §5D.3.
- **Coin is not on the HUD.** It is shown on the shop and bank screens, where it matters.
  A persistent balance is worth adding once something charges for anything.

## Needs a human

- **Whether prices feel right.** Every multiplier is reasoned from what a category costs to
  reach, not playtested. All in `CoinPricing`, one file, no deploy needed to retune.
- **Whether interest reads as a nudge or as pointless.** It is deliberately tiny. A player
  who deposits 10,000 coin and returns to 40 might read that as insulting rather than
  modest — the framing in `interestSummary` is the thing to check, as much as the number.
- **Whether removing caps loses anything.** The bet is that the offline time cap does all
  the pacing work and caps only ever added busywork. Only real play tells you whether the
  bag filling up was providing a rhythm nobody missed until it went.
