using System.Net;
using System.Text;
using System.Xml.Linq;

namespace WebDavLite.Services
{
    internal class WebDaveServerService
    {
        private readonly HttpListener _listener;
        private readonly string _rootPath;
        private readonly CancellationTokenSource _cts;
        private readonly AuthenticationService _authManager;
        private readonly bool _requireAuth;

        public WebDaveServerService(string prefix, string rootPath, bool requireAuth, AuthenticationService authManager)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _rootPath = Path.GetFullPath(rootPath);
            _cts = new CancellationTokenSource();
            _authManager = authManager;
            _requireAuth = requireAuth;

            // Create root directory if it doesn't exist
            Directory.CreateDirectory(_rootPath);
        }

        public AuthenticationService AuthManager => _authManager;

        public async Task StartAsync()
        {
            _listener.Start();
            Console.WriteLine($"WebDAV server started on {string.Join(", ", _listener.Prefixes)}");
            Console.WriteLine($"Root directory: {_rootPath}");
            Console.WriteLine("Press Ctrl+C to stop...");

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException)
                {
                    break;
                }
            }
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
            _listener.Close();
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                // Check authentication
                Console.WriteLine($"[DEBUG] _requireAuth = {_requireAuth}");

                if (_requireAuth && !_authManager.ValidateCredentials(request.Headers["Authorization"]))
                {
                    Console.WriteLine($"[DEBUG] Authentication FAILED - returning 401");
                    response.StatusCode = 401; // Unauthorized
                    response.AddHeader("WWW-Authenticate", "Basic realm=\"WebDAV Server\"");
                    await response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Authentication required"));
                    return;
                }

                if (_requireAuth)
                {
                    Console.WriteLine($"[DEBUG] Authentication PASSED");
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Authentication DISABLED - allowing request");
                }

                Console.WriteLine($"{request.HttpMethod} {request.Url?.AbsolutePath}");

                // Get the file system path
                var requestPath = Uri.UnescapeDataString(request.Url?.AbsolutePath ?? "/");
                var filePath = GetSafePath(requestPath);

                switch (request.HttpMethod)
                {
                    case "OPTIONS":
                        await HandleOptionsAsync(context);
                        break;
                    case "GET":
                    case "HEAD":
                        await HandleGetAsync(context, filePath);
                        break;
                    case "PUT":
                        await HandlePutAsync(context, filePath);
                        break;
                    case "DELETE":
                        await HandleDeleteAsync(context, filePath);
                        break;
                    case "MKCOL":
                        await HandleMkcolAsync(context, filePath);
                        break;
                    case "PROPFIND":
                        await HandlePropfindAsync(context, filePath);
                        break;
                    case "PROPPATCH":
                        await HandleProppatchAsync(context);
                        break;
                    case "COPY":
                    case "MOVE":
                        await HandleCopyMoveAsync(context, filePath);
                        break;
                    default:
                        response.StatusCode = 501; // Not Implemented
                        break;
                }
            }
            catch (UnauthorizedAccessException)
            {
                response.StatusCode = 403; // Forbidden
            }
            catch (FileNotFoundException)
            {
                response.StatusCode = 404; // Not Found
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                response.StatusCode = 500; // Internal Server Error
            }
            finally
            {
                response.Close();
            }
        }

        private async Task HandleOptionsAsync(HttpListenerContext context)
        {
            var response = context.Response;
            response.AddHeader("DAV", "1, 2");
            response.AddHeader("Allow", "OPTIONS, GET, HEAD, PUT, DELETE, MKCOL, PROPFIND, PROPPATCH, COPY, MOVE");
            response.StatusCode = 200;
            await Task.CompletedTask;
        }

        private async Task HandleGetAsync(HttpListenerContext context, string filePath)
        {
            var response = context.Response;

            if (Directory.Exists(filePath))
            {
                // Return HTML directory listing for browsers
                var html = GenerateDirectoryListing(filePath, context.Request.Url?.AbsolutePath ?? "/");
                var buffer = Encoding.UTF8.GetBytes(html);
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer);
            }
            else if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                response.ContentType = GetContentType(filePath);
                response.ContentLength64 = fileInfo.Length;
                response.AddHeader("Last-Modified", fileInfo.LastWriteTimeUtc.ToString("R"));

                if (context.Request.HttpMethod != "HEAD")
                {
                    using var fileStream = File.OpenRead(filePath);
                    await fileStream.CopyToAsync(response.OutputStream);
                }
                response.StatusCode = 200;
            }
            else
            {
                response.StatusCode = 404;
            }
        }

        private async Task HandlePutAsync(HttpListenerContext context, string filePath)
        {
            var response = context.Response;

            // Create parent directory if it doesn't exist
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            bool isNew = !File.Exists(filePath);

            using (var fileStream = File.Create(filePath))
            {
                await context.Request.InputStream.CopyToAsync(fileStream);
            }

            response.StatusCode = isNew ? 201 : 204; // Created or No Content
        }

        private async Task HandleDeleteAsync(HttpListenerContext context, string filePath)
        {
            var response = context.Response;

            if (Directory.Exists(filePath))
            {
                Directory.Delete(filePath, recursive: true);
                response.StatusCode = 204; // No Content
            }
            else if (File.Exists(filePath))
            {
                File.Delete(filePath);
                response.StatusCode = 204; // No Content
            }
            else
            {
                response.StatusCode = 404; // Not Found
            }

            await Task.CompletedTask;
        }

        private async Task HandleMkcolAsync(HttpListenerContext context, string filePath)
        {
            var response = context.Response;

            if (Directory.Exists(filePath) || File.Exists(filePath))
            {
                response.StatusCode = 405; // Method Not Allowed (already exists)
            }
            else
            {
                Directory.CreateDirectory(filePath);
                response.StatusCode = 201; // Created
            }

            await Task.CompletedTask;
        }

        private async Task HandlePropfindAsync(HttpListenerContext context, string filePath)
        {
            var response = context.Response;
            var depth = context.Request.Headers["Depth"] ?? "0";

            if (!Directory.Exists(filePath) && !File.Exists(filePath))
            {
                response.StatusCode = 404;
                return;
            }

            var xml = GeneratePropfindResponse(filePath, depth, context.Request.Url?.AbsolutePath ?? "/");
            var buffer = Encoding.UTF8.GetBytes(xml);

            response.StatusCode = 207; // Multi-Status
            response.ContentType = "application/xml; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
        }

        private async Task HandleProppatchAsync(HttpListenerContext context)
        {
            // Simplified PROPPATCH - just acknowledge the request
            var response = context.Response;
            response.StatusCode = 207; // Multi-Status

            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<D:multistatus xmlns:D=""DAV:"">
  <D:response>
    <D:href>" + context.Request.Url?.AbsolutePath + @"</D:href>
    <D:propstat>
      <D:status>HTTP/1.1 200 OK</D:status>
    </D:propstat>
  </D:response>
</D:multistatus>";

            var buffer = Encoding.UTF8.GetBytes(xml);
            response.ContentType = "application/xml; charset=utf-8";
            await response.OutputStream.WriteAsync(buffer);
        }

        private async Task HandleCopyMoveAsync(HttpListenerContext context, string sourcePath)
        {
            var response = context.Response;
            var destination = context.Request.Headers["Destination"];

            if (string.IsNullOrEmpty(destination))
            {
                response.StatusCode = 400; // Bad Request
                return;
            }

            // Parse destination URL to get path
            var destUri = new Uri(destination);
            var destPath = GetSafePath(Uri.UnescapeDataString(destUri.AbsolutePath));

            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                response.StatusCode = 404;
                return;
            }

            // Create parent directory if needed
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            bool isMove = context.Request.HttpMethod == "MOVE";

            if (File.Exists(sourcePath))
            {
                if (isMove)
                {
                    File.Move(sourcePath, destPath, overwrite: true);
                }
                else
                {
                    File.Copy(sourcePath, destPath, overwrite: true);
                }
            }
            else if (Directory.Exists(sourcePath))
            {
                CopyDirectory(sourcePath, destPath);
                if (isMove)
                {
                    Directory.Delete(sourcePath, recursive: true);
                }
            }

            response.StatusCode = 201; // Created
            await Task.CompletedTask;
        }

        private string GetSafePath(string requestPath)
        {
            // Remove leading slash and normalize path
            requestPath = requestPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_rootPath, requestPath);

            // Ensure the path is within the root directory (security check)
            var normalizedPath = Path.GetFullPath(fullPath);
            if (!normalizedPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Access denied");
            }

            return normalizedPath;
        }

        private string GeneratePropfindResponse(string filePath, string depth, string requestPath)
        {
            var ns = XNamespace.Get("DAV:");
            var root = new XElement(ns + "multistatus");

            // Add the requested resource
            root.Add(CreateResourceElement(filePath, requestPath, ns));

            // If depth is not 0, add children
            if (depth != "0" && Directory.Exists(filePath))
            {
                foreach (var entry in Directory.GetFileSystemEntries(filePath))
                {
                    var entryPath = requestPath.TrimEnd('/') + "/" + Path.GetFileName(entry);
                    root.Add(CreateResourceElement(entry, entryPath, ns));
                }
            }

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                root
            );

            return doc.ToString();
        }

        private XElement CreateResourceElement(string filePath, string href, XNamespace ns)
        {
            var isDirectory = Directory.Exists(filePath);
            var fileInfo = isDirectory ? null : new FileInfo(filePath);
            var dirInfo = isDirectory ? new DirectoryInfo(filePath) : null;

            var response = new XElement(ns + "response",
                new XElement(ns + "href", href.TrimEnd('/') + (isDirectory ? "/" : "")),
                new XElement(ns + "propstat",
                    new XElement(ns + "prop",
                        new XElement(ns + "displayname", Path.GetFileName(filePath)),
                        new XElement(ns + "getlastmodified",
                            (fileInfo?.LastWriteTimeUtc ?? dirInfo!.LastWriteTimeUtc).ToString("R")),
                        isDirectory
                            ? new XElement(ns + "resourcetype", new XElement(ns + "collection"))
                            : new XElement(ns + "resourcetype"),
                        fileInfo != null
                            ? new XElement(ns + "getcontentlength", fileInfo.Length)
                            : null,
                        fileInfo != null
                            ? new XElement(ns + "getcontenttype", GetContentType(filePath))
                            : null
                    ),
                    new XElement(ns + "status", "HTTP/1.1 200 OK")
                )
            );

            return response;
        }

        private string GenerateDirectoryListing(string dirPath, string requestPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset='utf-8'><title>Directory Listing</title></head><body>");
            sb.AppendLine($"<h1>Directory: {requestPath}</h1>");
            sb.AppendLine("<ul>");

            if (requestPath != "/")
            {
                sb.AppendLine("<li><a href='../'>Parent Directory</a></li>");
            }

            foreach (var dir in Directory.GetDirectories(dirPath))
            {
                var name = Path.GetFileName(dir);
                sb.AppendLine($"<li><a href='{Uri.EscapeDataString(name)}/'>{name}/</a></li>");
            }

            foreach (var file in Directory.GetFiles(dirPath))
            {
                var name = Path.GetFileName(file);
                var size = new FileInfo(file).Length;
                sb.AppendLine($"<li><a href='{Uri.EscapeDataString(name)}'>{name}</a> ({FormatBytes(size)})</li>");
            }

            sb.AppendLine("</ul></body></html>");
            return sb.ToString();
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        private string GetContentType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".txt" => "text/plain",
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
