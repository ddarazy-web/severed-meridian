$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$loggerPath = Join-Path $projectRoot 'Assets/Scripts/Systems/Diagnostics/Runtime/GameLog.cs'
if (-not (Test-Path -LiteralPath $loggerPath)) { throw '조건부 공통 로그 함수가 아직 없습니다.' }
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '이 검사에는 Windows .NET Framework C# 컴파일러가 필요합니다. 자동 설치하지 않습니다.' }
$fixtureRoot = Join-Path $projectRoot ('Logs/DebugLogging/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$fixturePath = Join-Path $fixtureRoot 'LoggingProbe.cs'
@'
namespace UnityEngine
{
    public class Object { }
    public static class Debug
    {
        public static int Calls;
        public static void Log(object message, Object context) { Calls++; }
    }
}
public static class LoggingProbe
{
    private static int arguments;
    private static object Message() { arguments++; return "probe"; }
    public static int Main(string[] args)
    {
        GameDiagnostics.GameLog.Debug(Message());
        GameDiagnostics.GameLog.Debug(Message(), new UnityEngine.Object());
        int expected = int.Parse(args[0]);
        System.Console.WriteLine("calls=" + UnityEngine.Debug.Calls + ", arguments=" + arguments);
        return UnityEngine.Debug.Calls == expected && arguments == expected ? 0 : 1;
    }
}
'@ | Set-Content -LiteralPath $fixturePath -Encoding utf8
foreach ($configuration in @(@{ name = 'release'; symbol = ''; expected = 0 }, @{ name = 'development'; symbol = 'DEVELOPMENT_BUILD'; expected = 2 }, @{ name = 'editor'; symbol = 'UNITY_EDITOR'; expected = 2 }, @{ name = 'debug-only'; symbol = 'DEBUG'; expected = 0 })) {
    $outputPath = Join-Path $fixtureRoot ($configuration.name + '.exe')
    $compilerArguments = @('/nologo', '/warnaserror', ('/out:' + $outputPath), $loggerPath, $fixturePath)
    if ($configuration.symbol) { $compilerArguments += '/define:' + $configuration.symbol }
    & $compilerPath @compilerArguments
    if ($LASTEXITCODE -ne 0) { throw "조건부 로그 검사 컴파일 실패: $($configuration.name)" }
    & $outputPath ([string]$configuration.expected)
    if ($LASTEXITCODE -ne 0) { throw "조건부 로그 호출/인수 제거 실패: $($configuration.name)" }
    Write-Output "PASS $($configuration.name)"
}
Write-Output "PASS DebugLogVerification: $fixtureRoot"
