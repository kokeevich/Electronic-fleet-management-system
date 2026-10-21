using Microsoft.EntityFrameworkCore;
using Курсовой_проект.Configuration;
using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

public static class DatabaseFactory
{
    public static OzvsKursachContext Create() => new(
        new DbContextOptionsBuilder<OzvsKursachContext>()
            .UseSqlServer(DatabaseSettings.GetConnectionString(), sql => sql.CommandTimeout(15))
            .Options);
}
