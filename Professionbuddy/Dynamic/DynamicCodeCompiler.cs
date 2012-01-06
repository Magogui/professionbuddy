using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using CommonBehaviors.Actions;
using Microsoft.CSharp;
using Styx;
using Styx.Database;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Inventory.Frames.MailBox;
using Styx.Logic.Inventory.Frames.Merchant;
using Styx.Logic.Pathing;
using Styx.Patchables;
using Styx.Plugins;
using Styx.Plugins.PluginClass;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using HighVoltz.Composites;
using Action = TreeSharp.Action;
using PrioritySelector = TreeSharp.PrioritySelector;
using System.Reflection.Emit;
using System.Threading;
namespace HighVoltz.Dynamic
{
    public class DynamicCodeCompiler
    {
        static Dictionary<string, ICSharpCode> CsharpCodeDict = new Dictionary<string, ICSharpCode>();
        static IEnumerable<KeyValuePair<string, ICSharpCode>> declarations = from dec in CsharpCodeDict
                                                                             where dec.Value.CodeType == CsharpCodeType.Declaration
                                                                             select dec;
        static IEnumerable<KeyValuePair<string, ICSharpCode>> noneDeclarations = from dec in CsharpCodeDict
                                                                                 where dec.Value.CodeType != CsharpCodeType.Declaration
                                                                                 select dec;
        static object _codeDriverInstance;
        public static bool CodeWasModified = true;

        static string _tempFolder;
        static public string TempFolder { get { return _tempFolder ?? (_tempFolder = Path.Combine(Professionbuddy.BotPath, "Temp")); } }

        public static void WipeTempFolder()
        {
            if (!Directory.Exists(TempFolder))
            {
                Directory.CreateDirectory(TempFolder);
            }
            foreach (string file in Directory.GetFiles(TempFolder, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(file);
                }
                catch { }
            }
            foreach (string dir in Directory.GetDirectories(TempFolder))
            {
                try
                {
                    Directory.Delete(dir);
                }
                catch { }
            }
        }

        static public void GenorateDynamicCode()
        {
            CsharpCodeDict.Clear();
            StoreMethodName(Professionbuddy.Instance.PbBehavior);
            // check if theres anything to compile
            if (CsharpCodeDict.Count == 0)
                return;
            Type dynamicType = CompileAndLoad();
            if (dynamicType != null)
            {
                _codeDriverInstance = Activator.CreateInstance(dynamicType);

                foreach (MethodInfo method in dynamicType.GetMethods())
                {
                    if (CsharpCodeDict.ContainsKey(method.Name))
                    {
                        if (CsharpCodeDict[method.Name].CodeType == CsharpCodeType.BoolExpression)
                            CsharpCodeDict[method.Name].CompiledMethod = Delegate.CreateDelegate(typeof(CanRunDecoratorDelegate), _codeDriverInstance, method.Name);
                        else if (CsharpCodeDict[method.Name].CodeType == CsharpCodeType.Statements)
                            CsharpCodeDict[method.Name].CompiledMethod = Delegate.CreateDelegate(typeof(System.Action<object>), _codeDriverInstance, method.Name);
                        else if (CsharpCodeDict[method.Name].CodeType == CsharpCodeType.Expression)
                        {
                            Type gType = typeof(Func<,>).MakeGenericType(new Type[] { typeof(object), ((IDynamicProperty)CsharpCodeDict[method.Name]).ReturnType });
                            CsharpCodeDict[method.Name].CompiledMethod = Delegate.CreateDelegate(gType, _codeDriverInstance, method.Name);
                        }

                    }
                }
            }
        }

        static void StoreMethodName(Composite comp)
        {
            if (comp is ICSharpCode)
            {
                CsharpCodeDict["Code" + Util.Rng.Next(int.MaxValue).ToString()] = (ICSharpCode)comp;
                ((ICSharpCode)comp).CompileError = "";
            }
            // check for DynamicExpression proprerties
            List<IDynamicProperty> dynProps = (from prop in comp.GetType().GetProperties()
                                               where typeof(IDynamicProperty).IsAssignableFrom(prop.PropertyType)
                                               select (IDynamicProperty)prop.GetValue(comp, null)).ToList();
            foreach (IDynamicProperty dynProp in dynProps)
            {
                CsharpCodeDict["Code" + Util.Rng.Next(int.MaxValue).ToString()] = dynProp;
                dynProp.CompileError = "";
            }

            if (comp is GroupComposite)
            {
                foreach (Composite child in ((GroupComposite)comp).Children)
                    StoreMethodName(child);
            }
        }

        #region Strings

        static string prefix =
        @"using HighVoltz;
        using System;
        using System.Reflection;
        using System.Data;
        using System.Threading;
        using System.Diagnostics;
        using System.Drawing;
        using System.Collections.Generic;
        using System.Collections; 
        using System.Linq; 
        using System.Text; 
        using System.IO; 
        using System.Windows.Forms;
        using Styx;
        using Styx.Helpers;
        using Styx.Logic.Combat;
        using Styx.WoWInternals;
        using Styx.WoWInternals.WoWObjects;
        using Styx.Logic;
        using Styx.Logic.AreaManagement;
        using Styx.Logic.BehaviorTree;
        using Styx.Logic.Inventory.Frames.Gossip;
        using Styx.Logic.Inventory.Frames.LootFrame;
        using Styx.Logic.Inventory.Frames.MailBox;
        using Styx.Logic.Inventory.Frames.Merchant;
        using Styx.Logic.Pathing;
        using Styx.Logic.Profiles;
        using Styx.Plugins;
        using Styx.Plugins.PluginClass;
        using Styx.WoWInternals.World;
        using Styx.Combat.CombatRoutine;
        using HighVoltz.Composites;
        using HighVoltz.Dynamic;
        public class CodeDriver
        {
";
        static string postfix =
            @"
            static LocalPlayer Me = ObjectManager.Me;
            static PbProfileSettings Settings = Professionbuddy.Instance.ProfileSettings;
            static object var1,var2,var3,var4,var5,var6,var7,var8,var9;
            public static Helpers.TradeskillHelper Alchemy { get { return Helpers.Alchemy;} }
            public static Helpers.TradeskillHelper Archaeology { get { return Helpers.Archaeology;} }
            public static Helpers.TradeskillHelper Blacksmithing { get { return Helpers.Blacksmithing;} }
            public static Helpers.TradeskillHelper Cooking { get { return Helpers.Cooking;} }
            public static Helpers.TradeskillHelper Enchanting { get { return Helpers.Enchanting;} }
            public static Helpers.TradeskillHelper Engineering { get { return Helpers.Engineering;} }
            public static Helpers.TradeskillHelper FirstAid { get { return Helpers.FirstAid;} }
            public static Helpers.TradeskillHelper Fishing { get { return Helpers.Fishing;} }
            public static Helpers.TradeskillHelper Inscription { get { return Helpers.Inscription;} }
            public static Helpers.TradeskillHelper Herbalism { get { return Helpers.Herbalism;} }
            public static Helpers.TradeskillHelper Jewelcrafting { get { return Helpers.Jewelcrafting;} }
            public static Helpers.TradeskillHelper Leatherworking { get { return Helpers.Leatherworking;} }
            public static Helpers.TradeskillHelper Mining { get { return Helpers.Mining;} }
            public static Helpers.TradeskillHelper Tailoring { get { return Helpers.Tailoring;} }
            public static DataStore DataStore {get{return Professionbuddy.Instance.DataStore;}}
            uint CanRepeatNum (uint id){  return Helpers.TradeskillHelper.CanRepeatNum(id);}
            bool CanCraft (uint id){  return Helpers.TradeskillHelper.CanCraft(id);}
            bool HasMats (uint id){  return Helpers.TradeskillHelper.HasMats(id);}
            bool HasTools (uint id){  return Helpers.TradeskillHelper.HasTools(id);}
            bool HasRecipe (uint id){  return Helpers.TradeskillHelper.HasRecipe(id);}
            bool HasNewMail { get{ return MailFrame.Instance.HasNewMail;}}
            int MailCount { get{ return MailFrame.Instance.MailCount;}}
            bool HasItem (uint id) {return InbagCount(id) > 0; }
            int InbagCount (uint id) {return Helpers.InbagCount(id); }
            void Log (System.Drawing.Color c,string f,params object[] args) {Helpers.Log(c,f,args); }
            void Log (string f,params object[] args) {Helpers.Log(f,args); }
            void Log(System.Drawing.Color headerColor, string header, System.Drawing.Color msgColor, string format, params object[] args) 
            {
                Professionbuddy.Log(headerColor, header, msgColor, format, args);
            }
            float DistanceTo(double x,double y,double z) {return Helpers.DistanceTo(x,y,z); }
            float DistanceTo(WoWPoint p) {return Helpers.DistanceTo(p.X,p.Y,p.Z); }
            void MoveTo(double x,double y,double z) {Helpers.MoveTo(x,y,z); }
            void MoveTo(WoWPoint p) {Helpers.MoveTo(p.X,p.Y,p.Z); }
            void CTM(double x,double y,double z) {Helpers.CTM(x,y,z); }
            void CTM(WoWPoint p) {Helpers.CTM(p.X,p.Y,p.Z); }
            void RefreshDataStore() {Professionbuddy.Instance.DataStore.ImportDataStore(); }
            void SwitchToBot(string botName) {try{Professionbuddy.ChangeSecondaryBot(botName);}catch{}}
            void SwitchCharacter(string character,string server,string botName){Helpers.SwitchCharacter(character,server,botName);}
            BotBase SecondaryBot {get{return Professionbuddy.Instance.SecondaryBot;}}
        }";
        #endregion
        static public StringBuilder CsharpStringBuilder { get; private set; }

        static public Type CompileAndLoad()
        {
            CompilerResults results = null;
            using (CSharpCodeProvider provider = new CSharpCodeProvider(new Dictionary<string, string>() { 
                {"CompilerVersion", "v3.5"},
            }))
            {

                CompilerParameters options = new CompilerParameters();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!asm.GetName().Name.Contains(Professionbuddy.Instance.Name))
                        options.ReferencedAssemblies.Add(asm.Location);
                }
                options.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);

                // disabled due to a bug in 2.0.0.3956;
                //options.GenerateInMemory = true; 
                options.GenerateExecutable = false;
                options.TempFiles = new TempFileCollection(TempFolder, false);
                options.IncludeDebugInformation = false;
                options.OutputAssembly = string.Format("{0}\\CodeAssembly{1:N}.dll", TempFolder, Guid.NewGuid());
                options.CompilerOptions = "/optimize";
                CsharpStringBuilder = new StringBuilder();
                CsharpStringBuilder.Append(prefix);
                // Line numbers are used to identify actions that genorated compile errors.
                int currentLine = CsharpStringBuilder.ToString().Count(c => c == '\n') + 1;
                // genorate CanRun Methods
                foreach (var met in declarations)
                {
                    CsharpStringBuilder.AppendFormat("{0}\n", met.Value.Code.Replace(Environment.NewLine, ""));
                    met.Value.CodeLineNumber = currentLine++;
                }
                foreach (var met in noneDeclarations)
                {
                    if (met.Value.CodeType == CsharpCodeType.BoolExpression)
                        CsharpStringBuilder.AppendFormat("public bool {0} (object context){{return {1};}}\n", met.Key, met.Value.Code.Replace(Environment.NewLine, ""));
                    else if (met.Value.CodeType == CsharpCodeType.Statements)
                        CsharpStringBuilder.AppendFormat("public void {0} (object context){{{1}}}\n", met.Key, met.Value.Code.Replace(Environment.NewLine, ""));
                    else if (met.Value.CodeType == CsharpCodeType.Expression)
                        CsharpStringBuilder.AppendFormat("public {0} {1} (object context){{return {2};}}\n",
                            ((IDynamicProperty)met.Value).ReturnType.Name, met.Key, met.Value.Code.Replace(Environment.NewLine, ""));
                    met.Value.CodeLineNumber = currentLine++;
                }
                CsharpStringBuilder.Append(postfix);
                results = provider.CompileAssemblyFromSource(
                options, CsharpStringBuilder.ToString());
            }
            if (results.Errors.HasErrors)
            {
                if (results.Errors.Count > 0)
                {
                    foreach (CompilerError error in results.Errors)
                    {
                        ICSharpCode icsc = CsharpCodeDict.Values.FirstOrDefault(c => c.CodeLineNumber == error.Line);
                        if (icsc != null)
                        {
                            Professionbuddy.Err("{0}\nCompile Error : {1}\n", icsc.AttachedComposite.Title, error.ErrorText);
                            icsc.CompileError = error.ErrorText;
                        }
                        else
                        {
                            Professionbuddy.Err("Unable to link action that produced Error: {0}", error.ErrorText);
                        }
                    }
                    MainForm.Instance.RefreshActionTree(typeof(ICSharpCode));
                }
                return null;
            }
            else
            {
                CodeWasModified = false;
                return results.CompiledAssembly.GetType("CodeDriver");
            }
        }
    }
}
