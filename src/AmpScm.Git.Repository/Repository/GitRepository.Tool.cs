using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Git.Implementation;
using AmpScm.Git.Repository;

namespace AmpScm.Git
{
    partial class GitRepository
    {
        protected internal ValueTask<int> RunPlumbingCommand(string command, params string[] args)
        {
            return RunPlumbingCommand(command, args, stdinText: null, expectedResults: null);
        }

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

        protected internal async ValueTask<int> RunPlumbingCommand(string command, string[] args, string? stdinText = null, int[]? expectedResults = null)
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

            var rcv = new OutputReceiver(p, true);

            if (!string.IsNullOrEmpty(stdinText))
                await p.StandardInput.WriteAsync(stdinText).ConfigureAwait(false);

            p.StandardInput.Close();

            await Task.WhenAll(p.WaitForExitAsync(), rcv.DoneTask).ConfigureAwait(false);

            if (expectedResults != null ? !(expectedResults.Length == 0 || expectedResults.Contains(p.ExitCode)) : p.ExitCode != 0)
                throw new GitExecCommandException($"Unexpected error {p.ExitCode} from 'git {command}' operation: {rcv.StdErr}");

            return p.ExitCode;
        }

        protected internal async ValueTask<(int ExitCode, string OutputText)> RunPlumbingCommandOut(string command, string[] args, string? stdinText = null, int[]? expectedResults = null)
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

            var rcv = new OutputReceiver(p, true);

            if (!string.IsNullOrEmpty(stdinText))
                await p.StandardInput.WriteAsync(stdinText).ConfigureAwait(false);

            p.StandardInput.Close();

            await Task.WhenAll(p.WaitForExitAsync(), rcv.DoneTask).ConfigureAwait(false);

            if (expectedResults != null ? !(expectedResults.Length == 0 || expectedResults.Contains(p.ExitCode)) : p.ExitCode != 0)
                throw new GitExecCommandException($"Unexpected error {p.ExitCode} from 'git {command}' operation: {rcv.StdErr}");

            return (p.ExitCode, rcv.StdOut);
        }

        protected internal async ValueTask<(int ExitCode, string OutputText, string ErrorText)> RunPlumbingCommandErr(string command, string[] args, string? stdinText = null, int[]? expectedResults = null)
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

            var rcv = new OutputReceiver(p, true);

            if (!string.IsNullOrEmpty(stdinText))
                await p.StandardInput.WriteAsync(stdinText).ConfigureAwait(false);

            p.StandardInput.Close();

            await Task.WhenAll(p.WaitForExitAsync(), rcv.DoneTask).ConfigureAwait(false);

            if (expectedResults != null ? !(expectedResults.Length == 0 || expectedResults.Contains(p.ExitCode)) : p.ExitCode != 0)
                throw new GitExecCommandException($"Unexpected error {p.ExitCode} from 'git {command}' operation");

            return (p.ExitCode, rcv.StdOut, rcv.StdErr ?? "");
        }

        internal async ValueTask<(int ExitCode, string OutputText, string ErrorText)> RunHookErr(string hook, string[] args, string? stdinText = null, int[]? expectedResults = null)
        {
            if (!await Configuration.HookExistsAsync(hook).ConfigureAwait(false))
                return (0, "", "");

            List<string> aa = new List<string>();
            aa.AddRange(new[] { "run", hook, "--" });
            aa.AddRange(args);
            return await RunPlumbingCommandErr("hook", aa.ToArray(), stdinText, expectedResults).ConfigureAwait(false);
        }

        protected internal IAsyncEnumerable<string> WalkPlumbingCommand(string command, string[] args, string? stdinText = null, int[]? expectedResults = null)
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

            return new StdOutputWalker(p, stdinText, expectedResults);
        }

        sealed class StdOutputWalker : IAsyncEnumerable<string>, IAsyncEnumerator<string>
        {
            readonly Process _p;
            readonly StreamReader _reader;
            string? _stdinText;
            bool _eof;
            string? _current;
            StringBuilder? _errText;
            readonly int[]? _expectedResults;
            readonly object _l = new();

            public StdOutputWalker(Process p, string? stdinText, int[]? expectedResults)
            {
                if (p is null)
                    throw new ArgumentNullException(nameof(p));

                _p = p;
                _stdinText = stdinText;
                _expectedResults = expectedResults;
                _reader = p.StandardOutput;
                _p.ErrorDataReceived += P_ErrorDataReceived;
                p.BeginErrorReadLine();
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
                    await _p.WaitForExitAsync().ConfigureAwait(false);

                    lock (_l)
                    {
                        if (_expectedResults != null ? !(_expectedResults.Length == 0 || _expectedResults.Contains(_p.ExitCode)) : _p.ExitCode != 0)
                            throw new GitExecCommandException($"Unexpected error {_p.ExitCode} from git plumbing operation: {_errText?.ToString()}");
                    }

                    _p.Dispose();

                    return false;
                }

                return true;
            }

            private void P_ErrorDataReceived(object sender, DataReceivedEventArgs e)
            {
                if (e.Data is not null)
                {
                    lock (_l)
                    {
                        (_errText ??= new StringBuilder()).AppendLine(e.Data);
                    }
                }
            }
        }


        sealed class OutputReceiver
        {
            readonly TaskCompletionSource<object?> _tcs;
            int _n;
            StringBuilder _stdOut;
            StringBuilder? _stdErr;
            public Task DoneTask { get; }

            public OutputReceiver(Process p, bool receiveStderr)
            {
                _tcs = new TaskCompletionSource<object?>();
                _n = receiveStderr ? 2 : 1;
                _stdOut = new StringBuilder();
                p.OutputDataReceived += P_OutputDataReceived;
                p.BeginOutputReadLine();

                if (receiveStderr)
                {
                    _stdErr = new StringBuilder();
                    p.ErrorDataReceived += P_ErrorDataReceived;
                    p.BeginErrorReadLine();
                }
                DoneTask = _tcs.Task;
            }

            private void P_ErrorDataReceived(object sender, DataReceivedEventArgs e)
            {
                if (e.Data is not null)
                {
                    _stdErr!.AppendLine(e.Data);
                }
                else if (Interlocked.Decrement(ref _n) == 0)
                {
                    _tcs.SetResult(null);
                }
            }

            private void P_OutputDataReceived(object sender, DataReceivedEventArgs e)
            {
                if (e.Data is not null)
                {
                    _stdOut!.AppendLine(e.Data);
                }
                else if (Interlocked.Decrement(ref _n) == 0)
                {
                    _tcs.SetResult(null);
                }
            }

            public string StdOut => _stdOut.ToString();
            public string? StdErr => _stdErr?.ToString();
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
