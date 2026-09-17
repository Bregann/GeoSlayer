/** A player in search results (Stage 18 task 9). */
export interface AdminPlayerSummary {
  playerId: number
  username: string
  adventurerLevel: number
  adventurerXp: number
  coin: number
  lastSyncAtUtc: string | null
}

/**
 * Everything about one player.
 *
 * Read-only — this is the support view. Most questions are answered by seeing what an
 * account actually looks like, without anyone needing to change it.
 */
export interface AdminPlayer {
  playerId: number
  userId: string
  username: string
  email: string
  isAdmin: boolean

  adventurerLevel: number
  adventurerXp: number
  bonusPointsEarned: number
  bonusPointsSpent: number
  curation: number

  coin: number
  coinDeposited: number

  cellsRevealed: number
  claimCount: number
  workerCount: number
  museumEntriesFound: number

  lastSyncAtUtc: string | null

  skills: AdminPlayerSkill[]
  materials: AdminPlayerHolding[]
  items: AdminPlayerHolding[]
}

export interface AdminPlayerSkill {
  skillType: number
  name: string
  level: number
  xp: number
}

export interface AdminPlayerHolding {
  id: number
  key: string
  name: string
  quantity: number
  /** Only meaningful for items; always false for materials. */
  isEquipped: boolean
}
