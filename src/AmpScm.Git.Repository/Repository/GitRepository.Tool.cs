using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Implementation;
using AmpScm.Git.Repository;

namespace AmpScm.Git
{
    partial class GitRepository
    {
#if NETFRAMEWORK
        static void FixConsoleUTF8BOMEncoding()
        {
            var ci = Console.InputEncoding;
            if (ci == Encoding.UTF8 && ci.GetPreamble().Length > 0)
            {
                // Workaround CHCP 65001 / UTF8 bug, where the process will always write a BOM to each started process
                // with Stdin redirected, which breaks processes which explicitly expect some strings as binary data
                Console.InputEncoding = new UTF8Encoding(false, true);
            }
        }
#endif

        protected internal async ValueTask<int> RunGitCommandAsync(string command, IEnumerable<string>? args, string? stdinText = null, int[]? expectedResults = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(GitConfiguration.GitProgramPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = this.FullPath,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            IEnumerable<string> allArgs = new string[] { command }.Concat(args ?? Array.Empty<string>());
#if NETFRAMEWORK
            startInfo.Arguments = string.Join(" ", allArgs.Select(x => EscapeGitCommandlineArgument(x)));
            FixConsoleUTF8BOMEncoding();
#else
            foreach (var v in allArgs)
                startInfo.ArgumentList.Add(v);
#endif

            using var p = Process.Start(startInfo);

            if (p == null)
                throw new GitExecCommandException($"Unable to start 'git {command}' operation");

            var rcv = new OutputReceiver(p);

            if (!string.IsNullOrEmpty(stdinText))
                await p.StandardInput.WriteAsync(stdinText).ConfigureAwait(false);

            p.StandardInput.Close();

            await Task.WhenAll(p.WaitForExitAsync(), rcv.DoneTask).ConfigureAwait(false);

            if (expectedResults != null ? !(expectedResults.Length == 0 || expectedResults.Contains(p.ExitCode)) : p.ExitCode != 0)
                throw new GitExecCommandException($"Unexpected error {p.ExitCode} from 'git {command}' operation: {rcv.StdErr}");

            return p.ExitCode;
        }

        protected internal async ValueTask<(int ExitCode, string OutputText)> RunGitCommandOutAsync(string command, IEnumerable<string>? args, string? stdinText = null, int[]? expectedResults = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(GitConfiguration.GitProgramPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = this.FullPath,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            IEnumerable<string> allArgs = new string[] { command }.Concat(args ?? Array.Empty<string>());
#if NETFRAMEWORK
            startInfo.Arguments = string.Join(" ", allArgs.Select(x => EscapeGitCommandlineArgument(x)));
            FixConsoleUTF8BOMEncoding();
#else
            foreach (var v in allArgs)
                startInfo.ArgumentList.Add(v);
#endif

            using var p = Process.Start(startInfo);

            if (p == null)
                throw new GitExecCommandException($"Unable to start 'git {command}' operation");

            var rcv = new OutputReceiver(p);

            if (!string.IsNullOrEmpty(stdinText))
                await p.StandardInput.WriteAsync(stdinText).ConfigureAwait(false);

            p.StandardInput.Close();

            await Task.WhenAll(p.WaitForExitAsync(), rcv.DoneTask).ConfigureAwait(false);

            if (expectedResults != null ? !(expectedResults.Length == 0 || expectedResults.Contains(p.ExitCode)) : p.ExitCode != 0)
                throw new GitExecCommandException($"Unexpected error {p.ExitCode} from 'git {command}' operation: {rcv.StdErr}");

            return (p.ExitCode, rcv.StdOut);
        }

        protected internal async ValueTask<(int ExitCode, string OutputText, string ErrorText)> RunGitCommandErrAsync(string command, IEnumerable<string>? args, string? stdinText = null, int[]? expectedResults = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(GitConfiguration.GitProgramPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = this.FullPath,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            IEnumerable<string> allArgs = new string[] { command }.Concat(args ?? Array.Empty<string>());
#if NETFRAMEWORK
            startInfo.Arguments = string.Join(" ", allArgs.Select(x => EscapeGitCommandlineArgument(x)));
            FixConsoleUTF8BOMEncoding();
#else
            foreach (var v in allArgs)
                startInfo.ArgumentList.Add(v);
#endif

            using var p = Process.Start(startInfo);

            if (p == null)
                throw new GitExecCommandException($"Unable to start 'git {command}' operation");

            var rcv = new OutputReceiver(p);

            if (!string.IsNullOrEmpty(stdinText))
                await p.StandardInput.WriteAsync(stdinText).ConfigureAwait(false);

            p.StandardInput.Close();

            await Task.WhenAll(p.WaitForExitAsync(), rcv.DoneTask).ConfigureAwait(false);

            if (expectedResults != null ? !(expectedResults.Length == 0 || expectedResults.Contains(p.ExitCode)) : p.ExitCode != 0)
                throw new GitExecCommandException($"Unexpected error {p.ExitCode} from 'git {command}' operation");

            return (p.ExitCode, rcv.StdOut, rcv.StdErr);
        }

        protected internal async ValueTask<Bucket> RunGitCommandBucketAsync(string command, IEnumerable<string>? args, string? stdinText = null, int[]? expectedResults = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(GitConfiguration.GitProgramPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = this.FullPath,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            IEnumerable<string> allArgs = new string[] { command }.Concat(args ?? Array.Empty<string>());
#if NETFRAMEWORK
            startInfo.Arguments = string.Join(" ", allArgs.Select(x => EscapeGitCommandlineArgument(x)));
            FixConsoleUTF8BOMEncoding();
#else
            foreach (var v in allArgs)
                startInfo.ArgumentList.Add(v);
#endif

            using var p = Process.Start(startInfo);

            if (p == null)
                throw new GitExecCommandException($"Unable to start 'git {command}' operation");

            var rcv = new ErrorReceiver(p);

            if (!string.IsNullOrEmpty(stdinText))
                await p.StandardInput.WriteAsync(stdinText).ConfigureAwait(false);

            p.StandardInput.Close();

            return p.StandardOutput.BaseStream.AsBucket().AtEof(async () =>
            {
                await Task.WhenAll(p.WaitForExitAsync(), rcv.DoneTask).ConfigureAwait(false);

                if (expectedResults != null ? !(expectedResults.Length == 0 || expectedResults.Contains(p.ExitCode)) : p.ExitCode != 0)
                    throw new GitExecCommandException($"Unexpected error {p.ExitCode} from 'git {command}' operation");
            });
        }

        internal async ValueTask<(int ExitCode, string OutputText, string ErrorText)> RunHookErrAsync(string hook, IEnumerable<string>? args, string? stdinText = null, int[]? expectedResults = null)
        {
            if (!await Configuration.HookExistsAsync(hook).ConfigureAwait(false))
                return (0, "", "");

            List<string> aa = new List<string>();
            aa.AddRange(new[] { "run", hook, "--" });
            aa.AddRange(args ?? Enumerable.Empty<string>());
            return await RunGitCommandErrAsync("hook", aa, stdinText, expectedResults).ConfigureAwait(false);
        }

        protected internal IAsyncEnumerable<string> WalkPlumbingCommand(string command, IEnumerable<string>? args, string? stdinText = null, int[]? expectedResults = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(GitConfiguration.GitProgramPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = this.FullPath,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            IEnumerable<string> allArgs = new string[] { command }.Concat(args ?? Array.Empty<string>());
#if NETFRAMEWORK
            startInfo.Arguments = string.Join(" ", allArgs.Select(x => EscapeGitCommandlineArgument(x)));
            FixConsoleUTF8BOMEncoding();
#else
            foreach (var v in allArgs)
                startInfo.ArgumentList.Add(v);
#endif

            var p = Process.Start(startInfo);

            if (p == null)
                throw new GitExecCommandException($"Unable to start 'git {command}' operation");

            if (string.IsNullOrEmpty(stdinText))
                p.StandardInput.Close();

            return new StdOutputLineWalker(p, stdinText, expectedResults);
        }

        sealed class StdOutputLineWalker : IAsyncEnumerable<string>, IAsyncEnumerator<string>
        {
            readonly Process _p;
            readonly StreamReader _reader;
            string? _stdinText;
            bool _eof;
            string? _current;
            readonly int[]? _expectedResults;
            ErrorReceiver _rcv;

            public StdOutputLineWalker(Process p, string? stdinText, int[]? expectedResults)
            {
                if (p is null)
                    throw new ArgumentNullException(nameof(p));

                _rcv = new ErrorReceiver(p);
                _p = p;
                _stdinText = stdinText;
                _expectedResults = expectedResults;
                _reader = p.StandardOutput;
            }

            public string Current => _current!;

            public ValueTask DisposeAsync()
            {
                _p.Dispose();
                return default;
            }

            public IAsyncEnumerator<string> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return this;
            }

            public async ValueTask<bool> MoveNextAsync()
            {
                if (_eof)
                    return false;

                if (!string.IsNullOrEmpty(_stdinText))
                {
                    await _p.StandardInput.WriteAsync(_stdinText).ConfigureAwait(false);
                    _p.StandardInput.Close();
                    _stdinText = null;
                }

                _current = await _reader.ReadLineAsync().ConfigureAwait(false);

                if (_current is null)
                {
                    _eof = true;
                    await Task.WhenAll(_p.WaitForExitAsync(), _rcv.DoneTask).ConfigureAwait(false);
                    using (_p)
                    {
                        if (_expectedResults != null ? !(_expectedResults.Length == 0 || _expectedResults.Contains(_p.ExitCode)) : _p.ExitCode != 0)
                            throw new GitExecCommandException($"Unexpected error {_p.ExitCode} from git plumbing operation: {_rcv.StdErr}");
                    }

                    return false;
                }

                return true;
            }
        }

        class ErrorReceiver
        {
            TaskCompletionSource<bool> Tcs { get; } = new();
            protected int N;
            public Task DoneTask { get; }
            readonly StringBuilder _stdErr = new();

            public ErrorReceiver(Process p)
            {
                N = 1;

                p.ErrorDataReceived += (_, e) => DoReceive(_stdErr, e);
                p.BeginErrorReadLine();

                DoneTask = Tcs.Task;
            }

            protected void DoReceive(StringBuilder std, DataReceivedEventArgs e)
            {
                if (e.Data is not null)
                {
                    lock (std)
                    {
                        std.AppendLine(e.Data);
                    }
                }
                else if (Interlocked.Decrement(ref N) == 0)
                {
                    Tcs.TrySetResult(true);
                }
            }

            public string StdErr
            {
                get
                {
                    lock (_stdErr)
                    {
                        return _stdErr.ToString();
                    }
                }
            }
        }


        sealed class OutputReceiver : ErrorReceiver
        {
            readonly StringBuilder _stdOut = new();

            public OutputReceiver(Process p)
                : base(p)
            {
                N++;

                _stdOut = new StringBuilder();
                p.OutputDataReceived += (_, e) => DoReceive(_stdOut, e);
                p.BeginOutputReadLine();
            }

            public string StdOut
            {
                get
                {
                    lock (_stdOut)
                    {
                        return _stdOut.ToString();
                    }
                }
            }
        }

#if NETFRAMEWORK
        static string EscapeGitCommandlineArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return "";

            bool escape = false;
            for (int i = 0; i < argument.Length; i++)
            {
                if (char.IsWhiteSpace(argument, i))
                {
                    escape = true;
                    break;
                }
                else if (argument[i] == '\"')
                {
                    escape = true;
                    break;
                }
            }

            if (!escape)
                return argument;

            StringBuilder sb = new StringBuilder(argument.Length + 5);

            sb.Append('\"');

            for (int i = 0; i < argument.Length; i++)
            {
                switch (argument[i])
                {
                    case '\"':
                        sb.Append('\\');
                        break;
                }

                sb.Append(argument[i]);
            }

            sb.Append('\"');

            return sb.ToString();
        }
#endif
    }
}
