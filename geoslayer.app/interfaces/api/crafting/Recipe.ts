import type { RecipeLockReason } from '@/interfaces/api/crafting/RecipeLockReason';
import type { RecipeInput } from '@/interfaces/api/crafting/RecipeInput';

export interface Recipe {
  key: string;
  name: string;
  description: string;
  skillName: string;
  levelRequired: number;
  durationSeconds: number;
  xpReward: number;
  outputName: string | null;
  outputQuantity: number;
  outputKind: string | null;
  inputs: RecipeInput[];
  canCraft: boolean;
  lockReason: RecipeLockReason | number;
  lockText: string | null;
}
