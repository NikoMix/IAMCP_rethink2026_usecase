using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.AI.Projects;
using Azure.Core;
using ProposalGenerator.Knowledge;

namespace ProposalGenerator.Knowledge.Tests;

/// <summary>
/// Runs <see cref="FoundryVectorStoreGateway"/> against the real SDK pipeline with an in-memory HTTP handler, so the
/// requests the SDK sends and the responses it parses are checked without Azure.
/// </summary>
public sealed class FoundryVectorStoreGatewayTests
{
    private const string StoreId = "vs_1";

    private static readonly long Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture).ToUnixTimeSeconds();

    private static (FoundryVectorStoreGateway Gateway, FakeHandler Handler) Create()
    {
        var handler = new FakeHandler();
        var options = new AIProjectClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(handler)) };
        options.RetryPolicy = new ClientRetryPolicy(maxRetries: 0);
        var client = new AIProjectClient(new Uri("https://example.test/api/projects/demo"), new FakeCredential(), options);
        return (new FoundryVectorStoreGateway(client, indexingTimeout: TimeSpan.FromSeconds(5), pollInterval: TimeSpan.Zero), handler);
    }

    private static JsonObject VectorStore(string id, string name) => new()
    {
        ["id"] = id, ["object"] = "vector_store", ["created_at"] = Now, ["name"] = name, ["usage_bytes"] = 0,
        ["file_counts"] = new JsonObject { ["in_progress"] = 0, ["completed"] = 0, ["failed"] = 0, ["cancelled"] = 0, ["total"] = 0 },
        ["status"] = "completed", ["metadata"] = new JsonObject(), ["last_active_at"] = Now,
    };

    private static JsonObject VectorStoreFile(string id, string status, JsonObject? attributes = null, JsonNode? lastError = null) => new()
    {
        ["id"] = id, ["object"] = "vector_store.file", ["created_at"] = Now, ["vector_store_id"] = StoreId, ["usage_bytes"] = 10,
        ["status"] = status, ["last_error"] = lastError, ["attributes"] = attributes ?? new JsonObject(),
        ["chunking_strategy"] = new JsonObject
        {
            ["type"] = "static",
            ["static"] = new JsonObject { ["max_chunk_size_tokens"] = 800, ["chunk_overlap_tokens"] = 400 },
        },
    };

    private static JsonObject List(params JsonNode[] items) => new()
    {
        ["object"] = "list", ["data"] = new JsonArray(items), ["first_id"] = null, ["last_id"] = null, ["has_more"] = false,
    };

    private static JsonObject Deleted(string id, string type) => new() { ["id"] = id, ["object"] = type, ["deleted"] = true };

    private static JsonObject UploadedFile(string id, string name) => new()
    {
        ["id"] = id, ["object"] = "file", ["bytes"] = 10, ["created_at"] = Now, ["filename"] = name, ["purpose"] = "assistants", ["status"] = "processed",
    };

    [Fact]
    public async Task AddFile_UploadsAttachesWithAttributesAndWaitsForIndexing()
    {
        var (gateway, handler) = Create();
        var polls = 0;
        handler.On(HttpMethod.Post, "/files", _ => UploadedFile("file_new", "rate-card.json"));
        handler.On(HttpMethod.Post, $"/vector_stores/{StoreId}/files", _ => VectorStoreFile("file_new", "in_progress"));
        handler.On(HttpMethod.Get, $"/vector_stores/{StoreId}/files/file_new", _ => VectorStoreFile("file_new", ++polls < 3 ? "in_progress" : "completed"));
        var attributes = new Dictionary<string, string> { ["managed_by"] = "proposal-generator-knowledge", ["source_path"] = "rate-card.json" };

        var id = await gateway.AddFileAsync(StoreId, "rate-card.json", Encoding.UTF8.GetBytes("{ }\n"), attributes, CancellationToken.None);

        Assert.Equal("file_new", id);
        Assert.Equal(3, polls);
        var upload = handler.Requests.Single(r => r.Path.EndsWith("/files", StringComparison.Ordinal) && r.Method == HttpMethod.Post && !r.Path.Contains("vector_stores", StringComparison.Ordinal));
        Assert.Contains("filename=rate-card.json", upload.Body.Replace("\"", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("assistants", upload.Body, StringComparison.Ordinal);
        Assert.Contains("{ }\n", upload.Body, StringComparison.Ordinal);
        Assert.Equal("Bearer fake-token", upload.Authorization);

        var attach = handler.Requests.Single(r => r.Path.EndsWith($"/vector_stores/{StoreId}/files", StringComparison.Ordinal) && r.Method == HttpMethod.Post);
        var body = JsonNode.Parse(attach.Body)!;
        Assert.Equal("file_new", body["file_id"]!.GetValue<string>());
        Assert.Equal("proposal-generator-knowledge", body["attributes"]!["managed_by"]!.GetValue<string>());
        Assert.Equal("rate-card.json", body["attributes"]!["source_path"]!.GetValue<string>());
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task AddFile_WhenIndexingFails_RemovesTheUploadAndThrows()
    {
        var (gateway, handler) = Create();
        handler.On(HttpMethod.Post, "/files", _ => UploadedFile("file_bad", "a.md"));
        handler.On(HttpMethod.Post, $"/vector_stores/{StoreId}/files", _ => VectorStoreFile("file_bad", "in_progress"));
        handler.On(HttpMethod.Get, $"/vector_stores/{StoreId}/files/file_bad", _ =>
            VectorStoreFile("file_bad", "failed", lastError: new JsonObject { ["code"] = "invalid_file", ["message"] = "unsupported" }));
        handler.On(HttpMethod.Delete, $"/vector_stores/{StoreId}/files/file_bad", _ => Deleted("file_bad", "vector_store.file.deleted"));
        handler.On(HttpMethod.Delete, "/files/file_bad", _ => Deleted("file_bad", "file"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.AddFileAsync(StoreId, "a.md", Encoding.UTF8.GetBytes("# A\n"), new Dictionary<string, string>(), CancellationToken.None));

        Assert.Contains("a.md", exception.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", exception.Message, StringComparison.Ordinal);
        Assert.Equal(
            [$"/vector_stores/{StoreId}/files/file_bad", "/files/file_bad"],
            handler.Requests.Where(r => r.Method == HttpMethod.Delete).Select(r => r.Suffix));
    }

    [Fact]
    public async Task ListFiles_ReturnsAttributesAsStrings()
    {
        var (gateway, handler) = Create();
        handler.On(HttpMethod.Get, $"/vector_stores/{StoreId}/files", _ => List(
            VectorStoreFile("file_a", "completed", new JsonObject { ["source_path"] = "a.md", ["content_sha256"] = "abc" }),
            VectorStoreFile("file_b", "completed", new JsonObject { ["count"] = 3, ["flag"] = true })));

        var files = await gateway.ListFilesAsync(StoreId, CancellationToken.None);

        Assert.Equal(["file_a", "file_b"], files.Select(f => f.FileId));
        Assert.Equal(new Dictionary<string, string> { ["source_path"] = "a.md", ["content_sha256"] = "abc" }, files[0].Attributes);
        Assert.Equal(new Dictionary<string, string> { ["count"] = "3", ["flag"] = "true" }, files[1].Attributes);
    }

    [Fact]
    public async Task FindVectorStore_ReturnsTheSingleMatchOrNull()
    {
        var (gateway, handler) = Create();
        handler.On(HttpMethod.Get, "/vector_stores", _ => List(VectorStore("vs_other", "other"), VectorStore("vs_kb", "knowledge")));

        Assert.Equal("vs_kb", await gateway.FindVectorStoreAsync("knowledge", CancellationToken.None));
        Assert.Null(await gateway.FindVectorStoreAsync("missing", CancellationToken.None));
    }

    [Fact]
    public async Task FindVectorStore_WithDuplicateNames_Throws()
    {
        var (gateway, handler) = Create();
        handler.On(HttpMethod.Get, "/vector_stores", _ => List(VectorStore("vs_1", "knowledge"), VectorStore("vs_2", "knowledge")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.FindVectorStoreAsync("knowledge", CancellationToken.None));

        Assert.Contains("vs_1, vs_2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateVectorStore_SendsNameAndMetadata()
    {
        var (gateway, handler) = Create();
        handler.On(HttpMethod.Post, "/vector_stores", _ => VectorStore("vs_new", "knowledge"));

        var id = await gateway.CreateVectorStoreAsync("knowledge", new Dictionary<string, string> { ["managed_by"] = "x" }, CancellationToken.None);

        Assert.Equal("vs_new", id);
        var body = JsonNode.Parse(handler.Requests.Single().Body)!;
        Assert.Equal("knowledge", body["name"]!.GetValue<string>());
        Assert.Equal("x", body["metadata"]!["managed_by"]!.GetValue<string>());
    }

    [Fact]
    public async Task RemoveFile_DetachesAndDeletes_AndToleratesMissingFiles()
    {
        var (gateway, handler) = Create();
        handler.On(HttpMethod.Delete, $"/vector_stores/{StoreId}/files/file_old", _ => null);
        handler.On(HttpMethod.Delete, "/files/file_old", _ => Deleted("file_old", "file"));

        await gateway.RemoveFileAsync(StoreId, "file_old", CancellationToken.None);

        Assert.Equal(
            [$"/vector_stores/{StoreId}/files/file_old", "/files/file_old"],
            handler.Requests.Where(r => r.Method == HttpMethod.Delete).Select(r => r.Suffix));
    }

    private sealed class FakeCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("fake-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed record RecordedRequest(HttpMethod Method, string Path, string Suffix, string Body, string? Authorization);

    /// <summary>Answers requests by method and path suffix; a null response means 404. Unknown requests fail the test.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly List<(HttpMethod Method, string Suffix, Func<string, JsonNode?> Respond)> _routes = [];

        public List<RecordedRequest> Requests { get; } = [];

        public void On(HttpMethod method, string suffix, Func<string, JsonNode?> respond) => _routes.Add((method, suffix, respond));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var route = _routes
                .Where(r => r.Method == request.Method && path.EndsWith(r.Suffix, StringComparison.Ordinal))
                .OrderByDescending(r => r.Suffix.Length)
                .FirstOrDefault();
            Requests.Add(new RecordedRequest(request.Method, path, route.Suffix ?? path, body, request.Headers.Authorization?.ToString()));
            if (route.Respond is null)
            {
                throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}.");
            }

            var response = route.Respond(body);
            return response is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"error":{"message":"not found","type":"invalid_request_error"}}""", Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response.ToJsonString(), Encoding.UTF8, "application/json") };
        }
    }
}
