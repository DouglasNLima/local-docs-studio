// Controlled allocation fixture only. Never calls SystemParametersInfo or any UI API.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <stdexcept>
#include <string>
#include <type_traits>

static_assert(sizeof(void*) == 8 && sizeof(HIGHCONTRASTW) == 16);
static_assert(offsetof(HIGHCONTRASTW, cbSize) == 0 && offsetof(HIGHCONTRASTW, dwFlags) == 4);
static_assert(offsetof(HIGHCONTRASTW, lpszDefaultScheme) == 8 && sizeof(WCHAR) == 2 && sizeof(BOOL) == 4);
static_assert(std::is_same_v<decltype(&SystemParametersInfoW), BOOL(WINAPI*)(UINT, UINT, PVOID, UINT)>);
static_assert(std::is_same_v<decltype(&LocalFree), HLOCAL(WINAPI*)(HLOCAL)>);

#define API extern "C" __declspec(dllexport)
struct Slot { WCHAR* data{}; unsigned units{}; std::uint64_t token{}; };
static Slot slots[8];
static std::uint64_t sequence{}, selected{};
static unsigned mode{}, allocations{}, releases{}, denied{}, reads{};
static Slot* find(std::uint64_t token) {
    for (auto& slot : slots) if (token != 0 && slot.token == token && slot.data) return &slot;
    return nullptr;
}

API unsigned WINAPI FixtureAbi(unsigned field) {
    const unsigned values[]{sizeof(void*), sizeof(HIGHCONTRASTW), offsetof(HIGHCONTRASTW, cbSize),
        offsetof(HIGHCONTRASTW, dwFlags), offsetof(HIGHCONTRASTW, lpszDefaultScheme), sizeof(WCHAR), sizeof(BOOL)};
    return field < 7 ? values[field] : 0xffffffffu;
}
API std::uint64_t WINAPI FixtureCreate(const WCHAR* value, unsigned units) {
    if (!value || units == 0 || units > 32768) { SetLastError(ERROR_INVALID_PARAMETER); return 0; }
    for (auto& slot : slots) if (!slot.data) {
        auto pointer = static_cast<WCHAR*>(LocalAlloc(LMEM_FIXED, units * sizeof(WCHAR)));
        if (!pointer) return 0;
        std::memcpy(pointer, value, units * sizeof(WCHAR));
        slot = {pointer, units, ++sequence}; ++allocations; return slot.token;
    }
    SetLastError(ERROR_NOT_ENOUGH_MEMORY); return 0;
}
API BOOL WINAPI FixtureSelect(std::uint64_t token, unsigned nextMode) {
    if (!find(token) || nextMode > 3) { SetLastError(ERROR_INVALID_PARAMETER); return FALSE; }
    selected = token; mode = nextMode; return TRUE;
}
API BOOL WINAPI FixtureReplace(std::uint64_t token, const WCHAR* value, unsigned units) {
    auto slot = find(token);
    if (!slot || !value || units != slot->units) { SetLastError(ERROR_INVALID_PARAMETER); return FALSE; }
    std::memcpy(slot->data, value, units * sizeof(WCHAR)); return TRUE;
}
// Same native ABI as the SPI function, with independently declared SDK structures.
API BOOL WINAPI FixtureGet(UINT action, UINT size, PVOID output, UINT flags) {
    auto state = static_cast<HIGHCONTRASTW*>(output); auto slot = find(selected);
    if (!state || !slot || action != SPI_GETHIGHCONTRAST || size != sizeof(HIGHCONTRASTW)
        || state->cbSize != sizeof(HIGHCONTRASTW) || flags != 0) {
        SetLastError(ERROR_INVALID_PARAMETER); return FALSE;
    }
    state->dwFlags = 0x8000007eu; // Raw high bits must survive without masking.
    state->lpszDefaultScheme = mode == 1 ? nullptr : slot->data;
    if (mode == 2) {
        state->lpszDefaultScheme = reinterpret_cast<WCHAR*>(static_cast<std::uintptr_t>(1));
        SetLastError(ERROR_ACCESS_DENIED); return FALSE;
    }
    if (mode == 3) state->cbSize = 12;
    return TRUE;
}
API BOOL WINAPI FixtureRead(std::uint64_t token, const WCHAR* pointer, unsigned index, WCHAR* result) {
    auto slot = find(token);
    if (!slot || !result || pointer != slot->data || index >= slot->units) {
        SetLastError(ERROR_INVALID_DATA); return FALSE;
    }
    ++reads; *result = pointer[index]; return TRUE;
}
API BOOL WINAPI FixtureBorrowerRelease(std::uint64_t token) {
    if (!find(token)) { SetLastError(ERROR_INVALID_HANDLE); return FALSE; }
    ++denied; SetLastError(ERROR_ACCESS_DENIED); return FALSE; // Never invokes a deallocator.
}
API BOOL WINAPI FixtureOwnerRelease(std::uint64_t token) {
    auto slot = find(token);
    if (!slot) { SetLastError(ERROR_INVALID_HANDLE); return FALSE; }
    if (LocalFree(slot->data) != nullptr) return FALSE;
    slot->data = nullptr; ++releases; return TRUE;
}
API unsigned WINAPI FixtureCount(unsigned field) {
    unsigned active = 0; for (const auto& slot : slots) if (slot.data) ++active;
    const unsigned values[]{allocations, releases, denied, reads, active};
    return field < 5 ? values[field] : 0xffffffffu;
}

#ifdef NATIVE_SELF_TEST
static unsigned checks{};
static void expect(bool value) { ++checks; if (!value) throw std::runtime_error("Native control failed"); }
class Owner {
public:
    std::uint64_t token;
    Owner(const WCHAR* value, unsigned units) : token(FixtureCreate(value, units)) {
        if (!token) throw std::runtime_error("Fixture allocation failed");
    }
    ~Owner() { if (!FixtureOwnerRelease(token)) std::terminate(); }
    Owner(const Owner&) = delete;
    Owner& operator=(const Owner&) = delete;
};
static std::wstring copy(std::uint64_t token, HIGHCONTRASTW& state) {
    if (!FixtureGet(SPI_GETHIGHCONTRAST, sizeof(state), &state, 0)) throw std::runtime_error("GET failed");
    if (!state.lpszDefaultScheme) return {};
    std::wstring result;
    for (unsigned index = 0; index < 32768; ++index) {
        WCHAR unit;
        if (!FixtureRead(token, state.lpszDefaultScheme, index, &unit)) throw std::runtime_error("Truncated fixture");
        if (!unit) return result;
        result += unit;
    }
    throw std::runtime_error("Unterminated fixture");
}
int main() {
    try {
        {
            const WCHAR empty[]{0, 0, 0, 0}; const WCHAR named[]{L'H', L'C', 0, 0};
            Owner owner(empty, 4); expect(FixtureSelect(owner.token, 0));
            HIGHCONTRASTW first{sizeof(HIGHCONTRASTW), 0, nullptr};
            auto old = copy(owner.token, first); expect(old.empty()); expect(first.lpszDefaultScheme != nullptr);
            expect(first.dwFlags == 0x8000007eu); expect(FixtureReplace(owner.token, named, 4));
            HIGHCONTRASTW second{sizeof(HIGHCONTRASTW), 0, nullptr};
            expect(copy(owner.token, second) == L"HC"); expect(first.lpszDefaultScheme == second.lpszDefaultScheme);
            expect(old.empty()); expect(FixtureCount(1) == 0); expect(!FixtureBorrowerRelease(owner.token));
            expect(FixtureSelect(owner.token, 1)); HIGHCONTRASTW nullState{sizeof(HIGHCONTRASTW), 0, nullptr};
            expect(copy(owner.token, nullState).empty()); expect(nullState.lpszDefaultScheme == nullptr);
            expect(FixtureSelect(owner.token, 2)); HIGHCONTRASTW failed{sizeof(HIGHCONTRASTW), 0, nullptr};
            expect(!FixtureGet(SPI_GETHIGHCONTRAST, sizeof(failed), &failed, 0)); expect(GetLastError() == ERROR_ACCESS_DENIED);
        }
        expect(FixtureCount(0) == 1 && FixtureCount(1) == 1 && FixtureCount(4) == 0);
        expect(!FixtureOwnerRelease(sequence)); // Duplicate owner release rejected before LocalFree.
        {
            const WCHAR truncated[]{L'A', L'B'}; Owner owner(truncated, 2);
            expect(FixtureSelect(owner.token, 0)); HIGHCONTRASTW state{sizeof(HIGHCONTRASTW), 0, nullptr};
            bool rejected = false; try { copy(owner.token, state); } catch (const std::runtime_error&) { rejected = true; }
            expect(rejected);
        }
        expect(FixtureCount(0) == 2 && FixtureCount(1) == 2 && FixtureCount(4) == 0);
        std::printf("{\"nativeChecks\":%u,\"pointerBytes\":8,\"structureBytes\":16,\"offsets\":[0,4,8],\"wcharBytes\":2,\"boolBytes\":4,\"allocations\":%u,\"ownerReleases\":%u,\"borrowerReleaseDenials\":%u,\"activeAllocations\":0,\"actualHighContrastCalls\":0}\n", checks, allocations, releases, denied);
        return 0;
    } catch (const std::exception& error) { std::fprintf(stderr, "%s\n", error.what()); return 1; }
}
#endif
