using Shipments.Api.Data;

namespace Shipments.Api.Tests;

public class ShipmentStatusRulesTests
{
    [Theory]
    [InlineData(ShipmentStatus.Created, ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.OutForDelivery)]
    [InlineData(ShipmentStatus.OutForDelivery, ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.Delayed)]
    [InlineData(ShipmentStatus.Delayed, ShipmentStatus.InTransit)]
    public void CanChange_AllowedTransition_ReturnsTrue(ShipmentStatus from, ShipmentStatus to)
    {
        // Act
        var result = ShipmentStatusRules.CanChange(from, to);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(ShipmentStatus.Created, ShipmentStatus.Delivered)]        // can't skip steps
    [InlineData(ShipmentStatus.OutForDelivery, ShipmentStatus.Created)]   // can't go backwards
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.InTransit)]      // no change isn't a change
    public void CanChange_NotAllowedTransition_ReturnsFalse(ShipmentStatus from, ShipmentStatus to)
    {
        var result = ShipmentStatusRules.CanChange(from, to);

        Assert.False(result);
    }

    [Theory]
    [InlineData(ShipmentStatus.Created)]
    [InlineData(ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.OutForDelivery)]
    [InlineData(ShipmentStatus.Delayed)]
    [InlineData(ShipmentStatus.Delivered)]
    public void CanChange_FromDelivered_IsNeverAllowed(ShipmentStatus to)
    {
        var result = ShipmentStatusRules.CanChange(ShipmentStatus.Delivered, to);

        Assert.False(result);
    }
}