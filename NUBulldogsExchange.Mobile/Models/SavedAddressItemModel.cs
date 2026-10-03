using System.Linq;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Mobile.Models;

public sealed class SavedAddressItemModel
{
    public UserAddress Address { get; }

    public SavedAddressItemModel(UserAddress address)
    {
        Address = address;
    }

    public Guid Id => Address.Id;
    public string Label => string.IsNullOrWhiteSpace(Address.Label) ? "Home" : Address.Label;
    public string RecipientName => Address.RecipientName;
    public string PhoneNumber => Address.PhoneNumber;
    public string AddressLine => Address.AddressLine;
    public string Barangay => Address.Barangay;
    public string City => Address.City;
    public string Province => Address.Province;
    public string PostalCode => Address.PostalCode ?? string.Empty;
    public bool IsDefault => Address.IsDefault;

    public string SummaryLine =>
        string.Join(", ", new[] { Address.Barangay, Address.City, Address.Province, Address.PostalCode }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    public string DisplayText =>
        string.IsNullOrWhiteSpace(SummaryLine)
            ? Label
            : $"{Label} — {SummaryLine}";

    public string FullAddressLine =>
        string.Join(", ", new[] { Address.AddressLine, Address.Barangay, Address.City, Address.Province, Address.PostalCode }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    public override string ToString() => DisplayText;
}
