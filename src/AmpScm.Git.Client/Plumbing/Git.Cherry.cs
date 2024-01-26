using System;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitCherryArgs : GitPlumbingArgs
    {
        public override void Verify()
        {
            throw new NotImplementedException();
        }
    }

    public partial class GitPlumbing
    {
        [GitCommand("cherry")]
        public static async ValueTask Cherry(this GitPlumbingClient c, GitCherryArgs options)
        {
            options.Verify();
            //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

            await c.ThrowNotImplemented();
        }
    }
}
