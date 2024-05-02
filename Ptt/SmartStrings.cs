using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ptt;



//public struct SmartString
//{
//    AbstractSmartString? implementation;

//    public Int32 NaiveLength => implementation?.NaiveLength ?? 0;

//    public override String ToString()
//    {
//        return 
//    }

//    public static SmartString Create(String separator, SmartString[] parts, String? opening = null, String? closing = null)
//    {
//        var naiveLength = parts.Sum(p => p.NaiveLength) + parts.Length * separator.Length;

//        if (opening is not null)
//        {
//            naiveLength += opening.Length;
//        }

//        if (closing is not null)
//        {
//            naiveLength += closing.Length;
//        }

//        return new SmartString
//        {
//            implementation = new JoinedSmartString
//            {
//                separator = separator,
//                parts = parts,
//                opening = opening,
//                closing = closing,
//                naiveLength = naiveLength,
//            }
//        };
//    }
//}


public abstract class AbstractSmartStringWriter
{
    public abstract void Open(Int32 length);

    public abstract void Write(String value);

    public abstract void WriteSpace();

    public abstract void Break(Boolean hard = false, Int32 relativeNesting = 0);

    public abstract void Close();
}

public class SmartStringWriter : AbstractSmartStringWriter
{
    public Int32 MaxWidth { get; init; } = 74;

    public Int32 NestingFactor { get; init; } = 4;

    Int32 Nesting => stack.Count;

    StringWriter writer = new StringWriter();

    Stack<Boolean> stack = new Stack<Boolean>();

    Int32 pendingRelativeNesting;

    Boolean isSpacePending;
    Boolean isBreakPending;

    public SmartStringWriter()
    {
        stack.Push(false);
    }

    void WritePending()
    {
        if (isBreakPending)
        {
            writer.WriteLine();

            writer.Write(new String(' ', Math.Max(0, Nesting * NestingFactor + pendingRelativeNesting)));

            pendingRelativeNesting = 0;
            isBreakPending = false;
        }
        else if (isSpacePending)
        {
            writer.Write(' ');
        }

        isSpacePending = false;
    }

    public override void Open(Int32 length)
    {
        stack.Push(Nesting * NestingFactor + length > MaxWidth);
    }

    public override void Close()
    {
        stack.Pop();
    }

    public override void Write(String value)
    {
        WritePending();

        writer.Write(value);
    }

    public override void WriteSpace()
    {
        isSpacePending = true;
    }

    public override void Break(Boolean hard = false, Int32 relativeNesting = 0)
    {
        if (hard || stack.Peek())
        {
            pendingRelativeNesting = relativeNesting;
            isBreakPending = true;
        }
    }

    public String GetResult()
    {
        if (stack.Count != 1)
        {
            throw new Exception("Assertion failure: stack was not empty");
        }

        return writer.ToString();
    }
}

//public abstract class AbstractSmartString
//{
//    public abstract Int32 NaiveLength { get; }

//    public abstract void Write(AbstractSmartStringWriter writer);
//}

//public class SimpleSmartString : AbstractSmartString
//{
//    internal String? value;

//    public override Int32 NaiveLength => value?.Length ?? 0;

//    public override void Write(AbstractSmartStringWriter writer)
//    {
//        if (value is not null)
//        {
//            writer.Write(value);
//        }
//    }
//}

//public class JoinedSmartString : AbstractSmartString
//{
//    internal String? separator;
//    internal SmartString[]? parts;
//    internal String? opening;
//    internal String? closing;

//    internal Int32 naiveLength;

//    public override Int32 NaiveLength => naiveLength;

//    public override void Write(AbstractSmartStringWriter writer)
//    {
//        writer.Open(naiveLength);

//        if (opening is not null)
//        {

//        }
//    }
//}

//public static class SmartStringExtensions
//{
    
//}
