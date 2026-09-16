namespace GeoSlayer.Domain.Enums;

/// <summary>How a skill is trained (Stage 04 task 1).</summary>
public enum SkillCategory
{
    /// <summary>Trained by walking and visiting — yields raw materials.</summary>
    Gathering,

    /// <summary>Trained by consuming what gathering produced.</summary>
    Production,

    /// <summary>Trained at POIs, without a material chain.</summary>
    Social,
}
