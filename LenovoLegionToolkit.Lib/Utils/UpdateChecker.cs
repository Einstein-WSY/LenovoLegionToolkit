using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Settings;
using NeoSmart.AsyncLock;
using Octokit;
using Octokit.Internal;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 基于 GitHub Releases 的更新检查器，负责查询最新版本并下载安装包。
/// </summary>
public class UpdateChecker
{
    private readonly HttpClientFactory _httpClientFactory;
    private readonly UpdateCheckSettings _updateCheckSettings = IoCContainer.Resolve<UpdateCheckSettings>();
    private readonly AsyncLock _updateSemaphore = new();

    private DateTime _lastUpdate;
    private TimeSpan _minimumTimeSpanForRefresh;
    private Update[] _updates = [];

    /// <summary>
    /// 是否禁用更新检查（禁用时所有查询直接返回空）。
    /// </summary>
    public bool Disable { get; set; }

    /// <summary>
    /// 最近一次更新检查的状态结果。
    /// </summary>
    public UpdateCheckStatus Status { get; set; }

    /// <summary>
    /// 构造更新检查器，并初始化检查频率与上次检查时间。
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂。</param>
    public UpdateChecker(HttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;

        UpdateMinimumTimeSpanForRefresh();
        _lastUpdate = _updateCheckSettings.Store.LastUpdateCheckDateTime ?? DateTime.MinValue;
    }

    /// <summary>
    /// 检查是否有可用更新，并返回最新版本号（无更新时返回 null）。
    /// </summary>
    /// <param name="forceCheck">是否强制忽略最小刷新间隔立即检查。</param>
    /// <returns>最新可用版本号；无更新、被禁用或出错时为 null。</returns>
    public async Task<Version?> CheckAsync(bool forceCheck)
    {
        using (await _updateSemaphore.LockAsync().ConfigureAwait(false))
        {
            if (Disable)
            {
                _lastUpdate = DateTime.UtcNow;
                _updates = [];
                return null;
            }

            try
            {
                var timeSpanSinceLastUpdate = DateTime.UtcNow - _lastUpdate;
                var shouldCheck = timeSpanSinceLastUpdate > _minimumTimeSpanForRefresh;

                if (!forceCheck && !shouldCheck)
                    return _updates.Length != 0 ? _updates.First().Version : null;

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Checking...");

                var adapter = new HttpClientAdapter(_httpClientFactory.CreateHandler);
                var productInformation = new ProductHeaderValue("LenovoLegionToolkit-UpdateChecker");
                var connection = new Connection(productInformation, adapter);
                var githubClient = new GitHubClient(connection);
                var releases = await githubClient.Repository.Release.GetAll("BartoszCichecki", "LenovoLegionToolkit", new ApiOptions { PageSize = 5 }).ConfigureAwait(false);

                var thisReleaseVersion = Assembly.GetEntryAssembly()?.GetName().Version;
                var thisBuildDate = Assembly.GetEntryAssembly()?.GetBuildDateTime() ?? new DateTime(2000, 1, 1);

                // 过滤规则：排除草稿/预发布，发布时间不早于当前构建，且版本号严格高于当前版本，按版本降序排列。
                var updates = releases
                    .Where(r => !r.Draft)
                    .Where(r => !r.Prerelease)
                    .Where(r => (r.PublishedAt ?? r.CreatedAt).UtcDateTime >= thisBuildDate)
                    .Select(r => new Update(r))
                    .Where(r => r.Version > thisReleaseVersion)
                    .OrderByDescending(r => r.Version)
                    .ToArray();

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Checked [updates.Length={updates.Length}]");

                _updates = updates;
                Status = UpdateCheckStatus.Success;

                return _updates.Length != 0 ? _updates.First().Version : null;
            }
            catch (RateLimitExceededException ex)
            {
                // 命中 GitHub API 速率限制，标记状态并放弃本次检查。
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Reached API Rate Limitation.", ex);

                Status = UpdateCheckStatus.RateLimitReached;
                return null;
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Error checking for updates.", ex);

                Status = UpdateCheckStatus.Error;
                return null;
            }
            finally
            {
                _lastUpdate = DateTime.UtcNow;
                _updateCheckSettings.Store.LastUpdateCheckDateTime = _lastUpdate;
                _updateCheckSettings.SynchronizeStore();
            }
        }
    }

    /// <summary>
    /// 获取缓存中的可用更新列表。
    /// </summary>
    public async Task<Update[]> GetUpdatesAsync()
    {
        using (await _updateSemaphore.LockAsync().ConfigureAwait(false))
            return _updates;
    }

    /// <summary>
    /// 下载最新版本的安装包到临时目录。
    /// </summary>
    /// <param name="progress">下载进度回调（0-1）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已下载安装包的临时文件路径。</returns>
    /// <exception cref="InvalidOperationException">无可用更新或缺少安装包下载地址时抛出。</exception>
    public async Task<string> DownloadLatestUpdateAsync(IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        using (await _updateSemaphore.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            // 使用随机 GUID 命名临时文件，避免并发/多次下载互相覆盖。
            var tempPath = Path.Combine(Folders.Temp, $"LenovoLegionToolkitSetup_{Guid.NewGuid()}.exe");
            var latestUpdate = _updates.OrderByDescending(u => u.Version).FirstOrDefault();

            if (latestUpdate.Equals(default))
                throw new InvalidOperationException("No updates available");

            if (latestUpdate.Url is null)
                throw new InvalidOperationException("Setup file URL could not be found");

            await using var fileStream = File.OpenWrite(tempPath);
            using var httpClient = _httpClientFactory.Create();
            await httpClient.DownloadAsync(latestUpdate.Url, fileStream, progress, cancellationToken).ConfigureAwait(false);

            return tempPath;
        }
    }

    /// <summary>
    /// 根据用户设置的检查频率更新最小刷新间隔。
    /// </summary>
    /// <exception cref="ArgumentException">遇到未知的检查频率枚举值时抛出。</exception>
    public void UpdateMinimumTimeSpanForRefresh() => _minimumTimeSpanForRefresh = _updateCheckSettings.Store.UpdateCheckFrequency switch
    {
        UpdateCheckFrequency.PerHour => TimeSpan.FromHours(1),
        UpdateCheckFrequency.PerThreeHours => TimeSpan.FromHours(3),
        UpdateCheckFrequency.PerTwelveHours => TimeSpan.FromHours(13),
        UpdateCheckFrequency.PerDay => TimeSpan.FromDays(1),
        UpdateCheckFrequency.PerWeek => TimeSpan.FromDays(7),
        UpdateCheckFrequency.PerMonth => TimeSpan.FromDays(30),
        _ => throw new ArgumentException(nameof(_updateCheckSettings.Store.UpdateCheckFrequency))
    };
}
