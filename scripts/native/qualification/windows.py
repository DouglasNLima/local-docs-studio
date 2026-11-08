"""Loaded only after hosted/owner guards. No WPF and no synthetic theme messages."""
import ctypes as c
from ctypes import wintypes as w
import time


class HC(c.Structure):
    _fields_ = [('size', w.UINT), ('flags', w.UINT), ('scheme', w.LPWSTR)]


class BasicLimits(c.Structure):
    _fields_ = [('processTime', c.c_longlong), ('jobTime', c.c_longlong), ('flags', w.DWORD),
                ('minWorkingSet', c.c_size_t), ('maxWorkingSet', c.c_size_t), ('processLimit', w.DWORD),
                ('affinity', c.c_size_t), ('priority', w.DWORD), ('scheduling', w.DWORD)]


class Io(c.Structure):
    _fields_ = [(name, c.c_ulonglong) for name in ('readOps', 'writeOps', 'otherOps', 'readBytes', 'writeBytes', 'otherBytes')]


class Limits(c.Structure):
    _fields_ = [('basic', BasicLimits), ('io', Io), ('processMemory', c.c_size_t), ('jobMemory', c.c_size_t),
                ('peakProcessMemory', c.c_size_t), ('peakJobMemory', c.c_size_t)]


class Memory(c.Structure):
    _fields_ = [('cb', w.DWORD), ('faults', w.DWORD)] + [(name, c.c_size_t) for name in
                 ('peakWorkingSet', 'workingSet', 'peakPaged', 'paged', 'peakNonPaged', 'nonPaged', 'pagefile', 'peakPagefile', 'private')]


class Accounting(c.Structure):
    _fields_ = [(name, c.c_longlong) for name in ('user', 'kernel', 'periodUser', 'periodKernel')] + [
        (name, w.DWORD) for name in ('faults', 'totalProcesses', 'activeProcesses', 'terminatedProcesses')]


class Mouse(c.Structure):
    _fields_ = [('x', w.LONG), ('y', w.LONG), ('data', w.DWORD), ('flags', w.DWORD), ('time', w.DWORD), ('extra', c.c_size_t)]


class Key(c.Structure):
    _fields_ = [('key', w.WORD), ('scan', w.WORD), ('flags', w.DWORD), ('time', w.DWORD), ('extra', c.c_size_t)]


class InputValue(c.Union):
    _fields_ = [('mouse', Mouse), ('key', Key)]


class Input(c.Structure):
    _fields_ = [('kind', w.DWORD), ('value', InputValue)]


class Windows:
    def __init__(self):
        self.user = c.WinDLL('user32', use_last_error=True); self.kernel = c.WinDLL('kernel32', use_last_error=True)
        self.psapi = c.WinDLL('psapi', use_last_error=True); self.job = None; self.assigned = False
        self.user.SystemParametersInfoW.argtypes = [w.UINT, w.UINT, c.POINTER(HC), w.UINT]
        self.user.SystemParametersInfoW.restype = w.BOOL
        self.kernel.CreateJobObjectW.argtypes = [c.c_void_p, w.LPCWSTR]; self.kernel.CreateJobObjectW.restype = w.HANDLE
        self.kernel.SetInformationJobObject.argtypes = [w.HANDLE, c.c_int, c.c_void_p, w.DWORD]
        self.kernel.AssignProcessToJobObject.argtypes = [w.HANDLE, w.HANDLE]
        self.kernel.TerminateJobObject.argtypes = [w.HANDLE, w.UINT]
        self.kernel.QueryInformationJobObject.argtypes = [w.HANDLE, c.c_int, c.c_void_p, w.DWORD, c.c_void_p]
        self.kernel.CloseHandle.argtypes = [w.HANDLE]
        self.psapi.GetProcessMemoryInfo.argtypes = [w.HANDLE, c.POINTER(Memory), w.DWORD]
        self.user.GetAsyncKeyState.argtypes = [c.c_int]; self.user.GetAsyncKeyState.restype = c.c_short
        self.user.SendInput.argtypes = [w.UINT, c.POINTER(Input), c.c_int]; self.user.SendInput.restype = w.UINT

    @staticmethod
    def require(ok):
        if not ok: raise c.WinError(c.get_last_error())

    def appearance(self):
        value = HC(); value.size = c.sizeof(HC)
        self.require(self.user.SystemParametersInfoW(0x42, value.size, c.byref(value), 0))
        return {'flags': value.flags, 'scheme': value.scheme or ''}

    def set_appearance(self, state):
        value = HC(c.sizeof(HC), state['flags'], state['scheme'])
        self.require(self.user.SystemParametersInfoW(0x43, value.size, c.byref(value), 2))
        if self.appearance() != state: raise RuntimeError('OS appearance restoration/change is unverified')

    def held_input(self):
        return [key for key in (1, 2, 4, 0x10, 0x11, 0x12) if self.user.GetAsyncKeyState(key) & 0x8000]

    def neutralise_input(self):
        # Entry must have no held button/modifier. Release only leftovers after this
        # guarded experiment; never send a new press or move the pointer in cleanup.
        held = self.held_input()
        for key in held:
            item = Input()
            if key in (1, 2, 4): item.kind = 0; item.value.mouse.flags = {1:4, 2:16, 4:64}[key]
            else: item.kind = 1; item.value.key.key = key; item.value.key.flags = 2
            self.require(self.user.SendInput(1, c.byref(item), c.sizeof(Input)) == 1)
        if self.held_input(): raise RuntimeError('Input cleanup could not be verified')
        return held

    def attach(self, child):
        self.assigned = False
        self.job = self.kernel.CreateJobObjectW(None, None); self.require(self.job)
        limits = Limits(); limits.basic.flags = 0x2000  # KILL_ON_JOB_CLOSE; no changed acceptance deadline.
        self.require(self.kernel.SetInformationJobObject(self.job, 9, c.byref(limits), c.sizeof(limits)))
        self.require(self.kernel.AssignProcessToJobObject(self.job, w.HANDLE(int(child._handle))))
        self.assigned = True

    def private_bytes(self, child):
        memory = Memory(); memory.cb = c.sizeof(memory)
        self.require(self.psapi.GetProcessMemoryInfo(w.HANDLE(int(child._handle)), c.byref(memory), memory.cb))
        return memory.private

    def kill(self, child):
        # An existing job handle does not prove assignment succeeded. Never abandon
        # the direct process if assignment/termination failed during setup.
        if self.job and self.assigned:
            self.kernel.TerminateJobObject(self.job, 1)
        if child.poll() is None:
            child.kill()

    def close(self):
        try:
            if self.job and self.assigned:
                # Root exit alone does not prove all inherited log handles/processes
                # have exited. Terminate any remainder and verify the job is empty.
                end = time.monotonic() + 10
                while True:
                    record = Accounting()
                    self.require(self.kernel.QueryInformationJobObject(self.job, 1, c.byref(record), c.sizeof(record), None))
                    if record.activeProcesses == 0: return True
                    self.require(self.kernel.TerminateJobObject(self.job, 1))
                    if time.monotonic() >= end: raise RuntimeError('Job-tree cleanup could not be verified within 10 seconds')
                    time.sleep(0.01)
            return False
        finally:
            if self.job: self.kernel.CloseHandle(self.job); self.job = None
            self.assigned = False
