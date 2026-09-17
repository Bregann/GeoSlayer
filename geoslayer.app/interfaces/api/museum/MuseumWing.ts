import type { MuseumEntry } from '@/interfaces/api/museum/MuseumEntry';

export interface MuseumWing {
  wing: string | number;
  name: string;
  found: number;
  total: number;
  isComplete: boolean;
  entries: MuseumEntry[];
}
