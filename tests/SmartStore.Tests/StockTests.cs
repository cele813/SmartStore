using SmartStore.Domain;
using Xunit;
namespace SmartStore.Tests;

public class StockTests
{
    [Fact]
    public void Stock_cannot_be_negative()
    {
        var product = new Product();
        product.AdjustStock(5, "Receipt");
        Assert.Throws<ArgumentException>(() => product.AdjustStock(-6, "Sale"));
        Assert.Equal(5, product.Stock);
        Assert.Single(product.Movements);
    }
    [Fact]
    public void Sale_preserves_price_snapshot()
    {
        var product = new Product { Price = 100 };
        var order = new SaleOrder { Items = [new() { ProductId = product.Id, Quantity = 2, UnitPrice = product.Price }] };
        product.Price = 200;
        Assert.Equal(200, order.Total);
    }
    [Theory]
    [InlineData(0, "Receipt")]
    [InlineData(1, " ")]
    public void Invalid_adjustment_is_rejected(int amount, string reason) => Assert.Throws<ArgumentException>(() => new Product().AdjustStock(amount, reason));
    [Fact]
    public void Stock_overflow_is_rejected()
    {
        var product = new Product(); product.AdjustStock(int.MaxValue, "Receipt");
        Assert.Throws<OverflowException>(() => product.AdjustStock(1, "Receipt"));
        Assert.Equal(int.MaxValue, product.Stock);
    }
}
