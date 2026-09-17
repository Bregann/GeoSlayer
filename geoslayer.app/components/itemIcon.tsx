import { Image, Text } from 'react-native';
import { useState } from 'react';

import { API_BASE_URL } from '@/constants/api';
import { itemIconStyles as styles } from '@/styles/itemIcon';

interface ItemIconProps {
  itemId: number;
  /** From the API — false means no artwork has been uploaded (Stage 18 task 3). */
  hasImage: boolean;
  /** Shown when there is no artwork, or when loading it fails. */
  fallback: string;
  size?: number;
}

/**
 * An item's artwork, or its emoji.
 *
 * Items were emoji-only until an admin could upload images. Both states are permanent:
 * artwork is added by hand, item by item, so most items will not have one for a long time
 * and the emoji has to stay a first-class answer rather than a placeholder.
 *
 * The fallback also covers failure. A missing or corrupt image should degrade to the emoji
 * the player already knows, not to a broken-image glyph — the icon is decoration, and
 * nothing about the item is unusable without it.
 */
export function ItemIcon({ itemId, hasImage, fallback, size = 28 }: ItemIconProps) {
  const [failed, setFailed] = useState(false);

  if (!hasImage || failed) {
    return <Text style={[styles.emoji, { fontSize: size }]}>{fallback}</Text>;
  }

  return (
    <Image
      source={{ uri: `${API_BASE_URL}/api/Admin/GetItemImage?itemId=${itemId}` }}
      style={{ width: size, height: size }}
      resizeMode="contain"
      // Degrade rather than show a broken image — see above.
      onError={() => setFailed(true)}
      accessibilityIgnoresInvertColors
    />
  );
}
