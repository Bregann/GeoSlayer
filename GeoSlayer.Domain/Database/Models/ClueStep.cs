using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>One step of a clue scroll (DESIGN.md §5B.2).</summary>
    public class ClueStep
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int ScrollId { get; set; }

        public int StepIndex { get; set; }

        public ClueStepType StepType { get; set; }

        /// <summary>The POI that answers this step, for <c>Direct</c> and <c>Cryptic</c>.</summary>
        public int? TargetPoiId { get; set; }

        /// <summary>The skill category that answers a <c>Category</c> step.</summary>
        public SkillType? TargetSkill { get; set; }

        public double? TargetLat { get; set; }
        public double? TargetLng { get; set; }

        /// <summary>Metres within which arrival counts.</summary>
        public double? TargetRadius { get; set; }

        /// <summary>What the player reads. Generated, never hand-authored.</summary>
        [Required, MaxLength(512)]
        public string RiddleText { get; set; } = null!;

        public DateTime? SolvedUtc { get; set; }

        /// <summary>True when this step was skipped rather than solved.</summary>
        public bool WasSkipped { get; set; }

        [ForeignKey(nameof(ScrollId))]
        public virtual PlayerClueScroll Scroll { get; set; } = null!;
    }
}
