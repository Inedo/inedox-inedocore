using System;
using System.Text;

namespace Inedo.Extensions
{
    /// <summary>
    /// Represents a POSIX-style octal file mode.
    /// </summary>
    [Serializable]
    internal struct PosixFileMode : IEquatable<PosixFileMode>
    {
        /// <summary>
        /// No permissions.
        /// </summary>
        public static readonly PosixFileMode Clear = new();
        /// <summary>
        /// All permissions (unrestricted).
        /// </summary>
        public static readonly PosixFileMode Full = FromDecimal(777);

        /// <summary>
        /// Initializes a new instance of the <see cref="PosixFileMode"/> struct.
        /// </summary>
        /// <param name="octalValue">The value in native octal format.</param>
        public PosixFileMode(int octalValue)
        {
            this.OctalValue = octalValue;
        }

        public static bool operator ==(PosixFileMode mode1, PosixFileMode mode2) => Equals(mode1, mode2);
        public static bool operator !=(PosixFileMode mode1, PosixFileMode mode2) => !Equals(mode1, mode2);

        /// <summary>
        /// Gets the value in native octal format.
        /// </summary>
        public int OctalValue { get; }
        /// <summary>
        /// Gets the value in human-readable decimal format.
        /// </summary>
        public int DecimalValue => GetDecimalValue((this.OctalValue >> 6) & 0x7, (this.OctalValue >> 3) & 0x7, this.OctalValue & 0x7);

        /// <summary>
        /// Parses a decimal value into a native octal representation.
        /// </summary>
        /// <param name="mode">Decimal representation of the octal value.</param>
        /// <returns>Parsed <see cref="PosixFileMode"/> instance.</returns>
        public static PosixFileMode FromDecimal(int mode)
        {
            int user = (mode / 100) % 10;
            int group = (mode / 10) % 10;
            int other = mode % 10;
            return new PosixFileMode(GetOctalValue(user, group, other));
        }
        public static bool Equals(PosixFileMode mode1, PosixFileMode mode2) => mode1.OctalValue == mode2.OctalValue;

        public bool Equals(PosixFileMode other) => Equals(this, other);
        public override bool Equals(object obj) => obj is PosixFileMode mode && Equals(this, mode);
        public override int GetHashCode() => this.OctalValue.GetHashCode();
        public override string ToString()
        {
            var buffer = new StringBuilder(9);
            FormatSegment((this.OctalValue >> 6) & 0x7, buffer);
            FormatSegment((this.OctalValue >> 3) & 0x7, buffer);
            FormatSegment(this.OctalValue & 0x7, buffer);
            return buffer.ToString();
        }

        private static int GetOctalValue(int user, int group, int other) => (user << 6) | (group << 3) | other;
        private static int GetDecimalValue(int user, int group, int other) => (user * 100) + (group * 10) + other;
        private static void FormatSegment(int segment, StringBuilder buffer)
        {
            buffer.Append((segment & 4) != 0 ? 'r' : '-');
            buffer.Append((segment & 2) != 0 ? 'w' : '-');
            buffer.Append((segment & 1) != 0 ? 'x' : '-');
        }
    }
}
