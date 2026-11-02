// 역할: 비밀번호나 API 인증 토큰처럼 민감한 설정값을 윈도우 암호화 기술로 안전하게 보호합니다.
using System.Security.Cryptography;
using System.Text;

namespace DeckDeckDeck.App.Infrastructure.Persistence;

internal static class ProtectedSettingValueCodec
{
    public static string Protect(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var plainBytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(value);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return string.Empty;
        }
    }
}
