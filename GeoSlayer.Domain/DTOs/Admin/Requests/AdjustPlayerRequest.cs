namespace GeoSlayer.Domain.DTOs.Admin.Requests
{
    /// <summary>
    /// Change a player's coin, materials or items (Stage 18 task 9).
    ///
    /// <para>One request shape for grants and removals, with a signed <see cref="Delta"/>
    /// rather than separate endpoints. Two endpoints that differ only in sign is two places
    /// to forget the audit entry.</para>
    /// </summary>
    public class AdjustPlayerRequest
    {
        public int PlayerId { get; set; }

        /// <summary>
        /// How much to add. Negative removes.
        ///
        /// <para>Uncapped — an admin is trusted and the audit trail is the control. What is
        /// <i>not</i> allowed is taking a player negative: removals clamp at zero, because a
        /// negative balance is a state no game rule produces and nothing downstream expects.</para>
        /// </summary>
        public long Delta { get; set; }

        /// <summary>
        /// Why. Required, and recorded verbatim in the audit trail.
        ///
        /// <para>The point of an audit entry is answering "why did this happen" months
        /// later, and "admin changed coin by +5000" does not answer it. A support ticket
        /// number or a sentence does.</para>
        /// </summary>
        public required string Reason { get; set; }
    }

    /// <summary>Change how much of a material a player holds.</summary>
    public class AdjustPlayerMaterialRequest : AdjustPlayerRequest
    {
        public int MaterialId { get; set; }
    }

    /// <summary>Change how many of an item a player holds.</summary>
    public class AdjustPlayerItemRequest : AdjustPlayerRequest
    {
        public int ItemId { get; set; }
    }
}
