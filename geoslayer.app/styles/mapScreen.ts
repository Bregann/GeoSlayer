import { StyleSheet } from 'react-native';

export const mapScreenStyles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0a0a1e',
  },
  loading: {
    flex: 1,
    backgroundColor: '#0a0a1e',
    alignItems: 'center',
    justifyContent: 'center',
  },
  loadingText: {
    color: '#39ff14',
    fontSize: 16,
    fontFamily: 'monospace',
  },
});

export const overviewStyles = StyleSheet.create({
  overviewButton: {
    position: 'absolute',
    right: 16,
    top: 120,
    backgroundColor: '#0a0a1e',
    borderWidth: 2,
    borderColor: '#39ff14',
    borderRadius: 8,
    paddingHorizontal: 14,
    paddingVertical: 10,
  },
  overviewText: {
    color: '#39ff14',
    fontFamily: 'monospace',
    fontWeight: 'bold',
    fontSize: 13,
  },
});

export const poiModalStyles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.7)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  card: {
    backgroundColor: '#0a0a1e',
    borderWidth: 2,
    borderColor: '#39ff14',
    borderRadius: 8,
    padding: 24,
    alignItems: 'center',
    minWidth: 250,
    maxWidth: 320,
  },
  icon: {
    fontSize: 40,
    marginBottom: 8,
  },
  name: {
    color: '#fff',
    fontSize: 18,
    fontWeight: 'bold',
    fontFamily: 'monospace',
    textAlign: 'center',
    marginBottom: 4,
  },
  skill: {
    color: '#39ff14',
    fontSize: 14,
    fontFamily: 'monospace',
    marginBottom: 4,
  },
  xp: {
    color: '#ffd700',
    fontSize: 16,
    fontWeight: 'bold',
    fontFamily: 'monospace',
    marginBottom: 4,
  },
  distance: {
    color: '#888',
    fontSize: 12,
    fontFamily: 'monospace',
    marginBottom: 16,
  },
  closeButton: {
    backgroundColor: '#39ff14',
    paddingHorizontal: 24,
    paddingVertical: 10,
    borderRadius: 4,
  },
  closeText: {
    color: '#0a0a1e',
    fontWeight: 'bold',
    fontFamily: 'monospace',
    fontSize: 14,
  },
  clusterItem: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: '#1a1a2e',
    width: '100%',
    gap: 10,
  },
  clusterIcon: {
    fontSize: 24,
  },
  clusterName: {
    color: '#fff',
    fontSize: 13,
    fontFamily: 'monospace',
    fontWeight: 'bold',
  },
  clusterSkill: {
    color: '#888',
    fontSize: 11,
    fontFamily: 'monospace',
  },
});
