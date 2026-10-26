import { execFile } from 'node:child_process';
import { freemem, totalmem } from 'node:os';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const run = promisify(execFile);
const collector = fileURLToPath(new URL('./capture-runner-network.ps1', import.meta.url));

// Observe only the proved loopback failure. Do not retry requests, reload the page,
// change the server or substitute diagnostic results for the original assertion.
export function observeBootstrapNetwork(page, testInfo) {
  const origin = new URL(testInfo.project.use.baseURL);
  let capture;
  const failed = (request) => {
    const url = new URL(request.url());
    if (capture || url.hostname !== '127.0.0.1' || url.origin !== origin.origin || request.failure()?.errorText !== 'net::ERR_NO_BUFFER_SPACE') return;
    const observation = {
      atUtc: new Date().toISOString(),
      url: url.href,
      resourceType: request.resourceType(),
      failure: request.failure(),
      browser: testInfo.project.name,
      run: process.env.GITHUB_RUN_ID,
      attempt: process.env.GITHUB_RUN_ATTEMPT,
      platform: process.platform,
      node: process.version,
      freeMemoryBytes: freemem(),
      totalMemoryBytes: totalmem(),
      processMemory: process.memoryUsage(),
    };
    capture = (async () => {
      if (process.env.GITHUB_ACTIONS === 'true' && process.platform === 'win32') {
        try {
          const result = await run('pwsh', ['-NoProfile', '-NonInteractive', '-File', collector, '-Port', origin.port],
            { windowsHide: true, timeout: 5_000, maxBuffer: 256 * 1024 });
          observation.runner = JSON.parse(result.stdout);
          observation.collectorStderr = result.stderr;
        } catch (error) {
          observation.collectorError = { message: error.message, code: error.code, stdout: error.stdout, stderr: error.stderr };
        }
      } else {
        observation.runner = 'NOT COLLECTED: runner-level inspection is restricted to GitHub Actions on Windows';
      }
      return observation;
    })();
  };
  page.on('requestfailed', failed);
  return async () => {
    page.off('requestfailed', failed);
    if (!capture) return;
    const observation = await capture;
    try {
      await testInfo.attach('bootstrap-network-failure', { body: JSON.stringify(observation, null, 2), contentType: 'application/json' });
    } catch (error) {
      // Retain diagnostic failure in the job log without replacing the original test result.
      console.error('Could not attach bootstrap network diagnostics:', error.message, JSON.stringify(observation));
    }
  };
}
