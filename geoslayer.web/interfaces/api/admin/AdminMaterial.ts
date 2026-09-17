/** A material as the admin interface sees it (Stage 18 task 6). */
export interface AdminMaterial {
  id: number
  key: string
  name: string

  category: number
  skillType: number | null
  tier: number
  levelRequired: number
  baseGatherSeconds: number
  xpPerUnit: number
  isUnique: boolean

  /** Derived from tier and category (§5D.1) — shown, not editable. */
  unitPrice: number

  /** XP per gathering second: the number §4.1a constrains. */
  xpPerSecond: number

  /** True when artwork has been uploaded for this material. */
  hasSprite: boolean
}
