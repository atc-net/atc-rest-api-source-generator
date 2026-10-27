namespace NexusSample.Api.Domain.Repositories;

/// <summary>
/// A small in-memory device tree (one Contoso edge gateway with two child devices) that answers
/// the device, device-management and insights operations.
/// </summary>
public sealed class DeviceInMemoryRepository
{
    private static readonly Guid GatewayId = new("6d2f0f0e-5c0a-4c37-9a2c-0a5b1f6a0001");
    private static readonly Guid ChargePointId = new("6d2f0f0e-5c0a-4c37-9a2c-0a5b1f6a0002");
    private static readonly Guid SensorId = new("6d2f0f0e-5c0a-4c37-9a2c-0a5b1f6a0003");

    private readonly List<Device> devices =
    [
        new Device(
            Id: GatewayId,
            ParentDeviceId: null,
            DeviceType: DeviceType.IotEdgeGateway,
            BrandName: "Contoso",
            ModelName: "Edge Gateway EG-200",
            SerialNumber: "CT-EG200-000175",
            ProviderData: new ProviderData(ProviderType.Edge, "contoso-edge-hub", "plant-north", DeviceType.IotEdgeGateway, "eg200-000175"),
            Properties: [new DeviceProperty("firmware", "string", "4.2.1"), new DeviceProperty("uptime", "integer", "86400", "s")]),
        new Device(
            Id: ChargePointId,
            ParentDeviceId: GatewayId,
            DeviceType: DeviceType.ElectricVehicleChargingStation,
            BrandName: "Contoso",
            ModelName: "ChargePoint CP-22",
            SerialNumber: "CT-CP22-004410",
            ProviderData: new ProviderData(ProviderType.ChargePointManagementSystem, "contoso-cpms", "bay-07", DeviceType.ElectricVehicleChargingStation, "cp22-004410"),
            Properties: [new DeviceProperty("maxPower", "number", "22", "kW")]),
        new Device(
            Id: SensorId,
            ParentDeviceId: GatewayId,
            DeviceType: DeviceType.TelemetryDataSource,
            BrandName: "Contoso",
            ModelName: "Temperature Sensor TS-10",
            SerialNumber: "CT-TS10-120933",
            ProviderData: new ProviderData(ProviderType.Direct, "contoso-opc-ua", "line-3", DeviceType.TelemetryDataSource, "ts10-120933"),
            Properties: [new DeviceProperty("samplingInterval", "integer", "30", "s")]),
    ];

    public async IAsyncEnumerable<DeviceIdentity> GetIdentitiesStreaming(
        string? filter,
        DeviceType? deviceType,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var device in Filter(devices, filter).Where(device => deviceType is null || device.DeviceType == deviceType))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return ToIdentity(device);
            await Task.Yield();
        }
    }

    public DeviceDetails? GetDetails(Guid id)
    {
        var device = devices.Find(candidate => candidate.Id == id);
        if (device is null)
        {
            return null;
        }

        var ancestors = new List<AncestorIdentity>();
        var parent = devices.Find(candidate => candidate.Id == device.ParentDeviceId);
        while (parent is not null)
        {
            ancestors.Insert(0, new AncestorIdentity(Id: parent.Id, DeviceType: parent.DeviceType, DisplayName: DisplayName(parent), Level: 0));
            parent = devices.Find(candidate => candidate.Id == parent.ParentDeviceId);
        }

        for (var level = 0; level < ancestors.Count; level++)
        {
            ancestors[level] = ancestors[level] with { Level = level };
        }

        var children = devices
            .Where(candidate => candidate.ParentDeviceId == id)
            .Select(ToIdentity)
            .ToList();

        return new DeviceDetails(device, new DeviceHierarchy(ToIdentity(device), ancestors, children));
    }

    public bool IsEdgeGateway(Guid id)
        => devices.Find(candidate => candidate.Id == id)?.DeviceType == DeviceType.IotEdgeGateway;

    public bool Exists(Guid id)
        => devices.Exists(candidate => candidate.Id == id);

    public static List<KeyValue> GetDeviceTypes()
        => Enum.GetValues<DeviceType>()
            .Select(deviceType => new KeyValue(deviceType.ToWireValue(), deviceType.ToString()))
            .ToList();

    public IAsyncEnumerable<PaginationResult<DeviceManagementDevice>> GetDeviceManagementPagesStreaming(
        int pageSize,
        int pageIndex,
        string? filter,
        CancellationToken cancellationToken = default)
    {
        var items = Filter(devices, filter)
            .Select(device => new DeviceManagementDevice(Id: device.Id, ParentDeviceId: device.ParentDeviceId, DeviceType: device.DeviceType, BrandName: device.BrandName, ModelName: device.ModelName, SerialNumber: device.SerialNumber, ProviderData: device.ProviderData, Properties: device.Properties))
            .ToList();

        return PagesStreaming(items, pageSize, pageIndex, cancellationToken);
    }

    public IAsyncEnumerable<PaginationResult<InsightDevice>> GetInsightPagesStreaming(
        int pageSize,
        int pageIndex,
        string? filter,
        CancellationToken cancellationToken = default)
    {
        var site = new IdValue(new Guid("6d2f0f0e-5c0a-4c37-9a2c-0a5b1f6a1001"), "Contoso Plant North");
        var customer = new IdValue(new Guid("6d2f0f0e-5c0a-4c37-9a2c-0a5b1f6a1002"), "Contoso Manufacturing");

        var items = Filter(devices, filter)
            .Select(device => new InsightDevice(
                Id: device.Id,
                Name: DisplayName(device),
                BrandName: device.BrandName,
                ModelName: device.ModelName,
                SerialNumber: device.SerialNumber,
                DeviceType: new KeyValue(device.DeviceType.ToWireValue(), device.DeviceType.ToString()),
                DeviceState: device.DeviceType == DeviceType.ElectricVehicleChargingStation ? DeviceState.Attention : DeviceState.Neutral,
                Customer: customer,
                Site: site))
            .ToList();

        return PagesStreaming(items, pageSize, pageIndex, cancellationToken);
    }

    private static IEnumerable<Device> Filter(
        IEnumerable<Device> source,
        string? filter)
        => string.IsNullOrWhiteSpace(filter)
            ? source
            : source.Where(device => DisplayName(device).Contains(filter, StringComparison.OrdinalIgnoreCase));

    private static string DisplayName(Device device)
        => $"{device.BrandName} {device.ModelName}";

    private static DeviceIdentity ToIdentity(Device device)
        => new(device.Id, device.DeviceType, DisplayName(device));

    private static async IAsyncEnumerable<PaginationResult<T>> PagesStreaming<T>(
        IReadOnlyList<T> items,
        int pageSize,
        int pageIndex,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var skip = pageIndex * pageSize;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = items.Skip(skip).Take(pageSize).ToList();
            skip += pageSize;

            var continuationToken = skip < items.Count
                ? skip.ToString(CultureInfo.InvariantCulture)
                : null;

            yield return new PaginationResult<T>(PageSize: pageSize, ContinuationToken: continuationToken, Items: page);
            await Task.Yield();
        }
        while (skip < items.Count);
    }
}