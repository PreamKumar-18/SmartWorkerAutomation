using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SmartWorkerAutomation.Common.Automation;
using Microsoft.Extensions.Configuration;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Backend equivalent of n8n's "Build Prompt1" -> "OpenAI Classify1" ->
/// "Parse & Validate1" chain from WF: Reply Processor (Classify): builds the
/// exact same system/user prompt, POSTs to OpenAI chat completions with the
/// same retry policy (5 attempts, 5s between, 60s timeout), then applies the
/// same deterministic validation to whatever the model returns - the model
/// never writes to the database directly, this validation layer decides
/// what's actually usable.
/// </summary>
public class ReplyClassificationService : IReplyClassificationService
{
    private static readonly string[] AllowedIntents =
    [
        "promise_to_pay", "partial_payment", "already_paid", "dispute",
        "wrong_recipient", "opt_out", "question", "acknowledgement", "unclear"
    ];

    private const int MaxAttempts = 5;
    private static readonly TimeSpan WaitBetweenAttempts = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

    private const string SystemPrompt = """
        You classify a customer's reply to a payment or delivery reminder.
        Return ONLY a JSON object with exactly these keys:
          intent          one of: promise_to_pay, partial_payment, already_paid, dispute, wrong_recipient, opt_out, question, acknowledgement, unclear
          promised_date   the date (YYYY-MM-DD) the customer commits to pay/deliver, resolved relative to TODAY; null if none
          promised_amount a number if the customer states a specific amount; null otherwise
          confidence      your confidence 0.0 to 1.0
          reasoning       one short sentence

        Definitions:
          promise_to_pay   commits to pay/deliver by a date
          partial_payment  commits to pay only part, or in instalments
          already_paid     claims they have already paid
          dispute          disputes the amount, item, or that they owe it
          wrong_recipient  says this is not for them / wrong number
          opt_out          asks to stop being contacted
          question         asks a question or requests a document
          acknowledgement  a simple ack with no commitment ("ok", "thanks", "noted")
          unclear          cannot be determined

        Output JSON only. No text before or after.
        """;

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public ReplyClassificationService(HttpClient httpClient, IConfiguration configuration)
    {
        _apiKey = RequireConfig(configuration, "OpenAI:ApiKey");

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
    }

    /// <summary>
    /// Same pattern as WhatsAppService/EmailService's RequireConfig - a
    /// plain <c>?? throw</c> only catches a missing key, not one present
    /// but blank, so this also rejects "".
    /// </summary>
    private static string RequireConfig(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} not configured.");
        }
        return value;
    }

    public async Task<ReplyClassificationResult> ClassifyAsync(ReplyClassificationInput input, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var userMessage = BuildUserMessage(input, today);

        var requestBody = new
        {
            model = "gpt-4o-mini",
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userMessage },
            },
        };

        var content = await SendWithRetryAsync(requestBody, cancellationToken);
        return ParseAndValidate(content, today);
    }

    private static string BuildUserMessage(ReplyClassificationInput input, DateOnly today)
    {
        var reference = input.ReferenceLabel ?? string.Empty;
        var amountPart = input.Amount is { } amount ? $" (amount: {amount})" : string.Empty;
        var dayOverdueText = input.DayOverdue?.ToString() ?? "n/a";

        return
            $"TODAY: {today:yyyy-MM-dd}\n" +
            $"Category: {input.CategoryName ?? string.Empty}\n" +
            $"Reminder was about: {reference}{amountPart} - days overdue: {dayOverdueText}\n" +
            $"Customer reply:\n\"\"\"\n{input.BodyText}\n\"\"\"";
    }

    private async Task<string?> SendWithRetryAsync(object requestBody, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(RequestTimeout);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                {
                    Content = JsonContent.Create(requestBody),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("choices", out var choices)
                        && choices.ValueKind == JsonValueKind.Array
                        && choices.GetArrayLength() > 0
                        && choices[0].TryGetProperty("message", out var message)
                        && message.TryGetProperty("content", out var contentProp))
                    {
                        return contentProp.GetString();
                    }

                    return null;
                }

                lastException = new HttpRequestException($"OpenAI returned {(int)response.StatusCode}: {body}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(WaitBetweenAttempts, cancellationToken);
            }
        }

        // Throw after exhausting retries rather than silently returning
        // "unclear": n8n's OpenAI HTTP node has no onError:
        // continueRegularOutput, so a permanent failure there aborts the
        // *entire* execution (nothing in that batch gets inserted, and
        // every item - including ones that classified fine - stays
        // eligible for the next 60s cycle, since none of them get a
        // reply_intents row). Throwing here and letting the caller
        // catch/skip per-record is actually safer: only the failed record
        // is retried next cycle, not the whole batch.
        if (lastException is not null)
        {
            throw lastException;
        }

        return null;
    }

    private static ReplyClassificationResult ParseAndValidate(string? content, DateOnly today)
    {
        JsonElement? parsed = null;
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                parsed = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                parsed = null;
            }
        }

        var intent = "unclear";
        if (parsed is { } p && p.TryGetProperty("intent", out var intentProp) && intentProp.ValueKind == JsonValueKind.String)
        {
            var candidate = intentProp.GetString();
            if (candidate is not null && AllowedIntents.Contains(candidate))
            {
                intent = candidate;
            }
        }

        DateOnly? promisedDate = null;
        if (parsed is { } p1 && p1.TryGetProperty("promised_date", out var dateProp) && dateProp.ValueKind == JsonValueKind.String)
        {
            promisedDate = ValidateDate(dateProp.GetString(), today);
        }

        decimal? promisedAmount = null;
        if (parsed is { } p2 && p2.TryGetProperty("promised_amount", out var amountProp))
        {
            promisedAmount = ValidateAmount(amountProp);
        }

        double confidence = 0;
        if (parsed is { } p3 && p3.TryGetProperty("confidence", out var confProp))
        {
            confidence = ClampConfidence(confProp);
        }

        // llm_raw = the model's own parsed JSON (unvalidated), same as n8n
        // storing JSON.stringify(parsed || { error: 'unparseable' }) -
        // deliberately NOT the post-validation intent/date/amount above.
        var rawJson = parsed is { } raw ? raw.GetRawText() : "{\"error\":\"unparseable\"}";

        return new ReplyClassificationResult(intent, promisedDate, promisedAmount, confidence, "gpt-4o-mini", rawJson);
    }

    private static DateOnly? ValidateDate(string? value, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateOnly.TryParse(value, out var parsed))
        {
            return null;
        }

        var maxDate = today.AddDays(90);
        return parsed >= today && parsed <= maxDate ? parsed : null;
    }

    private static decimal? ValidateAmount(JsonElement value)
    {
        decimal? amount = value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var n) => n,
            JsonValueKind.String when decimal.TryParse(value.GetString(), out var n) => n,
            _ => null,
        };

        return amount is > 0 ? amount : null;
    }

    private static double ClampConfidence(JsonElement value)
    {
        double? number = value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var n) => n,
            JsonValueKind.String when double.TryParse(value.GetString(), out var n) => n,
            _ => null,
        };

        if (number is not { } n2 || double.IsNaN(n2) || double.IsInfinity(n2))
        {
            return 0;
        }

        return Math.Max(0, Math.Min(1, n2));
    }
}
