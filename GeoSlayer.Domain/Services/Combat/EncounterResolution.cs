namespace GeoSlayer.Domain.Services.Combat
{
    /// <summary>
    /// The rules of a fight (DESIGN.md §5C.3), pure and static so they can be asserted
    /// without a database.
    ///
    /// <para>Auto-resolving by design: the walk was the input. There is no real-time
    /// interaction to get wrong on a phone in someone's pocket.</para>
    /// </summary>
    public static class EncounterResolution
    {
        /// <summary>How long a roaming encounter waits before lapsing.</summary>
        public static readonly TimeSpan RoamingLifetime = TimeSpan.FromHours(6);

        /// <summary>The rest after a loss — the only cost of losing (§5C.3).</summary>
        public static readonly TimeSpan LossCooldown = TimeSpan.FromMinutes(20);

        /// <summary>Metres from the POI a player must be within, matching POI visits.</summary>
        public const double ArrivalRadiusMetres = 50.0;

        /// <summary>
        /// Odds of winning, given the player's Combat level and the encounter's gate.
        ///
        /// <para>Never certain and never hopeless: clamped to 55–95%. A floor matters because
        /// an encounter you cannot win is a wasted walk, and §7.4 says the idle layer must
        /// not punish — meeting something far above your level should still be worth
        /// trying.</para>
        ///
        /// <para>Deliberately generous at parity. Combat is trained by walking to things;
        /// making the walk usually pay is what stops it feeling like a slot machine.</para>
        /// </summary>
        public static double WinChance(int combatLevel, int minCombatLevel)
        {
            var margin = combatLevel - minCombatLevel;
            var chance = 0.75 + (margin * 0.01);

            return Math.Clamp(chance, 0.55, 0.95);
        }

        /// <summary>
        /// Whether this fight is won, seeded so a replayed request cannot re-roll it.
        ///
        /// <para>Same reasoning as <c>DropRoller.SeedFor</c>: the outcome is a property of
        /// this encounter, not of when the client happened to ask.</para>
        /// </summary>
        public static bool Resolve(int encounterId, int combatLevel, int minCombatLevel)
        {
            var roll = new Random(encounterId).NextDouble();

            return roll < WinChance(combatLevel, minCombatLevel);
        }

        /// <summary>
        /// Material units awarded for a win.
        ///
        /// <para>Training grounds pay the same as roaming. Their advantage is that they are
        /// always there (§5C.1) — paying more as well would make historic ground a
        /// requirement rather than a boost, which §5C.2 forbids.</para>
        /// </summary>
        public static int MaterialsFor(int tier) => Math.Max(1, 4 - (tier / 3));

        /// <summary>
        /// XP for a resolved encounter.
        ///
        /// <para>A loss still pays a quarter. Walking to something and getting nothing reads
        /// as a punishment for being out, which is the opposite of the intent.</para>
        /// </summary>
        public static long XpFor(int tier, bool won)
        {
            long baseXp = tier * tier * 25;

            return won ? baseXp : baseXp / 4;
        }

        /// <summary>Whether an encounter is still open at this moment.</summary>
        public static bool IsOpen(DateTime? resolvedUtc, DateTime? expiresUtc, DateTime now) =>
            resolvedUtc is null && (expiresUtc is null || expiresUtc > now);
    }
}
