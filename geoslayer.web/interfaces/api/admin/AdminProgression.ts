/** The unlock ladder and upgrade tree (Stage 18 task 8). */
export interface AdminProgression {
  unlocks: AdminUnlock[]
  upgrades: AdminUpgrade[]
  /** Warnings about the ladder as a whole, such as the §3.1 cold start. */
  ladderWarnings: string[]
}

/** One rung of the unlock ladder (§3.1). */
export interface AdminUnlock {
  id: number
  adventurerLevel: number
  unlockType: number
  payload: string
  displayName: string
}

/** One Bonus Point upgrade (§3.0a). */
export interface AdminUpgrade {
  id: number
  key: string
  name: string
  category: string
  description: string
  maxRank: number
  /** Comma-separated cost per rank, e.g. "1,2,4,7,11". */
  costCurve: string
  effectPerRank: number
  minAdventurerLevel: number
  /** Total Bonus Points to max it — the number a curve makes hard to eyeball. */
  totalCost: number
}
