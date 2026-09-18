/** Create (id omitted) or update a Museum entry definition (Stage 18 task 8). */
export interface SaveMuseumEntryRequest {
  id?: number
  key: string
  name: string
  description: string
  wing: number
  rarity: number
  /** What the empty plinth says — §5A.1 rests on this reading as a pull. */
  unlockCondition: string
  sortOrder: number
}
