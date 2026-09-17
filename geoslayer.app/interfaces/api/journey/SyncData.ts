import type { CellDto } from '@/interfaces/api/journey/CellDto';
import type { MaterialGain } from '@/interfaces/api/materials/MaterialGain';
import type { NearbyPoi } from '@/interfaces/api/journey/NearbyPoi';
import type { OfflineAccrual } from '@/interfaces/api/idle/OfflineAccrual';
import type { TransitRedemption } from '@/interfaces/api/retention/TransitRedemption';
import type { UnlockEvent } from '@/interfaces/api/progression/UnlockEvent';

export interface SyncData {
  newCells: CellDto[];
  /** Cumulative Adventurer XP (DESIGN.md §3.0). */
  xp: number;
  /** Adventurer level. */
  level: number;
  /** Ladder rungs crossed by this sync, for the celebration (§3.1c). */
  unlocks: UnlockEvent[];
  /** Bonus Points granted by this sync's level-ups. */
  bonusPointsGranted: number;
  /** Materials picked up this sync (Stage 03). */
  materials: MaterialGain[];
  /** Cells banked as Uncharted Transit rather than revealed (Stage 14, §7.1). */
  transitBanked: number;
  /** Banked transit this walk redeemed, or null when none was. */
  transitRedemption: TransitRedemption | null;
  /** What workers produced while away, or null when nothing did (Stage 05). */
  offlineAccrual: OfflineAccrual | null;
  nearbyPois: NearbyPoi[];
}
