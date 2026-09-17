export interface Expedition {
  id: number;
  workerId: number;
  poiId: number;
  poiName: string;
  dispatchedUtc: string;
  returnsUtc: string;
  distanceMetres: number;
  hasReturned: boolean;
  secondsRemaining: number;
}
