using Remal.Domain.Common;
using Remal.Domain.Common.ValueObjects;
using Remal.Domain.Identity;

namespace Remal.Domain.Entities;

/// <summary>Saved delivery address belonging to a customer.</summary>
public class AddressBookEntry : AuditableEntity
{
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public string Label { get; set; } = "الرئيسي"; // e.g. "البيت", "الشغل"
    public string RecipientName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public Address Address { get; set; } = null!;
    public bool IsDefault { get; set; }
}
