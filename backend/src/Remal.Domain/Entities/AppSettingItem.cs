using Remal.Domain.Common;

namespace Remal.Domain.Entities;

/// <summary>
/// Site-wide settings stored as key/value pairs.
/// Examples: shipping_fee, free_shipping_threshold, announcement, currency.
/// </summary>
public class AppSettingItem : AuditableEntity
{
    public string Key { get; set; } = null!;
    public string? Value { get; set; }
    public string? Description { get; set; }
    public string DataType { get; set; } = "string"; // string|int|decimal|bool|json
}
