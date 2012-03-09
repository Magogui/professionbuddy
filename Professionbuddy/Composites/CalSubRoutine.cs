using System.Linq;
using System.Threading;
using TreeSharp;
using System.ComponentModel;


namespace HighVoltz.Composites
{
    #region CallSubRoutine

    sealed class CallSubRoutine : PBAction
    {
        SubRoutine _sub;
        [PbXmlAttribute]
        public string SubRoutineName
        {
            get { return (string)Properties["SubRoutineName"].Value; }
            set { Properties["SubRoutineName"].Value = value; }
        }

        public CallSubRoutine()
        {
            Properties["SubRoutineName"] = new MetaProp("SubRoutineName", typeof(string),
                new DisplayNameAttribute(Pb.Strings["Action_CallSubRoutine_SubRoutineName"]));
            SubRoutineName = "";
        }

        bool _ranonce;
        protected override RunStatus Run(object context)
        {
            if (!IsDone)
            {
                if (_sub == null)
                {
                    if (!GetSubRoutine())
                    {
                        Professionbuddy.Err("{0}: {1}.",Pb.Strings["Error_SubroutineNotFound"],SubRoutineName);
                        IsDone = true;
                    }
                }
                if (!_ranonce)
                {
                    // make sure all actions within the subroutine are reset before we start.
                    if (_sub != null) 
                        _sub.Reset();
                    _ranonce = true;
                }
                if (_sub != null)
                {
                    if (!_sub.IsRunning)
                        _sub.Start(SubRoutineName);
                    try
                    {
                        _sub.Tick(SubRoutineName);
                    }
                    catch (ThreadAbortException)
                    {
                        return RunStatus.Success;
                    }
                    catch {  }
                    IsDone = _sub.IsDone;
                    // we need to reset so calls to the sub from other places can
                    if (!IsDone)
                        return RunStatus.Success;
                }
            }
            return RunStatus.Failure;
        }

        public override void Reset()
        {
            base.Reset();
            _ranonce = false;
        }
        bool GetSubRoutine()
        {
            _sub = FindSubRoutineByName(SubRoutineName, Pb.PbBehavior);
            return _sub != null;
        }

        SubRoutine FindSubRoutineByName(string subName, Composite comp)
        {

            if (comp is SubRoutine && ((SubRoutine)comp).SubRoutineName == subName)
                return (SubRoutine)comp;
            var groupComposite = comp as GroupComposite;
            if (groupComposite != null)
            {
                return (groupComposite).Children.Select(c => FindSubRoutineByName(subName, c)).
                    FirstOrDefault(temp => temp != null);
            }
            return null;
        }

        public override string Name { get { return Pb.Strings["Action_CallSubRoutine_Name"]; } }

        public override string Title { get { return string.Format("{0}: {1}()", Name, SubRoutineName); } }
        public override string Help
        {
            get
            {
                return Pb.Strings["Action_CallSubRoutine_Help"];
            }
        }
        public override object Clone()
        {
            return new CallSubRoutine { SubRoutineName = this.SubRoutineName };
        }
    }
    #endregion
}
