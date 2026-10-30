// Diagnostico: quando o RDR2 para num breakpoint ou excecao fatal, grava a pilha
// e os bytes de codigo (ja decifrados) em volta de cada retorno, em <jogo>\BreakTrace_N.bin.
// Nao altera o comportamento: sempre devolve EXCEPTION_CONTINUE_SEARCH.
#include <windows.h>
#include <stdio.h>

static HMODULE g_game;
static SIZE_T g_gameSize;
static volatile LONG g_done;
static char g_path[MAX_PATH];

static void put(FILE* f, const char* tag, ULONG64 a, const void* p, DWORD n)
{
    fwrite(tag, 1, 4, f);
    fwrite(&a, 8, 1, f);
    fwrite(&n, 4, 1, f);
    if (n) fwrite(p, 1, n, f);
}

static void dumpAround(FILE* f, ULONG64 addr)
{
    MEMORY_BASIC_INFORMATION mbi;
    ULONG64 s = addr - 0x300;
    if (!VirtualQuery((LPCVOID) s, &mbi, sizeof(mbi)) || mbi.State != MEM_COMMIT) return;
    put(f, "CODE", s, (const void*) s, 0x340);
}

static LONG CALLBACK Veh(PEXCEPTION_POINTERS ep)
{
    DWORD code = ep->ExceptionRecord->ExceptionCode;
    if (code != 0x80000003 && code != 0xC0000409) return EXCEPTION_CONTINUE_SEARCH;
    ULONG64 ip = (ULONG64) ep->ExceptionRecord->ExceptionAddress;
    LONG n = InterlockedIncrement(&g_done);
    if (n > 8) return EXCEPTION_CONTINUE_SEARCH;

    char path[MAX_PATH];
    sprintf_s(path, MAX_PATH, "%s_%ld.bin", g_path, n);
    FILE* f = fopen(path, "wb");
    if (!f) return EXCEPTION_CONTINUE_SEARCH;
    ULONG64 base = (ULONG64) g_game;
    put(f, "BASE", base, &code, 4);

    CONTEXT ctx = *ep->ContextRecord;
    put(f, "CTX ", ip, &ctx, sizeof(ctx));
    for (int i = 0; i < 48 && ctx.Rip; i++)
    {
        HMODULE hm = 0;
        char name[MAX_PATH] = "";
        if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, (LPCSTR) ctx.Rip, &hm))
            GetModuleFileNameA(hm, name, MAX_PATH);
        put(f, "FRAM", ctx.Rip, name, (DWORD) strlen(name));
        put(f, "MODB", (ULONG64) hm, 0, 0);
        dumpAround(f, ctx.Rip);

        ULONG64 imageBase = 0;
        PRUNTIME_FUNCTION rf = RtlLookupFunctionEntry(ctx.Rip, &imageBase, NULL);
        if (!rf)
        {
            ctx.Rip = *(ULONG64*) ctx.Rsp;
            ctx.Rsp += 8;
            continue;
        }
        PVOID handlerData = 0;
        ULONG64 establisher = 0;
        RtlVirtualUnwind(UNW_FLAG_NHANDLER, imageBase, ctx.Rip, rf, &ctx, &handlerData, &establisher, NULL);
    }
    fclose(f);
    return EXCEPTION_CONTINUE_SEARCH;
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(inst);
        g_game = GetModuleHandleA(NULL);
        IMAGE_NT_HEADERS* nt = (IMAGE_NT_HEADERS*) ((BYTE*) g_game + ((IMAGE_DOS_HEADER*) g_game)->e_lfanew);
        g_gameSize = nt->OptionalHeader.SizeOfImage;
        GetModuleFileNameA(NULL, g_path, MAX_PATH);
        char* slash = strrchr(g_path, 0x5C);
        if (slash) strcpy_s(slash + 1, MAX_PATH - (slash + 1 - g_path), "BreakTrace");
        AddVectoredExceptionHandler(1, Veh);
    }
    return TRUE;
}
