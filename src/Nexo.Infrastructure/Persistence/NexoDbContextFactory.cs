using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Nexo.Infrastructure.Persistence;

// Usado apenas pela ferramenta "dotnet ef". Lê a connection string dos
// user-secrets do Nexo.Api (mesma fonte usada em runtime); se não encontrar
// (ex: máquina sem os secrets configurados), cai para um valor fictício que
// só serve para gerar migrations, nunca para aplicá-las.
public class NexoDbContextFactory : IDesignTimeDbContextFactory<NexoDbContext>
{
    private const string ApiUserSecretsId = "203d2fc0-3c53-4570-91e5-012efd1780f6";

    public NexoDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(ApiUserSecretsId)
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Database=nexo_design;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<NexoDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new NexoDbContext(optionsBuilder.Options);
    }
}
