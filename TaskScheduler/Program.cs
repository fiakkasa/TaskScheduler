using System.Collections.Concurrent;


var items = new ConcurrentDictionary<string, object>();
var count = 0;
var cts = new CancellationTokenSource();
cts.CancelAfter(400);

var t1 = EnqueueForProcessing(
    "hello",
    async (CancellationToken ct) =>
    {
        await Task.Delay(250, ct);
        Interlocked.Increment(ref count);
        return "banana";
    },
    cts.Token
);
var t2 = EnqueueForProcessing(
    "hello1",
    async (CancellationToken ct) =>
    {
        await Task.Delay(500, ct);
        Interlocked.Increment(ref count);
        return true;
    },
    cts.Token
);
var t3 = EnqueueForProcessing(
    "hello",
    async (CancellationToken ct) =>
    {
        await Task.Delay(250, ct);
        Interlocked.Increment(ref count);
        return ("hello", "world");
    },
    cts.Token
);
var t4 = EnqueueForProcessing(
    "hello1",
    async (CancellationToken ct) =>
    {
        await Task.Delay(250, ct);
        Interlocked.Increment(ref count);
        return true;
    },
    cts.Token
);

try
{
    await Task.WhenAll(t1, t2, t3, t4);
}
catch { }

Console.WriteLine("Queued Items: " + items.Count);
Console.WriteLine("Increments: " + count);

try
{
    foreach (var item in new object?[] { await t1, await t2, await t3, await t4 })
        Console.WriteLine(item);
}
catch { }

Task<T?> EnqueueForProcessing<T>(string key, Func<CancellationToken, Task<T?>> func, CancellationToken cancellationToken = default)
{
    var typedKey = key + "_" + typeof(T).FullName;

    if (items.TryGetValue(typedKey, out var tcsItem))
    {
        Console.WriteLine("Running => Key: " + typedKey);

        return ((TaskCompletionSource<T?>)tcsItem).Task;
    }

    var tcs = new TaskCompletionSource<T?>();

    items.TryAdd(typedKey, tcs);

    try
    {
        return tcs.Task;
    }
    finally
    {
        Console.WriteLine("Starting => Key: " + typedKey);

        Task.Run(async () =>
        {
            try
            {
                var result = await func(cancellationToken);

                tcs.SetResult(result);

                Console.WriteLine("Done => Key: " + typedKey);
            }
            catch (TaskCanceledException)
            {
                tcs.SetCanceled(cancellationToken);
                Console.WriteLine("Cancelled => Key: " + typedKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception => Key: " + typedKey);
                tcs.SetException(ex);
            }

            items.TryRemove(typedKey, out var _);
        });
    }
}
