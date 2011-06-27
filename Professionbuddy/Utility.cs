using System;
using System.Text;
using System.Text.RegularExpressions;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.Pathing;
using Styx.Logic.POI;
using System.Linq;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.Logic.BehaviorTree;
using System.Globalization;
using ObjectManager = Styx.WoWInternals.ObjectManager;

namespace HighVoltz
{
    /// <summary>
    /// Utility functions
    /// </summary>
    public static class Util
    {
        static Util()
        {
            IsBankFrameOpen = false;
        }
        /// <summary>
        ///  Random Number Genorator
        /// </summary>
        public static System.Random Rng = new Random(Environment.TickCount);
        /// <summary>
        /// Creates a random upper/lowercase string
        /// </summary>
        /// <returns>Random String</returns>
        public static string RandomString
        {
            get
            {
                int size = Rng.Next(6, 15);
                StringBuilder sb = new StringBuilder(size);
                for (int i = 0; i < size; i++)
                {
                    // random upper/lowercase character using ascii code
                    sb.Append((char)(Rng.Next(2) == 1 ? Rng.Next(65, 91) + 32 : Rng.Next(65, 91)));
                }
                return sb.ToString();
            }
        }

        public static void MoveTo(WoWPoint point)
        {
            if (BotPoi.Current.Type != PoiType.None)
                BotPoi.Clear();
            if (!ObjectManager.Me.Mounted && Mount.ShouldMount(point) && Mount.CanMount())
                Mount.MountUp();
            TreeRoot.StatusText = string.Format("PB: Moving to {0}", point);
            Navigator.MoveTo(point);
        }

        static public WoWPoint StringToWoWPoint(string location)
        {
            WoWPoint loc = WoWPoint.Zero;
            Regex pattern = new Regex(@"-?\d+\.?(\d+)?");
            MatchCollection matches = pattern.Matches(location);
            if (matches != null)
            {
                loc.X = matches[0].ToString().ToSingle();
                loc.Y = matches[1].ToString().ToSingle();
                loc.Z = matches[2].ToString().ToSingle();
            }
            return loc;
        }

        public static uint GetBankItemCount(uint itemID, uint inbagsCount)
        {
            uint count = 0;
            try
            {
                ObjectManager.GetObjectsOfType<WoWItem>().Where(o =>
                {
                    if (o != null && o.IsValid && o.Entry == itemID)
                    {
                        count += o.StackCount;
                        return true;
                    }
                    return false;
                }).ToList();
                return count - inbagsCount;
            }
            catch { return 0; }
        }

        // this factors in the material list
        public static int CalculateRecipeRepeat(Recipe recipe)
        {
            int ret = int.MaxValue;
            foreach (Ingredient ingred in recipe.Ingredients)
            {
                int ingredCnt = (int)ingred.InBagsCount -
                                (Professionbuddy.Instance.MaterialList.ContainsKey(ingred.ID)
                                     ? Professionbuddy.Instance.MaterialList[ingred.ID]
                                     : 0);
                int repeat = (int)System.Math.Floor((double)(ingredCnt / ingred.Required));
                if (ret > repeat)
                {
                    ret = repeat;
                }
            }
            return ret;
        }
        public static bool IsBankFrameOpen { get; private set; }

        static internal void OnBankFrameOpened(object obj, LuaEventArgs args)
        {
            IsBankFrameOpen = true;
        }

        static internal void OnBankFrameClosed(object obj, LuaEventArgs args)
        {
            IsBankFrameOpen = false;
        }
    }
    static class Exts
    {
        public static uint ToUint(this string str)
        {
            uint val;
            uint.TryParse(str, out val);
            return val;
        }

        public static float ToSingle(this string str)
        {
            float val;
            float.TryParse(str, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign 
            , CultureInfo.InvariantCulture, out val);
            return val;
        }
    }
}
