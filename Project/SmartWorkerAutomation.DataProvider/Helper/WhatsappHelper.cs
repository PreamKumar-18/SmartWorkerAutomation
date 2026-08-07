using SmartWorkerAutomation.Common.Common;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SmartWorkerAutomation.DataProvider.Helper;

public static class WhatsappHelper
{
    private static string FormatPhoneNumber(string phoneNumber)
    {
        // Remove any non-digit characters
        var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());
        // Assuming the country code is '91' for India, add it if not present
        if (!digitsOnly.StartsWith("91"))
        {
            digitsOnly = "91" + digitsOnly;
        }
        return digitsOnly;
    }

    public static async Task<bool> SendInvoice(string mobileNumber, string message, string pdfPath, WasenderApiSettings settings)
    {
        try
        {
            var fileName = Path.GetFileName(pdfPath);

            var payload = new
            {
                to = FormatPhoneNumber(mobileNumber),
                text = message,
                documentUrl = $"{settings.PdfUrl}{pdfPath}",
                fileName = fileName
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using HttpClient _httpClient = new HttpClient();
            {

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", settings.ApiKey);

                var response = await _httpClient.PostAsync(settings.BaseUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return false;
                }
            }
        }
        catch (Exception)
        {
            return false;
        }
    }


    public static async Task<bool> SendTextMessage(string mobileNumber, string message, WasenderApiSettings settings)
    {
        try
        {
            // ✅ Build request payload (text-only)
            var payload = new
            {
                to = FormatPhoneNumber(mobileNumber), // fallback number if needed
                text = message
            };

            // ✅ Convert to JSON
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using HttpClient _httpClient = new HttpClient();
            {
                // ✅ Add authorization header
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", settings.ApiKey);

                // ✅ Send POST request to the WhatsApp API endpoint
                var response = await _httpClient.PostAsync(settings.BaseUrl, content);

                // ✅ Handle success or failure
                if (response.IsSuccessStatusCode)
                {
                    //_logger.LogInformation("WhatsApp message sent successfully to {Mobile}", mobileNumber);
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    //_logger.LogError("Failed to send WhatsApp message to {Mobile}. Error: {Error}", mobileNumber, error);
                    return false;
                }
            }
        }
        catch (Exception)
        {
            //_logger.LogError("Exception while sending WhatsApp message: {Error}", ex.Message);
            return false;
        }
    }

}
