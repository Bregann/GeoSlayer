export interface Surge {
  id: number;
  description: string;
  targetTerrain: string | number | null;
  targetSkill: string | number | null;
  multiplier: number;
  endsUtc: string;
}
