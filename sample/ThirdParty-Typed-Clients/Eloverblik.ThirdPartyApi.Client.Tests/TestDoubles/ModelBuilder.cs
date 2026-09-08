namespace Eloverblik.ThirdPartyApi.Client.Tests.TestDoubles;

/// <summary>
/// Builders for the generated models.
/// <para>
/// Generated models are positional records without defaulted parameters, so a test
/// that only cares about two or three fields would otherwise have to spell out every
/// constructor argument. These helpers keep the arrange-step focused on what matters.
/// </para>
/// </summary>
public static class ModelBuilder
{
    public static MeteringPointThirdPartyDto MeteringPoint(
        string? meteringPointId = null,
        string? streetName = null,
        string? buildingNumber = null)
        => new(
            MeteringPointId: meteringPointId,
            TypeOfMp: null,
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