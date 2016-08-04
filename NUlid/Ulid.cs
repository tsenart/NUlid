﻿using NUlid.Rng;
using System;
using System.Linq;
using System.Text;

namespace NUlid
{
    /// <summary>
    /// Represents a ulid (Universally Unique Lexicographically Sortable Identifier), based/inspired on
    /// <see href="https://github.com/alizain/ulid">alizain/ulid</see>.
    /// </summary>
    public struct Ulid : IEquatable<Ulid>, IComparable<Ulid>, IComparable
    {
        //TODO: Document exceptions (especially in constructors/parse)


        // Base32 "alphabet"
        private const string BASE32 = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        // Internal structure for data
        private readonly byte[] tdata;
        private readonly byte[] rdata;

        // Default RNG to use when no RNG is specified
        private static readonly IUlidRng DEFAULTRNG = new CSUlidRng();
        // Default EPOCH used for Ulid's
        private static readonly DateTimeOffset EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// A read-only instance of the <see cref="Ulid"/> structure whose value is all zeros.
        /// </summary>
        public static readonly Ulid Empty = new Ulid(EPOCH, Array.AsReadOnly(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }).ToArray());

        /// <summary>
        /// Represents the largest possible value of <see cref="Ulid"/>. This field is read-only.
        /// </summary>
        public static readonly Ulid MaxValue = new Ulid(DateTimeOffset.MaxValue, Array.AsReadOnly(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 }).ToArray());

        /// <summary>
        /// Gets the "time part" of the <see cref="Ulid"/>.
        /// </summary>
        public DateTimeOffset Time { get { return ByteArrayToDateTimeOffset(tdata); } }

        /// <summary>
        /// Gets the "random part" of the <see cref="Ulid"/>.
        /// </summary>
        public byte[] Random { get { return Array.AsReadOnly(rdata).ToArray(); } }

        /// <summary>
        /// Creates and returns a new <see cref="Ulid"/> based on the current (UTC) time and default
        /// (<see cref="CSUlidRng"/>) RNG.
        /// </summary>
        /// <returns>Returns a new <see cref="Ulid"/>.</returns>
        public static Ulid NewUlid()
        {
            return NewUlid(DateTimeOffset.UtcNow, DEFAULTRNG);
        }

        /// <summary>
        /// Creates and returns a new <see cref="Ulid"/> based on the specified time and default
        /// (<see cref="CSUlidRng"/>) RNG.
        /// </summary>
        /// <param name="time">
        /// The <see cref="DateTimeOffset"/> to use for the time-part of the <see cref="Ulid"/>.
        /// </param>
        /// <returns>Returns a new <see cref="Ulid"/>.</returns>
        public static Ulid NewUlid(DateTimeOffset time)
        {
            return NewUlid(time, DEFAULTRNG);
        }

        /// <summary>
        /// Creates and returns a new <see cref="Ulid"/> based on the current (UTC) time and using the specified RNG.
        /// </summary>
        /// <param name="rng">The <see cref="IUlidRng"/> to use for random number generation.</param>
        /// <returns>Returns a new <see cref="Ulid"/>.</returns>
        public static Ulid NewUlid(IUlidRng rng)
        {
            return NewUlid(DateTimeOffset.UtcNow, rng);
        }

        /// <summary>
        /// Creates and returns a new <see cref="Ulid"/> based on the specified time and using the specified RNG.
        /// </summary>
        /// <param name="time">
        /// The <see cref="DateTimeOffset"/> to use for the time-part of the <see cref="Ulid"/>.
        /// </param>
        /// <param name="rng">The <see cref="IUlidRng"/> to use for random number generation.</param>
        /// <returns>Returns a new <see cref="Ulid"/>.</returns>
        public static Ulid NewUlid(DateTimeOffset time, IUlidRng rng)
        {
            return new Ulid(time, rng.GetRandomBytes(10));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Ulid"/> structure by using the specified array of bytes.
        /// </summary>
        /// <param name="bytes">
        /// A 16-element byte array containing values with which to initialize the <see cref="Ulid"/>.
        /// </param>
        public Ulid(byte[] bytes)
        {
            if (bytes.Length != 16)
                throw new ArgumentException("An array of 16 elements is required", nameof(bytes));

            tdata = new byte[6];
            rdata = new byte[10];
            Array.Copy(bytes, tdata, 6);
            Array.Copy(bytes, 6, rdata, 0, 10);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Ulid"/> structure by using the specified <see cref="Guid"/>
        /// </summary>
        /// <param name="guid">A <see cref="Guid"/> representing a <see cref="Ulid"/>.</param>
        public Ulid(Guid guid)
        {
            this = new Ulid(guid.ToByteArray());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Ulid"/> structure by using the value represented by the
        /// specified string.
        /// </summary>
        /// <param name="ulid">A string that contains a <see cref="Ulid"/>.</param>
        public Ulid(string ulid)
        {
            this = Parse(ulid);
        }

        // Internal constructor
        private Ulid(DateTimeOffset timePart, byte[] randomPart)
        {
            if (timePart < EPOCH)
                throw new ArgumentOutOfRangeException(nameof(timePart));
            if (randomPart.Length != 10)
                throw new InvalidOperationException("randomPart must be 10 bytes");

            tdata = new byte[6];
            rdata = new byte[10];

            Array.Copy(DateTimeOffsetToByteArray(timePart), tdata, 6);
            Array.Copy(randomPart, rdata, 10);
        }

        #region Helper functions
        private static byte[] DateTimeOffsetToByteArray(DateTimeOffset value)
        {
            var mb = BitConverter.GetBytes(value.ToUnixTimeMilliseconds());
            var x = new[] { mb[5], mb[4], mb[3], mb[2], mb[1], mb[0] };
            return x;
        }

        private static DateTimeOffset ByteArrayToDateTimeOffset(byte[] value)
        {
            var tmp = new byte[] { value[5], value[4], value[3], value[2], value[1], value[0], 0, 0 };
            return DateTimeOffset.FromUnixTimeMilliseconds(BitConverter.ToInt64(tmp, 0));
        }

        private static string ToBase32(byte[] value)
        {
            // Hand-optimized unrolled loops ahead

            if (value.Length == 6)
            {
                return new string(
                    new[] {
                        /* 0  */ BASE32[(value[0] & 224) >> 5],                             /* 1  */ BASE32[value[0] & 31],
                        /* 2  */ BASE32[(value[1] & 248) >> 3],                             /* 3  */ BASE32[((value[1] & 7) << 2) | ((value[2] & 192) >> 6)],
                        /* 4  */ BASE32[(value[2] & 62) >> 1],                              /* 5  */ BASE32[((value[2] & 1) << 4) | ((value[3] & 240) >> 4)],
                        /* 6  */ BASE32[((value[3] & 15) << 1) | ((value[4] & 128) >> 7)],  /* 7  */ BASE32[(value[4] & 124) >> 2],
                        /* 8  */ BASE32[((value[4] & 3) << 3) | ((value[5] & 224) >> 5)],   /* 9  */ BASE32[value[5] & 31],
                    }
                );
            }
            if (value.Length == 10)
            {
                return new string(
                    new[] {
                        /* 0  */ BASE32[(value[0] & 248) >> 3],                             /* 1  */ BASE32[((value[0] & 7) << 2) | ((value[1] & 192) >> 6)],
                        /* 2  */ BASE32[(value[1] & 62) >> 1],                              /* 3  */ BASE32[((value[1] & 1) << 4) | ((value[2] & 240) >> 4)],
                        /* 4  */ BASE32[((value[2] & 15) << 1) | ((value[3] & 128) >> 7)],  /* 5  */ BASE32[(value[3] & 124) >> 2],  
                        /* 6  */ BASE32[((value[3] & 3) << 3) | ((value[4] & 224) >> 5)],   /* 7  */ BASE32[value[4] & 31],
                        /* 8  */ BASE32[(value[5] & 248) >> 3],                             /* 9  */ BASE32[((value[5] & 7) << 2) | ((value[6] & 192) >> 6)],
                        /* 10 */ BASE32[(value[6] & 62) >> 1],                              /* 11 */ BASE32[((value[6] & 1) << 4) | ((value[7] & 240) >> 4)],
                        /* 12 */ BASE32[((value[7] & 15) << 1) | ((value[8] & 128) >> 7)],  /* 13 */ BASE32[(value[8] & 124) >> 2],
                        /* 14 */ BASE32[((value[8] & 3) << 3) | ((value[9] & 224) >> 5)],   /* 15 */ BASE32[value[9] & 31],
                    }
                );
            }

            throw new InvalidOperationException("Invalid length");
        }

        private static byte[] FromBase32(string value)
        {
            // Determine indexes of chars
            var ix = value.Select(c => BASE32.IndexOf(c)).ToArray();

            // Hand-optimized unrolled loops ahead
            unchecked
            {
                if (ix.Length == 10)
                {
                    return new byte[]
                    {
                    /* 0 */ (byte)((ix[0] << 5) | ix[1]),                           /* 1 */ (byte)((ix[2] << 3) | (ix[3] >> 2)),
                    /* 2 */ (byte)((ix[3] << 6) | (ix[4] << 1) | (ix[5] >> 4)),     /* 3 */ (byte)((ix[5] << 4) | (ix[6] >> 1)),
                    /* 4 */ (byte)((ix[6] << 7) | (ix[7] << 2) | (ix[8] >> 3)),     /* 5 */ (byte)((ix[8] << 5) | ix[9]),
                    };
                }

                if (ix.Length == 16)
                {
                    return new byte[]
                    {
                    /* 0 */ (byte)((ix[0] << 3) | (ix[1] >> 2)),                    /* 1 */ (byte)((ix[1] << 6) | (ix[2] << 1) | (ix[3] >> 4)),
                    /* 2 */ (byte)((ix[3] << 4) | (ix[4] >> 1)),                    /* 3 */ (byte)((ix[4] << 7) | (ix[5] << 2) | (ix[6] >> 3)),
                    /* 4 */ (byte)((ix[6] << 5) | ix[7]),                           /* 5 */ (byte)((ix[8] << 3) | ix[9] >> 2),
                    /* 6 */ (byte)((ix[9] << 6) | (ix[10] << 1) | (ix[11] >> 4)),   /* 7 */ (byte)((ix[11] << 4) | (ix[12] >> 1)),
                    /* 8 */ (byte)((ix[12] << 7) | (ix[13] << 2) | (ix[14] >> 3)),  /* 9 */ (byte)((ix[14] << 5) | ix[15]),
                    };
                }
            }

            throw new InvalidOperationException("Invalid length");
        }
        #endregion

        /// <summary>
        /// Converts the string representation of a <see cref="Ulid"/> equivalent.
        /// </summary>
        /// <param name="s">A string containing a <see cref="Ulid"/> to convert.</param>
        /// <returns>A <see cref="Ulid"/> equivalent to the value contained in s.</returns>
        /// <exception cref="ArgumentNullException">s is null or empty.</exception>
        /// <exception cref="FormatException">s is not in the correct format.</exception>
        public static Ulid Parse(string s)
        {
            if (string.IsNullOrEmpty(s))
                throw new ArgumentNullException(nameof(s));

            var u = s.ToUpperInvariant();
            if (u.Length != 26 || u.Any(c => BASE32.IndexOf(c) < 0))
                throw new FormatException("Invalid Base32 string");
            
            var t = FromBase32(u.Substring(0, 10));
            var r = FromBase32(u.Substring(10, 16));

            return new Ulid(t.Concat(r).ToArray());
        }

        /// <summary>
        /// Converts the string representation of a <see cref="Ulid"/> to an instance of a <see cref="Ulid"/>. A return
        /// value indicates whether the conversion succeeded.
        /// </summary>
        /// <param name="s">A string containing the value to convert.</param>
        /// <param name="result">
        /// When this method returns, contains a <see cref="Ulid"/> equivalent of the <see cref="Ulid"/> contained in
        /// s, if the conversion succeeded, or null if the conversion failed. The conversion fails if the s parameter
        /// is null or <see cref="System.String.Empty"/>, is not of the correct format, or represents an invalid ulid
        /// otherwise. This parameter is passed uninitialized; any value originally supplied in result will be 
        /// overwritten.
        /// </param>
        /// <returns>true if s was converted successfully; otherwise, false.</returns>
        public static bool TryParse(string s, out Ulid result)
        {
            try
            {
                result = Parse(s);
                return true;
            }
            catch
            {
                result = Empty;
                return false;
            }
        }

        /// <summary>
        /// Returns the <see cref="Ulid"/> in string-representation.
        /// </summary>
        /// <returns>The <see cref="Ulid"/> in string-representation.</returns>
        public override string ToString()
        {
            return ToBase32(tdata) + ToBase32(rdata);
        }

        /// <summary>
        /// Returns a 16-element byte array that contains the value of this instance.
        /// </summary>
        /// <returns>A 16-element byte array.</returns>
        public byte[] ToByteArray()
        {
            return tdata.Concat(rdata).ToArray();
        }

        /// <summary>
        /// Returns a <see cref="Guid"/> that represents the value of this instance.
        /// </summary>
        /// <returns>A <see cref="Guid"/> that represents the value of this instance.</returns>
        public Guid ToGuid()
        {
            return new Guid(this.ToByteArray());
        }

        /// <summary>
        /// Returns a value indicating whether this instance and a specified <see cref="Ulid"/> object represent the
        /// same value.
        /// </summary>
        /// <param name="other">An <see cref="Ulid"/> to compare to this instance.</param>
        /// <returns>true if other is equal to this instance; otherwise, false.</returns>
        public bool Equals(Ulid other)
        {
            if (ReferenceEquals(other, this))
                return true;
            return this == other;
        }

        /// <summary>
        /// Returns a value that indicates whether this instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns>
        /// true if obj is a <see cref="Ulid"/> that has the same value as this instance; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            // Check that obj is a ulid first
            if (obj == null || !(obj is Ulid))
                return false;
            else return Equals((Ulid)obj);
        }

        /// <summary>
        /// Compares this instance to a specified <see cref="Ulid"/> object and returns an indication of their relative
        /// values.
        /// </summary>
        /// <param name="other">A <see cref="Ulid"/> to compare to this instance.</param>
        /// <returns>
        ///     <para>
        ///     A signed number indicating the relative values of this instance and value.
        ///     </para>
        ///     <list type="table">
        ///         <listheader>
        ///             <term>Return value</term>
        ///             <term>Description</term>
        ///         </listheader>
        ///         <item>
        ///             <term>A negative integer</term>
        ///             <term>This instance is less than value.</term>
        ///         </item>
        ///         <item>
        ///             <term>Zero</term>
        ///             <term>This instance is equal to value.</term>
        ///         </item>
        ///         <item>
        ///             <term>A positive integer</term>
        ///             <term>This instance is greater than value.</term>
        ///         </item>
        ///     </list>
        /// </returns>
        public int CompareTo(Ulid other)
        {
            if (this.Time != other.Time)
                return this.Time.CompareTo(other.Time);
            for (int i = 0; i < 16; i++)
            {
                if (this.Random[i] != other.Random[i])
                    return this.Random[i].CompareTo(other.Random[i]);
            }
            return 0;
        }

        /// <summary>
        /// Compares this instance to a specified object and returns an indication of their relative values.
        /// </summary>
        /// <param name="obj">An object to compare, or null.</param>
        /// <returns>
        ///     <para>
        ///     A signed number indicating the relative values of this instance and value.
        ///     </para>
        ///     <list type="table">
        ///         <listheader>
        ///             <term>Return value</term>
        ///             <term>Description</term>
        ///         </listheader>
        ///         <item>
        ///             <term>A negative integer</term>
        ///             <term>This instance is less than value.</term>
        ///         </item>
        ///         <item>
        ///             <term>Zero</term>
        ///             <term>This instance is equal to value.</term>
        ///         </item>
        ///         <item>
        ///             <term>A positive integer</term>
        ///             <term>This instance is greater than value.</term>
        ///         </item>
        ///     </list>
        /// </returns>
        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }
            if (!(obj is Ulid))
            {
                throw new ArgumentException("Object must be Ulid", nameof(obj));
            }
            return CompareTo((Ulid)obj);
        }

        /// <summary>
        /// Indicates whether the values of two specified <see cref="Ulid"/> objects are equal.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <returns>true if x and y are equal; otherwise, false.</returns>
        public static bool operator ==(Ulid x, Ulid y)
        {
            return (x.Time == y.Time) && x.Random.SequenceEqual(y.Random);
        }

        /// <summary>
        /// Indicates whether the values of two specified <see cref="Ulid"/> objects are not equal.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <returns>true if x and y are not equal; otherwise, false.</returns>
        public static bool operator !=(Ulid x, Ulid y)
        {
            return !(x == y);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>The hash code for this instance.</returns>
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = (int)2166136261;
                // Suitable nullity checks etc, of course :)
                hash = (hash * 16777619) ^ Time.GetHashCode();
                for (int i = 0; i < Random.Length; i++)
                    hash = (hash * 16777619) ^ Random[i];
                return hash;
            }
        }
    }
}
