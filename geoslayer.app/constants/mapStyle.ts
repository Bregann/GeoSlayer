/**
 * MapLibre GL Style Spec — retro dark 2D vector style.
 * Uses OpenFreeMap tiles (free, no API key).
 *
 * @see https://maplibre.org/maplibre-style-spec/
 */

import type { StyleSpecification } from '@maplibre/maplibre-react-native';

/**
 * Hosted OpenFreeMap liberty style — use this as `mapStyle` prop.
 * Confirmed working, includes correct sources/glyphs/sprites.
 */
export const MAP_STYLE_URL = 'https://tiles.openfreemap.org/styles/liberty';

/** OpenFreeMap vector tile endpoint (free, no key required). */
const TILE_URL = 'https://tiles.openfreemap.org/planet/{z}/{x}/{y}.pbf';

/**
 * Full MapLibre GL JSON style object.
 * Edit the `paint` values to go 8-bit, neon, etc.
 */
export const RETRO_MAP_STYLE: StyleSpecification = {
  version: 8,
  name: 'GeoSlayer Retro',
  glyphs: 'https://tiles.openfreemap.org/fonts/{fontstack}/{range}.pbf',
  sources: {
    openmaptiles: {
      type: 'vector',
      tiles: [TILE_URL],
      minzoom: 0,
      maxzoom: 14,
    },
  },
  layers: [
    // Background
    {
      id: 'background',
      type: 'background',
      paint: { 'background-color': '#1a1a2e' },
    },
    // Water
    {
      id: 'water',
      type: 'fill',
      source: 'openmaptiles',
      'source-layer': 'water',
      paint: { 'fill-color': '#0e1a2b' },
    },
    // Land use (parks, green areas)
    {
      id: 'landuse-green',
      type: 'fill',
      source: 'openmaptiles',
      'source-layer': 'landuse',
      filter: ['in', 'class', 'park', 'grass', 'cemetery'],
      paint: { 'fill-color': '#1e3a2b' },
    },
    // Buildings
    {
      id: 'buildings',
      type: 'fill',
      source: 'openmaptiles',
      'source-layer': 'building',
      paint: {
        'fill-color': '#283655',
        'fill-opacity': 0.7,
      },
    },
    // Roads — minor
    {
      id: 'roads-minor',
      type: 'line',
      source: 'openmaptiles',
      'source-layer': 'transportation',
      filter: ['in', 'class', 'minor', 'service', 'path'],
      paint: {
        'line-color': '#2e2e50',
        'line-width': 1,
      },
    },
    // Roads — streets
    {
      id: 'roads-street',
      type: 'line',
      source: 'openmaptiles',
      'source-layer': 'transportation',
      filter: ['in', 'class', 'street', 'secondary', 'tertiary'],
      paint: {
        'line-color': '#38385e',
        'line-width': 2,
      },
    },
    // Roads — major / highway
    {
      id: 'roads-major',
      type: 'line',
      source: 'openmaptiles',
      'source-layer': 'transportation',
      filter: ['in', 'class', 'motorway', 'trunk', 'primary'],
      paint: {
        'line-color': '#4a4a68',
        'line-width': 3,
      },
    },
    // Transit / rail
    {
      id: 'transit',
      type: 'line',
      source: 'openmaptiles',
      'source-layer': 'transportation',
      filter: ['==', 'class', 'rail'],
      paint: {
        'line-color': '#2b2b4b',
        'line-width': 1,
        'line-dasharray': [4, 2],
      },
    },
    // Place labels
    {
      id: 'place-labels',
      type: 'symbol',
      source: 'openmaptiles',
      'source-layer': 'place',
      layout: {
        'text-field': '{name}',
        'text-size': 12,
        'text-anchor': 'center',
      },
      paint: {
        'text-color': '#8ec3b1',
        'text-halo-color': '#1a1a2e',
        'text-halo-width': 1,
      },
    },
  ],
};

/**
 * Fallback: if vector tiles are unavailable, use a free raster OSM server.
 * Pass this to MapView's `styleJSON` instead of `RETRO_MAP_STYLE`.
 */
export const OSM_RASTER_STYLE: StyleSpecification = {
  version: 8,
  name: 'OSM Raster Fallback',
  sources: {
    'osm-raster': {
      type: 'raster',
      tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
      tileSize: 256,
      attribution: '© OpenStreetMap contributors',
    },
  },
  layers: [
    {
      id: 'osm-tiles',
      type: 'raster',
      source: 'osm-raster',
    },
  ],
};

