import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  // Change this to your own reverse-domain id before publishing to Google Play.
  appId: 'ph.jeepneyrush.game',
  appName: 'Jeepney Rush',
  webDir: 'dist',
  backgroundColor: '#1b1033',
  android: {
    allowMixedContent: false,
    webContentsDebuggingEnabled: false,
  },
};

export default config;
