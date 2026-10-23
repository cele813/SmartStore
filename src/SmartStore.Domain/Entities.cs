namespace SmartStore.Domain;

public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
}
public sealed class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? SupplierId { get; set; }
    public int Stock { get; private set; }
    public List<StockMovement> Movements { get; set; } = [];
    public void AdjustStock(int quantity, string reason)
    {
        if (quantity == 0) throw new ArgumentException("Quantity must not be zero.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.");
        var next = checked(Stock + quantity);
        if (next < 0) throw new ArgumentException("Insufficient stock.");
        Stock = next;
        Movements.Add(new StockMovement { ProductId = Id, Quantity = quantity, Reason = reason.Trim() });
    }
}
public sealed class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class SaleOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<SaleOrderItem> Items { get; set; } = [];
    public decimal Total => Items.Sum(x => x.UnitPrice * x.Quantity);
}
public sealed class SaleOrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleOrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
