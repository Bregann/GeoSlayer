/** An encounter definition as the admin interface sees it (Stage 18 task 8). */
export interface AdminEncounter {
  id: number
  key: string
  name: string
  description: string

  tier: number
  minCombatLevel: number

  /** Permanent, fixed to historic ground — the reliable route (§5C.1). */
  isTrainingGround: boolean

  /**
   * Warnings about the whole ladder, repeated on every row.
   *
   * Set-level because that is what §5C.2 constrains — a single definition is never wrong
   * on its own, only in relation to the others.
   */
  setWarnings: string[]
}
