# OptiScaler: DLSS-G no RDR2 com driver NVIDIA 621.01

Patch sobre o OptiScaler master `e7e3872` (`rdr2-dlssg-nvpresent.diff`). Testado em 30/10/2026 com DX12, 4K DSR em tela cheia, DLSS ligado e `FGOutput = DLSSG`.

## Problemas e correções

1. **NvPresent64.dll (driver 621.01) fecha o jogo com 0xC0000409.** A camada de Present do driver encapsula de novo o swapchain do Streamline e chama um objeto já destruído, que cai em `std::terminate`. Correção: chave DRS `0xB0CC0875 = 0` no perfil do `rdr2.exe` (máscara de API do NvPresent; bit0 D3D12, bit1 D3D11, bit2 Vulkan). Script em `../NvidiaDrs/drs_np_api.py` (rodar como administrador). O NVIDIA App pode regravar o perfil: conferir com `drs_np.py`.
2. **Streamline iniciado no device D3D12 errado.** O RDR2 cria vários devices de teste antes do principal. Quirk `LazySLForD3D12`: o SL só sobe quando o swapchain DLSSG Dx12 é criado.
3. **"DirectX 12 não instalado" com o DLSS ligado.** O swapchain real criado pelo Streamline em tela cheia volta com `DXGI_STATUS_OCCLUDED` (0x087A0001), que é sucesso. Os hooks de `DxgiFactory_Hooks.cpp` só aceitavam `S_OK`, deixavam o swapchain sem wrapper e o jogo nunca chegava ao primeiro Present. Correção: `SUCCEEDED(result)` nos seis pontos de criação de swapchain.

## Observações

- `FGInput = Upscaler` exige o DLSS do jogo ligado; sem ele o frame gen não tem entrada.
- Em Vulkan com DLSS-G o jogo mostra o erro 16 do Social Club; usar DX12.
- `../BreakTrace` é um ASI de diagnóstico: grava pilha e código decifrado em volta de int3/fail-fast em `BreakTrace_N.bin`. Carregar pela raiz do jogo (`dinput8.dll`), não por `OptiScaler/plugins`.

## Build

```bat
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
msbuild /m /p:Configuration=Release /p:Platform=x64 OptiScaler.sln
```

Saída em `x64\Release\a\OptiScaler.dll`, instalada como `dxgi.dll` na pasta do jogo.
