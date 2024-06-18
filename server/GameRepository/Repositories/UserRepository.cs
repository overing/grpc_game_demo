
using System.Runtime.CompilerServices;
using Medo;
using Microsoft.EntityFrameworkCore;
using GameRepository.Models;
using GameCore.Models;

namespace GameRepository.Repositories;

public interface IUserRepository
{
    IAsyncEnumerable<UserData> GetAllAsync(CancellationToken cancellationToken = default);

    ValueTask<UserData?> GetWithIdAsync(Guid id, CancellationToken cancellationToken = default);

    ValueTask<UserData?> GetWithAccountAsync(string account, CancellationToken cancellationToken = default);

    ValueTask<UserData> CreateAsync(
        string account,
        string name,
        string email,
        CancellationToken cancellationToken = default);

    ValueTask<UserData?> UpdateNameAsync(Guid id, string newName);

    ValueTask<UserData?> UpdateSkinAsync(Guid id, byte newSkin);

    ValueTask UpdatePositionAsync(Guid id, float x, float y);

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
            yield return new(user.ID, user.Name, user.Email, user.Skin, user.PosX, user.PosY);
    }

    public async ValueTask<UserData?> GetWithIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<User>()
            .Where(u => u.ID == id)
            .Select(u => new UserData(u.ID, u.Name, u.Email, u.Skin, u.PosX, u.PosY))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async ValueTask<UserData?> GetWithAccountAsync(string account, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);

        return await dbContext.Set<User>()
            .Where(u => u.Account == account)
            .Select(u => new UserData(u.ID, u.Name, u.Email, u.Skin, u.PosX, u.PosY))
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
            Skin = 1,
            PosX = 0,
            PosY = 0,
        };
        await dbContext.Set<User>().AddAsync(user, cancellationToken);
        var affected = await dbContext.SaveChangesAsync(cancellationToken);
        if (affected == 0)
            throw new Exception("Save to db affected row is 0.");

        return new(user.ID, user.Name, user.Email, user.Skin, user.PosX, user.PosY);
    }

    public async ValueTask<UserData?> UpdateNameAsync(Guid id, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        var user = await dbContext.Set<User>().FirstOrDefaultAsync(u => u.ID == id);

        if (user is null)
            return null;

        user.Name = newName;
        user.UpdatedAt = DateTimeOffset.Now;

        await dbContext.SaveChangesAsync();

        return new(user.ID, user.Name, user.Email, user.Skin, user.PosX, user.PosY);
    }

    public async ValueTask<UserData?> UpdateSkinAsync(Guid id, byte newSkin)
    {
        if (newSkin < 1 || newSkin > 2)
            throw new ArgumentOutOfRangeException(nameof(newSkin), newSkin, "must between 1 and 2");

        var user = await dbContext.Set<User>().FirstOrDefaultAsync(u => u.ID == id);

        if (user is null)
            return null;

        user.Skin = newSkin;
        user.UpdatedAt = DateTimeOffset.Now;

        await dbContext.SaveChangesAsync();

        return new(user.ID, user.Name, user.Email, user.Skin, user.PosX, user.PosY);
    }

    public async ValueTask UpdatePositionAsync(Guid id, float x, float y)
    {
        await dbContext.Set<User>()
            .Where(u => u.ID == id)
            .ExecuteUpdateAsync(c => c.SetProperty(u => u.PosX, x).SetProperty(u => u.PosY, y).SetProperty(u => u.UpdatedAt, DateTimeOffset.Now));
    }

    public async ValueTask UpdateLoginTimeAsync(Guid id, DateTimeOffset dateTime)
    {
        var user = await dbContext.Set<User>().FirstOrDefaultAsync(u => u.ID == id);

        if (user is null)
            return;

        user.LastLoginAt = dateTime;
        user.UpdatedAt = dateTime;

        await dbContext.SaveChangesAsync();
    }
}