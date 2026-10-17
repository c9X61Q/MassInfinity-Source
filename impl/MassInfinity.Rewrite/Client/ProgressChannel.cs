// ⚠️ 重写实现 —— 不是还原代码。事件名/参数序取自官方 SDK 契约。
namespace MassInfinity.Rewrite.Client;

using MassInfinity.Rewrite.Protocol;
using Microsoft.AspNetCore.SignalR.Client;

internal static class ProgressChannel
{
    /// <summary>
    /// 订阅一次任务进度：
    /// <c>Progress(step,total,percentage,message)</c> 回调进度，
    /// <c>GameInstance</c> 结束并返回实例，<c>Disposed</c> 结束连接。
    /// </summary>
    public static async Task<GameInstanceDto> RunAsync(string baseUrl, string url, IProgress<Progress>? callback, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<GameInstanceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl.TrimEnd('/')}{url}")
            .Build();

        connection.On<int, int, int, string>("Progress", (step, total, percentage, message) =>
            callback?.Report(new Progress(step, total, percentage, message)));

        connection.On<GameInstanceDto>("GameInstance", i => tcs.TrySetResult(i));

        connection.On("Disposed", () => tcs.TrySetCanceled());

        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        try
        {
            await connection.StartAsync(ct).ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
