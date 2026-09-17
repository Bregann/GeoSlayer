export interface ExpeditionDestination {
  poiId: number;
  name: string;
  skill: string | number;
  skillName: string;
  firstVisitUtc: string;
  totalVisits: number;
  distanceMetres: number;
  durationHours: number;
  estimatedMaterials: number;
  isAvailable: boolean;
}
