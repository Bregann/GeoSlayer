export interface Craft {
  id: number;
  recipeKey: string;
  recipeName: string;
  startedUtc: string;
  completesUtc: string;
  isComplete: boolean;
  secondsRemaining: number;
}
