/** Create (id omitted) or update a material. */
export interface SaveMaterialRequest {
  id?: number
  key: string
  name: string
  category: number
  skillType: number | null
  tier: number
  levelRequired: number
  baseGatherSeconds: number
  xpPerUnit: number
  isUnique: boolean
}
