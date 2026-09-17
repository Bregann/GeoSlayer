import type { MuseumWing } from '@/interfaces/api/museum/MuseumWing';

export interface Museum {
  wings: MuseumWing[];
  totalFound: number;
  totalEntries: number;
  curation: number;
}
