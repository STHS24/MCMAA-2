## AI analysis

1. Streaming analysis fail:

Released session 2c8eac79 for model phi3:3.8b-mini-4k-instruct-q4_0
2025-10-21 10:00:18 fail: MCMAA.AI.OllamaAiAssistant[0]
      Error during streaming AI analysis
      System.Net.Http.HttpRequestException: An error occurred while sending the request.
       ---> System.Net.Http.HttpIOException: The response ended prematurely. (ResponseEnded)
         at System.Net.Http.HttpConnection.SendAsync(HttpRequestMessage request, Boolean async, CancellationToken cancellationToken)
         --- End of inner exception stack trace ---
         at System.Net.Http.HttpConnection.SendAsync(HttpRequestMessage request, Boolean async, CancellationToken cancellationToken)
         at System.Net.Http.HttpConnectionPool.SendWithVersionDetectionAndRetryAsync(HttpRequestMessage request, Boolean async, Boolean doRequestAuth, CancellationToken cancellationToken)
         at System.Net.Http.RedirectHandler.SendAsync(HttpRequestMessage request, Boolean async, CancellationToken cancellationToken)
         at Microsoft.Extensions.Http.Logging.LoggingHttpMessageHandler.<SendCoreAsync>g__Core|4_0(HttpRequestMessage request, Boolean useAsync, CancellationToken cancellationToken)
         at Microsoft.Extensions.Http.Logging.LoggingScopeHttpMessageHandler.<SendCoreAsync>g__Core|4_0(HttpRequestMessage request, Boolean useAsync, CancellationToken cancellationToken)
         at System.Net.Http.HttpClient.<SendAsync>g__Core|83_0(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationTokenSource cts, Boolean disposeCts, CancellationTokenSource pendingRequestsCts, CancellationToken originalCancellationToken)
         at MCMAA.AI.OllamaAiAssistant.AnalyzeStreamingAsync(ScanResult scanResult, AnalysisTask task, Action`1 onChunk, String modelOverride, CancellationToken cancellationToken) in /home/overtw/MCMAA-2/src/MCMAA.AI/Class1.cs:line 607
         at MCMAA.AI.OllamaAiAssistant.AnalyzeStreamingAsync(ScanResult scanResult, AnalysisTask task, Action`1 onChunk, String modelOverride, CancellationToken cancellationToken) in /home/overtw/MCMAA-2/src/MCMAA.AI/Class1.cs:line 663

❌ AI analysis failed:
   • Streaming analysis failed: An error occurred while sending the request.

## Fixes

[x] 1. Streaming analysis

## Warnings

1. src/MCMAA.AI/Class1.cs

MSBuild version 17.8.43+f0cbb1397 for .NET
  Determining projects to restore...
  All projects are up-to-date for restore.
  MCMAA.Core -> /home/overtw/MCMAA-2/src/MCMAA.Core/bin/Debug/net8.0/MCMAA.Core.dll
  MCMAA.Scanner -> /home/overtw/MCMAA-2/src/MCMAA.Scanner/bin/Debug/net8.0/MCMAA.Scanner.dll
/home/overtw/MCMAA-2/src/MCMAA.AI/Class1.cs(285,46): warning CS8604: Possible null reference argument for parameter 'key' in 'bool Dictionary<string, string>.ContainsKey(string key)'. [/home/overtw/MCMAA-2/src/MCMAA.AI/MCMAA.AI.csproj]
/home/overtw/MCMAA-2/src/MCMAA.AI/Class1.cs(309,38): warning CS0168: The variable 'ex' is declared but never used [/home/overtw/MCMAA-2/src/MCMAA.AI/MCMAA.AI.csproj]
  MCMAA.AI -> /home/overtw/MCMAA-2/src/MCMAA.AI/bin/Debug/net8.0/MCMAA.AI.dll
  MCMAA.Tests -> /home/overtw/MCMAA-2/tests/MCMAA.Tests/bin/Debug/net8.0/MCMAA.Tests.dll
  MCMAA.CLI -> /home/overtw/MCMAA-2/src/MCMAA.CLI/bin/Debug/net8.0/MCMAA.CLI.dll
  MCMAA.Tests -> /home/overtw/MCMAA-2/src/MCMAA.Tests/bin/Debug/net8.0/MCMAA.Tests.dll

Build succeeded.

/home/overtw/MCMAA-2/src/MCMAA.AI/Class1.cs(285,46): warning CS8604: Possible null reference argument for parameter 'key' in 'bool Dictionary<string, string>.ContainsKey(string key)'. [/home/overtw/MCMAA-2/src/MCMAA.AI/MCMAA.AI.csproj]
/home/overtw/MCMAA-2/src/MCMAA.AI/Class1.cs(309,38): warning CS0168: The variable 'ex' is declared but never used [/home/overtw/MCMAA-2/src/MCMAA.AI/MCMAA.AI.csproj]
    2 Warning(s)
    0 Error(s)

Time Elapsed 00:00:10.98

2. src/MCMAA.AI/Class1.cs

dotnet build
MSBuild version 17.8.43+f0cbb1397 for .NET
  Determining projects to restore...
  All projects are up-to-date for restore.
  MCMAA.Core -> /home/overtw/MCMAA-2/src/MCMAA.Core/bin/Debug/net8.0/MCMAA.Core.dll
  MCMAA.Scanner -> /home/overtw/MCMAA-2/src/MCMAA.Scanner/bin/Debug/net8.0/MCMAA.Scanner.dll
/home/overtw/MCMAA-2/src/MCMAA.AI/Class1.cs(309,34): warning CS0168: The variable '_' is declared but never used [/home/overtw/MCMAA-2/src/MCMAA.AI/MCMAA.AI.csproj]
  MCMAA.AI -> /home/overtw/MCMAA-2/src/MCMAA.AI/bin/Debug/net8.0/MCMAA.AI.dll
  MCMAA.Tests -> /home/overtw/MCMAA-2/src/MCMAA.Tests/bin/Debug/net8.0/MCMAA.Tests.dll
  MCMAA.Tests -> /home/overtw/MCMAA-2/tests/MCMAA.Tests/bin/Debug/net8.0/MCMAA.Tests.dll
  MCMAA.CLI -> /home/overtw/MCMAA-2/src/MCMAA.CLI/bin/Debug/net8.0/MCMAA.CLI.dll

Build succeeded.

/home/overtw/MCMAA-2/src/MCMAA.AI/Class1.cs(309,34): warning CS0168: The variable '_' is declared but never used [/home/overtw/MCMAA-2/src/MCMAA.AI/MCMAA.AI.csproj]
    1 Warning(s)
    0 Error(s)

Time Elapsed 00:00:09.04

## Fixes

[x] 1. src/MCMAA.AI/Class1.cs
[x] 2. src/MCMAA.AI/Class1.cs