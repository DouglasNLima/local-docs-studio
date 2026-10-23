import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// These jobs build/consume internal previews on every event. A develop push must
// exercise the same exclusion contract as a feature pull request or manual run.
export function checkNativeAcceptancePolicy(root) {
  const workflow = readFileSync(new URL('.github/workflows/native.yml', root), 'utf8');
  for (const [key, value] of Object.entries({
    LENS_NATIVE_ACCEPTANCE_POLICY: 'internal-no-associations',
    LENS_NATIVE_WIZARD_ARGUMENTS: '--no-file-associations',
    LENS_NATIVE_LIFECYCLE_ARGUMENTS: '-NoFileAssociations',
  })) {
    assert.match(workflow.split('\njobs:')[0], new RegExp(`^  ${key}: ${value}$`, 'm'), `Missing workflow-level ${key}`);
  }
  const commands = workflow.split(/\r?\n/).filter(line => /^\s+run:/.test(line)
    && /--installer-wizard|Test-Native(?:Installer|StateInstaller|ErInstaller|ClassInstaller)\.ps1/.test(line));
  assert.equal(commands.length, 7, 'Verify the wizard, lifecycle, every installed-authoring job and historical job policy');
  for (const command of commands) {
    const argument = command.includes('--installer-wizard') ? 'WIZARD' : 'LIFECYCLE';
    assert.ok(command.endsWith(` \${{ env.LENS_NATIVE_${argument}_ARGUMENTS }}`), 'Installer exclusion must use the unconditional job-purpose policy');
    assert.doesNotMatch(command, /head_ref|ref_name|contains\(|&&|\|\|/, 'Source branch must not change internal acceptance tasks');
  }
}
