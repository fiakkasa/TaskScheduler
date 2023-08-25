using System.Collections.Concurrent;

ConcurrentDictionary<string, object> items = new ConcurrentDictionary<string, object>();

Console.WriteLine("[Start]");
Console.WriteLine("----------------------------------------------");

Console.WriteLine(string.Empty);

Console.WriteLine("Processing => [Cancellable]");
Console.WriteLine("----------------------------------------------");
var (c1, r1) = await CancellableTest();
await Task.Delay(250);

Console.WriteLine(string.Empty);

Console.WriteLine("Processing => [ForEach]");
Console.WriteLine("----------------------------------------------");
var (c2, r2) = await ForEachTest();
await Task.Delay(250);

Console.WriteLine(string.Empty);

Console.WriteLine("Processing => [WhenAll]");
Console.WriteLine("----------------------------------------------");
var (c3, r3) = await WhenAllTest();
await Task.Delay(250);

Console.WriteLine(string.Empty);

Console.WriteLine("[End]");

Console.WriteLine(string.Empty);

Console.WriteLine("[Results]");
Console.WriteLine("----------------------------------------------");
Console.WriteLine("Remaining Queued Items: " + items.Count);
Console.WriteLine("Total Executions: " + (c1 + c2 + c3));

Console.WriteLine(string.Empty);

Console.WriteLine("Cancellable Executions: " + c1);
Console.WriteLine("Cancellable Results: " + r1.Count);
foreach (var item in r1) Console.WriteLine("- " + item);

Console.WriteLine(string.Empty);

Console.WriteLine("ForEach Executions: " + c2);
Console.WriteLine("ForEach Results: " + r2.Count);
foreach (var item in r2) Console.WriteLine("- " + item);

Console.WriteLine(string.Empty);

Console.WriteLine("WhenAll Executions: " + c3);
Console.WriteLine("WhenAll Results: " + r3.Count);
foreach (var item in r3) Console.WriteLine("- " + item);

// ----------------------------------------------

async Task<(int count, ConcurrentBag<object>)> CancellableTest()
{
    int count = 0;
    var cts = new CancellationTokenSource();
    var results = new ConcurrentBag<object>();
    cts.CancelAfter(400);

    try
    {
        await Task.WhenAll(
            EnqueueForProcessing(
                "cancellable",
                async (CancellationToken ct) =>
                {
                    await Task.Delay(250, ct);
                    Interlocked.Increment(ref count);
                    results.Add("banana");
                    return "banana";
                },
                cts.Token
            ),
            EnqueueForProcessing(
                "cancellable",
                async (CancellationToken ct) =>
                {
                    await Task.Delay(500, ct);
                    Interlocked.Increment(ref count);
                    results.Add("banana");
                    return "banana";
                },
                cts.Token
            ),
            EnqueueForProcessing(
                "cancellable",
                async (CancellationToken ct) =>
                {
                    await Task.Delay(500, ct);
                    Interlocked.Increment(ref count);
                    results.Add(true);
                    return true;
                },
                cts.Token
            ),
            EnqueueForProcessing(
                "cancellable",
                async (CancellationToken ct) =>
                {
                    await Task.Delay(250, ct);
                    Interlocked.Increment(ref count);
                    results.Add(("hello", "world"));
                    return ("hello", "world");
                },
                cts.Token
            ),
            EnqueueForProcessing(
                "cancellable",
                async (CancellationToken ct) =>
                {
                    await Task.Delay(250, ct);
                    Interlocked.Increment(ref count);
                    results.Add(true);
                    return true;
                },
                cts.Token
            )
        );
    }
    catch { }

    return (count, results);
}

async Task<(int count, ConcurrentBag<int> results)> ForEachTest()
{
    var count = 0;
    var results = new ConcurrentBag<int>();

    await Parallel.ForEachAsync(
        Enumerable.Range(0, 1_000),
        async (_, cato) => await EnqueueForProcessing(
            "forEach",
            async ct =>
            {
                await Task.Delay(250, ct);
                results.Add(Random.Shared.Next());
                Interlocked.Increment(ref count);
                return 1;
            },
            cato
        )
    );

    return (count, results);
}

async Task<(int count, List<int> results)> WhenAllTest()
{
    var count = 0;
    var results =  await Task.WhenAll(
        Enumerable.Range(0, 1_000)
        .Select((_, __) => EnqueueForProcessing(
                "whenAll",
                async ct =>
                {
                    await Task.Delay(250, ct);
                    Interlocked.Increment(ref count);
                    return Random.Shared.Next();
                }
            )
        )
    );

    return (count, results.Distinct().ToList());
}

Task<T?> EnqueueForProcessing<T>(string key, Func<CancellationToken, Task<T?>> func, CancellationToken cancellationToken = default)
{
    var typedKey = key + "_" + typeof(T).FullName;
    var tcs = new TaskCompletionSource<T?>();

    if (!items.TryAdd(typedKey, tcs))
    {
        items.TryGetValue(typedKey, out var tcsItem);

        return ((TaskCompletionSource<T?>)tcsItem!).Task;
    }

    Console.WriteLine("Current Items In Queue => " + items.Count);

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

            Console.WriteLine("Current Items In Queue => " + items.Count);
        }, CancellationToken.None);
    }
}
