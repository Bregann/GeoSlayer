export interface MaterialGain {
  materialId: number;
  key: string;
  name: string;
  tier: number;
  category: number;
  quantity: number;
  /** Units that did not fit and became Dust instead (DESIGN.md §7.4). */
}
