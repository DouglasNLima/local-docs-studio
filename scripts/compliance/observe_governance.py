# SPDX-License-Identifier: MIT
"""Retain public governance evidence through GET requests only; never activate controls."""
import argparse
import datetime
import json
from pathlib import Path
import subprocess

REPO = 'repos/DouglasNLima/local-docs-studio'


def get(path):
    result = subprocess.run(['gh', 'api', '--method', 'GET', path],
                            capture_output=True, text=True, encoding='utf-8')
    try:
        data = json.loads(result.stdout)
    except ValueError:
        raise RuntimeError('No JSON observation for ' + path) from None
    return {'exitCode': result.returncode, 'data': data}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--check-source', required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    raw = get(REPO)['data']
    result = {
        'observedAt': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'method': 'GET only; no administrative mutation',
        'repository': {key: raw.get(key) for key in [
            'full_name', 'visibility', 'default_branch', 'permissions',
            'allow_merge_commit', 'allow_squash_merge', 'allow_rebase_merge',
            'allow_auto_merge', 'delete_branch_on_merge']},
        'mfa': 'UNVERIFIED; no owner-approved private verification performed'
    }
    for key, route in [
        ('rulesets', '/rulesets?includes_parents=true'),
        ('developProtection', '/branches/develop/protection'),
        ('mainProtection', '/branches/main/protection'),
        ('developEffectiveRules', '/rules/branches/develop'),
        ('mainEffectiveRules', '/rules/branches/main'),
        ('privateReporting', '/private-vulnerability-reporting'),
        ('codeowners', '/codeowners/errors?ref=develop'),
        ('environments', '/environments'),
        ('ownerPermission', '/collaborators/DouglasNLima/permission')
    ]:
        result[key] = get(REPO + route)
        if key == 'ownerPermission':
            result[key]['data'] = {k: result[key]['data'].get(k)
                                   for k in ['permission', 'role_name']}
    releases = get(REPO + '/releases?per_page=100')
    if releases['exitCode'] or not isinstance(releases['data'], list):
        raise RuntimeError('Release observation unavailable')
    if len(releases['data']) == 100:
        raise RuntimeError('Release observation needs pagination')
    result['releases'] = [{k: r.get(k) for k in [
        'tag_name', 'draft', 'prerelease', 'published_at', 'html_url']}
        for r in releases['data']]
    result['pausedPullRequests'] = {}
    for number in [38, 50]:
        pr = get(REPO + '/pulls/' + str(number))['data']
        result['pausedPullRequests'][str(number)] = {
            **{k: pr.get(k) for k in ['state', 'draft', 'updated_at']},
            'headSha': pr['head']['sha']}
    (args.output / 'governance-observation.json').write_text(
        json.dumps(result, indent=2) + '\n', encoding='utf-8')
    checks = get(REPO + '/commits/' + args.check_source + '/check-runs?per_page=100')
    if checks['exitCode'] or checks['data']['total_count'] > 100:
        raise RuntimeError('Check observation unavailable or needs pagination')
    contexts = {
        'sourceRevision': args.check_source, 'observedAt': result['observedAt'],
        'contexts': [{'name': r['name'], 'integrationId': r['app']['id'],
                      'conclusion': r['conclusion'], 'url': r['html_url']}
                     for r in checks['data']['check_runs']]}
    (args.output / 'check-contexts.json').write_text(
        json.dumps(contexts, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'contexts': len(contexts['contexts']),
                      'releases': len(result['releases']), 'administrativeMutation': False}))


if __name__ == '__main__':
    main()
