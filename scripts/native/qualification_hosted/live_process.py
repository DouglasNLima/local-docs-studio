"""Kernel-only single-child supervision; imported only after external authority.

No window/input/theme APIs. The sole child is assigned while suspended. CPython's
Windows process primitive honours STARTUPINFO.handle_list; only three log handles
are inherited. A watchdog thread adds no experimental process.
"""
import ctypes as c
from ctypes import wintypes as w
import os
from pathlib import Path
import subprocess
import threading
import time


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
    """Transfer every returned native handle even when post-create setup fails."""
    def __init__(self, handles, message):
        super().__init__(message); self.handles = handles


class Kernel:
    def __init__(self):
        if os.name != 'nt' or c.sizeof(c.c_void_p) != 8 or c.sizeof(Limits) != 144 or c.sizeof(Accounting) != 48:
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
            'ResumeThread': ([w.HANDLE], w.DWORD),
            'GetErrorMode': ([], w.UINT), 'SetErrorMode': ([w.UINT], w.UINT)}
        for name, (arguments, result) in declarations.items():
            function = getattr(self.k, name); function.argtypes = arguments; function.restype = result

    @staticmethod
    def check(value):
        if not value: raise c.WinError(c.get_last_error())

    def job(self):
        handle = self.k.CreateJobObjectW(None, None); self.check(handle)
        try:
            limits = Limits(); limits.basic.flags = 0x2008; limits.basic.activeLimit = 1
            self.check(self.k.SetInformationJobObject(handle, 9, c.byref(limits), c.sizeof(limits)))
            return handle
        except BaseException:
            self.close(handle); raise

    def create(self, binary, environment, streams):
        handles = [self.crt.get_osfhandle(stream.fileno()) for stream in streams]
        startup = subprocess.STARTUPINFO(); startup.dwFlags = subprocess.STARTF_USESTDHANDLES
        startup.hStdInput, startup.hStdOutput, startup.hStdError = handles
        startup.lpAttributeList = {'handle_list': handles}
        previous = self.k.GetErrorMode()
        if previous & 4: raise ValueError('Inherited alignment-fault suppression is forbidden')
        created = None; failure = None; cleanup = []
        try:
            for handle in handles: os.set_handle_inheritable(handle, True)
            # Child-only inherited crash handling. Never hide alignment faults.
            self.k.SetErrorMode(previous | 3)
            created = self.api.CreateProcess(str(binary), subprocess.list2cmdline([str(binary)]), None, None, True,
                                             0x08000004, environment, str(Path(binary).parent), startup)
        except BaseException as error: failure = error
        finally:
            try:
                self.k.SetErrorMode(previous)
                if self.k.GetErrorMode() != previous: raise RuntimeError('Process error-mode restoration differs')
            except BaseException as error: cleanup.append(str(error))
            for handle in handles:
                try: os.set_handle_inheritable(handle, False)
                except BaseException as error: cleanup.append(str(error))
        if created and (failure or cleanup): raise CreatedProcessError(created, str(failure) + '; ' + '; '.join(cleanup))
        if cleanup: raise RuntimeError(str(failure) + '; ' + '; '.join(cleanup))
        if failure: raise failure
        return created

    def assign(self, job, process): self.check(self.k.AssignProcessToJobObject(job, process))
    def resume(self, thread):
        if self.k.ResumeThread(thread) != 1: raise RuntimeError('Unexpected suspended-thread state')
    def wait(self, process, seconds): return self.api.WaitForSingleObject(process, max(0, int(seconds * 1000))) == 0
    def exit(self, process): return self.api.GetExitCodeProcess(process)
    def stop(self, job, process, assigned):
        if assigned: self.check(self.k.TerminateJobObject(job, 0xe0000077))
        else: self.api.TerminateProcess(process, 0xe0000077)
    def counts(self, job):
        value = Accounting()
        self.check(self.k.QueryInformationJobObject(job, 1, c.byref(value), c.sizeof(value), None))
        return value.total, value.active
    def inspect(self, job):
        accounting = Accounting(); returned = w.DWORD()
        self.check(self.k.QueryInformationJobObject(job,1,c.byref(accounting),c.sizeof(accounting),c.byref(returned)))
        if returned.value != c.sizeof(accounting): raise ValueError('Unexpected accounting byte count')
        limits=Limits()
        self.check(self.k.QueryInformationJobObject(job,9,c.byref(limits),c.sizeof(limits),None))
        # Fixed diagnostic capacity; extra or truncated membership remains a failure.
        members=c.create_string_buffer(8+8*8)
        self.check(self.k.QueryInformationJobObject(job,3,members,c.sizeof(members),None))
        assigned,count=w.DWORD.from_buffer(members,0).value,w.DWORD.from_buffer(members,4).value
        if count>8 or assigned!=count: raise ValueError('Truncated Job membership')
        pids=[c.c_size_t.from_buffer(members,8+8*n).value for n in range(count)]
        return dict(totalProcesses=accounting.total,activeProcesses=accounting.active,terminatedProcesses=accounting.terminated,
                    pageFaults=accounting.faults,accountingBytes=c.string_at(c.byref(accounting),c.sizeof(accounting)).hex(),
                    returnedBytes=returned.value,limitFlags=limits.basic.flags,activeLimit=limits.basic.activeLimit,
                    assignedProcesses=assigned,processIds=pids)
    def close(self, handle): self.api.CloseHandle(handle)


def supervise(binary, environment, output, write, backend=None, clock=time.monotonic, timer=threading.Timer):
    """No retry. Raw outputs survive every exception; 5s cleanup is not observation."""
    backend = backend or Kernel()
    record = dict(processId=None, exitCode=None, exitCodeHex=None, created=False, assigned=False, resumed=False,
                  timedOut=False, cleanupVerified=False, totalProcesses=None, activeProcesses=None,
                  timeoutSeconds=30, cleanupSeconds=5, retryCount=0, error=None)
    job = process = thread = watchdog = None
    gate = threading.Lock(); finished = threading.Event(); started = clock()

    def timeout():
        with gate:
            if finished.is_set(): return
            record['timedOut'] = True
            if process:
                try: backend.stop(job, process, record['assigned'])
                except Exception as error: record['watchdogError'] = str(error)

    try:
        job = backend.job()
        with open(os.devnull, 'rb') as stdin, (output / 'stdout.json').open('xb') as stdout, (output / 'stderr.jsonl').open('xb') as stderr:
            watchdog = timer(max(0, 30 - (clock() - started)), timeout); watchdog.start()
            try: process, thread, pid, _ = backend.create(binary, environment, (stdin, stdout, stderr))
            except CreatedProcessError as error:
                process, thread, pid, _ = error.handles
                record.update(processId=pid, created=True, createdSeconds=clock() - started)
                raise
            record.update(processId=pid, created=True, createdSeconds=clock() - started)
            if record['timedOut'] or clock() - started >= 30: raise TimeoutError('Deadline before assignment')
            backend.assign(job, process); record.update(assigned=True, assignedSeconds=clock() - started)
            record['assignmentObservation'] = backend.inspect(job)
            assigned = record['assignmentObservation']
            if (assigned['totalProcesses'] != 1 or assigned['activeProcesses'] != 1 or assigned['processIds'] != [pid]
                    or assigned['limitFlags'] != 0x2008 or assigned['activeLimit'] != 1):
                raise RuntimeError('Single-process Job prerequisite failed while suspended')
            with gate:
                if record['timedOut'] or clock() - started >= 30: raise TimeoutError('Deadline before resume')
                backend.resume(thread); record.update(resumed=True, resumedSeconds=clock() - started)
            if not backend.wait(process, max(0, 30 - (clock() - started))):
                record['timedOut'] = True; raise TimeoutError('Original 30-second supervision expired')
            record['observedExitSeconds'] = clock() - started
            if record['observedExitSeconds'] > 30:
                record['timedOut'] = True; raise TimeoutError('Exit observed after the original deadline')
            record['exitCode'] = backend.exit(process)
    except BaseException as error:
        record['error'] = type(error).__name__ + ': ' + str(error)
    finally:
        record['cleanupStartedSeconds'] = clock() - started
        with gate:
            finished.set()
            if watchdog: watchdog.cancel()
            if process:
                try:
                    if record['error'] or record['timedOut']: backend.stop(job, process, record['assigned'])
                    remaining = max(0, 5 - (clock() - started - record['cleanupStartedSeconds']))
                    if not backend.wait(process, remaining): raise RuntimeError('Direct child cleanup unverified')
                    record['exitCode'] = backend.exit(process)
                    if record['assigned']:
                        record['cleanupObservation'] = backend.inspect(job)
                        record['totalProcesses'] = record['cleanupObservation']['totalProcesses']
                        record['activeProcesses'] = record['cleanupObservation']['activeProcesses']
                        record['cleanupVerified'] = record['totalProcesses'] == 1 and record['activeProcesses'] == 0
                except BaseException as error: record['cleanupError'] = str(error)
            if record['exitCode'] is not None: record['exitCodeHex'] = format(record['exitCode'] & 0xffffffff, '08x')
            for handle in (thread, process, job):
                if handle:
                    try: backend.close(handle)
                    except BaseException as error: record['closeError'] = str(error); record['cleanupVerified'] = False
            record['cleanupFinishedSeconds'] = record['finishedSeconds'] = clock() - started
            if record['cleanupFinishedSeconds'] - record['cleanupStartedSeconds'] > 5: record['cleanupVerified'] = False
            write(output / 'process.json', record)
    return record
