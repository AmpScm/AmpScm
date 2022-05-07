using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class GitCommandAttribute : Attribute
    {
        public GitCommandAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

}
