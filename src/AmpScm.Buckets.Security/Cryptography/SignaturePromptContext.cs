using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Cryptography
{
    public abstract class SignaturePromptContext
    {
        private SignaturePromptContext() { }


        public static SignaturePromptContext Empty { get; } = new DefType();

        private class DefType : SignaturePromptContext
        {
            public DefType()
            {
            }

        }
    }
}
