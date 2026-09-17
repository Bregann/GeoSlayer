/** Create (id omitted) or update an item. */
export interface SaveItemRequest {
  id?: number
  key: string
  name: string
  description: string
  kind: number
  slot: number
  modifier: number
  modifierValue: number
  secondaryModifier?: number | null
  secondaryModifierValue: number
  tier: number
}
