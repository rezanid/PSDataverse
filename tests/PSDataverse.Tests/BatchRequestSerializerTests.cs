namespace PSDataverse.Tests;

using FluentAssertions;
using PSDataverse.Dataverse.Model;

public class BatchRequestSerializerTests
{
    [Fact]
    public void SerializesGoldenRequestWithProtocolCrLfOnEveryPlatform()
    {
        var batch = new Batch<string>(
        [
            new Operation<string>
            {
                ContentId = "10",
                Method = "POST",
                Uri = "accounts",
                Headers = new Dictionary<string, string> { ["If-Match"] = "*" },
                Value = "{\"name\":\"Contoso\"}"
            },
            new Operation<string>
            {
                ContentId = "11",
                Method = "DELETE",
                Uri = "contacts(00000000-0000-0000-0000-000000000011)"
            }
        ])
        {
            Id = "batch-id",
            ChangeSet = { Id = "changeset-id" }
        };
        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", "BatchRequest.http"))
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\r\n", StringComparison.Ordinal);

        var actual = batch.ToString();

        actual.Should().Be(expected);
        actual.Replace("\r\n", string.Empty, StringComparison.Ordinal)
            .Should().NotContain("\n");
    }
}
