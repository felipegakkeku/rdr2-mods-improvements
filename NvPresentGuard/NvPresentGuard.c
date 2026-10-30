// NvPresentGuard.asi
// Mantém o NvPresent64.dll (driver NVIDIA 621.xx) desligado para o RDR2.
// O NvPresent embrulha de novo a swapchain do Streamline e derruba o DLSS-G do
// OptiScaler com fail-fast 0xc0000409. A chave DRS 0xB0CC0875 é a máscara de APIs
// dele (bit0 D3D12, bit1 D3D11, bit2 Vulkan); com 0 no perfil do rdr2.exe ele só repassa.
//
// O NVIDIA App e as atualizações de driver podem regravar o perfil. A cada abertura do
// jogo este ASI lê a chave e, se não estiver em 0, grava 0 e salva o perfil.
// Salvar exige administrador (o RDR2 roda com RUNASADMIN).
//
// Carregado cedo pelo OptiScaler (LoadAsiPlugins = true, pasta OptiScaler\plugins),
// antes de o jogo criar o device D3D12. Log em <jogo>\NvPresentGuard.log.

#include <windows.h>
#include <stdio.h>

#define NVP_MASK_ID 0xB0CC0875u
#define NVAPI_OK 0
#define NVAPI_SETTING_NOT_FOUND (-160)

typedef struct { UINT len; BYTE data[4096]; } DrsBinary;
typedef union { UINT u32; DrsBinary bin; WCHAR wsz[2048]; } DrsValue;
typedef struct {
    UINT version; WCHAR name[2048]; UINT id, type, location, isCurrentPredefined, isPredefinedValid;
    DrsValue predefined, current;
} DrsSetting;
typedef struct {
    UINT version, isPredefined; WCHAR appName[2048], friendlyName[2048], launcher[2048], fileInFolder[2048];
    UINT flags; WCHAR commandLine[2048];
} DrsApp;

typedef void* (*QueryInterface_t)(UINT);
typedef int (*Initialize_t)(void);
typedef int (*CreateSession_t)(void**);
typedef int (*Session_t)(void*);
typedef int (*FindApp_t)(void*, const WCHAR*, void**, DrsApp*);
typedef int (*GetSetting_t)(void*, void*, UINT, DrsSetting*);
typedef int (*SetSetting_t)(void*, void*, DrsSetting*);

static char g_log[MAX_PATH];
static DrsSetting g_setting;
static DrsApp g_app;

static void Log(const char* fmt, ...)
{
    FILE* f;
    if (fopen_s(&f, g_log, "a") != 0) return;
    SYSTEMTIME t; GetLocalTime(&t);
    fprintf(f, "[%02d:%02d:%02d.%03d] ", t.wHour, t.wMinute, t.wSecond, t.wMilliseconds);
    va_list a; va_start(a, fmt); vfprintf(f, fmt, a); va_end(a);
    fputc('\n', f);
    fclose(f);
}

static DWORD WINAPI Guard(LPVOID unused)
{
    (void) unused;
    HMODULE nvapi = LoadLibraryA("nvapi64.dll");
    if (!nvapi) { Log("nvapi64.dll ausente, nada a fazer"); return 0; }
    QueryInterface_t qi = (QueryInterface_t) GetProcAddress(nvapi, "nvapi_QueryInterface");
    if (!qi) { Log("nvapi_QueryInterface ausente"); return 0; }

    Initialize_t Initialize = (Initialize_t) qi(0x0150E828);
    CreateSession_t CreateSession = (CreateSession_t) qi(0x0694D52E);
    Session_t LoadSettings = (Session_t) qi(0x375DBD6B);
    Session_t SaveSettings = (Session_t) qi(0xFCBC7E14);
    Session_t DestroySession = (Session_t) qi(0xDAD9CFF8);
    FindApp_t FindApp = (FindApp_t) qi(0xEEE566B2);
    GetSetting_t GetSetting = (GetSetting_t) qi(0x73BF8338);
    SetSetting_t SetSetting = (SetSetting_t) qi(0x577DD202);
    if (!Initialize || !CreateSession || !LoadSettings || !SaveSettings || !DestroySession || !FindApp ||
        !GetSetting || !SetSetting)
    {
        Log("funcoes DRS ausentes no nvapi64.dll");
        return 0;
    }

    int r = Initialize();
    if (r != NVAPI_OK) { Log("NvAPI_Initialize falhou: %d", r); return 0; }
    void* session = NULL;
    if ((r = CreateSession(&session)) != NVAPI_OK) { Log("CreateSession falhou: %d", r); return 0; }
    if ((r = LoadSettings(session)) != NVAPI_OK) { Log("LoadSettings falhou: %d", r); goto done; }

    void* profile = NULL;
    g_app.version = sizeof(DrsApp) | (4 << 16);
    if ((r = FindApp(session, L"rdr2.exe", &profile, &g_app)) != NVAPI_OK)
    {
        Log("perfil do rdr2.exe nao encontrado: %d", r);
        goto done;
    }

    g_setting.version = sizeof(DrsSetting) | (1 << 16);
    r = GetSetting(session, profile, NVP_MASK_ID, &g_setting);
    if (r == NVAPI_OK && g_setting.current.u32 == 0)
    {
        Log("mascara do NvPresent ja esta em 0");
        goto done;
    }
    if (r == NVAPI_OK)
        Log("mascara do NvPresent estava em 0x%X, corrigindo", g_setting.current.u32);
    else if (r == NVAPI_SETTING_NOT_FOUND)
        Log("mascara do NvPresent ausente no perfil (padrao 0xFFFFFFFF), corrigindo");
    else
    {
        Log("GetSetting falhou: %d", r);
        goto done;
    }

    ZeroMemory(&g_setting, sizeof(g_setting));
    g_setting.version = sizeof(DrsSetting) | (1 << 16);
    g_setting.id = NVP_MASK_ID;
    g_setting.type = 0;
    g_setting.current.u32 = 0;
    if ((r = SetSetting(session, profile, &g_setting)) != NVAPI_OK) { Log("SetSetting falhou: %d", r); goto done; }
    if ((r = SaveSettings(session)) != NVAPI_OK)
    {
        Log("SaveSettings falhou: %d (o jogo precisa rodar como administrador)", r);
        goto done;
    }
    Log("mascara gravada em 0; NvPresent64 %s", GetModuleHandleA("NvPresent64.dll")
                                                    ? "ja estava carregado, vale a partir da proxima abertura"
                                                    : "ainda nao carregado, vale nesta abertura");

done:
    DestroySession(session);
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved)
{
    (void) reserved;
    if (reason != DLL_PROCESS_ATTACH) return TRUE;
    DisableThreadLibraryCalls(inst);

    // <jogo>\OptiScaler\plugins\NvPresentGuard.asi -> <jogo>\NvPresentGuard.log
    GetModuleFileNameA(GetModuleHandleA(NULL), g_log, MAX_PATH);
    char* slash = strrchr(g_log, 0x5C);
    if (slash) strcpy_s(slash + 1, MAX_PATH - (slash + 1 - g_log), "NvPresentGuard.log");
    DeleteFileA(g_log);

    // Sob o loader lock: o trabalho vai para uma thread, que roda assim que o lock é liberado.
    HANDLE t = CreateThread(NULL, 0, Guard, NULL, 0, NULL);
    if (t) CloseHandle(t);
    return TRUE;
}
