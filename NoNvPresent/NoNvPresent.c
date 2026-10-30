// NoNvPresent.asi
// Impede que o driver NVIDIA 621.xx carregue o NvPresent64.dll dentro do RDR2.
// O NvPresent64 embrulha a swapchain por baixo do Streamline/OptiScaler e derruba
// o DLSS-G do OptiScaler (Present devolve E_NOINTERFACE ou fail-fast 0xc0000409).
// Sem ele o driver usa o caminho de apresentação antigo, igual ao 617.42.
//
// Como funciona: quando o driver de vídeo (nvldumdx.dll, nvwgf2umx.dll) é carregado, troca na
// tabela de importação dele as funções LoadLibrary* por versões que respondem
// "módulo não encontrado" só para o NvPresent64. Nada fora do processo do jogo muda.
//
// Carregado cedo pelo OptiScaler (LoadAsiPlugins = true, pasta OptiScaler\plugins).
// O ASI loader do jogo (dinput8) carrega tarde demais: o driver já subiu.

#include <windows.h>
#include <shlwapi.h>
#include <stdio.h>

typedef struct _UNICODE_STR { USHORT Length, MaximumLength; PWSTR Buffer; } UNICODE_STR;
typedef struct _LDR_NOTIF_DATA { ULONG Flags; const UNICODE_STR *FullDllName; const UNICODE_STR *BaseDllName; PVOID DllBase; ULONG SizeOfImage; } LDR_NOTIF_DATA;
typedef VOID (CALLBACK *LDR_NOTIF_FN)(ULONG reason, const LDR_NOTIF_DATA *data, PVOID ctx);
typedef LONG (NTAPI *LdrRegisterDllNotification_t)(ULONG flags, LDR_NOTIF_FN fn, PVOID ctx, PVOID *cookie);

static char g_log[MAX_PATH];
static PVOID g_cookie;

static void Log(const char *fmt, ...)
{
    char buf[512];
    SYSTEMTIME t; GetLocalTime(&t);
    int n = _snprintf_s(buf, sizeof(buf), _TRUNCATE, "[%02d:%02d:%02d.%03d] ", t.wHour, t.wMinute, t.wSecond, t.wMilliseconds);
    va_list ap; va_start(ap, fmt);
    n += _vsnprintf_s(buf + n, sizeof(buf) - n, _TRUNCATE, fmt, ap);
    va_end(ap);
    if (n < (int)sizeof(buf) - 2) { buf[n++] = '\r'; buf[n++] = '\n'; }
    HANDLE h = CreateFileA(g_log, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_ALWAYS, 0, NULL);
    if (h != INVALID_HANDLE_VALUE) { DWORD w; WriteFile(h, buf, n, &w, NULL); CloseHandle(h); }
}

static BOOL IsBlockedA(LPCSTR name) { return name && StrStrIA(name, "nvpresent") != NULL; }
static BOOL IsBlockedW(LPCWSTR name) { return name && StrStrIW(name, L"nvpresent") != NULL; }

static HMODULE WINAPI hkLoadLibraryA(LPCSTR n)
{
    if (IsBlockedA(n)) { Log("bloqueado LoadLibraryA(%s)", n); SetLastError(ERROR_MOD_NOT_FOUND); return NULL; }
    return LoadLibraryA(n);
}
static HMODULE WINAPI hkLoadLibraryW(LPCWSTR n)
{
    if (IsBlockedW(n)) { Log("bloqueado LoadLibraryW(%ls)", n); SetLastError(ERROR_MOD_NOT_FOUND); return NULL; }
    return LoadLibraryW(n);
}
static HMODULE WINAPI hkLoadLibraryExA(LPCSTR n, HANDLE f, DWORD fl)
{
    if (IsBlockedA(n)) { Log("bloqueado LoadLibraryExA(%s, 0x%X)", n, fl); SetLastError(ERROR_MOD_NOT_FOUND); return NULL; }
    return LoadLibraryExA(n, f, fl);
}
static HMODULE WINAPI hkLoadLibraryExW(LPCWSTR n, HANDLE f, DWORD fl)
{
    if (IsBlockedW(n)) { Log("bloqueado LoadLibraryExW(%ls, 0x%X)", n, fl); SetLastError(ERROR_MOD_NOT_FOUND); return NULL; }
    return LoadLibraryExW(n, f, fl);
}

static const struct { const char *name; void *hook; } g_hooks[] = {
    { "LoadLibraryA", (void *)hkLoadLibraryA },
    { "LoadLibraryW", (void *)hkLoadLibraryW },
    { "LoadLibraryExA", (void *)hkLoadLibraryExA },
    { "LoadLibraryExW", (void *)hkLoadLibraryExW },
};

// Troca as entradas LoadLibrary* da IAT do módulo (importadas de KERNEL32, KERNELBASE ou api-ms-win-core-libraryloader).
static int PatchIat(BYTE *base)
{
    IMAGE_NT_HEADERS *nt = (IMAGE_NT_HEADERS *)(base + ((IMAGE_DOS_HEADER *)base)->e_lfanew);
    IMAGE_DATA_DIRECTORY dir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    if (!dir.VirtualAddress) return 0;
    int patched = 0;
    for (IMAGE_IMPORT_DESCRIPTOR *d = (IMAGE_IMPORT_DESCRIPTOR *)(base + dir.VirtualAddress); d->Name; d++) {
        const char *dll = (const char *)(base + d->Name);
        if (_strnicmp(dll, "kernel32", 8) && _strnicmp(dll, "kernelbase", 10) && _strnicmp(dll, "api-ms-win-core-libraryloader", 29)) continue;
        if (!d->OriginalFirstThunk) continue;
        IMAGE_THUNK_DATA *names = (IMAGE_THUNK_DATA *)(base + d->OriginalFirstThunk);
        IMAGE_THUNK_DATA *iat = (IMAGE_THUNK_DATA *)(base + d->FirstThunk);
        for (; names->u1.AddressOfData; names++, iat++) {
            if (IMAGE_SNAP_BY_ORDINAL(names->u1.Ordinal)) continue;
            const char *fn = ((IMAGE_IMPORT_BY_NAME *)(base + names->u1.AddressOfData))->Name;
            for (int i = 0; i < (int)(sizeof(g_hooks) / sizeof(g_hooks[0])); i++) {
                if (strcmp(fn, g_hooks[i].name)) continue;
                DWORD old;
                if (VirtualProtect(&iat->u1.Function, sizeof(iat->u1.Function), PAGE_READWRITE, &old)) {
                    iat->u1.Function = (ULONG_PTR)g_hooks[i].hook;
                    VirtualProtect(&iat->u1.Function, sizeof(iat->u1.Function), old, &old);
                    patched++;
                }
            }
        }
    }
    return patched;
}

static BOOL IsNvUmd(const WCHAR *base, int len)
{
    return (len >= 8 && !_wcsnicmp(base, L"nvwgf2um", 8)) || (len >= 7 && !_wcsnicmp(base, L"nvd3dum", 7))
        || (len >= 7 && !_wcsnicmp(base, L"nvldumd", 7)); // carregador do driver: é ele que sobe o NvPresent64
}

static VOID CALLBACK OnDll(ULONG reason, const LDR_NOTIF_DATA *data, PVOID ctx)
{
    (void)ctx;
    if (reason != 1 || !data || !data->BaseDllName) return; // 1 = carregado
    const UNICODE_STR *n = data->BaseDllName;
    int len = n->Length / 2;
    if (IsNvUmd(n->Buffer, len)) {
        int p = PatchIat((BYTE *)data->DllBase);
        Log("%.*ls carregado em %p: %d entradas LoadLibrary* trocadas", len, n->Buffer, data->DllBase, p);
    } else if (len >= 9 && !_wcsnicmp(n->Buffer, L"nvpresent", 9)) {
        Log("AVISO: %.*ls carregou mesmo assim (por outro caminho)", len, n->Buffer);
    }
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason != DLL_PROCESS_ATTACH) return TRUE;
    DisableThreadLibraryCalls(inst);

    GetModuleFileNameA(NULL, g_log, MAX_PATH);
    char *slash = strrchr(g_log, '\\');
    if (slash) strcpy_s(slash + 1, MAX_PATH - (slash + 1 - g_log), "NoNvPresent.log");
    DeleteFileA(g_log);
    Log("NoNvPresent carregado no pid %lu", GetCurrentProcessId());

    LdrRegisterDllNotification_t reg = (LdrRegisterDllNotification_t)GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "LdrRegisterDllNotification");
    LONG st = reg ? reg(0, OnDll, NULL, &g_cookie) : -1;
    Log("aviso de carga de DLL registrado: status 0x%08lX", (unsigned long)st);

    // Se o driver já estiver carregado, troca agora.
    static const WCHAR *umds[] = { L"nvldumdx.dll", L"nvwgf2umx.dll", L"nvd3dumx.dll" };
    for (int i = 0; i < 3; i++) {
        HMODULE m = GetModuleHandleW(umds[i]);
        if (m) Log("%ls já estava carregado: %d entradas trocadas", umds[i], PatchIat((BYTE *)m));
    }
    if (GetModuleHandleW(L"NvPresent64.dll")) Log("AVISO: NvPresent64.dll já estava carregado antes deste plugin");
    return TRUE;
}
