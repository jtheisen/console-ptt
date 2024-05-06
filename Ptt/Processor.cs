using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ptt;



public class Processor
{
    public required StateInContext State { get; init; }

    public void ProcessDirective(SyntaxDirectiveBlock directive)
    {

    }

    public void StartChain(Expression expr)
    {

    }

    public void Reason()
    {

    }

    // For parsing, we get a complete chain
    public void VerifyChain(Expression expr)
    {

    }
}
