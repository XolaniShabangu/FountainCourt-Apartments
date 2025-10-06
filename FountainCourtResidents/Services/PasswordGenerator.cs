using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Web;

namespace FountainCourtResidents.Services
{
    public static class PasswordGenerator
    {
        private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // no I/O
        private const string Lower = "abcdefghijkmnopqrstuvwxyz"; // no l
        private const string Digits = "0123456789";
        private const string Special = "!@#$%^&*?";

        public static string Generate(int length = 12)
        {
            // Ensure minimum length for complexity
            if (length < 12) length = 12;

            var all = Upper + Lower + Digits + Special;
            var chars = new List<char>(length);

            // Required categories (aligns with Identity defaults)
            chars.Add(GetRandomChar(Upper));
            chars.Add(GetRandomChar(Lower));
            chars.Add(GetRandomChar(Digits));
            chars.Add(GetRandomChar(Special));

            // Fill the rest
            while (chars.Count < length)
                chars.Add(GetRandomChar(all));

            // Shuffle to avoid predictable positions
            Shuffle(chars);

            return new string(chars.ToArray());
        }

        private static char GetRandomChar(string allowed)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var buffer = new byte[4];
                while (true)
                {
                    rng.GetBytes(buffer);
                    uint num = BitConverter.ToUInt32(buffer, 0);
                    int idx = (int)(num % (uint)allowed.Length);
                    return allowed[idx];
                }
            }
        }

        private static void Shuffle(List<char> list)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = GetInt32(rng, i + 1);
                    (list[i], list[j]) = (list[j], list[i]);
                }
            }
        }

        private static int GetInt32(RandomNumberGenerator rng, int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            var uint32Buffer = new byte[4];
            uint biasFree = (uint.MaxValue / (uint)maxExclusive) * (uint)maxExclusive;
            uint r;
            do
            {
                rng.GetBytes(uint32Buffer);
                r = BitConverter.ToUInt32(uint32Buffer, 0);
            } while (r >= biasFree);
            return (int)(r % (uint)maxExclusive);
        }
    }
}