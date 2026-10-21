#nullable enable
using System;
using System.Security.Cryptography;

namespace TransportTycoon.Core.Tests.Simulation
{
    internal static class DeterministicStateHasher
    {
        public static byte[] ComputeSha256(byte[] canonicalSnapshot)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (canonicalSnapshot == null)
            {
                throw new ArgumentNullException(nameof(canonicalSnapshot));
            }
#pragma warning restore CA1510

#pragma warning disable CA1850 // Unity's Core API cannot rely on the newer HashData helper.
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(canonicalSnapshot);
            }
#pragma warning restore CA1850
        }

        public static string ToHexString(byte[] hash)
        {
            const string hexDigits = "0123456789ABCDEF";
            var characters = new char[hash.Length * 2];
            for (int index = 0; index < hash.Length; index++)
            {
                byte value = hash[index];
                characters[index * 2] = hexDigits[value >> 4];
                characters[(index * 2) + 1] = hexDigits[value & 0x0F];
            }

            return new string(characters);
        }
    }
}
