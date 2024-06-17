using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameRepository.Models;

[Table("user")]
[PrimaryKey(nameof(ID))]
[Index(nameof(Account), IsUnique = true)]
public sealed record class User
{
    [Column("id")]
    public Guid ID { get; set; }

    [Column("account")]
    public string Account { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("email")]
    public string Email { get; set; } = null!;

    [Column("skin")]
    public byte Skin { get; set; }

    [Column("pos_x")]
    public float PosX { get; set; }

    [Column("pos_y")]
    public float PosY { get; set; }

    [Column("last_login_at")]
    public DateTimeOffset LastLoginAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
    }
}
