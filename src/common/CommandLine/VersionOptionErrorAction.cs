// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;

namespace PowerToys.Common.CommandLine
{
    internal sealed class VersionOptionErrorAction : SynchronousCommandLineAction
    {
        private readonly VersionOption _option;
        private readonly SynchronousCommandLineAction _originalAction;

        private VersionOptionErrorAction(VersionOption option, SynchronousCommandLineAction originalAction)
        {
            _option = option;
            _originalAction = originalAction;
        }

        public override bool ClearsParseErrors => true;

        public override bool Terminating => _originalAction.Terminating;

        internal static void Apply(RootCommand root)
        {
            var option = root.Options.OfType<VersionOption>().Single();
            option.Action = new VersionOptionErrorAction(option, (SynchronousCommandLineAction)option.Action!);
        }

        public override int Invoke(ParseResult parseResult)
        {
            // GA retains the version option's own errors, but its action still reports success.
            if (parseResult.Errors.Any(error => error.SymbolResult is OptionResult result && result.Option == _option))
            {
                return new ParseErrorAction().Invoke(parseResult);
            }

            return _originalAction.Invoke(parseResult);
        }
    }
}
