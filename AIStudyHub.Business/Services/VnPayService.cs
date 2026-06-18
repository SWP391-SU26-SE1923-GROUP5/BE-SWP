using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AIStudyHub.Business.Services;

public sealed class VnPayService : IVnPayService
{
    private readonly VnPayOptions _options;

    public VnPayService(IOptions<VnPayOptions> options)
    {
        _options = options.Value;
    }

    public string CreatePaymentUrl(HttpContext context, Guid paymentId, decimal amount, string orderInfo)
    {
        var tick = DateTime.UtcNow.Ticks.ToString();
        var vnpayData = new SortedList<string, string>(new VnPayCompare())
        {
            { "vnp_Version", "2.1.0" },
            { "vnp_Command", "pay" },
            { "vnp_TmnCode", _options.TmnCode },
            { "vnp_Amount", ((long)(amount * 100)).ToString() }, // Amount in VND, multiplied by 100
            { "vnp_CreateDate", DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss") },
            { "vnp_CurrCode", "VND" },
            { "vnp_IpAddr", GetIpAddress(context) },
            { "vnp_Locale", "vn" },
            { "vnp_OrderInfo", orderInfo },
            { "vnp_OrderType", "other" },
            { "vnp_ReturnUrl", _options.ReturnUrl },
            { "vnp_TxnRef", paymentId.ToString() } // Reference to Payment record
        };

        var queryString = BuildQueryString(vnpayData);
        var signData = queryString;
        var vnpSecureHash = HmacSHA512(_options.HashSecret, signData);
        
        return $"{_options.BaseUrl}?{queryString}&vnp_SecureHash={vnpSecureHash}";
    }

    public bool ValidateSignature(IQueryCollection query)
    {
        var vnpayData = new SortedList<string, string>(new VnPayCompare());
        string vnp_SecureHash = string.Empty;

        foreach (var (key, value) in query)
        {
            if (string.IsNullOrEmpty(key) || key.StartsWith("vnp_SecureHash"))
            {
                if (key == "vnp_SecureHash")
                {
                    vnp_SecureHash = value.ToString();
                }
                continue;
            }

            vnpayData.Add(key, value.ToString());
        }

        var signData = BuildQueryString(vnpayData);
        var checkSum = HmacSHA512(_options.HashSecret, signData);

        return checkSum.Equals(vnp_SecureHash, StringComparison.InvariantCultureIgnoreCase);
    }

    private static string GetIpAddress(HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        return ipAddress;
    }

    private static string BuildQueryString(SortedList<string, string> requestData)
    {
        var data = new StringBuilder();
        foreach (var kvp in requestData)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                data.Append(WebUtility.UrlEncode(kvp.Key) + "=" + WebUtility.UrlEncode(kvp.Value) + "&");
            }
        }

        if (data.Length > 0)
        {
            data.Remove(data.Length - 1, 1);
        }

        return data.ToString();
    }

    private static string HmacSHA512(string key, string inputData)
    {
        var hash = new StringBuilder();
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);
        using (var hmac = new HMACSHA512(keyBytes))
        {
            byte[] hashValue = hmac.ComputeHash(inputBytes);
            foreach (var theByte in hashValue)
            {
                hash.Append(theByte.ToString("x2"));
            }
        }
        return hash.ToString();
    }
}

public class VnPayCompare : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        var compareInfo = CompareInfo.GetCompareInfo("en-US");
        return compareInfo.Compare(x, y, CompareOptions.Ordinal);
    }
}
