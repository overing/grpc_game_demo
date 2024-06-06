using System.Security.Cryptography;
using System.Text;

namespace GameRepository;

public interface IPasswordEncoder
{
    byte[] Encode(string password);
}

sealed class PasswordEncoder : IPasswordEncoder
{
    const string Salt = "ec72afb8cdd74023b12dae6c8bc1a3e4";

    public byte[] Encode(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return MD5.HashData(Encoding.UTF8.GetBytes(password + Salt));
    }
}
