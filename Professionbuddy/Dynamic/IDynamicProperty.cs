using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HighVoltz.Composites;

namespace HighVoltz.Dynamic
{
    interface IDynamicProperty : ICSharpCode
    {
        /// <summary>
        /// This is the IPBComposite that this propery belongs to. It's set at compile time
        /// </summary>
        new IPBComposite AttachedComposite { get; set; }
        Type ReturnType { get; }
    }
}
