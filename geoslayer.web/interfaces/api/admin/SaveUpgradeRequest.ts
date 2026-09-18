/** Create (id omitted) or update a Bonus Point upgrade (Stage 18 task 8). */
export interface SaveUpgradeRequest {
  id?: number
  key: string
  name: string
  category: string
  description: string
  maxRank: number

  /**
   * Comma-separated cost per rank, e.g. "1,2,4,7,11".
   *
   * Validated server-side before storage: UpgradeDefinition.Costs parses this with
   * int.Parse on every upgrades-screen load, so a malformed value would crash that screen
   * for every player.
   */
  costCurve: string

  effectPerRank: number
  minAdventurerLevel: number
}
