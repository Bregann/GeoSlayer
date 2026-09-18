/** A Museum entry definition as the admin interface sees it (Stage 18 task 8). */
export interface AdminMuseumEntry {
  id: number
  key: string
  name: string
  description: string

  wing: number
  rarity: number

  /** What the empty plinth says — §5A.1 rests on this reading as a pull. */
  unlockCondition: string

  sortOrder: number

  /** How many players have found it, for judging whether it is too rare. */
  foundBy: number

  /** True when artwork has been uploaded for this entry. */
  hasSprite: boolean

  setWarnings: string[]
}
