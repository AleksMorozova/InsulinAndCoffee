using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Tests;

public class SupplyServiceTests
{
    private static readonly DateOnly Today = new(2026, 6, 28);

    [Fact]
    public void Calculate_RoundsDaysLeftAndEstimatesRunOutDate()
    {
        var result = SupplyService.Calculate(Supply(quantity: 22, dailyUsage: 4, threshold: 3), Today);

        Assert.Equal(5.5m, result.DaysLeft);
        Assert.Equal(Today.AddDays(6), result.EstimatedRunOutDate);
        Assert.Equal("Ok", result.Status);
    }

    [Theory]
    [InlineData(12, 4, 10, "Critical")]
    [InlineData(20, 4, 10, "Low")]
    [InlineData(44, 4, 10, "Ok")]
    public void Calculate_AssignsExpectedStatus(decimal quantity, decimal usage, int threshold, string expectedStatus)
    {
        var result = SupplyService.Calculate(Supply(quantity, usage, threshold), Today);

        Assert.Equal(expectedStatus, result.Status);
    }

    [Fact]
    public void Calculate_WhenDailyUsageIsZero_ReturnsUnknownWithoutEstimate()
    {
        var result = SupplyService.Calculate(Supply(quantity: 10, dailyUsage: 0, threshold: 10), Today);

        Assert.Equal("Unknown", result.Status);
        Assert.Null(result.DaysLeft);
        Assert.Null(result.EstimatedRunOutDate);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public async Task CreateAsync_WhenValuesAreNegative_ThrowsValidation(decimal quantity, decimal usage)
    {
        await using var db = CreateDbContext();
        var service = new SupplyService(db, TimeProvider.System);
        var request = new CreateSupplyItemRequest("Test", quantity, "pcs", usage, 5);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request, CancellationToken.None));
    }

    private static SupplyItem Supply(decimal quantity, decimal dailyUsage, int threshold) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test supply",
        Unit = "pcs",
        CurrentQuantity = quantity,
        DailyUsage = dailyUsage,
        LowStockThresholdDays = threshold
    };

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
