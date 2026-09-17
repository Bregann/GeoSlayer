import coreWebVitals from 'eslint-config-next/core-web-vitals'
import typescript from 'eslint-config-next/typescript'

/**
 * eslint-config-next ships flat configs natively from v16, so they are imported directly
 * rather than through FlatCompat — the compat shim cannot serialise the plugin graph and
 * throws on a circular structure.
 */
const config = [
  ...coreWebVitals,
  ...typescript,
  { ignores: ['.next/**', 'node_modules/**'] },
]

export default config
