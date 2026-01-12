// ReSharper disable StringLiteralTypo
#pragma warning disable CA5394 // Random is used only for sample data generation

namespace Showcase.Api.Domain.Repositories;

public sealed class UserInMemoryRepository
{
    private static readonly string[] FirstNames =
    [
        "Emma", "Liam", "Olivia", "Noah", "Ava", "Oliver", "Sophia", "Lucas",
        "Isabella", "Mason", "Mia", "Ethan", "Charlotte", "James", "Amelia",
        "Alexander", "Harper", "Sebastian", "Evelyn", "Benjamin", "Luna", "William",
        "Ella", "Henry", "Scarlett", "Michael", "Grace", "Daniel", "Chloe", "Jacob"
    ];

    private static readonly string[] LastNames =
    [
        "Jensen", "Nielsen", "Hansen", "Pedersen", "Andersen", "Christensen", "Larsen",
        "Sørensen", "Rasmussen", "Jørgensen", "Petersen", "Madsen", "Kristensen",
        "Olsen", "Thomsen", "Møller", "Poulsen", "Johansen", "Knudsen", "Eriksen"
    ];

    private static readonly (string City, string PostalCode, double Lat, double Lon)[] DenmarkCities =
    [
        ("Copenhagen", "1000", 55.6761, 12.5683),
        ("Aarhus", "8000", 56.1629, 10.2039),
        ("Odense", "5000", 55.4038, 10.4024),
        ("Aalborg", "9000", 57.0488, 9.9217),
        ("Esbjerg", "6700", 55.4761, 8.4593),
        ("Randers", "8900", 56.4607, 10.0364),
        ("Kolding", "6000", 55.4904, 9.4722),
        ("Horsens", "8700", 55.8607, 9.8503),
        ("Vejle", "7100", 55.7113, 9.5364),
        ("Roskilde", "4000", 55.6415, 12.0803)
    ];

    private static readonly (string City, string PostalCode, double Lat, double Lon)[] SwedenCities =
    [
        ("Stockholm", "111 29", 59.3293, 18.0686),
        ("Gothenburg", "411 06", 57.7089, 11.9746),
        ("Malmö", "211 18", 55.6050, 13.0038),
        ("Uppsala", "751 05", 59.8586, 17.6389),
        ("Linköping", "581 83", 58.4108, 15.6214)
    ];

    private static readonly (string City, string PostalCode, double Lat, double Lon)[] FinlandCities =
    [
        ("Helsinki", "00100", 60.1699, 24.9384),
        ("Espoo", "02100", 60.2055, 24.6559),
        ("Tampere", "33100", 61.4978, 23.7610),
        ("Turku", "20100", 60.4518, 22.2666),
        ("Oulu", "90100", 65.0121, 25.4651)
    ];

    private static readonly (string City, string PostalCode, double Lat, double Lon)[] GermanyCities =
    [
        ("Berlin", "10115", 52.5200, 13.4050),
        ("Munich", "80331", 48.1351, 11.5820),
        ("Hamburg", "20095", 53.5511, 9.9937),
        ("Frankfurt", "60311", 50.1109, 8.6821),
        ("Cologne", "50667", 50.9375, 6.9603)
    ];

    private static readonly (string City, string PostalCode, double Lat, double Lon)[] EnglandCities =
    [
        ("London", "EC1A 1BB", 51.5074, -0.1278),
        ("Manchester", "M1 1AD", 53.4808, -2.2426),
        ("Birmingham", "B1 1AA", 52.4862, -1.8904),
        ("Liverpool", "L1 1AA", 53.4084, -2.9916),
        ("Bristol", "BS1 1AA", 51.4545, -2.5879)
    ];

    private static readonly (string City, string State, string PostalCode, double Lat, double Lon)[] UsaCities =
    [
        ("New York", "NY", "10001", 40.7128, -74.0060),
        ("Los Angeles", "CA", "90001", 34.0522, -118.2437),
        ("Chicago", "IL", "60601", 41.8781, -87.6298),
        ("Houston", "TX", "77001", 29.7604, -95.3698),
        ("Phoenix", "AZ", "85001", 33.4484, -112.0740)
    ];

    private static readonly string[] StreetNames =
    [
        "Main Street", "Oak Avenue", "Park Road", "High Street", "Church Lane",
        "Station Road", "Mill Lane", "School Road", "The Green", "Kings Road",
        "Queen Street", "Victoria Road", "Castle Street", "Bridge Street", "Market Place"
    ];

    private static readonly string[] Bios =
    [
        "Passionate about technology and innovation.",
        "Coffee enthusiast and amateur photographer.",
        "Love hiking and exploring new places.",
        "Software developer with 10+ years experience.",
        "Marketing professional focused on digital growth.",
        "Entrepreneur building the next big thing.",
        "Data scientist turning numbers into insights.",
        "Creative designer with an eye for detail.",
        "Project manager keeping teams on track.",
        "HR professional building great company cultures."
    ];

    private readonly List<UserEntity> users = [];

    public UserInMemoryRepository()
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var usedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Generate 50 Danish users
        GenerateUsers(random, usedEmails, 50, "Denmark", "DK", DenmarkCities, null);

        // Generate 10 Swedish users
        GenerateUsers(random, usedEmails, 10, "Sweden", "SE", SwedenCities, null);

        // Generate 10 Finnish users
        GenerateUsers(random, usedEmails, 10, "Finland", "FI", FinlandCities, null);

        // Generate 10 German users
        GenerateUsers(random, usedEmails, 10, "Germany", "DE", GermanyCities, null);

        // Generate 10 English users
        GenerateUsers(random, usedEmails, 10, "England", "GB", EnglandCities, null);

        // Generate 10 USA users
        GenerateUsaUsers(random, usedEmails, 10);
    }

    private void GenerateUsers(
        Random random,
        HashSet<string> usedEmails,
        int count,
        string country,
        string countryCode,
        (string City, string PostalCode, double Lat, double Lon)[] cities,
        string? state)
    {
        for (var i = 0; i < count; i++)
        {
            var firstName = FirstNames[random.Next(FirstNames.Length)];
            var lastName = LastNames[random.Next(LastNames.Length)];
            var email = GenerateUniqueEmail(random, usedEmails, firstName, lastName);
            var city = cities[random.Next(cities.Length)];
            var role = (UserRoleType)random.Next(4);
            var dob = DateOnly.FromDateTime(
                DateTime
                    .Today
                    .AddYears(-random.Next(20, 65))
                    .AddDays(-random.Next(365)));

            var user = new UserEntity(
                Id: Guid.NewGuid(),
                FirstName: firstName,
                LastName: lastName,
                Email: email,
                Phone: GeneratePhone(random, countryCode),
                Website: random.Next(3) == 0 ? $"https://{firstName.ToLowerInvariant()}{lastName.ToLowerInvariant()}.com" : null,
                DateOfBirth: dob,
                Age: CalculateAge(dob),
                Bio: random.Next(2) == 0 ? Bios[random.Next(Bios.Length)] : null,
                AvatarUrl: $"https://api.dicebear.com/7.x/avataaars/svg?seed={firstName}{lastName}",
                IsActive: random.Next(10) > 1, // 90% active
                Role: role,
                Address: new AddressEntity(
                    Street: $"{random.Next(1, 200)} {StreetNames[random.Next(StreetNames.Length)]}",
                    City: city.City,
                    State: state,
                    PostalCode: city.PostalCode,
                    Country: country,
                    CountryCode: countryCode,
                    Latitude: city.Lat + (random.NextDouble() - 0.5) * 0.1,
                    Longitude: city.Lon + (random.NextDouble() - 0.5) * 0.1),
                CreatedAt: DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                UpdatedAt: DateTime.UtcNow.AddDays(-random.Next(0, 30)));

            users.Add(user);
        }
    }

    private void GenerateUsaUsers(
        Random random,
        HashSet<string> usedEmails,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            var firstName = FirstNames[random.Next(FirstNames.Length)];
            var lastName = LastNames[random.Next(LastNames.Length)];
            var email = GenerateUniqueEmail(random, usedEmails, firstName, lastName);
            var city = UsaCities[random.Next(UsaCities.Length)];
            var role = (UserRoleType)random.Next(4);
            var dob = DateOnly.FromDateTime(
                DateTime
                    .Today
                    .AddYears(-random.Next(20, 65))
                    .AddDays(-random.Next(365)));

            var user = new UserEntity(
                Id: Guid.NewGuid(),
                FirstName: firstName,
                LastName: lastName,
                Email: email,
                Phone: GeneratePhone(random, "US"),
                Website: random.Next(3) == 0 ? $"https://{firstName.ToLowerInvariant()}{lastName.ToLowerInvariant()}.com" : null,
                DateOfBirth: dob,
                Age: CalculateAge(dob),
                Bio: random.Next(2) == 0 ? Bios[random.Next(Bios.Length)] : null,
                AvatarUrl: $"https://api.dicebear.com/7.x/avataaars/svg?seed={firstName}{lastName}",
                IsActive: random.Next(10) > 1,
                Role: role,
                Address: new AddressEntity(
                    Street: $"{random.Next(1, 9999)} {StreetNames[random.Next(StreetNames.Length)]}",
                    City: city.City,
                    State: city.State,
                    PostalCode: city.PostalCode,
                    Country: "United States",
                    CountryCode: "US",
                    Latitude: city.Lat + (random.NextDouble() - 0.5) * 0.1,
                    Longitude: city.Lon + (random.NextDouble() - 0.5) * 0.1),
                CreatedAt: DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                UpdatedAt: DateTime.UtcNow.AddDays(-random.Next(0, 30)));

            users.Add(user);
        }
    }

    private static string GenerateUniqueEmail(
        Random random,
        HashSet<string> usedEmails,
        string firstName,
        string lastName)
    {
        var domains = new[] { "example.com", "test.org", "demo.net", "sample.io", "mail.com" };
        string email;
        var attempt = 0;

        do
        {
            var suffix = attempt > 0
                ? random
                    .Next(100, 999)
                    .ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}{suffix}@{domains[random.Next(domains.Length)]}";
            attempt++;
        }
        while (!usedEmails.Add(email));

        return email;
    }

    private static string GeneratePhone(
        Random random,
        string countryCode)
        => countryCode switch
        {
            "DK" => $"+45{random.Next(20000000, 99999999)}",
            "SE" => $"+46{random.Next(70000000, 79999999):D8}",
            "FI" => $"+358{random.Next(40000000, 49999999):D8}",
            "DE" => $"+49{random.Next(15000000, 17999999):D8}0",
            "GB" => $"+44{random.Next(70000000, 79999999):D8}",
            "US" => $"+1{random.Next(200000000, 999999999):D9}",
            _ => $"+{random.Next(100000000, int.MaxValue)}",
        };

    private static int CalculateAge(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    public Task<List<UserEntity>> GetAll(
        string? search = null,
        string? country = null,
        UserRoleType? role = null,
        bool? isActive = null,
        int? limit = null)
    {
        var query = users.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                $"{u.FirstName} {u.LastName}".Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            query = query.Where(u =>
                u.Address.Country.Contains(country, StringComparison.OrdinalIgnoreCase) ||
                (u.Address.CountryCode?.Equals(country, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return Task.FromResult(query.ToList());
    }

    public Task<UserEntity?> GetById(Guid id)
        => Task.FromResult(users.FirstOrDefault(u => u.Id == id));

    public Task<UserEntity?> GetByEmail(string email)
        => Task.FromResult(users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<UserEntity> Create(
        string firstName,
        string lastName,
        string email,
        string? phone,
        string? website,
        DateOnly dateOfBirth,
        string? bio,
        string? avatarUrl,
        bool isActive,
        UserRoleType role,
        AddressEntity address)
    {
        var user = new UserEntity(
            Id: Guid.NewGuid(),
            FirstName: firstName,
            LastName: lastName,
            Email: email,
            Phone: phone,
            Website: website,
            DateOfBirth: dateOfBirth,
            Age: CalculateAge(dateOfBirth),
            Bio: bio,
            AvatarUrl: avatarUrl ?? $"https://api.dicebear.com/7.x/avataaars/svg?seed={firstName}{lastName}",
            IsActive: isActive,
            Role: role,
            Address: address,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow);

        users.Add(user);
        return Task.FromResult(user);
    }

    public Task<UserEntity?> Update(
        Guid id,
        string firstName,
        string lastName,
        string email,
        string? phone,
        string? website,
        DateOnly dateOfBirth,
        string? bio,
        string? avatarUrl,
        bool isActive,
        UserRoleType role,
        AddressEntity address)
    {
        var index = users.FindIndex(u => u.Id == id);
        if (index < 0)
        {
            return Task.FromResult<UserEntity?>(null);
        }

        var existing = users[index];
        var updated = new UserEntity(
            Id: id,
            FirstName: firstName,
            LastName: lastName,
            Email: email,
            Phone: phone,
            Website: website,
            DateOfBirth: dateOfBirth,
            Age: CalculateAge(dateOfBirth),
            Bio: bio,
            AvatarUrl: avatarUrl ?? existing.AvatarUrl,
            IsActive: isActive,
            Role: role,
            Address: address,
            CreatedAt: existing.CreatedAt,
            UpdatedAt: DateTime.UtcNow);

        users[index] = updated;
        return Task.FromResult<UserEntity?>(updated);
    }

    public Task<UserEntity?> Delete(Guid id)
    {
        var index = users.FindIndex(u => u.Id == id);
        if (index < 0)
        {
            return Task.FromResult<UserEntity?>(null);
        }

        var user = users[index];
        users.RemoveAt(index);
        return Task.FromResult<UserEntity?>(user);
    }

    public Task<string[]> GetDistinctCountries()
        => Task.FromResult(users
            .Select(u => u.Address.Country)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray());
}