var mode = args.Length > 0 ? args[0] : "all";
var count = args.Length > 1 ? int.Parse(args[1]) : 2000;
var warmup = args.Length > 2 ? int.Parse(args[2]) : 500;
var total = warmup + count;
var payloads = LoadPayloads();
var ops = BuildOps(total);
var measuredOps = ops.Skip(warmup).ToList();

Console.WriteLine($"payloads: {payloads.Count} real documents, avg {(int)payloads.Average(p => p.ToBson().Length)} B");
Console.WriteLine($"warmup: {warmup} events discarded per technology");
Console.WriteLine($"op mix (measured): {measuredOps.Count(o => o == 'i')} insert, {measuredOps.Count(o => o == 'u')} update, {measuredOps.Count(o => o == 'd')} delete");
Console.WriteLine($"{"technology",-14}{"n",8}{"median ms",12}{"p95 ms",10}{"p99 ms",10}{"max ms",10}");
if (mode == "all")
{
    foreach (var m in new[] { "mongo", "kafka", "eventstore" })
    {
        await Report(m, total, warmup, payloads, ops);
    }
}
else
{
    await Report(mode, total, warmup, payloads, ops);
}

static List<BsonDocument> LoadPayloads()
{
    var path = Path.Combine(AppContext.BaseDirectory, "data", "sample-docs.json");
    if (!File.Exists(path))
    {
        path = Path.Combine(Directory.GetCurrentDirectory(), "data", "sample-docs.json");
    }

    return BsonSerializer.Deserialize<BsonDocument>($"{{\"d\":{File.ReadAllText(path)}}}")["d"].AsBsonArray
        .Select(v => v.AsBsonDocument).ToList();
}

static List<char> BuildOps(int n)
{
    var ops = new List<char>(n);
    for (var i = 0; i < n; i++)
    {
        ops.Add(i % 555 == 277 ? 'd' : i % 5000 == 2500 ? 'i' : 'u');
    }

    return ops;
}

static async Task Report(string mode, int total, int warmup, List<BsonDocument> payloads, List<char> ops)
{
    var latencies = mode switch
    {
        "mongo" => await RunMongo(total, payloads, ops),
        "kafka" => await RunKafka(total, payloads, ops),
        "eventstore" => await RunEventStore(total, payloads, ops),
        _ => throw new ArgumentException($"unknown mode {mode}")
    };
    var measured = latencies.Skip(warmup).ToList();
    measured.Sort();
    Console.WriteLine($"{mode,-14}{measured.Count,8}{Pct(measured, 50),12:F1}{Pct(measured, 95),10:F1}{Pct(measured, 99),10:F1}{measured[^1],10:F1}");
}

static double Pct(List<double> sorted, int p) => sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * p / 100.0))];

static async Task<List<double>> RunMongo(int n, List<BsonDocument> payloads, List<char> ops)
{
    var client = new MongoClient("mongodb://localhost:27018/?replicaSet=rs0&directConnection=true");
    var db = client.GetDatabase("probe");
    var name = "events_" + ObjectId.GenerateNewId();
    await db.CreateCollectionAsync(name);
    var coll = db.GetCollection<BsonDocument>(name);

    var live = new List<ObjectId>();
    foreach (var payload in payloads)
    {
        var doc = (BsonDocument)payload.DeepClone();
        var id = ObjectId.GenerateNewId();
        doc["_id"] = id;
        await coll.InsertOneAsync(doc);
        live.Add(id);
    }

    var pending = new ConcurrentDictionary<ObjectId, long>();
    var latencies = new List<double>(n);
    var received = new TaskCompletionSource();
    var seen = 0;
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

    var pipeline = new EmptyPipelineDefinition<ChangeStreamDocument<BsonDocument>>()
        .Match(c => c.OperationType == ChangeStreamOperationType.Insert
            || c.OperationType == ChangeStreamOperationType.Update
            || c.OperationType == ChangeStreamOperationType.Delete);
    _ = Task.Run(async () =>
    {
        using var cursor = await coll.WatchAsync(pipeline, new ChangeStreamOptions { MaxAwaitTime = TimeSpan.FromMilliseconds(20) }, cts.Token);
        while (await cursor.MoveNextAsync(cts.Token))
        {
            foreach (var change in cursor.Current)
            {
                if (!pending.TryRemove(change.DocumentKey["_id"].AsObjectId, out var t0))
                {
                    continue;
                }

                latencies.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
                if (Interlocked.Increment(ref seen) >= n)
                {
                    received.TrySetResult();
                    return;
                }
            }
        }
    }, cts.Token);

    await Task.Delay(1500);
    var next = 0;
    for (var i = 0; i < n; i++)
    {
        switch (ops[i])
        {
            case 'i':
                var doc = (BsonDocument)payloads[i % payloads.Count].DeepClone();
                var id = ObjectId.GenerateNewId();
                doc["_id"] = id;
                pending[id] = Stopwatch.GetTimestamp();
                await coll.InsertOneAsync(doc);
                live.Add(id);
                break;
            case 'u':
                var uid = live[next++ % live.Count];
                pending[uid] = Stopwatch.GetTimestamp();
                await coll.UpdateOneAsync(
                    new BsonDocument("_id", uid),
                    new BsonDocument("$set", new BsonDocument
                    {
                        { "publisherStatus.status", i % 2 == 0 ? "Published" : "FailedToPublish" },
                        { "publisherStatus.timestamp", DateTime.UtcNow },
                        { "seq", i }
                    }));
                break;
            case 'd':
                var did = live[^1];
                live.RemoveAt(live.Count - 1);
                pending[did] = Stopwatch.GetTimestamp();
                await coll.DeleteOneAsync(new BsonDocument("_id", did));
                break;
        }

        await Task.Delay(3);
    }

    await received.Task;
    await cts.CancelAsync();
    await db.DropCollectionAsync(name);
    return latencies;
}

static async Task<List<double>> RunKafka(int n, List<BsonDocument> payloads, List<char> ops)
{
    var topic = "probe-" + Guid.NewGuid().ToString("N");
    var values = payloads.Select(p => p.ToJson()).ToList();
    var deleteValues = payloads.Select(p => new BsonDocument("internalId", p.GetValue("internalId", "")).ToJson()).ToList();
    var latencies = new List<double>(n);
    var received = new TaskCompletionSource();
    var seen = 0;
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

    using (var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = "localhost:29092" }).Build())
    {
        await admin.CreateTopicsAsync([new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }]);
    }

    var consumerConfig = new ConsumerConfig
    {
        BootstrapServers = "localhost:29092",
        GroupId = "probe-" + Guid.NewGuid().ToString("N"),
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false,
        FetchWaitMaxMs = 10
    };
    var assigned = new TaskCompletionSource();
    _ = Task.Run(() =>
    {
        using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig)
            .SetPartitionsAssignedHandler((_, _) => assigned.TrySetResult())
            .Build();
        consumer.Subscribe(topic);
        while (!cts.IsCancellationRequested)
        {
            var result = consumer.Consume(cts.Token);
            if (result?.Message == null)
            {
                continue;
            }

            var value = result.Message.Value;
            latencies.Add(Stopwatch.GetElapsedTime(long.Parse(value.AsSpan(0, value.IndexOf('|')))).TotalMilliseconds);
            if (Interlocked.Increment(ref seen) >= n)
            {
                received.TrySetResult();
                return;
            }
        }
    }, cts.Token);

    await assigned.Task;
    var producerConfig = new ProducerConfig { BootstrapServers = "localhost:29092", LingerMs = 0, Acks = Acks.Leader };
    using (var producer = new ProducerBuilder<Null, string>(producerConfig).Build())
    {
        for (var i = 0; i < n; i++)
        {
            var body = ops[i] == 'd' ? deleteValues[i % deleteValues.Count] : values[i % values.Count];
            producer.Produce(topic, new Message<Null, string> { Value = Stopwatch.GetTimestamp() + "|" + ops[i] + "|" + body });
            await Task.Delay(3);
        }

        producer.Flush(TimeSpan.FromSeconds(30));
    }

    await received.Task;
    await cts.CancelAsync();
    return latencies;
}

static async Task<List<double>> RunEventStore(int n, List<BsonDocument> payloads, List<char> ops)
{
    var settings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
    using var client = new EventStoreClient(settings);
    var stream = "probe-" + Guid.NewGuid().ToString("N");
    var bodies = payloads.Select(p => Encoding.UTF8.GetBytes(p.ToJson())).ToList();
    var deleteBodies = payloads.Select(p => Encoding.UTF8.GetBytes(new BsonDocument("internalId", p.GetValue("internalId", "")).ToJson())).ToList();
    var latencies = new List<double>(n);
    var received = new TaskCompletionSource();
    var seen = 0;
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

    _ = Task.Run(async () =>
    {
        await using var subscription = client.SubscribeToStream(stream, FromStream.Start, cancellationToken: cts.Token);
        await foreach (var message in subscription.Messages.WithCancellation(cts.Token))
        {
            if (message is StreamMessage.Event(var resolved))
            {
                latencies.Add(Stopwatch.GetElapsedTime(BitConverter.ToInt64(resolved.Event.Data.Span)).TotalMilliseconds);
                if (Interlocked.Increment(ref seen) >= n)
                {
                    received.TrySetResult();
                    return;
                }
            }
        }
    }, cts.Token);

    await Task.Delay(1500);
    for (var i = 0; i < n; i++)
    {
        var body = ops[i] == 'd' ? deleteBodies[i % deleteBodies.Count] : bodies[i % bodies.Count];
        var type = ops[i] switch { 'i' => "DraftAdded", 'd' => "DraftRemoved", _ => "DraftUpdated" };
        var data = new byte[8 + body.Length];
        BitConverter.TryWriteBytes(data, Stopwatch.GetTimestamp());
        body.CopyTo(data, 8);
        await client.AppendToStreamAsync(stream, StreamState.Any, [new EventData(EventStore.Client.Uuid.NewUuid(), type, data)]);
        await Task.Delay(3);
    }

    await received.Task;
    await cts.CancelAsync();
    return latencies;
}