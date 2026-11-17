using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CoordinateSharp;
using LenovoLegionToolkit.Lib.Settings;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 通过在线地理定位服务获取当前坐标，并计算本地日出/日落时间（结果按天缓存到设置中）。
/// </summary>
/// <param name="settings">用于缓存上次定位结果与日出日落时间的设置。</param>
/// <param name="httpClientFactory">HTTP 客户端工厂。</param>
public class SunriseSunset(SunriseSunsetSettings settings, HttpClientFactory httpClientFactory)
{
    /// <summary>
    /// 获取当日日出与日落时间；当天已计算过则直接返回缓存值。
    /// </summary>
    /// <param name="token">取消令牌。</param>
    /// <returns>日出与日落时间；定位或计算失败时两者均为 null。</returns>
    public async Task<(Time?, Time?)> GetSunriseSunsetAsync(CancellationToken token = default)
    {
        var (sunrise, sunset) = (settings.Store.Sunrise, settings.Store.Sunset);
        // 当天内若已有缓存结果则直接复用，避免重复发起网络请求。
        if (settings.Store.LastCheckDateTime == DateTime.Today && sunrise is not null && sunset is not null)
            return (sunrise, sunset);

        var coordinate = await GetGeoLocationAsync(token).ConfigureAwait(false);

        if (coordinate is null)
            return (null, null);

        (sunrise, sunset) = CalculateSunriseSunset(coordinate);

        // 记录本次计算结果的时间戳（本地日期，与缓存判断一致）与数值，供后续当天读取复用。
        settings.Store.LastCheckDateTime = DateTime.Today;
        settings.Store.Sunrise = sunrise;
        settings.Store.Sunset = sunset;
        settings.SynchronizeStore();

        return (sunrise, sunset);
    }

    /// <summary>
    /// 通过 ip-api.com 根据公网 IP 粗略获取经纬度坐标。
    /// </summary>
    /// <param name="token">取消令牌。</param>
    /// <returns>坐标对象；请求或解析失败时返回 null。</returns>
    private async Task<Coordinate?> GetGeoLocationAsync(CancellationToken token)
    {
        try
        {
            using var httpClient = httpClientFactory.Create();
            // fields 参数仅请求经纬度，减少响应体积；返回结果为 JSON。
            var responseJson = await httpClient.GetStringAsync("http://ip-api.com/json?fields=lat,lon", token).ConfigureAwait(false);
            var responseJsonNode = JsonNode.Parse(responseJson);
            if (responseJsonNode is not null && double.TryParse(responseJsonNode["lat"]?.ToString(), out var lat) && double.TryParse(responseJsonNode["lon"]?.ToString(), out var lon))
                return new Coordinate(lat, lon, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to get geolocation.", ex);
        }

        return null;
    }

    /// <summary>
    /// 根据坐标计算日出与日落时间，仅保留时分。
    /// </summary>
    /// <param name="coordinate">地理坐标。</param>
    /// <returns>日出与日落时间；任意一个无法计算时两者均为 null。</returns>
    private static (Time?, Time?) CalculateSunriseSunset(Coordinate coordinate)
    {
        var sunrise = coordinate.CelestialInfo.SunRise;
        var sunset = coordinate.CelestialInfo.SunSet;

        if (sunrise is null || sunset is null)
            return (null, null);

        return (new Time(sunrise.Value.Hour, sunrise.Value.Minute), new Time(sunset.Value.Hour, sunset.Value.Minute));
    }
}
