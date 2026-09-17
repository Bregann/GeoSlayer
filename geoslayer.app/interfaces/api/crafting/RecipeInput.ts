export interface RecipeInput {
  materialId: number;
  key: string;
  name: string;
  quantity: number;
  held: number;
  hasEnough: boolean;
  /** Only obtainable from POI visits — "go somewhere new", not "keep playing". */
  isTravelGated: boolean;
}
