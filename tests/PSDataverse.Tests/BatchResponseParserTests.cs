namespace PSDataverse.Tests;

using System.Net;
using FluentAssertions;
using PSDataverse.Dataverse;
using PSDataverse.Dataverse.Model;

public class BatchResponseParserTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void ParsesMultipleSuccessfulOperationsAndLineEndings(string lineEnding)
    {
        var response = ReadFixture("BatchResponse-Success.http", lineEnding);

        var result = BatchResponse.Parse(response);

        result.Id.Should().Be("batch-1");
        result.BoundaryId.Should().Be("changeset-1");
        result.IsSuccessful.Should().BeTrue();
        result.Operations.Should().HaveCount(2);
        result.Operations[0].StatusCode.Should().Be(HttpStatusCode.NoContent);
        result.Operations[0].Content.Should().BeNull();
        result.Operations[0].Headers!["OData-Version"].Should().Be("4.0, 4.01");
        result.Operations[1].StatusCode.Should().Be(HttpStatusCode.Created);
        result.Operations[1].Content.Should().Be("{\"accountid\":\"2\",\"name\":\"Contoso\"}");
    }

    [Fact]
    public void ParsesNestedODataError()
    {
        var result = BatchResponse.Parse(ReadFixture("BatchResponse-Error.http"));

        result.IsSuccessful.Should().BeFalse();
        result.Operations.Should().ContainSingle();
        result.Operations[0].ContentId.Should().Be("9");
        result.Operations[0].StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Operations[0].Error!.Code.Should().Be("0x80040216");
        result.Operations[0].Error!.InnerError!.Message.Should().Be("duplicate");
    }

    [Fact]
    public void ParsesDataverseThrottleFaultShape()
    {
        var result = BatchResponse.Parse(ReadFixture("BatchResponse-Throttled.http"));

        result.IsSuccessful.Should().BeFalse();
        result.Operations[0].Error!.Code.Should().Be("429");
        result.Operations[0].Error!.Message.Should().Be("Rate limit exceeded.");
        result.Operations[0].Error!.Type.Should().Contain("DataverseOperationException");
    }

    [Fact]
    public void PreservesUnexpectedHtmlAsTypedError()
    {
        var response = ReadFixture("BatchResponse-Error.http")
            .Replace("Content-Type: application/json", "Content-Type: text/html", StringComparison.Ordinal)
            .Replace(
                "{\"error\":{\"code\":\"0x80040216\",\"message\":\"A record with this name already exists.\",\"innererror\":{\"message\":\"duplicate\"}}}",
                "<html><body>Bad gateway</body></html>",
                StringComparison.Ordinal)
            .Replace("HTTP/1.1 400 Bad Request", "HTTP/1.1 502 Bad Gateway", StringComparison.Ordinal);

        var result = BatchResponse.Parse(response);

        result.Operations[0].Error!.Code.Should().Be("502");
        result.Operations[0].Error!.Message.Should().Contain("Bad gateway");
    }

    [Theory]
    [InlineData("Content-Type: multipart/mixed", "boundary parameter")]
    [InlineData("Content-Type: application/json", "multipart/mixed")]
    public void RejectsMalformedContentType(string contentType, string expectedMessage)
    {
        var response = ReadFixture("BatchResponse-Success.http");
        var currentHeader = response.Split('\n')[1];
        response = response.Replace(currentHeader, contentType, StringComparison.Ordinal);

        var action = () => BatchResponse.Parse(response);

        action.Should().Throw<ParseException>().WithMessage($"*{expectedMessage}*");
    }

    [Fact]
    public void RejectsMismatchedClosingBoundaryWithLineNumber()
    {
        var response = ReadFixture("BatchResponse-Success.http")
            .Replace("--changesetresponse_changeset-1--", "--changesetresponse_wrong--", StringComparison.Ordinal);

        var action = () => BatchResponse.Parse(response);

        action.Should().Throw<ParseException>()
            .WithMessage("Line *Expected changeset boundary*")
            .WithMessage("*changesetresponse_wrong*");
    }

    private static string ReadFixture(string name, string lineEnding = "\n")
    {
        var path = Path.Combine(AppContext.BaseDirectory, "samples", name);
        var normalized = File.ReadAllText(path)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return lineEnding == "\n" ? normalized : normalized.Replace("\n", lineEnding, StringComparison.Ordinal);
    }
}
