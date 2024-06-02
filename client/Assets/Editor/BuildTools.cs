using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using Debug = UnityEngine.Debug;

public static class BuildTools
{
    [MenuItem("File/Build Protos", priority = 220)]
    static void BuildProtos()
    {
        var grpcToolsPath = @"Packages\Grpc.Tools.2.64.0\tools\windows_x64";
        var outputPath = @"Assets\Scripts\Generated\Protos";
        Directory.CreateDirectory(outputPath);
        var command = $@"{grpcToolsPath}\protoc";
        var args = new[]
        {
            "--csharp_out",
            outputPath,
            "--grpc_out",
            outputPath,
            $"--plugin=protoc-gen-grpc=\"{grpcToolsPath}\\grpc_csharp_plugin.exe\"",
            @"--proto_path=..\server\GameCore\Protos",
            "protocols.proto"
        };
        var exitCode = RunCommandAsync(command, args);
        if (exitCode == 0)
            AssetDatabase.Refresh();
    }

    static int RunCommandAsync(string command, params string[] args)
    {
        Debug.LogFormat("RunCommandAsync: \"{0}\" {1}", command, args);
        var info = new ProcessStartInfo(command, string.Join(" ", args))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            StandardOutputEncoding = Encoding.UTF8,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        using var process = Process.Start(info)!;
        while (!process.WaitForExit(33))
            Thread.Sleep(1);

        if (process.StandardOutput.ReadToEnd() is { Length: > 0 } output)
            Debug.Log(output);

        if (process.StandardError.ReadToEnd() is { Length: > 0 } error)
            Debug.LogError(error);

        return process.ExitCode;
    }
}
