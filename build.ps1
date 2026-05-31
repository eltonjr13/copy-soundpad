# Script de Compilação do SoundDeck

$CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

# Criar pasta bin se não existir
if (!(Test-Path bin)) {
    New-Item -ItemType Directory -Path bin | Out-Null
}

Write-Host "Iniciando compilação do SoundDeck..."

# Obter todos os arquivos fontes do projeto
$Sources = Get-ChildItem -Path src -Filter *.cs -Recurse | ForEach-Object { $_.FullName }

# Definir as referências necessárias
$References = @(
    "System.dll",
    "System.Core.dll",
    "System.Xaml.dll",
    "System.Drawing.dll",
    "System.Windows.Forms.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll",
    "packages\Newtonsoft.Json.13.0.4\lib\net45\Newtonsoft.Json.dll",
    "packages\NAudio.2.3.0\lib\net472\NAudio.dll",
    "packages\NAudio.Core.2.3.0\lib\netstandard2.0\NAudio.Core.dll",
    "packages\NAudio.Asio.2.3.0\lib\netstandard2.0\NAudio.Asio.dll",
    "packages\NAudio.Midi.2.3.0\lib\netstandard2.0\NAudio.Midi.dll",
    "packages\NAudio.Wasapi.2.3.0\lib\netstandard2.0\NAudio.Wasapi.dll",
    "packages\NAudio.WinMM.2.3.0\lib\netstandard2.0\NAudio.WinMM.dll",
    "packages\NAudio.WinForms.2.3.0\lib\net472\NAudio.WinForms.dll",
    "packages\System.Buffers.4.5.1\lib\net461\System.Buffers.dll",
    "packages\System.Memory.4.5.5\lib\net461\System.Memory.dll",
    "packages\System.Numerics.Vectors.4.5.0\lib\net46\System.Numerics.Vectors.dll",
    "packages\System.Runtime.CompilerServices.Unsafe.4.5.3\lib\net461\System.Runtime.CompilerServices.Unsafe.dll",
    "packages\System.Resources.Extensions.8.0.0\lib\net462\System.Resources.Extensions.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\netstandard.dll"
)

# Montar a lista de argumentos como um array do PowerShell
$CompilerArgs = @("/target:winexe", "/out:bin\SoundDeck.exe")

foreach ($ref in $References) {
    $CompilerArgs += "/r:$ref"
}

foreach ($src in $Sources) {
    $CompilerArgs += $src
}

# Executar a compilação via csc.exe passando o array de argumentos
& $CscPath $CompilerArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "Compilação concluída com sucesso! Copiando dependências de bibliotecas externas..."

    # Copiar DLLs do Newtonsoft.Json e NAudio para a pasta bin
    Copy-Item "packages\Newtonsoft.Json.13.0.4\lib\net45\Newtonsoft.Json.dll" -Destination bin\ -Force
    Copy-Item "packages\NAudio.2.3.0\lib\net472\NAudio.dll" -Destination bin\ -Force
    Copy-Item "packages\NAudio.Core.2.3.0\lib\netstandard2.0\NAudio.Core.dll" -Destination bin\ -Force
    Copy-Item "packages\NAudio.Asio.2.3.0\lib\netstandard2.0\NAudio.Asio.dll" -Destination bin\ -Force
    Copy-Item "packages\NAudio.Midi.2.3.0\lib\netstandard2.0\NAudio.Midi.dll" -Destination bin\ -Force
    Copy-Item "packages\NAudio.Wasapi.2.3.0\lib\netstandard2.0\NAudio.Wasapi.dll" -Destination bin\ -Force
    Copy-Item "packages\NAudio.WinMM.2.3.0\lib\netstandard2.0\NAudio.WinMM.dll" -Destination bin\ -Force
    Copy-Item "packages\NAudio.WinForms.2.3.0\lib\net472\NAudio.WinForms.dll" -Destination bin\ -Force
    
    # Copiar dependências complementares do runtime .NET
    Copy-Item "packages\System.Buffers.4.5.1\lib\net461\System.Buffers.dll" -Destination bin\ -Force
    Copy-Item "packages\System.Memory.4.5.5\lib\net461\System.Memory.dll" -Destination bin\ -Force
    Copy-Item "packages\System.Numerics.Vectors.4.5.0\lib\net46\System.Numerics.Vectors.dll" -Destination bin\ -Force
    Copy-Item "packages\System.Runtime.CompilerServices.Unsafe.4.5.3\lib\net461\System.Runtime.CompilerServices.Unsafe.dll" -Destination bin\ -Force
    Copy-Item "packages\System.Resources.Extensions.8.0.0\lib\net462\System.Resources.Extensions.dll" -Destination bin\ -Force

    Write-Host "Processo finalizado com sucesso! O executável está pronto em 'bin\SoundDeck.exe'" -ForegroundColor Green
} else {
    Write-Error "Falha na compilação do SoundDeck. Verifique os erros acima."
}
