using System;
using System.Net;
using System.Net.Http;

namespace LenovoLegionToolkit.Lib;

/// <summary>
/// HttpClient 工厂：集中管理代理配置，并按需创建带代理设置的 <see cref="HttpClient"/>。
/// 代理参数由 <see cref="SetProxy"/> 全局保存，后续创建的所有客户端都会生效。
/// </summary>
public class HttpClientFactory
{
    private Uri? _url; // 代理服务器地址；为 null 表示不使用代理
    private string? _username; // 代理认证用户名
    private string? _password; // 代理认证密码
    private bool _allowAllCerts; // 是否信任所有服务器证书（用于自签名/企业代理场景）

    /// <summary>
    /// 创建配置好代理（含认证与可选的证书跳过）的 <see cref="HttpClientHandler"/>。
    /// </summary>
    public HttpClientHandler CreateHandler()
    {
        var handler = new HttpClientHandler();

        if (_url is not null)
        {
            handler.UseProxy = true;
            handler.Proxy = new WebProxy(_url)
            {
                UseDefaultCredentials = false,
                BypassProxyOnLocal = false,
            };

            if (_username is not null && _password is not null)
                handler.DefaultProxyCredentials = new NetworkCredential(_username, _password);

            if (_allowAllCerts)
                // 危险操作：跳过 TLS 证书校验。仅当用户在设置中显式开启时才生效。
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return handler;
    }

    /// <summary>
    /// 创建一个新的 <see cref="HttpClient"/>（dispose 由调用方负责）。
    /// </summary>
    /// <returns>已应用代理配置的客户端实例。</returns>
    public HttpClient Create() => new(CreateHandler(), true);

    /// <summary>
    /// 设置全局代理参数；传 null 的 url 表示取消代理。
    /// </summary>
    /// <param name="url">代理服务器地址，null 表示不使用代理。</param>
    /// <param name="username">代理认证用户名，可为 null。</param>
    /// <param name="password">代理认证密码，可为 null。</param>
    /// <param name="allowAllCerts">true 时跳过服务器证书校验。</param>
    public void SetProxy(Uri? url, string? username, string? password, bool allowAllCerts)
    {
        _url = url;
        _username = username;
        _password = password;
        _allowAllCerts = allowAllCerts;
    }
}
