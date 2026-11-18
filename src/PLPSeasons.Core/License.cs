using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PLPSeasons.Core;

/// PLP Seasons licence keys (same keys as the Mac app): "PLP-" + base64url(JSON) + "." + base64url(Ed25519 signature).
/// Bought once → {"product":"plp-snow","version":1,...} without expiry. Old subscription keys → "plp-seasons" v2 with "exp".
public static class License
{
    public const string PublicKey = "R7R+PdyiQp1m/GJUcS65B5TzrbGTCH7AiNTWERZgiTk=";

    public static string Clean(string? token)
    {
        var t = string.Concat((token ?? "").Where(c => !char.IsWhiteSpace(c)));
        if (t.StartsWith("PLP-", StringComparison.OrdinalIgnoreCase)) t = t[4..];
        return t;
    }

    static byte[]? B64u(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        s += new string('=', (4 - s.Length % 4) % 4);
        try { return Convert.FromBase64String(s); } catch { return null; }
    }

    /// true when the key is genuine and not expired
    public static bool Valid(string? token, string? publicKey = null, DateTimeOffset? now = null)
    {
        var t = Clean(token);
        if (t.Length == 0 || t.Length > 4096) return false;
        var parts = t.Split('.');
        if (parts.Length != 2) return false;
        var data = B64u(parts[0]); var sig = B64u(parts[1]);
        byte[] pub; try { pub = Convert.FromBase64String(publicKey ?? PublicKey); } catch { return false; }
        if (data == null || sig == null || sig.Length != 64 || pub.Length != 32) return false;
        if (!Ed25519.Verify(pub, data, sig)) return false;
        try
        {
            using var doc = JsonDocument.Parse(data);
            var r = doc.RootElement;
            string product = r.GetProperty("product").GetString() ?? "";
            int version = r.GetProperty("version").GetInt32();
            string id = r.GetProperty("id").GetString() ?? "";
            long issued = r.GetProperty("issued").GetInt64();
            long n = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
            if (id.Length == 0 || issued <= 0 || issued > n + 300) return false;
            if (product == "plp-snow" && version == 1) return true;
            if (product == "plp-seasons" && version == 2 && r.TryGetProperty("exp", out var e)) return e.GetInt64() >= n;
            return false;
        }
        catch { return false; }
    }
}

/// Ed25519 signature check (RFC 8032), plain BigInteger arithmetic: one check at start, speed does not matter.
public static class Ed25519
{
    static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
    static readonly BigInteger Lq = BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493");
    static readonly BigInteger D = Mod(-121665 * Inv(121666));
    static readonly BigInteger I = BigInteger.ModPow(2, (P - 1) / 4, P);
    static readonly BigInteger[] G;

    static Ed25519()
    {
        var gy = Mod(4 * Inv(5));
        var gx = RecoverX(gy, 0)!.Value;
        G = new[] { gx, gy, BigInteger.One, Mod(gx * gy) };
    }

    static BigInteger Mod(BigInteger a) { var r = a % P; return r < 0 ? r + P : r; }
    static BigInteger Inv(BigInteger a) => BigInteger.ModPow(Mod(a), P - 2, P);

    static BigInteger? RecoverX(BigInteger y, int sign)
    {
        if (y >= P) return null;
        var x2 = Mod((y * y - 1) * Inv(D * y * y + 1));
        if (x2 == 0) return sign == 0 ? BigInteger.Zero : null;
        var x = BigInteger.ModPow(x2, (P + 3) / 8, P);
        if (Mod(x * x - x2) != 0) x = Mod(x * I);
        if (Mod(x * x - x2) != 0) return null;
        if ((int)(x & 1) != sign) x = P - x;
        return x;
    }

    static BigInteger[] Add(BigInteger[] a, BigInteger[] b)
    {
        var A = Mod((a[1] - a[0]) * (b[1] - b[0]));
        var B = Mod((a[1] + a[0]) * (b[1] + b[0]));
        var C = Mod(2 * a[3] * b[3] * D);
        var Dd = Mod(2 * a[2] * b[2]);
        BigInteger E = B - A, F = Dd - C, Gg = Dd + C, H = B + A;
        return new[] { Mod(E * F), Mod(Gg * H), Mod(F * Gg), Mod(E * H) };
    }

    static BigInteger[] Mul(BigInteger s, BigInteger[] q)
    {
        var r = new[] { BigInteger.Zero, BigInteger.One, BigInteger.One, BigInteger.Zero };
        while (s > 0)
        {
            if (!s.IsEven) r = Add(r, q);
            q = Add(q, q);
            s >>= 1;
        }
        return r;
    }

    static bool Equal(BigInteger[] a, BigInteger[] b) =>
        Mod(a[0] * b[2] - b[0] * a[2]) == 0 && Mod(a[1] * b[2] - b[1] * a[2]) == 0;

    static BigInteger Le(ReadOnlySpan<byte> b) => new BigInteger(b, isUnsigned: true, isBigEndian: false);

    static BigInteger[]? Decompress(ReadOnlySpan<byte> s)
    {
        if (s.Length != 32) return null;
        var y = Le(s);
        int sign = (int)(y >> 255);
        y &= (BigInteger.One << 255) - 1;
        var x = RecoverX(y, sign);
        if (x == null) return null;
        return new[] { x.Value, y, BigInteger.One, Mod(x.Value * y) };
    }

    public static bool Verify(byte[] publicKey, byte[] message, byte[] signature)
    {
        if (publicKey.Length != 32 || signature.Length != 64) return false;
        var A = Decompress(publicKey); if (A == null) return false;
        var R = Decompress(signature.AsSpan(0, 32)); if (R == null) return false;
        var s = Le(signature.AsSpan(32, 32));
        if (s >= Lq) return false;
        var h = Le(SHA512.HashData(signature.AsSpan(0, 32).ToArray().Concat(publicKey).Concat(message).ToArray())) % Lq;
        return Equal(Mul(s, G), Add(R, Mul(h, A)));
    }
}
