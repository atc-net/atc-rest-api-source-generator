namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class EndpointDefinitionExtractorTests
{
    // ========== GetCommonPathPrefix - Empty input ==========
    [Fact]
    public void GetCommonPathPrefix_EmptyList_ReturnsEmpty()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix([]);
        Assert.Equal(string.Empty, result);
    }

    // ========== GetCommonPathPrefix - Single path ==========
    [Theory]
    [InlineData("/pets", "/pets")]
    [InlineData("/pets/{petId}", "/pets")]
    [InlineData("/users", "/users")]
    [InlineData("/users/{userId}", "/users")]
    [InlineData("/health", "/health")]
    [InlineData("/accounts", "/accounts")]
    [InlineData("/admin", "/admin")]
    [InlineData("/orders", "/orders")]
    [InlineData("/reports", "/reports")]
    [InlineData("/notifications", "/notifications")]
    [InlineData("/categories", "/categories")]
    [InlineData("/products", "/products")]
    [InlineData("/webhooks", "/webhooks")]
    [InlineData("/exports", "/exports")]
    [InlineData("/colors", "/colors")]
    [InlineData("/coordinates", "/coordinates")]
    [InlineData("/documents", "/documents")]
    public void GetCommonPathPrefix_SingleSimplePath_ReturnsFirstSegment(
        string path,
        string expected)
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix([path]);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/pet/findByStatus", "/pet")]
    [InlineData("/pet/findByTags", "/pet")]
    [InlineData("/pet/{petId}/uploadImage", "/pet")]
    [InlineData("/store/inventory", "/store")]
    [InlineData("/store/order", "/store")]
    [InlineData("/store/order/{orderId}", "/store")]
    [InlineData("/user/createWithList", "/user")]
    [InlineData("/user/login", "/user")]
    [InlineData("/user/logout", "/user")]
    [InlineData("/admin/settings", "/admin")]
    [InlineData("/me/profile", "/me")]
    [InlineData("/public/health", "/public")]
    [InlineData("/external/payment", "/external")]
    [InlineData("/reports/generate", "/reports")]
    [InlineData("/accounts/paginated", "/accounts")]
    [InlineData("/accounts/async-enumerable", "/accounts")]
    [InlineData("/coordinates/list", "/coordinates")]
    [InlineData("/orders/{orderId}/tracking", "/orders")]
    public void GetCommonPathPrefix_SingleMultiSegmentPath_ReturnsFirstSegmentOnly(
        string path,
        string expected)
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix([path]);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/edge-provider/devices/{providerDeviceId}/opc-ua/command/scan", "/edge-provider")]
    [InlineData("/device-management/devices", "/device-management")]
    [InlineData("/insights/devices", "/insights")]
    public void GetCommonPathPrefix_SingleDeepPath_ReturnsFirstSegmentOnly(
        string path,
        string expected)
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix([path]);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/files/form-data/multiFile", "/files")]
    [InlineData("/files/form-data/singleFile", "/files")]
    [InlineData("/tests/create/create-empty", "/tests")]
    [InlineData("/tests/create/create-location-body", "/tests")]
    public void GetCommonPathPrefix_SingleThreeSegmentPath_ReturnsFirstSegmentOnly(
        string path,
        string expected)
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix([path]);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetCommonPathPrefix_SinglePathParameterOnly_ReturnsSlash()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(["/{id}"]);
        Assert.Equal("/", result);
    }

    [Fact]
    public void GetCommonPathPrefix_SingleSlash_ReturnsSlash()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(["/"]);
        Assert.Equal("/", result);
    }

    // ========== GetCommonPathPrefix - Multiple paths with common root ==========
    [Fact]
    public void GetCommonPathPrefix_PetsGroup_ReturnsPets()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/pets",
            "/pets/{petId}",
        ]);

        Assert.Equal("/pets", result);
    }

    [Fact]
    public void GetCommonPathPrefix_UsersGroupWithSubpaths_ReturnsUsers()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/users",
            "/users/{userId}",
        ]);

        Assert.Equal("/users", result);
    }

    [Fact]
    public void GetCommonPathPrefix_AccountsGroupMultipleVariants_ReturnsAccounts()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/accounts",
            "/accounts/{accountId}",
            "/accounts/paginated",
            "/accounts/async-enumerable",
        ]);

        Assert.Equal("/accounts", result);
    }

    [Fact]
    public void GetCommonPathPrefix_PetStoreFullPetGroup_ReturnsPet()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/pet",
            "/pet/{petId}",
            "/pet/findByStatus",
            "/pet/findByTags",
            "/pet/{petId}/uploadImage",
        ]);

        Assert.Equal("/pet", result);
    }

    [Fact]
    public void GetCommonPathPrefix_StoreGroup_ReturnsStore()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/store/inventory",
            "/store/order",
            "/store/order/{orderId}",
        ]);

        Assert.Equal("/store", result);
    }

    [Fact]
    public void GetCommonPathPrefix_UserGroup_ReturnsUser()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/user",
            "/user/{username}",
            "/user/createWithList",
            "/user/login",
            "/user/logout",
        ]);

        Assert.Equal("/user", result);
    }

    [Fact]
    public void GetCommonPathPrefix_FilesGroup_ReturnsCommonPrefix()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/files/form-data/multiFile",
            "/files/form-data/singleFile",
        ]);

        Assert.Equal("/files/form-data", result);
    }

    [Fact]
    public void GetCommonPathPrefix_NotificationsSubscriptionsGroup_ReturnsFullCommon()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/notifications/subscriptions",
            "/notifications/subscriptions/{subscriptionId}",
        ]);

        Assert.Equal("/notifications/subscriptions", result);
    }

    // ========== GetCommonPathPrefix - Multiple paths with param breaking ==========
    [Fact]
    public void GetCommonPathPrefix_MultiplePathsStopsAtParam()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/orders/{orderId}/tracking",
            "/orders/{orderId}/items",
        ]);

        Assert.Equal("/orders", result);
    }

    // ========== GetCommonPathPrefix - Multiple paths no common prefix ==========
    [Fact]
    public void GetCommonPathPrefix_NoCommonPrefix_ReturnsSlash()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/devices",
            "/insights",
        ]);

        Assert.Equal("/", result);
    }

    [Fact]
    public void GetCommonPathPrefix_MixedGroupNoCommon_ReturnsSlash()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/admin/settings",
            "/orders",
            "/reports",
        ]);

        Assert.Equal("/", result);
    }

    // ========== GetCommonPathPrefix - API-versioned paths ==========
    [Fact]
    public void GetCommonPathPrefix_ApiVersionedPaths_ReturnsFullCommonPrefix()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/api/v1/charge-points",
            "/api/v1/charge-points/{chargePointId}",
        ]);

        Assert.Equal("/api/v1/charge-points", result);
    }

    [Fact]
    public void GetCommonPathPrefix_ApiVersionedPathsStopsAtParam()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/api/v1/charge-points/{chargePointId}/logs",
            "/api/v1/charge-points/{chargePointId}/reboot",
        ]);

        Assert.Equal("/api/v1/charge-points", result);
    }

    [Fact]
    public void GetCommonPathPrefix_ApiVersionedWithMapSuffix()
    {
        var result = EndpointDefinitionExtractor.GetCommonPathPrefix(
        [
            "/api/v1/charge-points",
            "/api/v1/charge-points/{chargePointId}",
            "/api/v1/charge-points/map",
        ]);

        Assert.Equal("/api/v1/charge-points", result);
    }
}