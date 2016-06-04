using System;

namespace LateNightStupidities.webdiff
{
    public class WebdiffErrorException : Exception
    {
        public int ExitCode { get; }

        public WebdiffErrorException(string message, int exitCode) : base(message)
        {
            this.ExitCode = exitCode;
        }
    }
}