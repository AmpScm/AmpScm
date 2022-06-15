using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Git
{
    public enum OpenPgpTagType
    {
        /// <summary>Reserved</summary>
        None = 0,
        PublicKeySession = 1,
        Signature = 2,
        SymetricSessionKey = 3,
        OnePassSignature = 4,
        SecretKey = 5,
        PublicKey = 6,
        SecretSubkey = 7,
        CompressedData = 8,
        SymetricEncryptedData = 9,
        Marker = 10,
        Literal = 11,
        Trust = 12,
        UserID = 13,
        PublicSubkey = 14,
        // 15-59 undefined yet
        //60 to 63 -- Private or Experimental Values
    }

    enum OpenPgpSignatureType : byte
    {
        BinaryDocument = 0x00,
        CanonicalTextDocument = 0x01, // EOL -> CRLF
    }

    enum OpenPgpSubPacketType : byte
    {
        /// <summary>Reserved</summary>
        None = 0,
        SignatureCreationTime = 2,
        SignatureExpirationTime = 3,
        Issuer = 16
    }

    enum OpenPgpHashAlgorithm : byte
    {
        None,
        MD5 = 1,
        Sha1 = 2,
        MD160 = 3,
        SHA256 = 8,
        SHA384 = 9,
        SHA512 = 10,
        SHA224 = 11
    }

    enum OpenPgpPublicKeyType : byte
    {
        None,
        Rsa = 1,
        RsaEncryptOnly = 2,
        RsaSignOnly = 3,
        Elgamal = 16,
        DSA = 17,
        EllipticCurve = 18,
        ECDSA = 19,
        DHE = 21
    }

    public class OpenPgpSignatureBucket : WrappingBucket
    {
        enum SState
        {
            init,
            body,
            eof
        }
        SState _state;
        private OpenPgpSignatureType _signatureType;
        private ulong? _signer;
        private OpenPgpPublicKeyType _publicKeyType;
        private OpenPgpHashAlgorithm _hashAlgorithm;
        private ushort _hashStart;
        DateTime? _signTime;

        new OpenPgpContainer Inner => (OpenPgpContainer)base.Inner;

        public OpenPgpSignatureBucket(Bucket inner)
            : base(new OpenPgpContainer(inner))
        {
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            if (_state == SState.init)
                await ReadHeaderAsync().ConfigureAwait(false);

            return await Inner.ReadAsync(requested).ConfigureAwait(false);
        }

        public ValueTask<OpenPgpTagType> ReadTagAsync()
        {
            return Inner.ReadTagAsync();
        }

        public async ValueTask ReadHeaderAsync()
        {
            if (_state != SState.init)
                return;

            var tag = await Inner.ReadTagAsync().ConfigureAwait(false);
            switch (tag)
            {
                case OpenPgpTagType.Signature:
                    {
                        byte version = await Inner.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(Inner);

                        if (Inner.IsSshSignature)
                        {
                            var bb = await Inner.ReadExactlyAsync(3).ConfigureAwait(false);

                            if (bb.Length < 3)
                                throw new BucketEofException();

                            byte[] ver = new byte[4];
                            ver[0] = version;
                            bb.CopyTo(ver.AsSpan(1));

                            uint sshVersion = NetBitConverter.ToUInt32(ver, 0);

                            if (sshVersion != 1)
                                throw new BucketException();

                            // BH: No idea what we read here...
                            var publicKeyLen = await Inner.ReadNetworkUInt32Async().ConfigureAwait(false);

                            if (publicKeyLen > 0)
                            {
                                using var publicKeyBucket = Inner.NoClose().TakeExactly(publicKeyLen);

                                while (await publicKeyBucket.ReadRemainingBytesAsync().ConfigureAwait(false) > 0)
                                {
                                    var s = (await ReadSshStringAsync(publicKeyBucket).ConfigureAwait(false)).ToASCIIString();

                                    Console.WriteLine($"PK: {s}");
                                }
                            }

                            var sigNamespace = (await ReadSshStringAsync(Inner).ConfigureAwait(false)).ToASCIIString();
                            var sigReserved = (await ReadSshStringAsync(Inner).ConfigureAwait(false)).ToASCIIString();
                            var sigHashAlgo = (await ReadSshStringAsync(Inner).ConfigureAwait(false)).ToASCIIString();
                            var signatureLen = await Inner.ReadNetworkUInt32Async().ConfigureAwait(false);

                            if (signatureLen > 0)
                            {
                                using var signatureBucket = Inner.NoClose().TakeExactly(signatureLen);

                                while (await signatureBucket.ReadRemainingBytesAsync().ConfigureAwait(false) > 0)
                                {
                                    var s = (await ReadSshStringAsync(signatureBucket).ConfigureAwait(false)).ToASCIIString();

                                    Console.WriteLine($"SIG: {s}");
                                }
                            }
                        }
                        else if (version == 4 || version == 5)
                        {
                            /*                        
                             * - One-octet version number (4).
                             *
                             * - One-octet signature type.
                             *
                             * - One-octet public-key algorithm.
                             *
                             * - One-octet hash algorithm.
                             *
                             * - Two-octet scalar octet count for following hashed subpacket data.
                             *   Note that this is the length in octets of all of the hashed
                             *   subpackets; a pointer incremented by this number will skip over
                             *   the hashed subpackets.
                             *
                             * - Hashed subpacket data set (zero or more subpackets).
                             *
                             * - Two-octet scalar octet count for the following unhashed subpacket
                             *   data.  Note that this is the length in octets of all of the
                             *   unhashed subpackets; a pointer incremented by this number will
                             *   skip over the unhashed subpackets.
                             *
                             * - Unhashed subpacket data set (zero or more subpackets).
                             *
                             * - Two-octet field holding the left 16 bits of the signed hash
                             *   value.
                             *
                             * - One or more multiprecision integers comprising the signature.
                             *   This portion is algorithm specific, as described above.
                             */

                            int hdrLen = (version == 4) ? 5 : 7;

                            var bb = await Inner.ReadExactlyAsync(hdrLen).ConfigureAwait(false);

                            if (bb.Length != hdrLen)
                                throw new BucketEofException(Inner);

                            _signatureType = (OpenPgpSignatureType)bb[0];
                            _publicKeyType = (OpenPgpPublicKeyType)bb[1];
                            _hashAlgorithm = (OpenPgpHashAlgorithm)bb[2];
                            int subLen;

                            if (version == 4)
                                subLen = NetBitConverter.ToUInt16(bb, 3);
                            else
                                subLen = (int)NetBitConverter.ToUInt32(bb, 3);

                            if (subLen > 0)
                            {
                                using var subRead = Inner.NoClose().TakeExactly(subLen);

                                while (true)
                                {
                                    uint? len = await ReadLengthAsync(subRead).ConfigureAwait(false);

                                    if (!len.HasValue)
                                        break;

                                    var b = await subRead.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(subRead);
                                    len--;

                                    switch ((OpenPgpSubPacketType)b)
                                    {
                                        case OpenPgpSubPacketType.SignatureCreationTime:
                                            if (len != 4)
                                                throw new InvalidOperationException();

                                            var time = await subRead.ReadNetworkInt32Async().ConfigureAwait(false);

                                            _signTime = DateTimeOffset.FromUnixTimeSeconds(time).DateTime;
                                            break;
                                        case OpenPgpSubPacketType.Issuer:
                                            if (len != 8)
                                                throw new InvalidOperationException();

                                            _signer = await subRead.ReadNetworkUInt64Async().ConfigureAwait(false);
                                            break;
                                        default:
                                            if (len != await subRead.ReadSkipAsync(len.Value).ConfigureAwait(false))
                                                throw new BucketEofException(subRead);
                                            break;
                                    }

                                }
                            }

                            // TODO: Fetch Date and Issuer if needed
                            uint unhashedLen;

                            if (version == 4)
                                unhashedLen = await Inner.ReadNetworkUInt16Async().ConfigureAwait(false);
                            else
                                unhashedLen = await Inner.ReadNetworkUInt32Async().ConfigureAwait(false);

                            if (unhashedLen > 0)
                            {
                                if (unhashedLen != await Inner.ReadSkipAsync(unhashedLen).ConfigureAwait(false))
                                    throw new BucketEofException(Inner);
                            }

                            // First 2 bytes of hash
                            _hashStart = NetBitConverter.ToUInt16(bb, 0);

                            if (version != 4)
                            {
                                // In v5, 16 bytes of salt
                                bb = await Inner.ReadExactlyAsync(16).ConfigureAwait(false);
                                if (bb.Length != 16)
                                    throw new BucketEofException(Inner);
                            }
                        }
                        else if (version == 3)
                        {
                            // Implementations MUST accept V3 signatures.
                            /*
                             *      - One-octet version number (3).
                             *      - One-octet length of following hashed material.  MUST be 5.
                             *          - One-octet signature type.
                             *          - Four-octet creation time.
                             *      - Eight-octet key ID of signer.
                             *      - One-octet public key algorithm.
                             *      - One-octet hash algorithm.
                             *      - Two-octet field holding left 16 bits of signed hash value.
                             */
                            var bb = await Inner.ReadExactlyAsync(18).ConfigureAwait(false);
                            if (bb[0] != 5)
                                throw new BucketException($"HashInfoLen must by 5 for v3 in {Inner.Name}");
                            _signatureType = (OpenPgpSignatureType)bb[1];
                            _signTime = DateTimeOffset.FromUnixTimeSeconds(NetBitConverter.ToUInt32(bb, 2)).DateTime;
                            _signer = NetBitConverter.ToUInt64(bb, 6);
                            _publicKeyType = (OpenPgpPublicKeyType)bb[14];
                            _hashAlgorithm = (OpenPgpHashAlgorithm)bb[15];
                            _hashStart = NetBitConverter.ToUInt16(bb, 16);
                        }
                        else
                            throw new NotImplementedException("Only signature versions 3, 4 and 5 are supported");
                    }
                    break;
                default:
                    throw new BucketException($"Unexpected OpenPgp Tag '{tag}' in '{Inner.Name}' Bucket");
            }

            _state = SState.body;
        }

        private static async ValueTask<uint?> ReadLengthAsync(Bucket bucket)
        {
            var b = await bucket.ReadByteAsync().ConfigureAwait(false);

            if (!b.HasValue)
                return null;

            if (b < 192)
                return b;

            else if (b < 224)
            {
                var b2 = await bucket.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(bucket);

                return (uint)(((b - 192) << 8) + b2 + 192);
            }
            else if (b == 255)
            {
                return await bucket.ReadNetworkUInt32Async().ConfigureAwait(false);
            }
            else
                throw new NotImplementedException("Partial lengths");
        }

        private static async ValueTask<BucketBytes> ReadSshStringAsync(Bucket bucket)
        {
            int len = (int)await bucket.ReadNetworkUInt32Async().ConfigureAwait(false);

            if (len == 0)
                return BucketBytes.Empty;

            return await bucket.ReadExactlyAsync(len).ConfigureAwait(false);
        }

        sealed class OpenPgpContainer : WrappingBucket
        {
            enum SState
            {
                init,
                size,
                body,
                sshbody,
                eof
            }
            SState _state;
            bool _oldFormat;
            OpenPgpTagType? _tag;
            uint _remaining;

            public OpenPgpContainer(Bucket inner) : base(inner)
            {
            }

            public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
            {
                if (_state != SState.body && _state != SState.sshbody)
                    await RunAsync().ConfigureAwait(false);

                if (_state == SState.eof)
                    return BucketBytes.Eof;

                if (_state != SState.sshbody)
                {
                    BucketBytes bb;
                    if (_remaining == 0)
                    {
                        bb = await Inner.ReadAsync(1).ConfigureAwait(false);

                        if (!bb.IsEof)
                            throw new BucketException($"Expected EOF, but got byte on {Inner.Name}");

                        _state = SState.eof;
                        return BucketBytes.Eof;
                    }

                    requested = Math.Min(requested, _remaining < MaxRead ? (int)_remaining : MaxRead);

                    bb = await Inner.ReadAsync(requested).ConfigureAwait(false);

                    _remaining -= (uint)bb.Length;

                    return bb;
                }
                else
                {
                    return await Inner.ReadAsync(requested).ConfigureAwait(false);
                }
            }

            public override BucketBytes Peek()
            {
                switch (_state)
                {
                    case SState.body:
                        var bb = Inner.Peek();
                        if (bb.Length > _remaining)
                            bb = bb.Slice(0, _remaining > int.MaxValue ? int.MaxValue : (int)_remaining);
                        return bb;
                    case SState.sshbody:
                        return Inner.Peek();
                    default:
                        return base.Peek();
                }
            }
            public bool IsSshSignature => _state == SState.sshbody;


            private async ValueTask RunAsync()
            {
                byte b;

                switch (_state)
                {
                    case SState.init:
                        b = await Inner.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(Inner);

                        if ((b & 0x80) == 0)
                        {
                            if (b == 'S')
                            {
                                // We may have an SSH Signature
                                var bb = Inner.Peek();

                                if ((bb.Length >= 5 && bb.StartsWithASCII("SHSIG"))
                                    || (bb.Length >= 2 && bb.StartsWithASCII("SH")))
                                {
                                    bb = await Inner.ReadExactlyAsync(5).ConfigureAwait(false);

                                    if (bb.Length == 5 && bb.StartsWithASCII("SHSIG"))
                                    {
                                        _state = SState.sshbody;
                                        _tag = OpenPgpTagType.Signature;
                                        break;
                                    }
                                }
                            }
                            throw new GitBucketException("Not a valid RFC1440 body");
                        }
                        _oldFormat = (0 == (b & 0x40));
                        if (_oldFormat)
                            _tag = (OpenPgpTagType)((b & 0x2c) >> 2);
                        else
                            _tag = (OpenPgpTagType)(b & 0x2F);
                        _state = SState.size;
                        goto case SState.size;
                    case SState.size:
                        if (!_oldFormat)
                        {
                            uint len = await ReadLengthAsync(Inner).ConfigureAwait(false) ?? throw new BucketEofException(Inner);

                            _remaining = len;
                        }
                        else
                            throw new NotImplementedException("Old size");
                        _state = SState.body;
                        break;
                    case SState.body:
                    case SState.sshbody:
                    case SState.eof:
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            public override ValueTask<long?> ReadRemainingBytesAsync()
            {
                if (_state == SState.body)
                    return new(_remaining);
                else if (_state == SState.eof)
                    return new(0);

                return new((long?)null);
            }

            public async ValueTask<OpenPgpTagType> ReadTagAsync()
            {
                if (!_tag.HasValue)
                    await RunAsync().ConfigureAwait(false);

                return _tag!.Value;
            }
        }
    }
}
