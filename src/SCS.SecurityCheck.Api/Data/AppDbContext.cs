using Microsoft.EntityFrameworkCore;

namespace SCS.SecurityCheck.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}