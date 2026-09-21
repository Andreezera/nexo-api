namespace Nexo.Domain.Enums;

public static class TransactionCategoryExtensions
{
    // Os nomes do enum não têm acento por serem identificadores (e estão gravados assim no banco).
    // Este é o texto que o usuário vê nas respostas.
    public static string ToDisplayName(this TransactionCategory category) => category switch
    {
        TransactionCategory.Alimentacao => "Alimentação",
        TransactionCategory.Saude => "Saúde",
        TransactionCategory.Educacao => "Educação",
        TransactionCategory.Vestuario => "Vestuário",
        TransactionCategory.Eletronicos => "Eletrônicos",
        TransactionCategory.Salario => "Salário",
        _ => category.ToString()
    };
}
