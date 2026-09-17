export interface BankedTransit {
  gridLat: number;
  gridLng: number;
  south: number;
  west: number;
  north: number;
  east: number;
  /** Under 1 for a cycling-pace cell, which banked only partially. */
  weight: number;
  expiresUtc: string;
}
