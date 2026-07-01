using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;
using AIStudyHub.Business.Options;
using AIStudyHub.Business.Services;

namespace AIStudyHub.Tests.Services;

public class VnPayServiceTests
{
    private readonly Mock<ILogger<VnPayService>> _loggerMock;
    private readonly IOptions<VnPayOptions> _options;
    private readonly VnPayService _service;

    public VnPayServiceTests()
    {
        _loggerMock = new Mock<ILogger<VnPayService>>();
        
        var vnPayOptions = new VnPayOptions
        {
            TmnCode = "TESTCODE",
            HashSecret = "TESTSECRET1234567890TESTSECRET12",
            BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            ReturnUrl = "https://localhost:5001/api/Payment/vnpay-return"
        };
        _options = Microsoft.Extensions.Options.Options.Create(vnPayOptions);
        
        _service = new VnPayService(_options, _loggerMock.Object);
    }

    [Fact]
    public void CreatePaymentUrl_ReturnsValidUrlWithSignature()
    {
        // Arrange
        var ip = "127.0.0.1";
        var paymentId = Guid.NewGuid();
        var amount = 10000m;
        var orderInfo = "Test Payment";

        // Act
        var url = _service.CreatePaymentUrl(ip, paymentId, amount, orderInfo);

        // Assert
        Assert.NotNull(url);
        Assert.StartsWith("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?", url);
        Assert.Contains("vnp_Amount=1000000", url); // 10000 * 100
        Assert.Contains($"vnp_TxnRef={paymentId}", url);
        Assert.Contains("vnp_SecureHash=", url);
    }

    [Fact]
    public void ValidateSignature_ValidSignature_ReturnsTrue()
    {
        // Arrange
        // We will generate a signature using the same logic just for testing
        var paymentId = Guid.NewGuid();
        var amount = 10000m;
        var orderInfo = "Test Payment";
        
        // This generates a full URL with SecureHash
        var url = _service.CreatePaymentUrl("127.0.0.1", paymentId, amount, orderInfo);
        
        // Parse the query string to IQueryCollection
        var uri = new Uri(url);
        var queryDictionary = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        var queryCollection = new QueryCollection(queryDictionary);

        // Act
        var isValid = _service.ValidateSignature(queryCollection);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateSignature_InvalidSignature_ReturnsFalse()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var url = _service.CreatePaymentUrl("127.0.0.1", paymentId, 10000m, "Test Payment");
        
        var uri = new Uri(url);
        var queryDictionary = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        
        // Tamper with the amount
        queryDictionary["vnp_Amount"] = "9999999";
        var queryCollection = new QueryCollection(queryDictionary);

        // Act
        var isValid = _service.ValidateSignature(queryCollection);

        // Assert
        Assert.False(isValid);
    }
}
