using System;

namespace LenovoLegionToolkit.Lib.PackageDownloader;

/// <summary>
/// 当指定机型的更新目录（catalog）在服务器上不存在时抛出的异常。
/// </summary>
/// <param name="message">异常消息。</param>
/// <param name="ex">导致该异常的内部异常。</param>
public class UpdateCatalogNotFoundException(string? message, Exception? ex) : Exception(message, ex);
