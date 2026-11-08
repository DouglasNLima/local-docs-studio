"""One-child Job supervision. Importing this module makes no native calls."""
import ctypes as c
from ctypes import wintypes as w
import hashlib
import json
import os
from pathlib import Path
import subprocess
import threading
import time

from job_evidence import CLEANUP_TARGETS, PID_CAPACITY, check_identity, check_snapshot, decode_snapshot


class Basic(c.Structure):
    _fields_ = [('processTime', c.c_longlong), ('jobTime', c.c_longlong), ('flags', w.DWORD),
                ('minWorkingSet', c.c_size_t), ('maxWorkingSet', c.c_size_t), ('activeLimit', w.DWORD),
                ('affinity', c.c_size_t), ('priority', w.DWORD), ('scheduling', w.DWORD)]


class Io(c.Structure):
    _fields_ = [(name, c.c_ulonglong) for name in ('readOps', 'writeOps', 'otherOps', 'readBytes', 'writeBytes', 'otherBytes')]


class Limits(c.Structure):
    _fields_ = [('basic', Basic), ('io', Io)] + [(name, c.c_size_t) for name in
                  ('processMemory', 'jobMemory', 'peakProcessMemory', 'peakJobMemory')]


class Accounting(c.Structure):
    _fields_ = [(name, c.c_longlong) for name in ('user', 'kernel', 'periodUser', 'periodKernel')] + [
        (name, w.DWORD) for name in ('faults', 'total', 'active', 'terminated')]


class CreatedProcessError(RuntimeError):
    """Transfer returned handles even when post-create restoration fails."""
    def __init__(self, handles, message):
        super().__init__(message)
        self.handles = handles


class NativeFailure(OSError):
    def __init__(self, name, record):
        super().__init__(name + ' failed')
        self.native = record
        self.winerror = record['winError']


class Kernel:
    def __init__(self):
        if (os.name != 'nt' or c.sizeof(c.c_void_p) != 8 or c.sizeof(Basic) != 64
                or c.sizeof(Limits) != 144 or c.sizeof(Accounting) != 48
                or [getattr(Accounting, n).offset for n in ('faults', 'total', 'active', 'terminated')] != [32, 36, 40, 44]):
            raise ValueError('Unsupported Windows supervision ABI')
        import _winapi
        import msvcrt
        self.api, self.crt = _winapi, msvcrt
        self.k = c.WinDLL('kernel32', use_last_error=True)
        declarations = {
            'CreateJobObjectW': ([c.c_void_p, w.LPCWSTR], w.HANDLE),
            'SetInformationJobObject': ([w.HANDLE, c.c_int, c.c_void_p, w.DWORD], w.BOOL),
            'AssignProcessToJobObject': ([w.HANDLE, w.HANDLE], w.BOOL),
            'QueryInformationJobObject': ([w.HANDLE, c.c_int, c.c_void_p, w.DWORD, c.c_void_p], w.BOOL),
            'TerminateJobObject': ([w.HANDLE, w.UINT], w.BOOL),
            'TerminateProcess': ([w.HANDLE, w.UINT], w.BOOL),
            'CloseHandle': ([w.HANDLE], w.BOOL),
            'ResumeThread': ([w.HANDLE], w.DWORD),
            'GetCurrentProcess': ([], w.HANDLE),
            'GetProcessId': ([w.HANDLE], w.DWORD),
            'GetThreadId': ([w.HANDLE], w.DWORD),
            'GetProcessIdOfThread': ([w.HANDLE], w.DWORD),
            'GetHandleInformation': ([w.HANDLE, c.c_void_p], w.BOOL),
            'GetProcessTimes': ([w.HANDLE, c.c_void_p, c.c_void_p, c.c_void_p, c.c_void_p], w.BOOL),
            'QueryFullProcessImageNameW': ([w.HANDLE, w.DWORD, w.LPWSTR, c.c_void_p], w.BOOL),
            'IsProcessInJob': ([w.HANDLE, w.HANDLE, c.c_void_p], w.BOOL),
            'GetErrorMode': ([], w.UINT), 'SetErrorMode': ([w.UINT], w.UINT)}
        for name, (arguments, result) in declarations.items():
            function = getattr(self.k, name)
            function.argtypes, function.restype = arguments, result

    def result(self, name, *args):
        c.set_last_error(0)
        value = getattr(self.k, name)(*args)
        return dict(returnValue=value, winError=c.get_last_error() if not value else None)

    def checked(self, name, *args):
        result = self.result(name, *args)
        if not result['returnValue']:
            raise NativeFailure(name, result)
        return result['returnValue']

    def job(self):
        return self.checked('CreateJobObjectW', None, None)

    def configure(self, job):
        limits = Limits()
        limits.basic.flags, limits.basic.activeLimit = 0x2008, 1
        return self.result('SetInformationJobObject', job, 9, c.byref(limits), c.sizeof(limits))

    def handle_flags(self, handle):
        flags = w.DWORD()
        self.checked('GetHandleInformation', handle, c.byref(flags))
        return flags.value

    def association(self, job, process):
        value = w.BOOL()
        self.checked('IsProcessInJob', process, job, c.byref(value))
        return bool(value.value)

    def image(self, process):
        path = c.create_unicode_buffer(32768)
        size = w.DWORD(len(path))
        self.checked('QueryFullProcessImageNameW', process, 0, path, c.byref(size))
        if not 0 < size.value < len(path):
            raise ValueError('Truncated process image path')
        stamps = [c.c_ulonglong() for _ in range(4)]
        self.checked('GetProcessTimes', process, *[c.byref(v) for v in stamps])
        return dict(processId=self.checked('GetProcessId', process), imagePath=path.value,
                    imageSha256=hashlib.sha256(Path(path.value).read_bytes()).hexdigest(), creationFileTime=stamps[0].value)

    def parent(self, job):
        handle = self.k.GetCurrentProcess()  # Pseudo-handle is never owned or closed.
        return dict(self.image(handle), inAnyJob=self.association(None, handle),
                    inPrivateJob=self.association(job, handle), jobHandleFlags=self.handle_flags(job))

    def identity(self, process, thread):
        return dict(self.image(process), threadId=self.checked('GetThreadId', thread),
                    threadProcessId=self.checked('GetProcessIdOfThread', thread),
                    processHandleFlags=self.handle_flags(process), threadHandleFlags=self.handle_flags(thread))

    def create(self, binary, environment, streams):
        handles = [self.crt.get_osfhandle(stream.fileno()) for stream in streams]
        original = [os.get_handle_inheritable(h) for h in handles]
        if any(original):
            raise ValueError('Log handles must initially be non-inheritable')
        startup = subprocess.STARTUPINFO()
        startup.dwFlags = subprocess.STARTF_USESTDHANDLES
        startup.hStdInput, startup.hStdOutput, startup.hStdError = handles
        startup.lpAttributeList = {'handle_list': handles}
        previous = self.k.GetErrorMode()
        if previous & 4:
            raise ValueError('Inherited alignment-fault suppression is forbidden')
        created = None
        failure = None
        cleanup = []
        try:
            for handle in handles:
                os.set_handle_inheritable(handle, True)
            self.k.SetErrorMode(previous | 3)
            created = self.api.CreateProcess(str(binary), subprocess.list2cmdline([str(binary)]), None, None, True,
                                             0x08000004, environment, str(Path(binary).parent), startup)
        except BaseException as error:
            failure = error
        finally:
            try:
                self.k.SetErrorMode(previous)
                if self.k.GetErrorMode() != previous:
                    raise RuntimeError('Process error-mode restoration differs')
            except BaseException as error:
                cleanup.append(str(error))
            for handle, flag in zip(handles, original):
                try:
                    os.set_handle_inheritable(handle, flag)
                    if os.get_handle_inheritable(handle) != flag:
                        raise RuntimeError('Log handle restoration differs')
                except BaseException as error:
                    cleanup.append(str(error))
        if created and (failure or cleanup):
            raise CreatedProcessError(created, str(failure) + '; ' + '; '.join(cleanup))
        if cleanup:
            raise RuntimeError(str(failure) + '; ' + '; '.join(cleanup))
        if failure:
            raise failure
        return created

    def assign(self, job, process):
        return self.result('AssignProcessToJobObject', job, process)

    def resume(self, thread):
        c.set_last_error(0)
        value = self.k.ResumeThread(thread)
        return dict(returnValue=value, winError=c.get_last_error() if value == 0xffffffff else None)

    def wait(self, process, seconds):
        # CPython raises OSError on WAIT_FAILED; call() retains its winerror.
        value = self.api.WaitForSingleObject(process, max(0, int(seconds * 1000)))
        return dict(waitResult=value, requestedMilliseconds=max(0, int(seconds * 1000)))

    def exit(self, process):
        return self.api.GetExitCodeProcess(process)

    def stop(self, job, process, assigned):
        return self.result('TerminateJobObject' if assigned else 'TerminateProcess', job if assigned else process, 0xe0000077)

    def inspect(self, job):
        result = {}
        for name, information_class, size in (('accounting', 1, 48), ('limits', 9, 144), ('pids', 3, 8 + PID_CAPACITY * 8)):
            buffer = c.create_string_buffer(size)
            returned = w.DWORD()
            start = self.now()
            call = self.result('QueryInformationJobObject', job, information_class, buffer, size, c.byref(returned))
            result[name] = dict(call, informationClass=information_class, capacityBytes=size,
                                returnedBytes=returned.value, rawBytes=buffer.raw.hex(), startedSeconds=start, finishedSeconds=self.now())
        return result

    def close(self, handle):
        return self.result('CloseHandle', handle)


def supervise(binary, environment, output, write, backend=None, clock=time.monotonic, timer=threading.Timer, sleep=time.sleep):
    """One launch/assignment, sticky failures, four fixed samples; never retry."""
    started = clock()
    now = lambda: clock() - started
    record = dict(supervisorSchemaVersion=2, supervisorPid=os.getpid(), processId=None, threadId=None,
                  exitCode=None, exitCodeHex=None, created=False, assigned=False, resumed=False, timedOut=False,
                  ownedChildHandle=False, childIdentityVerified=False, childExitObserved=False, cleanupVerified=False, emptyMembershipObserved=False,
                  jobHandleClosed=False, totalProcesses=None, activeProcesses=None, timeoutSeconds=30, cleanupSeconds=5,
                  cleanupTargets=list(CLEANUP_TARGETS), retryCount=0, assignmentAttempts=0, error=None,
                  errors=[], loggingErrors=[], operations=[], jobObservations=[], terminationRequests=[])
    job = process = thread = watchdog = None
    lock = threading.RLock()
    finished = threading.Event()
    seen = None
    closed = set()
    output = Path(output)
    journal = (output / 'supervisor.jsonl').open('x', encoding='utf-8', newline='\n')
    journal_usable = True

    def emit(event):
        nonlocal journal_usable
        with lock:
            event = dict(event, sequence=len(record['operations']), seconds=now())
            record['operations'].append(event)
            if journal_usable:
                try:
                    journal.write(json.dumps(event, allow_nan=False) + '\n')
                    journal.flush()
                except BaseException as error:
                    journal_usable = False
                    record['loggingErrors'].append(type(error).__name__ + ': ' + str(error))
                    failed(RuntimeError('Supervisor journal write/flush failed'))
            return event['sequence']

    def call(name, function, *args, details=None):
        begin = emit(dict(name=name, phase='BEGIN', arguments=details))
        try:
            if not journal_usable and name in ('job', 'configure', 'create', 'assign', 'resume'):
                raise RuntimeError('No new launch/assignment/resume after journal failure')
            if name in ('create', 'assign', 'resume') and (record['timedOut'] or now() >= 30):
                record['timedOut'] = True
                raise TimeoutError('Original deadline before native ' + name)
            value = function(*args)
        except BaseException as error:
            emit(dict(name=name, phase='END', beginSequence=begin, error=dict(type=type(error).__name__,
                      message=str(error), winError=getattr(error, 'winerror', None), native=getattr(error, 'native', None))))
            raise
        emit(dict(name=name, phase='END', beginSequence=begin, result=value))
        return value

    def failed(error):
        message = type(error).__name__ + ': ' + str(error)
        record['errors'].append(message)
        record['error'] = record['error'] or message

    def attempt(function):
        try:
            return function()
        except BaseException as error:
            failed(error)
            return None

    def success(value):
        if not (type(value['returnValue']) is int and value['returnValue'] != 0 and value['winError'] is None):
            raise NativeFailure('Native operation', value)

    def observe(phase, target=None):
        nonlocal seen
        entry = dict(phase=phase, startedSeconds=now())
        if target is not None:
            entry['targetSeconds'] = target
        record['jobObservations'].append(entry)
        entry['raw'] = call('observe:' + phase, backend.inspect, job, details=dict(jobHandle=job))
        entry['finishedSeconds'] = now()
        entry['decoded'] = decode_snapshot(entry['raw'])
        if phase == 'CREATED_EMPTY' and bytes.fromhex(entry['raw']['accounting']['rawBytes']) != bytes(48):
            raise ValueError('New Job accounting is not zero')
        if phase == 'AFTER_ASSIGNMENT':
            record['assignmentObservation'] = entry['decoded']
        if phase == 'CLEANUP_3':
            record['cleanupObservation'] = entry['decoded']
        seen = check_snapshot(entry['decoded'], phase, record['processId'], seen)
        return entry['decoded']

    def terminate(reason):
        with lock:
            if record['terminationRequests'] or not process or not record['ownedChildHandle']:
                return
            request = dict(reason=reason, seconds=now(), assigned=record['assigned'], jobHandle=job, processHandle=process)
            record['terminationRequests'].append(request)
            request['result'] = call('terminate', backend.stop, job, process, record['assigned'], details=request.copy())
            success(request['result'])

    def timeout():
        with lock:
            if finished.is_set():
                return
            record['timedOut'] = True
            attempt(lambda: terminate('SUPERVISION_DEADLINE'))

    def capture_exit():
        record['exitCode'] = attempt(lambda: call('exit', backend.exit, process, details=dict(processHandle=process)))
        record['childIdentityAtExit'] = attempt(lambda: call('identity-at-exit', backend.identity, process, thread,
                                                          details=dict(processHandle=process, threadHandle=thread)))
        def validate():
            check_identity(record['childIdentityAtExit'], record['processId'], record['threadId'],
                           record['binaryPath'], record['binarySha256'], record['supervisorPid'])
            if record['childIdentityAtExit'] != record['childIdentityBefore']:
                raise ValueError('Child identity changed before handle release')
        attempt(validate)
        attempt(lambda: observe('PROCESS_EXIT'))

    def close(handle, kind):
        if handle and handle not in closed:
            closed.add(handle)  # Register the attempt before I/O or the native call.
            value = call('close:' + kind, backend.close, handle, details=dict(handle=handle))
            success(value)
            return True
        return False

    def retain_created(pid, tid):
        record.update(processId=pid, threadId=tid, processHandle=process, threadHandle=thread, created=True, createdSeconds=now())
        if len({job, process, thread}) != 3 or any(type(h) is not int or h <= 0 for h in (job, process, thread)):
            raise ValueError('Aliased or invalid owned handles')
        # Ownership follows the successful CreateProcess return, independently of
        # subsequent experimental image/PID/hash acceptance. Never reopen a PID.
        record['ownedChildHandle'] = True

    try:
        backend = backend or Kernel()
        backend.now = now
        record['binaryPath'] = str(Path(binary).resolve())
        record['binarySha256'] = hashlib.sha256(Path(binary).read_bytes()).hexdigest()
        record['childEnvironmentKeys'] = sorted(environment)
        record['childEnvironmentSha256'] = hashlib.sha256(json.dumps(environment, sort_keys=True).encode()).hexdigest()
        job = call('job', backend.job)
        record['jobHandle'] = job
        observe('CREATED_EMPTY')
        success(call('configure', backend.configure, job, details=dict(jobHandle=job, flags=0x2008, activeLimit=1)))
        observe('CONFIGURED_EMPTY')
        record['parentIdentity'] = call('parent', backend.parent, job, details=dict(jobHandle=job))
        parent = record['parentIdentity']
        if parent['processId'] != os.getpid() or parent['inPrivateJob'] is not False or parent['jobHandleFlags'] != 0:
            raise ValueError('Private Job/supervisor identity differs')
        with open(os.devnull, 'rb') as stdin, (output / 'stdout.json').open('xb') as stdout, (output / 'stderr.jsonl').open('xb') as stderr:
            watchdog = timer(max(0, 30 - now()), timeout)
            watchdog.start()
            try:
                process, thread, pid, tid = call('create', backend.create, Path(record['binaryPath']), environment,
                                                (stdin, stdout, stderr), details=dict(binary=record['binaryPath'], flags=0x08000004))
            except CreatedProcessError as error:
                process, thread, pid, tid = error.handles
                retain_created(pid, tid)
                record['childIdentityBefore'] = call('identity-before', backend.identity, process, thread)
                check_identity(record['childIdentityBefore'], pid, tid, record['binaryPath'], record['binarySha256'], os.getpid())
                record['childIdentityVerified'] = True
                raise
            retain_created(pid, tid)
            record['childIdentityBefore'] = call('identity-before', backend.identity, process, thread,
                                               details=dict(processHandle=process, threadHandle=thread))
            check_identity(record['childIdentityBefore'], pid, tid, record['binaryPath'], record['binarySha256'], os.getpid())
            record['childIdentityVerified'] = True
            if record['timedOut'] or now() >= 30:
                raise TimeoutError('Deadline before assignment')
            if call('association-before', backend.association, job, process, details=dict(jobHandle=job, processHandle=process)) is not False:
                raise ValueError('Repeated or inherited private Job assignment')
            observe('BEFORE_ASSIGNMENT')
            record['assignmentAttempts'] += 1
            result = call('assign', backend.assign, job, process, details=dict(jobHandle=job, processHandle=process, processId=pid))
            record['assignmentResult'] = result
            record.update(assigned=bool(result['returnValue']), assignedSeconds=now())
            attempt(lambda: observe('AFTER_ASSIGNMENT'))
            success(result)
            if record['errors']:
                raise ValueError('Assignment observation failed')
            if call('association-after', backend.association, job, process, details=dict(jobHandle=job, processHandle=process)) is not True:
                raise ValueError('Assignment returned success without association')
            with lock:
                if record['timedOut'] or now() >= 30:
                    raise TimeoutError('Deadline before resume')
                result = call('resume', backend.resume, thread, details=dict(threadHandle=thread))
                if result != dict(returnValue=1, winError=None):
                    raise ValueError('Unexpected suspended-thread result: ' + str(result))
                record.update(resumed=True, resumedSeconds=now())
            observe('AFTER_RESUME')
            wait = call('wait', backend.wait, process, max(0, 30 - now()), details=dict(processHandle=process))
            if wait['waitResult'] != 0:
                record['timedOut'] = wait['waitResult'] == 258
                raise TimeoutError('Child wait timed out') if record['timedOut'] else RuntimeError('Unexpected child wait result')
            record.update(childExitObserved=True, observedExitSeconds=now())
            if now() > 30:
                record['timedOut'] = True
                raise TimeoutError('Exit observed after original deadline')
            capture_exit()
    except BaseException as error:
        failed(error)
    finally:
        record['cleanupStartedSeconds'] = now()
        with lock:
            finished.set()
            if watchdog:
                watchdog.cancel()
        # Independent stages: a failed termination must not suppress later evidence.
        if process:
            if record['errors'] or record['timedOut']:
                attempt(lambda: terminate('FAILED_OBSERVATION'))
            if not record['childExitObserved'] and record['ownedChildHandle']:
                wait = attempt(lambda: call('cleanup-wait', backend.wait, process,
                                            max(0, 5 - (now() - record['cleanupStartedSeconds']))))
                if wait and wait['waitResult'] == 0:
                    record['childExitObserved'] = True
                    record.setdefault('observedExitSeconds', now())
                    attempt(capture_exit)
                else:
                    failed(RuntimeError('Direct child exit remains unverified'))
            thread_closed = attempt(lambda: close(thread, 'thread'))
            process_closed = attempt(lambda: close(process, 'process'))
            if thread_closed and process_closed:
                record['childHandlesClosedSeconds'] = now()
        if job:
            for index, target in enumerate(CLEANUP_TARGETS):
                due = record['cleanupStartedSeconds'] + target
                if now() < due:
                    sleep(due - now())
                if now() >= record['cleanupStartedSeconds'] + 5:
                    failed(TimeoutError('Fixed cleanup observation missed its bound'))
                    continue
                upper = (*CLEANUP_TARGETS[1:], 5)[index]
                if now() >= record['cleanupStartedSeconds'] + upper:
                    failed(TimeoutError('Fixed cleanup observation missed its scheduling window'))
                attempt(lambda i=index, t=target: observe('CLEANUP_' + str(i), t))
            record['jobHandleClosed'] = bool(attempt(lambda: close(job, 'job')))
            record['jobHandleClosedSeconds'] = now()
        final = record.get('cleanupObservation')
        if final:
            record['totalProcesses'], record['activeProcesses'] = final['totalProcesses'], final['activeProcesses']
            record['emptyMembershipObserved'] = final['activeProcesses'] == final['assignedProcesses'] == 0
        if record['exitCode'] is not None:
            record['exitCodeHex'] = format(record['exitCode'] & 0xffffffff, '08x')
        record['cleanupFinishedSeconds'] = record['finishedSeconds'] = now()
        if now() - record['cleanupStartedSeconds'] > 5:
            failed(TimeoutError('Cleanup exceeded original separate bound'))
        record['cleanupVerified'] = (record['emptyMembershipObserved'] and record['totalProcesses'] == 1
                                     and record['childExitObserved'] and 'childHandlesClosedSeconds' in record
                                     and record['jobHandleClosed'] and not record['errors'])
        try:
            journal.close()
        except BaseException as error:
            failed(error)
            journal_usable = False
        record['journalComplete'] = journal_usable
        record['cleanupVerified'] = record['cleanupVerified'] and journal_usable
        record['supervisorJournalSha256'] = attempt(lambda: hashlib.sha256((output / 'supervisor.jsonl').read_bytes()).hexdigest())
        write(output / 'process.json', record)
    return record
