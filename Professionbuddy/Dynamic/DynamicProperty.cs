using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Globalization;
using HighVoltz.Composites;

namespace HighVoltz.Dynamic
{

    public class DynamicExpression<T> : IDynamicProperty
    {
        Func<object, T> _expressionMethod;
        public DynamicExpression() : this(null, "") { }

        public DynamicExpression(IPBComposite parent, string code)
        {
            this.Code = code;
            _expressionMethod = context => default(T);
            AttachedComposite = parent;
        }
        public int CodeLineNumber { get; set; }

        string _compileError;
        public string CompileError
        {
            get { return _compileError; }
            set
            {
                if (value != "" || (value == "" && _compileError != ""))
                {
                    if (MainForm.IsValid)
                    {
                        if (AttachedComposite != null)
                        {
                            if (value != "")
                                ((PBAction)AttachedComposite).Color = System.Drawing.Color.Red;
                            else
                                ((PBAction)AttachedComposite).Color = System.Drawing.Color.Black;
                            MainForm.Instance.RefreshActionTree(AttachedComposite);
                        }
                        else
                            MainForm.Instance.RefreshActionTree();
                    }
                }
                if (MainForm.IsValid)
                    MainForm.Instance.ActionGrid.Refresh();
                _compileError = value;
            }
        }

        public override string ToString()
        {
            return Code;
        }

        public CsharpCodeType CodeType { get { return CsharpCodeType.Expression; } }

        public virtual Delegate CompiledMethod
        {
            get { return _expressionMethod; }
            set { _expressionMethod = (Func<object, T>)value; }
        }

        public Composites.IPBComposite AttachedComposite { get; set; }

        public string Code { get; set; }

        public T Value { get { return _expressionMethod(AttachedComposite); } }

        public Type ReturnType { get { return typeof(T); } }

        public static implicit operator T(DynamicExpression<T> exp)
        {
            return exp.Value;
        }

        public class DynamivExpressionConverter : TypeConverter
        {
            public override bool CanConvertTo(ITypeDescriptorContext context, System.Type destinationType)
            {
                if (destinationType == typeof(DynamivExpressionConverter))
                    return true;
                return base.CanConvertTo(context, destinationType);
            }

            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, System.Type destinationType)
            {
                if (destinationType == typeof(System.String) && value is DynamicExpression<T>)
                {
                    return ((DynamicExpression<T>)value).Code;
                }
                return base.ConvertTo(context, culture, value, destinationType);
            }

            public override bool CanConvertFrom(ITypeDescriptorContext context, System.Type sourceType)
            {
                if (sourceType == typeof(string))
                    return true;
                return base.CanConvertFrom(context, sourceType);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            {
                if (value is string)
                {
                    DynamicExpression<T> ge = new DynamicExpression<T>() { Code = (string)value };
                    return ge;
                }
                return base.ConvertFrom(context, culture, value);
            }
        }

    }
}
