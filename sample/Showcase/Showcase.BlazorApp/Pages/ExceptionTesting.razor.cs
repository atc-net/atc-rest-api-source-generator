namespace Showcase.BlazorApp.Pages;

public partial class ExceptionTesting
{
    private static readonly ExceptionTestInfo[] ExceptionTests =
    [
        new(0, "No Exception", "Success response - no exception thrown", 200),
        new(1, "Exception", "General Exception - 500 Internal Server Error", 500),
        new(2, "ArgumentException", "ArgumentException - 400 Bad Request", 400),
        new(3, "ArgumentNullException", "ArgumentNullException - 400 Bad Request", 400),
        new(4, "InvalidOperationException", "InvalidOperationException - 409 Conflict", 409),
        new(5, "UnauthorizedAccessException", "UnauthorizedAccessException - 401 Unauthorized", 401),
        new(6, "NotImplementedException", "NotImplementedException - 501 Not Implemented", 501),
        new(7, "TimeoutException", "TimeoutException - 504 Gateway Timeout", 504),
        new(8, "ValidationException", "ValidationException - 400 Bad Request", 400),
        new(9, "KeyNotFoundException", "KeyNotFoundException - 500 Internal Server Error (default)", 500),
        new(10, "NullReferenceException", "NullReferenceException - 500 Internal Server Error", 500),
    ];

    private readonly List<TestResult> testResults = [];

    private bool isLoading;
    private int currentTestCode = -1;

    [Inject]
    private GatewayService Gateway { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    private async Task RunTestAsync(int code)
    {
        isLoading = true;
        currentTestCode = code;
        StateHasChanged();

        try
        {
            var testInfo = ExceptionTests.First(t => t.Code == code);
            var (isSuccess, actualStatus, message, _) = await Gateway
                .TestExceptionAsync(code)
                .ConfigureAwait(false);

            var result = new TestResult(
                DateTime.Now,
                code,
                testInfo.ExceptionType,
                actualStatus,
                testInfo.ExpectedStatus,
                actualStatus == testInfo.ExpectedStatus,
                message);

            testResults.Insert(0, result);

            if (result.StatusMatched)
            {
                Snackbar.Add(
                    $"Code {code}: Status {actualStatus} matches expected!",
                    Severity.Success);
            }
            else
            {
                Snackbar.Add(
                    $"Code {code}: Status {actualStatus} does NOT match expected {testInfo.ExpectedStatus}!",
                    Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(
                $"Error running test: {ex.Message}",
                Severity.Error);
        }
        finally
        {
            isLoading = false;
            currentTestCode = -1;
            StateHasChanged();
        }
    }

    private async Task RunAllTestsAsync()
    {
        isLoading = true;
        currentTestCode = -1;
        StateHasChanged();

        try
        {
            foreach (var test in ExceptionTests)
            {
                currentTestCode = test.Code;
                StateHasChanged();

                var (isSuccess, actualStatus, message, _) = await Gateway
                    .TestExceptionAsync(test.Code)
                    .ConfigureAwait(false);

                var result = new TestResult(
                    DateTime.Now,
                    test.Code,
                    test.ExceptionType,
                    actualStatus,
                    test.ExpectedStatus,
                    actualStatus == test.ExpectedStatus,
                    message);

                testResults.Insert(0, result);
                StateHasChanged();

                // Small delay between tests for visibility
                await Task.Delay(100).ConfigureAwait(false);
            }

            var passed = testResults.Take(ExceptionTests.Length).Count(r => r.StatusMatched);
            var total = ExceptionTests.Length;
            Snackbar.Add(
                $"Tests complete: {passed}/{total} passed",
                passed == total
                    ? Severity.Success
                    : Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add(
                $"Error running tests: {ex.Message}",
                Severity.Error);
        }
        finally
        {
            isLoading = false;
            currentTestCode = -1;
            StateHasChanged();
        }
    }

    private void ClearResults()
    {
        testResults.Clear();
        StateHasChanged();
    }

    private static Color GetExpectedStatusColor(int status)
        => status switch
        {
            200 => Color.Success,
            400 => Color.Warning,
            401 => Color.Error,
            404 => Color.Warning,
            409 => Color.Warning,
            500 => Color.Error,
            501 => Color.Error,
            504 => Color.Error,
            _ => Color.Default,
        };

    private static Color GetStatusColor(int status)
        => status switch
        {
            >= 200 and < 300 => Color.Success,
            >= 400 and < 500 => Color.Warning,
            >= 500 => Color.Error,
            _ => Color.Default,
        };

    private sealed record ExceptionTestInfo(int Code, string ExceptionType, string Description, int ExpectedStatus);

    private sealed record TestResult(
        DateTime Timestamp,
        int Code,
        string ExceptionType,
        int ActualStatus,
        int ExpectedStatus,
        bool StatusMatched,
        string Message);
}