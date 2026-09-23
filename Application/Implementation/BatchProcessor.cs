namespace Application.Implementation.NovelWebsites;

public static class BatchProcessor
{
    public static async Task<T[]> ProcessInBatches<T>(
        IEnumerable<Func<Task<T>>> taskFactories,
        int batchSize = 100,
        TimeSpan? delayBetweenBatches = null,
        CancellationToken cancellationToken = default)
    {
        var factories = taskFactories.ToList();
        var results = new T[factories.Count];
        var delay = delayBetweenBatches ?? TimeSpan.Zero;

        for (int i = 0; i < factories.Count; i += batchSize)
        {
            Console.WriteLine($"Start Batch From {i}");
            // start only this batch
            var batch = factories
                .Skip(i)
                .Take(batchSize)
                .Select((f, idx) => f())   // now tasks are created/started here
                .ToArray();

            var batchResults = await Task.WhenAll(batch);
            Array.Copy(batchResults, 0, results, i, batchResults.Length);

            if (delay > TimeSpan.Zero && i + batchSize < factories.Count)
                await Task.Delay(delay, cancellationToken);
        }

        return results;
    }

}