using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>One material a recipe consumes (DESIGN.md §4.2).</summary>
    public class RecipeInput
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int RecipeId { get; set; }

        public int MaterialId { get; set; }

        public int Quantity { get; set; }

        [ForeignKey(nameof(RecipeId))]
        public virtual Recipe Recipe { get; set; } = null!;

        [ForeignKey(nameof(MaterialId))]
        public virtual Material Material { get; set; } = null!;
    }
}
