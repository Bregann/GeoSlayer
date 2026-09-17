/** Coin in hand and on deposit (DESIGN.md §5.4a). */
export interface BankAccount {
  coin: number;
  /** Earns interest, but must be withdrawn before it can be spent. */
  deposited: number;
  interestJustPaid: number;
  /** What a full offline window pays at the current balance. */
  interestPerFullWindow: number;
  capHours: number;
}
