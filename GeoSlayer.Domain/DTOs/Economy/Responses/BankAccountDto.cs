namespace GeoSlayer.Domain.DTOs.Economy.Responses
{
    /// <summary>A player's coin, in hand and on deposit (DESIGN.md §5.4a).</summary>
    public class BankAccountDto
    {
        /// <summary>Spendable coin.</summary>
        public long Coin { get; set; }

        /// <summary>Coin on deposit. Earns interest, but must be withdrawn to spend.</summary>
        public long Deposited { get; set; }

        /// <summary>Interest settled onto the balance by this request.</summary>
        public long InterestJustPaid { get; set; }

        /// <summary>
        /// What a full offline window would pay at the current balance.
        ///
        /// <para>Shown because an invisible bonus is one the player never trusts — and
        /// because the rate is deliberately small, so it needs stating rather than
        /// discovering.</para>
        /// </summary>
        public long InterestPerFullWindow { get; set; }

        /// <summary>Hours of interest one absence can earn — base plus Offline Cap upgrades.</summary>
        public double CapHours { get; set; }
    }
}
