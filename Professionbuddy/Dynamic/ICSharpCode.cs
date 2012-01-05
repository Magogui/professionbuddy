using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HighVoltz.Composites;

namespace HighVoltz.Dynamic
{
    public enum CsharpCodeType { BoolExpression, Statements, Declaration, Expression }

    public interface ICSharpCode
    {
        int CodeLineNumber { get; set; }
        string CompileError { get; set; }
        CsharpCodeType CodeType { get; }
        string Code { get; }
        Delegate CompiledMethod { get; set; }
        IPBComposite AttachedComposite { get; }
    }
}
