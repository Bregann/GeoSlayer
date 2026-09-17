/** An item as the admin interface sees it (DESIGN.md §4.3, Stage 18 task 4). */
export interface AdminItem {
  id: number
  key: string
  name: string
  description: string

  kind: number
  slot: number
  modifier: number
  modifierValue: number
  secondaryModifier: number | null
  secondaryModifierValue: number
  tier: number

  /** The same effect text the player app shows. */
  modifierText: string

  hasImage: boolean

  /**
   * Whether anything in the game reads this item's modifier.
   *
   * §4.3: an equipped item that changes no behaviour is a bug. Surfaced so an admin can
   * see the wiring rather than discover months later that it never did anything.
   */
  modifierIsRead: boolean
}
