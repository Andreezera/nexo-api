using Nexo.Domain.Enums;

namespace Nexo.Tests;

public class TransactionCategoryExtensionsTests
{
    [Theory]
    [InlineData(TransactionCategory.Alimentacao, "Alimentação")]
    [InlineData(TransactionCategory.Saude, "Saúde")]
    [InlineData(TransactionCategory.Educacao, "Educação")]
    [InlineData(TransactionCategory.Vestuario, "Vestuário")]
    [InlineData(TransactionCategory.Eletronicos, "Eletrônicos")]
    [InlineData(TransactionCategory.Salario, "Salário")]
    [InlineData(TransactionCategory.Mercado, "Mercado")]
    [InlineData(TransactionCategory.Outros, "Outros")]
    public void ToDisplayName_UsaAcentuacaoCorreta(TransactionCategory category, string expected)
    {
        Assert.Equal(expected, category.ToDisplayName());
    }

    [Fact]
    public void ToDisplayName_CobreTodasAsCategorias()
    {
        foreach (var category in Enum.GetValues<TransactionCategory>())
            Assert.False(string.IsNullOrWhiteSpace(category.ToDisplayName()));
    }
}
