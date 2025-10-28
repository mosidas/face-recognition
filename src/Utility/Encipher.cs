using System.Text;
using System.Security.Cryptography;

namespace Utility;

public class Encipher
{
  public const int KeySize = 32; // 256-bit
  public const int NonceSize = 12; // AES-GCM推奨
  public const int TagSize = 16;   // 128-bit

  public static byte[] GenerateKey()
  {
      return RandomNumberGenerator.GetBytes(KeySize);
  }

  public static byte[] GenerateNonce()
  {
      return RandomNumberGenerator.GetBytes(NonceSize);
  }

  public static byte[] Encrypt(byte[] key, byte[] nonce, byte[] plaintext, byte[]? aad = null)
  {
    if (key is null || key.Length != KeySize) throw new ArgumentException("invalid key size", nameof(key));
    if (nonce is null || nonce.Length != NonceSize) throw new ArgumentException("invalid nonce size", nameof(nonce));
    ArgumentNullException.ThrowIfNull(plaintext);

    byte[] ctTag = new byte[plaintext.Length + TagSize];
    var ctSpan  = ctTag.AsSpan(0, plaintext.Length);
    var tagSpan = ctTag.AsSpan(plaintext.Length, TagSize);

    using var gcm = new AesGcm(key, TagSize);
    gcm.Encrypt(nonce, plaintext, ctSpan, tagSpan, aad);

    return ctTag; // ciphertext||tag
  }

  public static byte[] Decrypt(byte[] key, byte[] nonce, byte[] ciphertextWithTag, byte[]? aad = null)
  {
      if (key is null || key.Length != KeySize) throw new ArgumentException("invalid key size", nameof(key));
      if (nonce is null || nonce.Length != NonceSize) throw new ArgumentException("invalid nonce size", nameof(nonce));
      if (ciphertextWithTag is null || ciphertextWithTag.Length < TagSize)
          throw new ArgumentException("ciphertext too short", nameof(ciphertextWithTag));

      int n = ciphertextWithTag.Length - TagSize;
      var ctSpan  = ciphertextWithTag.AsSpan(0, n);
      var tagSpan = ciphertextWithTag.AsSpan(n, TagSize);

      byte[] pt = new byte[n];
      using var gcm = new AesGcm(key, TagSize);
      gcm.Decrypt(nonce, ctSpan, tagSpan, pt, aad);

      return pt;
  }
}
