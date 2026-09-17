using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Admin.Responses
{
    /// <summary>A player in search results (Stage 18 task 9).</summary>
    public class AdminPlayerSummaryDto
    {
        public int PlayerId { get; set; }
        public required string Username { get; set; }
        public int AdventurerLevel { get; set; }
        public long AdventurerXp { get; set; }
        public long Coin { get; set; }
        public DateTime? LastSyncAtUtc { get; set; }
    }

    /// <summary>
    /// Everything about one player (Stage 18 task 9).
    ///
    /// <para>Read-only. This is the support view — "what does their account actually look
    /// like" — which answers most questions without anyone needing to change anything.</para>
    /// </summary>
    public class AdminPlayerDto
    {
        public int PlayerId { get; set; }
        public required string UserId { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
        public bool IsAdmin { get; set; }

        public int AdventurerLevel { get; set; }
        public long AdventurerXp { get; set; }
        public int BonusPointsEarned { get; set; }
        public int BonusPointsSpent { get; set; }
        public long Curation { get; set; }

        public long Coin { get; set; }
        public long CoinDeposited { get; set; }

        public int CellsRevealed { get; set; }
        public int ClaimCount { get; set; }
        public int WorkerCount { get; set; }
        public int MuseumEntriesFound { get; set; }

        public DateTime? LastSyncAtUtc { get; set; }

        public List<AdminPlayerSkillDto> Skills { get; set; } = [];
        public List<AdminPlayerHoldingDto> Materials { get; set; } = [];
        public List<AdminPlayerHoldingDto> Items { get; set; } = [];
    }

    /// <summary>One of a player's skills.</summary>
    public class AdminPlayerSkillDto
    {
        public SkillType SkillType { get; set; }
        public required string Name { get; set; }
        public int Level { get; set; }
        public long Xp { get; set; }
    }

    /// <summary>One line of a player's inventory or equipment.</summary>
    public class AdminPlayerHoldingDto
    {
        public int Id { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }
        public long Quantity { get; set; }

        /// <summary>Only meaningful for items; always false for materials.</summary>
        public bool IsEquipped { get; set; }
    }
}
