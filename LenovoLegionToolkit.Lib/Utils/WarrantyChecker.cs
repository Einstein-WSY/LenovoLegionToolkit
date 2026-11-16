using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Settings;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 通过 Lenovo PCSupport 接口查询整机保修信息（起止日期与产品页面链接），结果会缓存到设置中。
/// </summary>
/// <param name="settings">用于缓存保修信息的应用设置。</param>
/// <param name="httpClientFactory">HTTP 客户端工厂。</param>
public class WarrantyChecker(ApplicationSettings settings, HttpClientFactory httpClientFactory)
{
    /// <summary>
    /// 获取保修信息；未强制刷新且已有缓存时直接返回缓存值。
    /// </summary>
    /// <param name="machineInformation">机器信息（使用序列号与机型码查询）。</param>
    /// <param name="forceRefresh">是否强制忽略缓存重新查询。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>保修信息；查询失败时返回 null。</returns>
    public async Task<WarrantyInfo?> GetWarrantyInfo(MachineInformation machineInformation, bool forceRefresh = false, CancellationToken token = default)
    {
        if (!forceRefresh && settings.Store.WarrantyInfo.HasValue)
            return settings.Store.WarrantyInfo.Value;

        using var httpClient = httpClientFactory.Create();

        var warrantyInfo = await GetStandardWarrantyInfo(httpClient, machineInformation, token).ConfigureAwait(false);

        // 无论成功与否都写入缓存，避免频繁请求远程接口。
        settings.Store.WarrantyInfo = warrantyInfo;
        settings.SynchronizeStore();

        return warrantyInfo;
    }

    /// <summary>
    /// 调用 PCSupport 接口查询基础与升级保修，取最早的开始日期与最晚的结束日期，并获取产品页面链接。
    /// </summary>
    private static async Task<WarrantyInfo?> GetStandardWarrantyInfo(HttpClient httpClient, MachineInformation machineInformation, CancellationToken token)
    {
        // 以 JSON 形式提交序列号与机型码。
        var content = JsonContent.Create(new { serialNumber = machineInformation.SerialNumber, machineType = machineInformation.MachineType });
        var response = await httpClient.PostAsync("https://pcsupport.lenovo.com/dk/en/api/v4/upsell/redport/getIbaseInfo", content, token).ConfigureAwait(false);
        var responseContent = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        var node = JsonNode.Parse(responseContent);

        // code 非 0 表示接口返回业务错误。
        if (node is null || node["code"]?.GetValue<int>() != 0)
            return null;

        var baseWarranties = node["data"]?["baseWarranties"]?.AsArray() ?? [];
        var upgradeWarranties = node["data"]?["upgradeWarranties"]?.AsArray() ?? [];

        // 合并基础与升级保修，取整体区间：最早开始时间与最晚结束时间。
        var startDate = baseWarranties.Concat(upgradeWarranties)
            .Select(n => n?["startDate"])
            .Where(n => n is not null)
            .Select(n => DateTime.Parse(n!.ToString()))
            .Min();
        var endDate = baseWarranties.Concat(upgradeWarranties)
            .Select(n => n?["endDate"])
            .Where(n => n is not null)
            .Select(n => DateTime.Parse(n!.ToString()))
            .Max();

        var productString = await httpClient.GetStringAsync($"https://pcsupport.lenovo.com/dk/en/api/v4/mse/getproducts?productId={machineInformation.SerialNumber}", token).ConfigureAwait(false);
        var productNode = JsonNode.Parse(productString);
        var firstProductNode = (productNode as JsonArray)?.FirstOrDefault();
        var id = firstProductNode?["Id"];
        var link = id is null ? null : new Uri($"https://pcsupport.lenovo.com/products/{id}");

        return new WarrantyInfo(startDate, endDate, link);
    }
}
