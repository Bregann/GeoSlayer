/** A recipe as the admin interface sees it (Stage 18 task 7). */
export interface AdminRecipe {
  id: number
  key: string
  name: string
  description: string

  skillType: number
  levelRequired: number
  durationSeconds: number
  xpReward: number

  outputMaterialId: number | null
  outputItemId: number | null
  outputQuantity: number
  /** What it makes, named — the chain is unreadable as bare ids. */
  outputName: string | null

  inputs: AdminRecipeInput[]

  /** Coin value of everything consumed (§5D.1). */
  inputCost: number
  /** Coin value of what comes out. Zero for an item — its worth is its modifier. */
  outputValue: number

  /**
   * Things worth knowing, none of which blocked the save.
   *
   * A loss-making recipe is the headline: §5D.1 expects produced goods to beat their
   * parts, but an admin mid-tune must be able to save something temporarily unattractive.
   */
  warnings: string[]
}

export interface AdminRecipeInput {
  materialId: number
  materialKey: string
  materialName: string
  quantity: number
}
