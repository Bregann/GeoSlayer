/** Create (id omitted) or update a recipe. */
export interface SaveRecipeRequest {
  id?: number
  key: string
  name: string
  description: string
  skillType: number
  levelRequired: number
  durationSeconds: number
  xpReward: number
  /** Exactly one of these is set — a recipe makes one thing. */
  outputMaterialId: number | null
  outputItemId: number | null
  outputQuantity: number
  inputs: SaveRecipeInput[]
}

export interface SaveRecipeInput {
  materialId: number
  quantity: number
}
