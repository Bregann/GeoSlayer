namespace GeoSlayer.Domain.DTOs.Admin.Requests
{
    /// <summary>
    /// Change one tunable number (Stage 18).
    ///
    /// <para>Only the value is editable. The key, bounds and description are part of the
    /// setting's definition, which lives in <c>GameSettingKeys</c> — letting an admin edit
    /// those would mean a row whose bounds no longer match what the code expects.</para>
    /// </summary>
    public class SaveGameSettingRequest
    {
        public int Id { get; set; }
        public required string Value { get; set; }
    }
}
