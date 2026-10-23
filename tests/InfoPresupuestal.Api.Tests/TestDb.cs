using InfoPresupuestal.Api.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Tests;

internal static class TestDb
{
    public static AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
