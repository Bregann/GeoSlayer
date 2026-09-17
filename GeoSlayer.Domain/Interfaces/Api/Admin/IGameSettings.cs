namespace GeoSlayer.Domain.Interfaces.Api.Admin
{
    /// <summary>
    /// Tunable numbers, read from the database and cached (Stage 18).
    ///
    /// <para>Every getter takes the shipped default as a fallback. That is not defensive
    /// noise: these are read mid-request while a player is walking, and a missing row should
    /// degrade the balance rather than fail the sync.</para>
    /// </summary>
    public interface IGameSettings
    {
        /// <summary>Loads every setting, replacing what is cached.</summary>
        Task Reload();

        /// <summary>The value for a key, or the shipped default when it is missing.</summary>
        double Get(string key, double fallback);

        /// <summary>The value as an integer, rounded.</summary>
        int GetInt(string key, int fallback);
    }
}
