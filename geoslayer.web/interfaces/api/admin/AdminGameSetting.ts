/** A tunable number as the admin interface sees it (Stage 18). */
export interface AdminGameSetting {
  id: number
  key: string
  value: string

  /** What it shipped as, so drift from the tuned balance is visible. */
  default: string

  category: string
  description: string

  minValue: number
  maxValue: number

  /** True when the current value differs from what shipped. */
  isChanged: boolean
}
