using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.PackageDownloader;

/// <summary>
/// 安装包下载器基类：实现下载到临时文件、校验哈希、规范化文件名并落盘的通用流程。
/// </summary>
/// <param name="httpClientFactory">HTTP 客户端工厂。</param>
public abstract class AbstractPackageDownloader(HttpClientFactory httpClientFactory) : IPackageDownloader
{
    /// <summary>
    /// 供派生类使用的 HTTP 客户端工厂。
    /// </summary>
    protected HttpClientFactory HttpClientFactory => httpClientFactory;

    /// <summary>
    /// 获取指定机型与系统下的可用安装包列表。
    /// </summary>
    /// <param name="machineType">机型的 Machine Type 标识。</param>
    /// <param name="os">目标操作系统。</param>
    /// <param name="progress">进度回调（0-1）。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>安装包列表。</returns>
    public abstract Task<List<Package>> GetPackagesAsync(string machineType, OS os, IProgress<float>? progress = null, CancellationToken token = default);

    /// <summary>
    /// 下载指定安装包到目标目录，并在下载后校验其 SHA256。
    /// </summary>
    /// <param name="package">待下载的安装包。</param>
    /// <param name="location">目标保存目录。</param>
    /// <param name="progress">下载进度回调（0-1）。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>已保存文件的完整路径。</returns>
    /// <exception cref="InvalidDataException">校验和不匹配时抛出。</exception>
    public async Task<string> DownloadPackageFileAsync(Package package, string location, IProgress<float>? progress = null, CancellationToken token = default)
    {
        using var httpClient = httpClientFactory.Create();

        // 先下载到随机命名的临时文件，校验通过后再移动到最终位置。
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        await using (var fileStream = File.OpenWrite(tempPath))
            await httpClient.DownloadAsync(package.FileLocation, fileStream, progress, token).ConfigureAwait(false);

        await TryValidateChecksum(package, tempPath, httpClient, token).ConfigureAwait(false);

        var filename = SanitizeFileName(package.Title) + " - " + package.FileName;
        var finalPath = Path.Combine(location, filename);

        File.Move(tempPath, finalPath, true);

        return finalPath;
    }

    /// <summary>
    /// 校验下载文件的 SHA256：先与包内声明的 CRC 字段比较，再回退到下载同目录的 .sha256 文件。
    /// </summary>
    /// <exception cref="InvalidDataException">两种校验方式均不匹配时抛出。</exception>
    private static async Task TryValidateChecksum(Package package, string tempPath, HttpClient httpClient, CancellationToken token)
    {
        await using var fileStream = File.OpenRead(tempPath);
        using var managedSha256 = SHA256.Create();

        var fileSha256Bytes = await managedSha256.ComputeHashAsync(fileStream, token).ConfigureAwait(false);
        // 将哈希字节转换为大写十六进制字符串，便于与服务器提供的校验值比较。
        var fileSha256 = fileSha256Bytes.Aggregate(string.Empty, (current, b) => current + b.ToString("X2"));

        // 优先使用包元数据中的 FileCrc（实际存放的是 SHA256 字符串）。
        if (!string.IsNullOrEmpty(package.FileCrc) && fileSha256.Equals(package.FileCrc, StringComparison.InvariantCultureIgnoreCase))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Package file checksum match. [fileName={package.FileName}, fileLocation={package.FileLocation}, fileCrc={package.FileCrc}]");
            return;
        }

        // 回退：尝试下载同名的 ".sha256" 校验文件；该文件可能不存在，不视为致命错误。
        try
        {
            var externalSha256 = await httpClient.GetStringAsync($"{package.FileLocation}.sha256", token).ConfigureAwait(false);
            if (fileSha256.Equals(externalSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"External file checksum match. [fileName={package.FileName}, fileLocation={package.FileLocation}, fileCrc={package.FileCrc}]");
                return;
            }
        }
        catch (HttpRequestException ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"External file checksum not found. [statusCode={ex.StatusCode}, fileName={package.FileName}, fileLocation={package.FileLocation}, fileCrc={package.FileCrc}]");
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"File checksum mismatch. [fileName={package.FileName}, fileLocation={package.FileLocation}]");

        throw new InvalidDataException("File checksum mismatch");
    }

    /// <summary>
    /// 将标题中的非法文件名字符替换为下划线，并去除末尾的点，避免生成非法路径。
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        var invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
        return Regex.Replace(name, invalidRegStr, "_");
    }
}
