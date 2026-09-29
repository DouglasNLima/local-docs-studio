async function waitForCurrentServiceWorker() {
  const serviceWorker = navigator.serviceWorker;
  const previousController = serviceWorker?.controller;
  if (!previousController) return;

  let timerId;
  let onControllerChange;
  try {
    const registration = await serviceWorker.getRegistration();
    if (!registration) return;

    const controllerChanged = new Promise((resolve) => {
      onControllerChange = resolve;
      serviceWorker.addEventListener('controllerchange', onControllerChange);
    });
    const timeout = new Promise((resolve) => {
      timerId = window.setTimeout(resolve, 8_000);
    });

    await Promise.race([registration.update(), timeout]);
    if (serviceWorker.controller === previousController && (registration.installing || registration.waiting)) {
      await Promise.race([controllerChanged, timeout]);
    }
  } catch {
    // An offline launch still uses the cached app shell.
  } finally {
    window.clearTimeout(timerId);
    if (onControllerChange) serviceWorker.removeEventListener('controllerchange', onControllerChange);
  }
}

await waitForCurrentServiceWorker();

const [{ createAppController }, { runNativeBridgeSmoke }] = await Promise.all([
  import('./app-controller.js'),
  import('./native/native-smoke-runner.js'),
]);

createAppController();
window.__lensDocsNativeSmokePromise = runNativeBridgeSmoke();
