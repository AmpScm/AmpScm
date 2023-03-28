using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Cryptography
{
    public abstract record class SignaturePromptContext
    {
        private SignaturePromptContext() { }


        public static SignaturePromptContext Empty { get; } = new DefType();

        private sealed record class DefType : SignaturePromptContext
        {
            public DefType()
            {
            }

        }
    }

    public record SignatureFetchContext
    {
        public ReadOnlyMemory<byte> Fingerprint { get; internal init; }

        public bool RequiresPrivateKey { get; internal init; }
    }
}
