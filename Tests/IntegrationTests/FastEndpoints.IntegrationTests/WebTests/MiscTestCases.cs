﻿using IntegrationTests.Shared.Fixtures;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using TestCases.EventHandlingTest;
using Xunit;
using Xunit.Abstractions;

namespace FastEndpoints.IntegrationTests.WebTests;

public class MiscTestCases : EndToEndTestBase
{
    public MiscTestCases(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) : base(
        endToEndTestFixture, outputHelper)
    {
    }

    [Fact]
    public async Task MultiVerbEndpointAnonymousUserPutFail()
    {
        using var imageContent = new ByteArrayContent(Array.Empty<byte>());
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

        using var form = new MultipartFormDataContent { { imageContent, "File", "test.png" } };

        var res = await GuestClient.PutAsync("/api/uploads/image/save", form);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClaimMissing()
    {
        var (_, result) = await AdminClient.POSTAsync<
            TestCases.MissingClaimTest.ThrowIfMissingEndpoint,
            TestCases.MissingClaimTest.ThrowIfMissingRequest,
            ErrorResponse>(new()
            {
                TestProp = "xyz"
            });

        result?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result?.Errors.Should().NotBeNull();
        result?.Errors.Count.Should().Be(1);
        result?.Errors.Should().ContainKey("null-claim");
    }

    [Fact]
    public async Task ClaimMissingButDontThrow()
    {
        var (res, result) = await AdminClient.POSTAsync<
            TestCases.MissingClaimTest.DontThrowIfMissingEndpoint,
            TestCases.MissingClaimTest.DontThrowIfMissingRequest,
            string>(new()
            {
                TestProp = "xyz"
            });

        res?.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().Be("you sent xyz");
    }

    [Fact]
    public async Task HeaderMissing()
    {
        var (_, result) = await AdminClient.POSTAsync<
            TestCases.MissingHeaderTest.ThrowIfMissingEndpoint,
            TestCases.MissingHeaderTest.ThrowIfMissingRequest,
            ErrorResponse>(new()
            {
                TenantID = "abc"
            });

        result?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result?.Errors.Should().NotBeNull();
        result?.Errors.Count.Should().Be(1);
        result?.Errors.Should().ContainKey("TenantID");
    }

    [Fact]
    public async Task HeaderMissingButDontThrow()
    {
        var (res, result) = await AdminClient.POSTAsync<
            TestCases.MissingHeaderTest.DontThrowIfMissingEndpoint,
            TestCases.MissingHeaderTest.DontThrowIfMissingRequest,
            string>(new()
            {
                TenantID = "abc"
            });

        res?.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().Be("you sent abc");
    }

    [Fact]
    public async Task RouteValueReadingInEndpointWithoutRequest()
    {
        var (rsp, res) = await GuestClient.GETAsync<
            EmptyRequest,
            TestCases.RouteBindingInEpWithoutReq.Response>(
            "/api/test-cases/ep-witout-req-route-binding-test/09809/12", new());

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res!.CustomerID.Should().Be(09809);
        res!.OtherID.Should().Be(12);
    }

    [Fact]
    public async Task RouteValueReadingIsRequired()
    {
        var (rsp, res) = await GuestClient.GETAsync<
            EmptyRequest,
            ErrorResponse>(
            "/api/test-cases/ep-witout-req-route-binding-test/09809/lkjhlkjh", new());

        rsp?.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res?.Errors.Should().NotBeNull();
        res.Errors.Should().ContainKey("OtherID");
    }

    [Fact]
    public async Task RouteValueBinding()
    {
        var (rsp, res) = await GuestClient
            .POSTAsync<TestCases.RouteBindingTest.Request, TestCases.RouteBindingTest.Response>(
                "api/test-cases/route-binding-test/something/true/99/483752874564876/2232.12/123.45?Url=https://test.com&Custom=12",
                new()
                {
                    Bool = false,
                    DecimalNumber = 1,
                    Double = 1,
                    FromBody = "from body value",
                    Int = 1,
                    Long = 1,
                    String = "nothing",
                    Custom = new() { Value = 11111 }
                });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.String.Should().Be("something");
        res?.Bool.Should().Be(true);
        res?.Int.Should().Be(99);
        res?.Long.Should().Be(483752874564876);
        res?.Double.Should().Be(2232.12);
        res?.FromBody.Should().Be("from body value");
        res?.Decimal.Should().Be(123.45m);
        res?.Url.Should().Be("https://test.com/");
        res?.Custom.Value.Should().Be(12);
    }

    [Fact]
    public async Task RouteValueBindingFromQueryParams()
    {
        var (rsp, res) = await GuestClient
            .POSTAsync<TestCases.RouteBindingTest.Request, TestCases.RouteBindingTest.Response>(
                "api/test-cases/route-binding-test/something/true/99/483752874564876/2232.12/123.45/" +
                "?Bool=false&String=everything",
                new()
                {
                    Bool = false,
                    DecimalNumber = 1,
                    Double = 1,
                    FromBody = "from body value",
                    Int = 1,
                    Long = 1,
                    String = "nothing"
                });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.String.Should().Be("everything");
        res?.Bool.Should().BeFalse();
        res?.Int.Should().Be(99);
        res?.Long.Should().Be(483752874564876);
        res?.Double.Should().Be(2232.12);
        res?.FromBody.Should().Be("from body value");
        res?.Decimal.Should().Be(123.45m);
        res?.Blank.Should().BeNull();
    }

    [Fact]
    public async Task JsonArrayBindingToIEnumerableProps()
    {
        var (rsp, res) = await GuestClient
            .GETAsync<TestCases.JsonArrayBindingForIEnumerableProps.Request, TestCases.JsonArrayBindingForIEnumerableProps.Response>(
            "/api/test-cases/json-array-binding-for-ienumerable-props?" +
            "doubles=[123.45,543.21]&" +
            "dates=[\"2022-01-01\",\"2022-02-02\"]&" +
            "guids=[\"b01ec302-0adc-4a2b-973d-bbfe639ed9a5\",\"e08664a4-efd8-4062-a1e1-6169c6eac2ab\"]&" +
            "ints=[1,2,3]",
            new());

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.Doubles.Length.Should().Be(2);
        res?.Doubles[0].Should().Be(123.45);
        res?.Dates.Count.Should().Be(2);
        res?.Dates.First().Should().Be(DateTime.Parse("2022-01-01"));
        res?.Guids.Count.Should().Be(2);
        res?.Guids[0].Should().Be(Guid.Parse("b01ec302-0adc-4a2b-973d-bbfe639ed9a5"));
        res?.Ints.Count().Should().Be(3);
        res?.Ints.First().Should().Be(1);
    }

    [Fact]
    public async Task BindingFromAttributeUse()
    {
        var (rsp, res) = await GuestClient
            .POSTAsync<TestCases.RouteBindingTest.Request, TestCases.RouteBindingTest.Response>(
                "api/test-cases/route-binding-test/something/true/99/483752874564876/2232.12/123.45/" +
                "?Bool=false&String=everything&XBlank=256",
                new()
                {
                    Bool = false,
                    DecimalNumber = 1,
                    Double = 1,
                    FromBody = "from body value",
                    Int = 1,
                    Long = 1,
                    String = "nothing",
                    Blank = 1
                });

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.String.Should().Be("everything");
        res?.Bool.Should().BeFalse();
        res?.Int.Should().Be(99);
        res?.Long.Should().Be(483752874564876);
        res?.Double.Should().Be(2232.12);
        res?.FromBody.Should().Be("from body value");
        res?.Decimal.Should().Be(123.45m);
        res?.Blank.Should().Be(256);
    }

    [Fact]
    public async Task TestEventHandling()
    {
        var event1 = new NewItemAddedToStock { ID = 1, Name = "one", Quantity = 10 };
        var event2 = new NewItemAddedToStock { ID = 2, Name = "two", Quantity = 20 };
        var event3 = new NewItemAddedToStock { ID = 3, Name = "three", Quantity = 30 };

        await Event<NewItemAddedToStock>.PublishAsync(event3, Mode.WaitForAll);
        await Event<NewItemAddedToStock>.PublishAsync(event2, Mode.WaitForAny);
        await Event<NewItemAddedToStock>.PublishAsync(event1, Mode.WaitForNone);

        event1.ID.Should().Be(0);
        event2.ID.Should().Be(0);
        event3.ID.Should().Be(0);

        event1.Name.Should().Be("pass");
        event2.Name.Should().Be("pass");
        event3.Name.Should().Be("pass");
    }

    [Fact]
    public async Task RangeHandling()
    {
        var res = await RangeClient.GetStringAsync("api/test-cases/range");
        res.Should().Be("fghij");
    }

    [Fact]
    public async Task FileHandling()
    {
        using var imageContent = new ByteArrayContent(
            await new StreamContent(
                    File.OpenRead("test.png"))
                .ReadAsByteArrayAsync());
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

        using var form = new MultipartFormDataContent
        {
            {imageContent, "File", "test.png"},
            {new StringContent("500"), "Width"},
            {new StringContent("500"), "Height"}
        };

        var res = await AdminClient.PostAsync("api/uploads/image/save", form);

        using var md5Instance = MD5.Create();
        using var stream = await res.Content.ReadAsStreamAsync();
        var resMD5 = BitConverter.ToString(md5Instance.ComputeHash(stream)).Replace("-", "");

        resMD5.Should().Be("8A1F6A8E27D2E440280050DA549CBE3E");
    }

    [Fact]
    public async Task FileHandlingFileBinding()
    {
        using var imageContent1 = new ByteArrayContent(
            await new StreamContent(
                    File.OpenRead("test.png"))
                .ReadAsByteArrayAsync());
        imageContent1.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

        using var imageContent2 = new ByteArrayContent(
            await new StreamContent(
                    File.OpenRead("test.png"))
                .ReadAsByteArrayAsync());
        imageContent2.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

        using var form = new MultipartFormDataContent
        {
            {imageContent1, "File1", "test.png"},
            {imageContent2, "File2", "test.png"},
            {new StringContent("500"), "Width"},
            {new StringContent("500"), "Height"}
        };

        var res = await AdminClient.PostAsync("/api/uploads/image/save-typed", form);

        using var md5Instance = MD5.Create();
        using var stream = await res.Content.ReadAsStreamAsync();
        var resMD5 = BitConverter.ToString(md5Instance.ComputeHash(stream)).Replace("-", "");

        resMD5.Should().Be("8A1F6A8E27D2E440280050DA549CBE3E");
    }

    [Fact]
    public async Task PreProcessorsAreRunIfValidationFailuresOccur()
    {
        var (rsp, res) = await AdminClient.POSTAsync<
            TestCases.PreProcessorIsRunOnValidationFailure.Endpoint,
            TestCases.PreProcessorIsRunOnValidationFailure.Request,
            ErrorResponse>
        (new()
        {
            FailureCount = 0,
            FirstName = ""
        });

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Should().NotBeNull();
        res.Errors.Count.Should().Be(2);
        res.Errors["x"].First().Should().Be("blah");
    }

    [Fact]
    public async Task OnBeforeOnAfterValidation()
    {
        var (rsp, res) = await AdminClient.POSTAsync<
            TestCases.OnBeforeAfterValidationTest.Endpoint,
            TestCases.OnBeforeAfterValidationTest.Request,
            TestCases.OnBeforeAfterValidationTest.Response>(new()
            {
                Host = "blah",
                Verb = Http.DELETE
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Host.Should().Be("localhost");
    }

    [Fact]
    public async Task PreProcessorShortCircuitMissingHeader()
    {
        var (rsp, res) = await GuestClient.GETAsync<
            Sales.Orders.Retrieve.Endpoint,
            Sales.Orders.Retrieve.Request,
            ErrorResponse>(new() { OrderID = "order1" });

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Should().NotBeNull();
        res.Errors.Count.Should().Be(1);
        res.Errors.Should().ContainKey("MissingHeaders");
    }

    [Fact]
    public async Task PreProcessorShortCircuitWrongHeaderValue()
    {
        Func<Task> func = async () =>
        {
            await AdminClient.POSTAsync<
                Sales.Orders.Retrieve.Endpoint,
                Sales.Orders.Retrieve.Request,
                object>(new() { OrderID = "order1" });
        };

        await func.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PreProcessorShortCircuitHandlerExecuted()
    {
        var (rsp, res) = await CustomerClient.GETAsync<
            Sales.Orders.Retrieve.Endpoint,
            Sales.Orders.Retrieve.Request,
            ErrorResponse>(new() { OrderID = "order1" });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Message.Should().Be("ok!");
    }

    [Fact]
    public async Task PlainTextBodyModelBinding()
    {
        using var stringContent = new StringContent("this is the body content");
        stringContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");

        var rsp = await AdminClient.PostAsync("test-cases/plaintext/12345", stringContent);

        var res = await rsp.Content.ReadFromJsonAsync<TestCases.PlainTextRequestTest.Response>();

        res.BodyContent.Should().Be("this is the body content");
        res.Id.Should().Be(12345);
    }

    [Fact]
    public async Task GlobalRoutePrefixOverride()
    {
        using var stringContent = new StringContent("this is the body content");
        stringContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");

        var rsp = await AdminClient.PostAsync("/mobile/api/test-cases/global-prefix-override/12345", stringContent);

        var res = await rsp.Content.ReadFromJsonAsync<TestCases.PlainTextRequestTest.Response>();

        res.BodyContent.Should().Be("this is the body content");
        res.Id.Should().Be(12345);
    }

    [Fact]
    public async Task GETRequestWithRouteParameterAndReqDto()
    {
        var (rsp, res) = await CustomerClient.GETAsync<EmptyRequest, ErrorResponse>(
            "/api/sales/orders/retrieve/54321",
            new());

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Message.Should().Be("ok!");
    }

    [Fact]
    public async Task QueryParamReadingInEndpointWithoutRequest()
    {
        var (rsp, res) = await GuestClient.GETAsync<
            EmptyRequest,
            TestCases.RouteBindingInEpWithoutReq.Response>(
            "/api/test-cases/ep-witout-req-query-param-binding-test?customerId=09809&otherId=12", new());

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res!.CustomerID.Should().Be(09809);
        res!.OtherID.Should().Be(12);
    }

    [Fact]
    public async Task QueryParamReadingIsRequired()
    {
        var (rsp, res) = await GuestClient.GETAsync<
            EmptyRequest,
            ErrorResponse>(
            "/api/test-cases/ep-witout-req-query-param-binding-test?customerId=09809&otherId=lkjhlkjh", new());

        rsp?.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res?.Errors.Should().ContainKey("OtherID");
    }

    [Fact]
    public async Task ThrottledGlobalResponse()
    {
        HttpResponseMessage? response = null;

        for (int i = 0; i < 5; i++)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get
            };
            request.Headers.Add("X-Custom-Throttle-Header", "test");
            request.RequestUri =
                new Uri("api/test-cases/global-throttle-error-response?customerId=09809&otherId=12",
                    UriKind.Relative);
            response = await GuestClient.SendAsync(request);
        }

        var responseContent = await response!.Content.ReadAsStringAsync();
        responseContent.Should().Be("Custom Error Response");
        response!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task NotThrottledGlobalResponse()
    {
        HttpResponseMessage? response = null;

        for (int i = 0; i < 3; i++)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get
            };
            request.Headers.Add("X-Custom-Throttle-Header", "test-2");
            request.RequestUri =
                new Uri("api/test-cases/global-throttle-error-response?customerId=09809&otherId=12",
                    UriKind.Relative);
            response = await GuestClient.SendAsync(request);
        }

        response!.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}