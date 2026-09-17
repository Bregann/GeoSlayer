/** Create (id omitted) or update an encounter definition. */
export interface SaveEncounterRequest {
  id?: number
  key: string
  name: string
  description: string
  tier: number
  minCombatLevel: number
  isTrainingGround: boolean
}
