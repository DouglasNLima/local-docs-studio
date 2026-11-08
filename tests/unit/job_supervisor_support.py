"""Pure fake kernel/clock shared by offline controls. Never constructs Kernel."""
import copy
import hashlib
import os
from pathlib import Path
import struct

FIXTURE_BYTES = b'offline-only-not-an-executable'
FIXTURE_SHA256 = hashlib.sha256(FIXTURE_BYTES).hexdigest()


class Clock:
    def __init__(self): self.value = 0.0
    def __call__(self): return self.value
    def sleep(self, seconds): self.value += seconds


class Timer:
    def __init__(self, seconds, callback):
        assert 0 <= seconds <= 30
        self.callback = callback
    def start(self): pass
    def cancel(self): pass


def raw_job(total, active, pids=None, flags=0x2008, limit=1, terminated=0, now=0):
    pids = ([133] if active else []) if pids is None else pids
    accounting = bytes(32) + struct.pack('<IIII', 0, total, active, terminated)
    limits = bytearray(144)
    struct.pack_into('<I', limits, 16, flags)
    struct.pack_into('<I', limits, 40, limit)
    members = struct.pack('<II', len(pids), len(pids)) + b''.join(struct.pack('<Q', p) for p in pids)
    result = {}
    for name, number, raw, capacity in (('accounting', 1, accounting, 48), ('limits', 9, bytes(limits), 144),
                                        ('pids', 3, members, 72)):
        result[name] = dict(informationClass=number, returnValue=1, winError=None, capacityBytes=capacity,
                            returnedBytes=len(raw), rawBytes=raw.ljust(capacity, b'\0').hex(),
                            startedSeconds=now, finishedSeconds=now)
    return result


class FakeKernel:
    def __init__(self, fail=None, code=0, counts=(1, 0), mutations=None, clock=None):
        self.calls = []
        self.fail, self.code, self.final_counts = fail, code, counts
        self.waits = self.observations = 0
        self.configured = self.assigned = self.resumed = self.exited = False
        self.clock = clock or Clock()
        self.mutations = mutations or {}
        self.now = self.clock
        self.binary = None

    def call(self, name):
        self.calls.append(name)
        if self.fail == name:
            error = OSError('Injected ' + name + ' failure')
            error.winerror = 5
            raise error

    def job(self): self.call('job'); return 10
    def configure(self, job):
        self.call('configure'); self.configured = True
        return dict(returnValue=1, winError=None)

    def parent(self, job):
        self.call('parent')
        return dict(processId=os.getpid(), imagePath='C:\\Python\\python.exe', imageSha256='a' * 64,
                    creationFileTime=10, inAnyJob=True, inPrivateJob=False, jobHandleFlags=0)

    def create(self, binary, *args):
        self.call('create'); self.binary = binary
        return 11, 12, 133, 134

    def identity(self, process, thread):
        self.call('identity')
        return dict(processId=133, threadId=134, threadProcessId=133, imagePath=str(self.binary.resolve()),
                    imageSha256=FIXTURE_SHA256, creationFileTime=20, processHandleFlags=0, threadHandleFlags=0)

    def association(self, job, process):
        self.call('association')
        return self.assigned

    def assign(self, job, process):
        self.calls.append('assign')
        self.assigned = self.fail != 'assign'
        return dict(returnValue=int(self.assigned), winError=None if self.assigned else 5)

    def resume(self, thread):
        self.call('resume'); self.resumed = True
        return dict(returnValue=1, winError=None)

    def wait(self, handle, seconds):
        self.call('wait'); self.waits += 1
        timeout = self.fail == 'timeout' and self.waits == 1
        self.clock.sleep(seconds if timeout else min(0.1, seconds))
        self.exited = not timeout
        return dict(waitResult=258 if timeout else 0, requestedMilliseconds=int(seconds * 1000))

    def exit(self, *args): self.call('exit'); return self.code
    def stop(self, job, process, assigned):
        self.call('stop-job' if assigned else 'stop-direct')
        return dict(returnValue=1, winError=None)

    def inspect(self, job):
        self.call('inspect')
        total, active = ((self.final_counts if self.exited else (1, 1)) if self.assigned else (0, 0))
        value = raw_job(total, active, flags=0x2008 if self.configured else 0,
                        limit=1 if self.configured else 0, now=self.now())
        mutation = self.mutations.get(self.observations)
        self.observations += 1
        if mutation: mutation(value)
        return copy.deepcopy(value)

    def close(self, handle):
        self.call('close')
        return dict(returnValue=1, winError=None)
