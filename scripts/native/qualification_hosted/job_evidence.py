"""Pure decoding/acceptance for the fixed Job protocol; no Windows calls."""
import math
import re
import struct

CLEANUP_TARGETS = (0, 0.1, 1, 4)
PID_CAPACITY = 8
PHASES = ('CREATED_EMPTY', 'CONFIGURED_EMPTY', 'BEFORE_ASSIGNMENT',
          'AFTER_ASSIGNMENT', 'AFTER_RESUME', 'PROCESS_EXIT',
          'CLEANUP_0', 'CLEANUP_1', 'CLEANUP_2', 'CLEANUP_3')


def require(value, message):
    if not value:
        raise ValueError(message)


def number(value):
    return type(value) in (int, float) and math.isfinite(value) and value >= 0


def decode_query(query, information_class, capacity):
    require(set(query) == {'informationClass','returnValue','winError','capacityBytes','returnedBytes',
                          'rawBytes','startedSeconds','finishedSeconds'}, 'Missing or extra native query fields')
    require(query['informationClass'] == information_class and type(query['informationClass']) is int,
            'Wrong Job query class')
    require(type(query['returnValue']) is int and query['returnValue'] != 0 and query['winError'] is None,
            'Failed native Job query')
    require(type(query['capacityBytes']) is int and query['capacityBytes'] == capacity
            and type(query['returnedBytes']) is int and 0 <= query['returnedBytes'] <= capacity,
            'Invalid Job query capacity/returned length')
    raw = bytes.fromhex(query['rawBytes'])
    require(len(raw) == capacity, 'Missing raw Job query bytes')
    require(number(query['startedSeconds']) and number(query['finishedSeconds'])
            and query['startedSeconds'] <= query['finishedSeconds'], 'Invalid native query chronology')
    return raw


def decode_snapshot(raw):
    """Retain the allocated bytes, but interpret only a successful written prefix."""
    require(set(raw) == {'accounting', 'limits', 'pids'}, 'Incomplete Job query set')
    accounting = decode_query(raw['accounting'], 1, 48)
    limits = decode_query(raw['limits'], 9, 144)
    pids = decode_query(raw['pids'], 3, 8 + PID_CAPACITY * 8)
    require(raw['accounting']['returnedBytes'] == 48 and raw['limits']['returnedBytes'] == 144,
            'Truncated fixed Job structure')
    assigned, count = struct.unpack_from('<II', pids)
    require(raw['pids']['returnedBytes'] >= 8 and count <= PID_CAPACITY
            and 8 + count * 8 <= raw['pids']['returnedBytes'] and assigned == count,
            'Missing or truncated Job PID list')
    members = list(struct.unpack_from('<' + 'Q' * count, pids, 8))
    require(all(p > 0 for p in members) and len(members) == len(set(members)), 'Invalid or duplicate Job PID')
    queries = [raw[name] for name in ('accounting', 'limits', 'pids')]
    require(all(a['finishedSeconds'] <= b['startedSeconds'] for a, b in zip(queries, queries[1:])),
            'Reordered Job queries')
    faults, total, active, terminated = struct.unpack_from('<IIII', accounting, 32)
    return dict(totalProcesses=total, activeProcesses=active, terminatedProcesses=terminated,
                pageFaults=faults, returnedBytes=48, accountingBytes=accounting.hex(),
                limitFlags=struct.unpack_from('<I', limits, 16)[0], activeLimit=struct.unpack_from('<I', limits, 40)[0],
                assignedProcesses=assigned, processIds=members)


def check_snapshot(value, phase, pid, seen=None):
    seen = seen or dict(counterEmpty=False, pidListEmpty=False)
    initial = phase == 'CREATED_EMPTY'
    empty = phase in PHASES[:3]
    require(value['limitFlags'] == (0 if initial else 0x2008)
            and value['activeLimit'] == (0 if initial else 1), 'Applied Job limits differ')
    require(value['totalProcesses'] == (0 if empty else 1) and value['terminatedProcesses'] == 0,
            'Cumulative association discrepancy or quota termination')
    require(value['activeProcesses'] in (0, 1) and set(value['processIds']) <= ({pid} if pid else set()),
            'Extra or foreign Job member')
    if empty:
        require(value['activeProcesses'] == value['assignedProcesses'] == 0, 'New private Job is not empty')
    elif phase == 'AFTER_ASSIGNMENT':
        require(value['activeProcesses'] == value['assignedProcesses'] == 1 and value['processIds'] == [pid],
                'Suspended assignment was not proved')
    else:
        # The three native queries are not atomic. Only the known PID may disappear.
        if seen['counterEmpty']:
            require(value['activeProcesses'] == 0, 'Active Job count reappeared')
        if seen['pidListEmpty']:
            require(value['assignedProcesses'] == 0, 'Job PID membership reappeared')
        if phase == 'CLEANUP_3':
            require(value['activeProcesses'] == value['assignedProcesses'] == 0, 'Final Job membership remains active')
        seen = dict(counterEmpty=seen['counterEmpty'] or value['activeProcesses'] == 0,
                    pidListEmpty=seen['pidListEmpty'] or value['assignedProcesses'] == 0)
    return seen


def check_identity(value, pid, tid, path, sha256, parent_pid):
    require(type(value['processId']) is int and value['processId'] == pid and pid != parent_pid
            and type(value['threadId']) is int and value['threadId'] == tid
            and type(value['threadProcessId']) is int and value['threadProcessId'] == pid, 'Wrong process/thread handle identity')
    require(value['imagePath'].casefold() == path.casefold() and value['imageSha256'] == sha256
            and type(value['creationFileTime']) is int and value['creationFileTime'] > 0,
            'Wrong child image or creation identity')
    require(type(value['processHandleFlags']) is int and type(value['threadHandleFlags']) is int
            and value['processHandleFlags'] == value['threadHandleFlags'] == 0, 'Unexpected inherited child handle')


def verify_supervision(record):
    """Every retained violation is sticky; final empty membership cannot erase it."""
    require(record['supervisorSchemaVersion'] == 2 and record['errors'] == [] and record['error'] is None,
            'Supervisor has missing or failed operations')
    require(record['journalComplete'] is True and record['loggingErrors'] == [], 'Supervisor journal failed')
    require(all(type(record[k]) is int for k in ('supervisorSchemaVersion','supervisorPid','processId','threadId',
            'exitCode','timeoutSeconds','cleanupSeconds','retryCount','assignmentAttempts','totalProcesses','activeProcesses',
            'jobHandle','processHandle','threadHandle')), 'Invalid supervisor integer type')
    handles = [record[k] for k in ('jobHandle','processHandle','threadHandle')]
    require(all(h > 0 for h in handles) and len(set(handles)) == 3 and record['ownedChildHandle'] is True,
            'Missing or aliased owned handles')
    require(record['created'] is True and record['assigned'] is True and record['resumed'] is True
            and record['timedOut'] is False and record['assignmentAttempts'] == 1
            and record['exitCode'] == 0 and record['exitCodeHex'] == '00000000', 'Child execution failed')
    require(record['timeoutSeconds'] == 30 and record['cleanupSeconds'] == 5 and record['retryCount'] == 0
            and tuple(record['cleanupTargets']) == CLEANUP_TARGETS, 'Changed supervision bounds')
    require(record['childIdentityVerified'] is True and record['childExitObserved'] is True,
            'Unverified child or direct exit')
    parent = record['parentIdentity']
    require(parent['processId'] == record['supervisorPid'] and parent['processId'] > 0
            and type(parent['creationFileTime']) is int and parent['creationFileTime'] > 0
            and re.fullmatch('[0-9a-f]{64}', parent['imageSha256'])
            and parent['imagePath'] and type(parent['inAnyJob']) is bool
            and parent['inPrivateJob'] is False and type(parent['jobHandleFlags']) is int and parent['jobHandleFlags'] == 0,
            'Missing or contradictory supervisor identity')
    for value in (record['childIdentityBefore'], record['childIdentityAtExit']):
        check_identity(value, record['processId'], record['threadId'], record['binaryPath'],
                       record['binarySha256'], record['supervisorPid'])
    require(record['childIdentityBefore'] == record['childIdentityAtExit'], 'Child handle identity changed')
    entries = record['jobObservations']
    require([e['phase'] for e in entries] == list(PHASES), 'Missing, repeated or reordered Job boundary')
    seen = None
    for entry in entries:
        decoded = decode_snapshot(entry['raw'])
        if entry['phase'] == 'CREATED_EMPTY':
            require(bytes.fromhex(entry['raw']['accounting']['rawBytes']) == bytes(48), 'New Job accounting is not zero')
        require(entry.get('decoded') == decoded, 'Interpreted Job values contradict raw bytes')
        seen = check_snapshot(decoded, entry['phase'], record['processId'], seen)
        require(number(entry['startedSeconds']) and number(entry['finishedSeconds'])
                and entry['startedSeconds'] <= entry['raw']['accounting']['startedSeconds']
                <= entry['raw']['pids']['finishedSeconds'] <= entry['finishedSeconds'], 'Job boundary chronology differs')
    require(all(a['finishedSeconds'] <= b['startedSeconds'] for a, b in zip(entries, entries[1:])), 'Job observations reordered')
    require(record['assignmentObservation'] == entries[3]['decoded']
            and record['cleanupObservation'] == entries[-1]['decoded'], 'Legacy observation aliases differ')
    times = [record[k] for k in ('createdSeconds', 'assignedSeconds', 'resumedSeconds', 'observedExitSeconds')]
    require(all(number(t) and t <= 30 for t in times) and times == sorted(times), 'Child deadline/chronology differs')
    start, end = record['cleanupStartedSeconds'], record['finishedSeconds']
    require(number(start) and number(end) and times[-1] <= start <= end <= start + 5 and end <= 35,
            'Cleanup deadline exceeded')
    require(record['childHandlesClosedSeconds'] >= entries[5]['finishedSeconds']
            and record['childHandlesClosedSeconds'] <= entries[6]['startedSeconds'], 'Child handles released at wrong boundary')
    for entry, target, upper in zip(entries[6:], CLEANUP_TARGETS, (*CLEANUP_TARGETS[1:], 5)):
        require(entry['targetSeconds'] == target and start + target <= entry['startedSeconds']
                and entry['startedSeconds'] < start + upper and entry['finishedSeconds'] <= start + 5,
                'Missing, bunched or late fixed cleanup observation')
    require(record['cleanupVerified'] is True and record['jobHandleClosed'] is True
            and record['jobHandleClosedSeconds'] >= entries[-1]['finishedSeconds'], 'Empty Job/handle closure unverified')
    require(record['totalProcesses'] == 1 and record['activeProcesses'] == 0 and record['terminationRequests'] == [],
            'Termination or accounting discrepancy cannot pass')
    verify_operations(record)
    return 'PASS_FIXED_JOB_OBSERVATIONS_ONLY'


def verify_operations(record):
    names = ['job','observe:CREATED_EMPTY','configure','observe:CONFIGURED_EMPTY','parent','create',
             'identity-before','association-before','observe:BEFORE_ASSIGNMENT','assign','observe:AFTER_ASSIGNMENT',
             'association-after','resume','observe:AFTER_RESUME','wait','exit','identity-at-exit','observe:PROCESS_EXIT',
             'close:thread','close:process',*[f'observe:CLEANUP_{n}' for n in range(4)],'close:job']
    operations = record['operations']
    require(len(operations) == 2 * len(names) and [o['sequence'] for o in operations] == list(range(len(operations))),
            'Missing, repeated or extra supervisor operation')
    require(all(number(o['seconds']) for o in operations)
            and [o['seconds'] for o in operations] == sorted(o['seconds'] for o in operations), 'Operation chronology differs')
    pairs = {}
    for index, name in enumerate(names):
        begin, end = operations[index*2:index*2+2]
        require(set(begin) == {'name','phase','arguments','sequence','seconds'}
                and set(end) == {'name','phase','beginSequence','result','sequence','seconds'}, 'Failed or incomplete operation')
        require(begin['name'] == end['name'] == name and begin['phase'] == 'BEGIN' and end['phase'] == 'END'
                and end['beginSequence'] == begin['sequence'], 'Reordered native operation')
        pairs[name] = (begin, end)
    job, process, thread = [record[k] for k in ('jobHandle','processHandle','threadHandle')]
    def bound(name, args, value):
        begin, end = pairs[name]
        require(begin['arguments'] == args and end['result'] == value, 'Operation contradicts summary: ' + name)
    def succeeded(name, args):
        value = pairs[name][1]['result']
        require(set(value) == {'returnValue','winError'} and type(value['returnValue']) is int
                and value['returnValue'] != 0 and value['winError'] is None, 'Failed native operation: ' + name)
        bound(name, args, value)
    bound('job', None, job)
    succeeded('configure', dict(jobHandle=job,flags=0x2008,activeLimit=1))
    bound('parent', dict(jobHandle=job), record['parentIdentity'])
    begin, end = pairs['create']
    require(begin['arguments'] == dict(binary=record['binaryPath'], flags=0x08000004)
            and list(end['result']) == [process,thread,record['processId'],record['threadId']], 'Created handles/PIDs differ')
    for name, key in (('identity-before','childIdentityBefore'),('identity-at-exit','childIdentityAtExit')):
        bound(name, dict(processHandle=process,threadHandle=thread), record[key])
    for name, value in (('association-before',False),('association-after',True)):
        require(pairs[name][1]['result'] is value, 'Native association result differs')
        bound(name, dict(jobHandle=job,processHandle=process), value)
    succeeded('assign', dict(jobHandle=job,processHandle=process,processId=record['processId']))
    require(pairs['assign'][1]['result'] == record['assignmentResult'], 'Assignment return was overwritten')
    bound('resume', dict(threadHandle=thread), dict(returnValue=1,winError=None))
    wait = pairs['wait'][1]['result']
    require(set(wait) == {'waitResult','requestedMilliseconds'} and type(wait['waitResult']) is int and wait['waitResult'] == 0
            and type(wait['requestedMilliseconds']) is int and 0 <= wait['requestedMilliseconds'] <= 30000, 'Wrong native wait')
    bound('wait', dict(processHandle=process), wait)
    bound('exit', dict(processHandle=process), record['exitCode'])
    for kind, handle in (('thread',thread),('process',process),('job',job)):
        succeeded('close:'+kind,dict(handle=handle))
    for entry in record['jobObservations']:
        begin, end = pairs['observe:'+entry['phase']]
        bound('observe:'+entry['phase'],dict(jobHandle=job),entry['raw'])
        require(entry['startedSeconds'] <= begin['seconds'] <= entry['raw']['accounting']['startedSeconds']
                <= entry['raw']['pids']['finishedSeconds'] <= end['seconds'] <= entry['finishedSeconds'],
                'Snapshot is outside its recorded native operation')
    for name, key, following in (('create','createdSeconds','identity-before'),('assign','assignedSeconds','observe:AFTER_ASSIGNMENT'),
                                 ('resume','resumedSeconds','observe:AFTER_RESUME'),('wait','observedExitSeconds','exit')):
        require(pairs[name][1]['seconds'] <= record[key] <= pairs[following][0]['seconds'], 'Summary operation time differs')
    require(pairs['observe:PROCESS_EXIT'][1]['seconds'] <= record['cleanupStartedSeconds'] <= pairs['close:thread'][0]['seconds']
            and pairs['close:process'][1]['seconds'] <= record['childHandlesClosedSeconds'] <= pairs['observe:CLEANUP_0'][0]['seconds']
            and pairs['close:job'][1]['seconds'] <= record['jobHandleClosedSeconds'] <= record['finishedSeconds'],
            'Handle/cleanup summary contradicts operation chronology')
