/** Create (id omitted) or update one rung of the unlock ladder (Stage 18 task 8). */
export interface SaveUnlockRequest {
  id?: number
  adventurerLevel: number
  unlockType: number
  /** The skill or system granted — a SkillType name, or a system key. */
  payload: string
  /** What the unlock celebration shows. */
  displayName: string
}
