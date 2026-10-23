using System.ComponentModel.DataAnnotations;
namespace SmartStore.Application;

public sealed record CategoryRequest([Required, StringLength(100, MinimumLength = 1)] string Name);
public sealed record SupplierRequest([Required, StringLength(200)] string Name, [EmailAddress, StringLength(254)] string? Email, [StringLength(30)] string? Phone);
public sealed record ProductRequest(
    [Required, StringLength(50)] string Sku,
    [Required, StringLength(200)] string Name,
    [Range(typeof(decimal), "0", "9999999999999999.99")] decimal Price,
    Guid CategoryId, Guid? SupplierId);
public sealed record StockRequest(int Quantity, [Required, StringLength(300)] string Reason);
public sealed record SaleLine(Guid ProductId, [Range(1, int.MaxValue)] int Quantity);
public sealed record SaleRequest([Required, MinLength(1), MaxLength(100)] List<SaleLine> Items);
