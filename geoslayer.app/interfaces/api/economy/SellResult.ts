/** The outcome of selling at a shop (DESIGN.md §5.4). */
export interface SellResult {
  coinEarned: number;
  coinBalance: number;
  sold: SoldMaterial[];
  /** The shop it was sold at — named, because the walk there was the cost. */
  poiName: string;
}

export interface SoldMaterial {
  materialId: number;
  key: string;
  name: string;
  quantity: number;
  coin: number;
}
