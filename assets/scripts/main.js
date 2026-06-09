import { createAppController } from './app-controller.js';
import { runNativeBridgeSmoke } from './native/native-smoke-runner.js';

createAppController();
window.__lensDocsNativeSmokePromise = runNativeBridgeSmoke();
