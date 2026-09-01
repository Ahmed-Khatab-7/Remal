namespace Remal.Domain.Common.ValueObjects;

/// <summary>
/// Owned-entity Value Object representing a delivery address.
/// </summary>
public class Address : IEquatable<Address>
{
    public string Line { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public string? Governorate { get; private set; }
    public string? PostalCode { get; private set; }
    public string? Landmark { get; private set; }

    private Address() { }

    public Address(string line, string city, string? governorate = null, string? postalCode = null, string? landmark = null)
    {
        if (string.IsNullOrWhiteSpace(line)) throw new ArgumentException("Address line required", nameof(line));
        if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("City required", nameof(city));
        Line = line.Trim();
        City = city.Trim();
        Governorate = governorate?.Trim();
        PostalCode = postalCode?.Trim();
        Landmark = landmark?.Trim();
    }

    public override string ToString()
    {
        var parts = new List<string?> { Line, City, Governorate, PostalCode };
        return string.Join("، ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    public bool Equals(Address? other) => other is not null
        && Line == other.Line && City == other.City
        && Governorate == other.Governorate && PostalCode == other.PostalCode;

    public override bool Equals(object? obj) => obj is Address a && Equals(a);
    public override int GetHashCode() => HashCode.Combine(Line, City, Governorate, PostalCode);
}
