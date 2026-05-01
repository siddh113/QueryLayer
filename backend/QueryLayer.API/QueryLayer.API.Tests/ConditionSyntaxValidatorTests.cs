using QueryLayer.Api.Services.AI;
using Xunit;

namespace QueryLayer.Api.Tests;

public class ConditionSyntaxValidatorTests
{
    private readonly ConditionSyntaxValidator _validator = new();

    // -------------------------------------------------------------------------
    // Valid cases
    // -------------------------------------------------------------------------

    [Fact]
    public void Valid_SimpleGreaterThan_WithKnownField()
    {
        var (isValid, error) = _validator.Validate("amount > 500", new[] { "amount" });
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    [Fact]
    public void Valid_EqualityWithStringLiteral_WithKnownField()
    {
        var (isValid, error) = _validator.Validate("state == 'approved'", new[] { "state" });
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    [Fact]
    public void Valid_CompoundAndCondition_WithKnownField()
    {
        var (isValid, error) = _validator.Validate(
            "status == 'approved' AND previous.status != 'approved'",
            new[] { "status" });
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    [Fact]
    public void Valid_AuthRoleComparison_NoFieldsNeeded()
    {
        var (isValid, error) = _validator.Validate("auth.role == 'manager'", Array.Empty<string>());
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    [Fact]
    public void Valid_AuthUserId_NoFieldsNeeded()
    {
        var (isValid, error) = _validator.Validate("auth.user_id == '123'", Array.Empty<string>());
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    [Fact]
    public void Valid_AuthRoleWithKnownFields()
    {
        var (isValid, error) = _validator.Validate("auth.role == 'manager'", new[] { "amount", "state" });
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    [Fact]
    public void Valid_PreviousFieldRef_WithKnownField()
    {
        var (isValid, error) = _validator.Validate("previous.state != 'approved'", new[] { "state" });
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    [Fact]
    public void Valid_LessThanOperator()
    {
        var (isValid, error) = _validator.Validate("amount < 100", new[] { "amount" });
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    [Fact]
    public void Valid_GreaterThanOrEqual()
    {
        var (isValid, error) = _validator.Validate("amount >= 500", new[] { "amount" });
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    [Fact]
    public void Valid_LessThanOrEqual()
    {
        var (isValid, error) = _validator.Validate("amount <= 1000", new[] { "amount" });
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    [Fact]
    public void Valid_NotEqual()
    {
        var (isValid, error) = _validator.Validate("state != 'draft'", new[] { "state" });
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    [Fact]
    public void Valid_NoKnownFields_DoesNotFailOnFieldRef()
    {
        // When knownFields is empty the validator skips field existence checks
        var (isValid, error) = _validator.Validate("amount > 500", Array.Empty<string>());
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    // -------------------------------------------------------------------------
    // Invalid cases
    // -------------------------------------------------------------------------

    [Fact]
    public void Invalid_EmptyString_ReturnsError()
    {
        var (isValid, error) = _validator.Validate("", Array.Empty<string>());
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("empty", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_WhitespaceOnly_ReturnsError()
    {
        var (isValid, error) = _validator.Validate("   ", Array.Empty<string>());
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("empty", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_UnknownFieldWithKnownFields_ReturnsError()
    {
        var (isValid, error) = _validator.Validate("nonexistent_field > 5", new[] { "amount" });
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("nonexistent_field", error);
    }

    [Fact]
    public void Invalid_PreviousRefToUnknownField_ReturnsError()
    {
        var (isValid, error) = _validator.Validate("previous.nonexistent > 5", new[] { "amount" });
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("previous.nonexistent", error);
    }

    [Fact]
    public void Invalid_PlainText_NoComparison_ReturnsError()
    {
        var (isValid, error) = _validator.Validate("just some text", Array.Empty<string>());
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("no valid comparison", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_OnlyKeyword_ReturnsError()
    {
        var (isValid, error) = _validator.Validate("AND", Array.Empty<string>());
        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void Invalid_MixedKnownAndUnknown_FailsOnUnknown()
    {
        // amount is valid but badfield is not
        var (isValid, error) = _validator.Validate("amount > 500 AND badfield == 'x'", new[] { "amount" });
        Assert.False(isValid);
        Assert.Contains("badfield", error!);
    }
}
