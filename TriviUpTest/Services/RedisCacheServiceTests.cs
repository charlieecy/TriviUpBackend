using Xunit;
using Moq;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TriviUpBackend.Services.Cache;
using System.Text;
using System.Text.Json;

namespace TriviUpTest.Services;

public class RedisCacheServiceTests
{
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<ILogger<RedisCacheService>> _mockLogger;
    private readonly RedisCacheService _service;

    public RedisCacheServiceTests()
    {
        _mockCache = new Mock<IDistributedCache>();
        _mockLogger = new Mock<ILogger<RedisCacheService>>();
        _service = new RedisCacheService(_mockCache.Object, _mockLogger.Object);
    }

    // ========== GetAsync Tests ==========

    [Fact]
    public async Task GetAsync_ExistingKey_ReturnsDeserializedValue()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = new TestData { Name = "Test", Value = 42 };
        var json = JsonSerializer.Serialize(expectedValue);
        _mockCache.Setup(c => c.GetStringAsync(key, default)).ReturnsAsync(json);

        // Act
        var result = await _service.GetAsync<TestData>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task GetAsync_NonExistingKey_ReturnsDefault()
    {
        // Arrange
        var key = "non-existing-key";
        _mockCache.Setup(c => c.GetStringAsync(key, default)).ReturnsAsync((string?)null);

        // Act
        var result = await _service.GetAsync<TestData>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_CacheThrowsException_ReturnsDefault()
    {
        // Arrange
        var key = "error-key";
        _mockCache.Setup(c => c.GetStringAsync(key, default)).ThrowsAsync(new Exception("Redis connection failed"));

        // Act
        var result = await _service.GetAsync<TestData>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_InvalidJson_ReturnsDefault()
    {
        // Arrange
        var key = "invalid-json-key";
        _mockCache.Setup(c => c.GetStringAsync(key, default)).ReturnsAsync("not valid json {{{");

        // Act
        var result = await _service.GetAsync<TestData>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_ComplexObject_ReturnsCorrectlyDeserialized()
    {
        // Arrange
        var key = "complex-key";
        var expectedValue = new ComplexData
        {
            Id = 1,
            Name = "Complex",
            Nested = new NestedData { Value = "nested-value" },
            Items = new List<int> { 1, 2, 3 }
        };
        var json = JsonSerializer.Serialize(expectedValue);
        _mockCache.Setup(c => c.GetStringAsync(key, default)).ReturnsAsync(json);

        // Act
        var result = await _service.GetAsync<ComplexData>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Complex", result.Name);
        Assert.NotNull(result.Nested);
        Assert.Equal("nested-value", result.Nested.Value);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task GetAsync_EmptyString_ReturnsDefault()
    {
        // Arrange
        var key = "empty-string-key";
        _mockCache.Setup(c => c.GetStringAsync(key, default)).ReturnsAsync(string.Empty);

        // Act
        var result = await _service.GetAsync<TestData>(key);

        // Assert
        Assert.Null(result);
    }

    // ========== SetAsync Tests ==========

    [Fact]
    public async Task SetAsync_ValidKeyAndValue_SetsCacheWithOptions()
    {
        // Arrange
        var key = "test-key";
        var value = new TestData { Name = "Test", Value = 100 };
        TimeSpan? expiry = TimeSpan.FromMinutes(5);

        // Act
        await _service.SetAsync(key, value, expiry);

        // Assert
        _mockCache.Verify(c => c.SetStringAsync(
            key,
            It.IsAny<string>(),
            It.Is<DistributedCacheEntryOptions>(o =>
                o.AbsoluteExpirationRelativeToNow == expiry),
            default),
            Times.Once);
    }

    [Fact]
    public async Task SetAsync_NullExpiry_SetsCacheWithoutExpiration()
    {
        // Arrange
        var key = "test-key";
        var value = new TestData { Name = "No Expiry", Value = 0 };

        // Act
        await _service.SetAsync(key, value, null);

        // Assert
        _mockCache.Verify(c => c.SetStringAsync(
            key,
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            default),
            Times.Once);
    }

    [Fact]
    public async Task SetAsync_CacheThrowsException_DoesNotRethrow()
    {
        // Arrange
        var key = "error-key";
        var value = new TestData { Name = "Error", Value = 0 };
        _mockCache.Setup(c => c.SetStringAsync(key, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), default))
            .ThrowsAsync(new Exception("Redis write failed"));

        // Act & Assert - should not throw
        var exception = await Record.ExceptionAsync(() => _service.SetAsync(key, value));
        Assert.Null(exception);
    }

    [Fact]
    public async Task SetAsync_SerializesValueCorrectly()
    {
        // Arrange
        var key = "serialize-key";
        var value = new TestData { Name = "Serialize Test", Value = 999 };
        string? capturedJson = null;

        _mockCache.Setup(c => c.SetStringAsync(key, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), default))
            .Callback<string, string, DistributedCacheEntryOptions, CancellationToken>((k, json, o, t) => capturedJson = json)
            .Returns(Task.CompletedTask);

        // Act
        await _service.SetAsync(key, value);

        // Assert
        Assert.NotNull(capturedJson);
        var deserialized = JsonSerializer.Deserialize<TestData>(capturedJson);
        Assert.NotNull(deserialized);
        Assert.Equal("Serialize Test", deserialized.Name);
        Assert.Equal(999, deserialized.Value);
    }

    [Fact]
    public async Task SetAsync_ZeroExpiry_SetsCacheWithZeroExpiry()
    {
        // Arrange
        var key = "zero-expiry-key";
        var value = new TestData { Name = "Zero Expiry", Value = 0 };
        TimeSpan? expiry = TimeSpan.Zero;

        // Act
        await _service.SetAsync(key, value, expiry);

        // Assert
        _mockCache.Verify(c => c.SetStringAsync(
            key,
            It.IsAny<string>(),
            It.Is<DistributedCacheEntryOptions>(o =>
                o.AbsoluteExpirationRelativeToNow == TimeSpan.Zero),
            default),
            Times.Once);
    }

    // ========== RemoveAsync Tests ==========

    [Fact]
    public async Task RemoveAsync_ExistingKey_CallsCacheRemove()
    {
        // Arrange
        var key = "to-remove-key";

        // Act
        await _service.RemoveAsync(key);

        // Assert
        _mockCache.Verify(c => c.RemoveAsync(key, default), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_CacheThrowsException_DoesNotRethrow()
    {
        // Arrange
        var key = "error-key";
        _mockCache.Setup(c => c.RemoveAsync(key, default)).ThrowsAsync(new Exception("Redis delete failed"));

        // Act & Assert - should not throw
        var exception = await Record.ExceptionAsync(() => _service.RemoveAsync(key));
        Assert.Null(exception);
    }

    [Fact]
    public async Task RemoveAsync_MultipleKeys_CallsRemoveForEach()
    {
        // Arrange
        var keys = new[] { "key1", "key2", "key3" };

        // Act
        foreach (var key in keys)
        {
            await _service.RemoveAsync(key);
        }

        // Assert
        foreach (var key in keys)
        {
            _mockCache.Verify(c => c.RemoveAsync(key, default), Times.Once);
        }
    }

    // ========== RemoveByPrefixAsync Tests ==========

    [Fact]
    public async Task RemoveByPrefixAsync_AnyPrefix_LogsInvalidationRequest()
    {
        // Arrange
        var prefix = "quizzes:public:";

        // Act
        await _service.RemoveByPrefixAsync(prefix);

        // Assert - just verify no exception is thrown and logs are attempted
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<It.IsAnyType>(),
                It.IsAny<object>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveByPrefixAsync_DifferentPrefixes_LogsEachPrefix()
    {
        // Arrange
        var prefixes = new[] { "quizzes:public:", "quiz:1:", "users:" };

        // Act
        foreach (var prefix in prefixes)
        {
            await _service.RemoveByPrefixAsync(prefix);
        }

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<It.IsAnyType>(),
                It.IsAny<object>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task RemoveByPrefixAsync_EmptyPrefix_LogsInvalidationRequest()
    {
        // Arrange
        var prefix = "";

        // Act
        await _service.RemoveByPrefixAsync(prefix);

        // Assert - should not throw
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<It.IsAnyType>(),
                It.IsAny<object>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ========== Helper Classes ==========

    private class TestData
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }

    private class ComplexData
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public NestedData? Nested { get; set; }
        public List<int> Items { get; set; } = new();
    }

    private class NestedData
    {
        public string Value { get; set; } = "";
    }
}
