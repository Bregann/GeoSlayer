/** One line of the admin audit trail (Stage 18 task 9). */
export interface AdminAudit {
  id: number
  adminUsername: string
  entityType: string
  entityId: string | null
  action: string
  detail: string | null
  occurredUtc: string
}
