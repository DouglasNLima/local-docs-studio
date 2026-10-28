"""Validate proposed solo governance without applying settings or granting rights."""
import hashlib
import json
from pathlib import Path
import re

from rights_ledger import boundary

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/open-source/evidence/goal60'
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
    require(not any(p.name.upper() in {'LICENSE', 'LICENSE.MD', 'LICENSE.TXT',
                                      'LICENCE', 'LICENCE.MD', 'LICENCE.TXT'}
                    for p in ROOT.iterdir()), 'Operative root licence is not approved')
    invariants = json.loads((EVIDENCE / 'baseline-invariants.json').read_text())
    for name, expected in invariants['sha256'].items():
        # Git text blobs use LF; avoid treating Windows checkout CRLF as a source change.
        content = (ROOT / name).read_bytes().replace(b'\r\n', b'\n')
        require(hashlib.sha256(content).hexdigest() == expected,
                'Protected trust/package source changed: ' + name)
    require(boundary('src/native/LensDocsStudio.Core/Rendering/BoundedErLayout.cs')[0]
            == 'R6', 'Mermaider adaptation must remain outside first-party scope')
    require(boundary('docs/native/evidence/example.json')[0] == 'R5',
            'Historical evidence is not first-party implementation')
    candidate = (ROOT / 'docs/open-source/licence-proposal.md').read_text(encoding='utf-8')
    block = re.search(r'```text\n(.*?)\n```', candidate, re.S).group(1)
    upstream = (ROOT / 'src/native/LensDocsStudio.Core/Rendering/PinnedMermaider/NOTICE.md'
                ).read_text(encoding='utf-8')
    mit = upstream[upstream.index('MIT License'):].strip()
    require(block == mit.replace('Copyright (c) 2025 Nullean',
                                'Copyright (c) 2026 DouglasNLima'),
            'Candidate must preserve unmodified MIT terms with the proposed notice')
    print(json.dumps({'rulesets': 2, 'developChecks': len(COMMON | NATIVE),
                      'mainChecks': len(COMMON), 'trustAndMetadata': 'unchanged',
                      'mitCandidate': 'unmodified terms; non-operative',
                      'administrativeMutation': False}))


if __name__ == '__main__':
    main()
