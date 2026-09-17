export interface DistrictStatus {
  districtKey: string | null;
  name: string | null;
  description: string | null;
  outputBonus: number;
  claimCount: number;
  nearestName: string | null;
  missingTerrains: string[];
  missingClaims: number;
}
