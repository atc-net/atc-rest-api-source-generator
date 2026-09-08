namespace Eloverblik.ThirdPartyApi.Client.Tests.TestDoubles;

/// <summary>
/// Factory helpers for generated models.
/// <para>
/// ⚠️ Generated models are positional records where <b>no</b> parameter has a default value,
/// so a test that only cares about three fields still has to pass all 23 arguments.
/// This helper hides that noise. See issues/client-testing.md (Phase 2 follow-up).
/// </para>
/// </summary>
public static class ModelBuilder
{
    public static MeteringPointThirdPartyDto MeteringPoint(
        string meteringPointId,
        string streetName = "Hovedgaden",
        string buildingNumber = "1")
        => new(
            MeteringPointId: meteringPointId,
            TypeOfMp: "E17",
            AccessFrom: null,
            AccessTo: null,
            StreetCode: null,
            StreetName: streetName,
            BuildingNumber: buildingNumber,
            FloorId: null,
            RoomId: null,
            Postcode: null,
            CityName: null,
            CitySubDivisionName: null,
            MunicipalityCode: null,
            LocationDescription: null,
            SettlementMethod: null,
            MeterReadingOccurrence: null,
            FirstConsumerPartyName: null,
            SecondConsumerPartyName: null,
            ConsumerCvr: null,
            DataAccessCvr: null,
            MeterNumber: null,
            ConsumerStartDate: null,
            ChildMeteringPoints: null);
}
