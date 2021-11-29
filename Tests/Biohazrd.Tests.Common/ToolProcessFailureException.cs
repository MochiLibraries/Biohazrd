using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace Biohazrd.Tests.Common
{
    public sealed class ToolProcessFailureException : Exception
    {
        public ProcessStartInfo ProcessStartInfo { get; }

        private static string CreateMessage(Process process, string? actionBeingDone, string? standardInput)
        {
            string SmartEscape(string commandOrArgument)
            {
                foreach (char c in commandOrArgument)
                {
                    switch (c)
                    {
                        case >= 'a' and <= 'z':
                        case >= 'A' and <= 'Z':
                        case >= '0' and <= '9':
                        case '.':
                        case '-':
                        case '_':
                        case '=':
                        case ':':
                        case '~':
                        case '/':
                        case '\\':
                            continue;
                        default:
                            if (OperatingSystem.IsWindows())
                            { return $"\"{commandOrArgument}\""; }
                            else
                            { return $"'{commandOrArgument}'"; }
                    }
                }

                return commandOrArgument;
            }

            ProcessStartInfo startInfo = process.StartInfo;
            StringBuilder message = new();
            message.Append($"`{SmartEscape(startInfo.FileName)}");

            if (!String.IsNullOrEmpty(startInfo.Arguments))
            { message.Append($" {startInfo.Arguments}"); }
            else if (startInfo.ArgumentList is Collection<string> { Count: > 0 })
            {
                foreach (string argument in startInfo.ArgumentList)
                { message.Append($" {SmartEscape(argument)}"); }
            }

            message.Append($"` failed with exit code {process.ExitCode}");

            if (actionBeingDone is not null)
            { message.Append($" while {actionBeingDone}"); }

            message.AppendLine(".");

            message.AppendLine($"Working directory: `{(startInfo.WorkingDirectory is "" ? Environment.CurrentDirectory : startInfo.WorkingDirectory)}`");

            if (standardInput is not null)
            {
                message.AppendLine("Standard input:");
                message.AppendLine("```");
                message.AppendLine(standardInput);
                message.AppendLine("```");
            }

            return message.ToString();
        }

        public ToolProcessFailureException(Process process, string? actionBeingDone = null, string? standardInput = null, Exception? innerException = null)
            : base(CreateMessage(process, actionBeingDone, standardInput), innerException)
            => ProcessStartInfo = process.StartInfo;
    }
}
