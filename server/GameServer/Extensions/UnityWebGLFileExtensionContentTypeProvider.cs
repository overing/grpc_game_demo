using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace GameServer;

public static class ApplicationBuilderUnityWebGLGameClientStaticFileExtensions
{
    public const string GuessWebGLClientPath = "{GUESS_WEBGL_CLIENT_PATH}";

    public static IApplicationBuilder UseUnityWebGLClientStaticFile(this IApplicationBuilder appBuilder, string folder = GuessWebGLClientPath, string requestPath = "/game")
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentNullException(nameof(folder));
        if (string.IsNullOrWhiteSpace(requestPath))
            throw new ArgumentNullException(nameof(requestPath));
        if (folder == "{GUESS_WEBGL_CLIENT_PATH}")
        {
            folder = GuessWebGLClientPath();
            if (!Directory.Exists(folder))
                return appBuilder;
        }
        else if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException(folder);

        var fileProvider = new PhysicalFileProvider(folder);
        var mimeProvider = new UnityWebGLFileExtensionContentTypeProvider();
        var defaultFileOptions = new DefaultFilesOptions(new()
        {
            FileProvider = fileProvider,
            RequestPath = requestPath,
        })
        {
            DefaultFileNames = ["index.html"],
        };
        var staticFileOptions = new StaticFileOptions
        {
            FileProvider = fileProvider,
            RequestPath = requestPath,
            ContentTypeProvider = mimeProvider,
            OnPrepareResponse = context =>
            {
                var (file, headers) = (context.File, context.Context.Response.Headers);
                if (file.Name.EndsWith(".br", StringComparison.OrdinalIgnoreCase) && file.Exists)
                    headers["Content-Encoding"] = "br";
                if (mimeProvider.TryGetContentType(file.Name, out var contentType))
                    headers["Content-Type"] = contentType;
            },
        };
        return appBuilder
            .UseDefaultFiles(defaultFileOptions)
            .UseStaticFiles(staticFileOptions);

        static string GuessWebGLClientPath()
        {
            const string pattern = @"client\build\webgl";
            string? folder = Environment.CurrentDirectory;
            do
            {
                var path = Path.Combine(folder, pattern);
                if (Directory.Exists(path))
                    return path;
                folder = Path.GetDirectoryName(folder);
            }
            while (!string.IsNullOrWhiteSpace(folder));
            return pattern;
        }
    }
}

/// <summary>
/// 因為 FileExtensionContentTypeProvider 只會抓最後一個小數點的副檔名來判斷 mime
/// <para/>所以需要這個輔助類別來處理 Unity WebGL 的 `.wasm.br` 這種兩個小數點的副檔名
/// <see cref="https://github.com/dotnet/aspnetcore/blob/v6.0.9/src/Middleware/StaticFiles/src/FileExtensionContentTypeProvider.cs#L439" />
/// </summary>
public sealed class UnityWebGLFileExtensionContentTypeProvider : IContentTypeProvider
{
    FileExtensionContentTypeProvider _default;
    Dictionary<string, string> _extras;

    public UnityWebGLFileExtensionContentTypeProvider()
    {
        _extras = new();
        _extras[".unityweb"] = "application/unityweb";
        // Datafile mappings
        _extras[".data"] = "application/octet-stream";
        _extras[".data.gz"] = "application/octet-stream";
        _extras[".data.br"] = "application/octet-stream";
        // js filemappings
        _extras[".js.gz"] = "application/javascript";
        _extras[".js.br"] = "application/javascript";
        //wasm file mappings
        _extras[".wasm"] = "application/wasm";
        _extras[".wasm.gz"] = "application/wasm";
        _extras[".wasm.br"] = "application/wasm";
        //aas bundle file
        _extras[".bundle"] = "application/octet-stream";

        _default = new();
        foreach (var extra in _extras)
            _default.Mappings[extra.Key] = extra.Value;
    }

    public bool TryGetContentType(string subpath, [MaybeNullWhen(false)] out string contentType)
    {
        if (_default.TryGetContentType(subpath, out contentType))
            return true;

        foreach (var extra in _extras)
        {
            if (subpath.EndsWith(extra.Key, StringComparison.OrdinalIgnoreCase))
            {
                contentType = extra.Value;
                return true;
            }
        }
        return false;
    }
}
