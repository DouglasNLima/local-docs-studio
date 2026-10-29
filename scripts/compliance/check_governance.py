"""Validate proposed solo governance and scoped MIT adoption without applying settings."""
import json
import base64
import hashlib
from pathlib import Path

from rights_ledger import boundary
from licence_scope import main as check_licence

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/open-source/evidence/goal62'
CONFIG = ROOT / 'docs/open-source/configuration'
FAMILIES = ('Pie Mindmap Flowchart Gantt Sankey Sequence Quadrant Timeline Journey '
            'Xy Requirement Radar Block C4 Kanban Architecture GitGraph TreeView '
            'Packet Treemap Venn').split()
COMMON = {'Test static app', 'OSS provenance'}
NATIVE = {'native', 'diagram-arrangement', 'arrangement-responsiveness',
          'complex-er-preview', 'state-installed-authoring', 'er-installed-authoring',
          'class-installed-authoring'} | {
              'companion-installed-authoring (' + family + ')' for family in FAMILIES}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate_ruleset(payload, branch, observed):
    require(payload['target'] == 'branch' and payload['enforcement'] == 'active',
            'Ruleset must actively target branches')
    require(payload['bypass_actors'] == [], 'No bypass actor is authorised')
    require(payload['conditions'] == {'ref_name': {
        'include': ['refs/heads/' + branch], 'exclude': []}}, 'Unexpected ref scope')
    rules = {rule['type']: rule for rule in payload['rules']}
    require(len(rules) == len(payload['rules']) == 4, 'Duplicate or additional rules')
    require(set(rules) == {'deletion', 'non_fast_forward', 'pull_request',
                           'required_status_checks'}, 'Incomplete branch protection')
    pr = rules['pull_request']['parameters']
    require(pr == {
        'dismiss_stale_reviews_on_push': True,
        'require_code_owner_review': False, 'require_last_push_approval': False,
        'required_approving_review_count': 0, 'required_review_thread_resolution': True,
        'allowed_merge_methods': ['merge']
    }, 'Approval or merge settings differ from the authorised solo proposal')
    status = rules['required_status_checks']['parameters']
    require(status['strict_required_status_checks_policy'] is True
            and status['do_not_enforce_on_create'] is False, 'Weak status-check policy')
    checks = status['required_status_checks']
    names = {check['context'] for check in checks}
    expected = COMMON | (NATIVE if branch == 'develop' else set())
    require(names == expected and len(names) == len(checks),
            'Missing, duplicate or inapplicable required context')
    require(all(check['integration_id'] == 15368
                and (check['context'], 15368) in observed for check in checks),
            'Required check was not observed from GitHub Actions')


def validate_admin_packet(plan):
    repo = '/repos/DouglasNLima/local-docs-studio'
    require(plan['status'] == 'NOT APPLIED', 'Administrative activation is not authorised')
    actions = plan['actions']
    require(len(actions) == 4, 'Unexpected administrative action')
    expected = [('POST', repo + '/rulesets', 'develop-ruleset.json'),
                ('POST', repo + '/rulesets', 'main-ruleset.json'),
                ('PATCH', repo, 'repository-settings.json')]
    for action, (method, target, name) in zip(actions[:3], expected, strict=True):
        require((action['method'], action['target'], action['bodyFile']) ==
                (method, target, name), 'Unexpected administrative target or payload')
        digest = hashlib.sha256((CONFIG / name).read_bytes().replace(b'\r\n', b'\n')).hexdigest()
        require(action['bodySha256'] == digest, 'Administrative payload digest changed')
    reporting = actions[3]
    require((reporting['method'], reporting['target'], reporting['body'], reporting['recipient']) ==
            ('PUT', repo + '/private-vulnerability-reporting', None, 'DouglasNLima'),
            'Unexpected private reporting action')
    rollback = actions[2]['rollback']
    raw = (CONFIG / 'repository-settings-rollback.json').read_bytes().replace(b'\r\n', b'\n')
    require(rollback == {'method': 'PATCH', 'target': repo,
                         'bodyFile': 'repository-settings-rollback.json',
                         'bodySha256': hashlib.sha256(raw).hexdigest()},
            'Rollback is not bound to the exact observed settings')
    before = json.loads((EVIDENCE / 'governance-observation.json').read_text(encoding='utf-8'))
    require(json.loads(raw) == {k: before['repository'][k] for k in json.loads(raw)},
            'Rollback differs from the recorded before-state')


def validate_sandbox_packet():
    sandbox = json.loads((CONFIG / 'sandbox-transaction.json').read_text(encoding='utf-8'))
    target = '/repos/DouglasNLima/local-docs-studio-governance-sandbox-62'
    require(sandbox['target'] == target and sandbox['create']['body']['name'] ==
            'local-docs-studio-governance-sandbox-62', 'Unexpected disposable target')
    require(sandbox['cleanup']['target'] == target, 'Unsafe sandbox cleanup target')
    require(sandbox['status'] == 'NOT EXECUTED; TARGET-SPECIFIC OWNER APPROVAL REQUIRED',
            'Sandbox execution is not authorised')
    fixture = (CONFIG / 'sandbox-workflow.yml.txt').read_text(encoding='utf-8')
    require(base64.b64decode(sandbox['seedWorkflow']['body']['content']).decode('utf-8')
            == fixture, 'Encoded sandbox workflow differs from reviewed inert text')
    require(sandbox['seedWorkflow']['target'].startswith(target + '/contents/'),
            'Sandbox workflow targets another repository')
    for action, branch in zip(sandbox['applyRulesets'], ('develop', 'main'), strict=True):
        require(action == {'method': 'POST', 'target': target + '/rulesets',
                           'body': json.loads((CONFIG / (branch + '-ruleset.json')).read_text())},
                'Sandbox rules differ from the production proposal')


def main():
    evidence = json.loads((EVIDENCE / 'check-contexts.json').read_text(encoding='utf-8'))
    observed = {(row['name'], row['integrationId']) for row in evidence['contexts']}
    for branch in ('develop', 'main'):
        validate_ruleset(json.loads((CONFIG / (branch + '-ruleset.json')).read_text()),
                         branch, observed)
    settings = json.loads((CONFIG / 'repository-settings.json').read_text())
    require(settings == {'allow_auto_merge': False, 'allow_merge_commit': True,
                         'allow_squash_merge': False, 'allow_rebase_merge': False,
                         'delete_branch_on_merge': False}, 'Unexpected repository settings')
    validate_admin_packet(json.loads((CONFIG / 'admin-activation-plan.json').read_text()))
    validate_sandbox_packet()
    schema = json.loads((EVIDENCE / 'github-rule-schema.json').read_text(encoding='utf-8'))
    parameters = schema['schemas']['repository-rule-pull-request']['properties']['parameters']
    require(parameters['properties']['required_approving_review_count']['minimum'] == 0,
            'Published schema does not support zero reviews')
    # Goal #60's no-LICENSE invariant is deliberately superseded by exact MIT,
    # scope, metadata and preserved-byte checks. Its historical receipt is untouched.
    check_licence()
    require(boundary('src/native/LensDocsStudio.Core/Rendering/BoundedErLayout.cs')[0]
            == 'R6', 'Mermaider adaptation must remain outside first-party scope')
    require(boundary('docs/native/evidence/example.json')[0] == 'R5',
            'Historical evidence is not first-party implementation')
    print(json.dumps({'rulesets': 2, 'developChecks': len(COMMON | NATIVE),
                      'mainChecks': len(COMMON), 'trust': 'unchanged', 'metadata': 'scoped licence pointer',
                      'mitLicence': 'operative for enumerated original portions',
                      'administrativeMutation': False}))


if __name__ == '__main__':
    main()
