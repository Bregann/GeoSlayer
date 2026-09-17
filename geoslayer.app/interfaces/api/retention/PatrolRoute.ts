import type { PatrolWaypoint } from '@/interfaces/api/retention/PatrolWaypoint';

export interface PatrolRoute {
  id: number;
  name: string;
  waypointCount: number;
  completionCount: number;
  lastCompletedUtc: string | null;
  upkeepReward: number;
  waypoints: PatrolWaypoint[];
}
