
using System.Runtime.CompilerServices;
using Medo;
using Microsoft.EntityFrameworkCore;
using GameRepository.Models;
using GameCore.Models;

namespace GameRepository.Repositories;

public interface IUserRepository
{
    IAsyncEnumerable<UserData> GetAllAsync(CancellationToken cancellationToken = default);

    ValueTask<UserData?> GetWithAccountAsync(string account, CancellationToken cancellationToken = default);

    ValueTask<UserData> CreateAsync(
        string account,
        string name,
        string email,
        CancellationToken cancellationToken = default);

    ValueTask UpdateLoginTimeAsync(Guid id, DateTimeOffset dateTime);
}

internal sealed class UserRepository(
    GameDbContext dbContext,
    TimeProvider timeProvider)
    : IUserRepository
{
    public async IAsyncEnumerable<UserData> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var user in dbContext.Set<User>().AsNoTracking().AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return new(user.ID, user.Name, user.Email);
    }

    public async ValueTask<UserData?> GetWithAccountAsync(string account, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);

        return await dbContext.Set<User>()
            .Where(u => u.Account == account)
            .Select(u => new UserData(u.ID, u.Name, u.Email))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async ValueTask<UserData> CreateAsync(
        string account,
        string name,
        string email,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var now = timeProvider.GetLocalNow();
        var user = new User
        {
            ID = Uuid7.NewGuid(),
            Account = account,
            Name = name,
            Email = email,
            LastLoginAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await dbContext.Set<User>().AddAsync(user, cancellationToken);
        var affected = await dbContext.SaveChangesAsync(cancellationToken);
        if (affected == 0)
            throw new Exception("Save to db affected row is 0.");

        return new(user.ID, user.Name, user.Email);
    }

    public async ValueTask UpdateLoginTimeAsync(Guid id, DateTimeOffset dateTime)
    {
        var user = await dbContext.Set<User>().FirstOrDefaultAsync(u => u.ID == id);

        if (user is null)
            return;

        user.LastLoginAt = dateTime;

        await dbContext.SaveChangesAsync();
    }
}