//#define PBDEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel;
using System.IO;
using System.IO.Packaging;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using Styx.Helpers;
using TreeSharp;
using Styx.Logic.BehaviorTree;
using System.Xml;
using HighVoltz.Composites;

namespace HighVoltz
{
    public partial class MainForm : Form
    {
        [Flags]
        enum CopyPasteOperactions { Cut = 0, IgnoreRoot = 1, Copy = 2 };
        CopyPasteOperactions copyAction = CopyPasteOperactions.Cut;
        TreeNode copySource = null;
        public static MainForm Instance { get; private set; }
        public static bool IsValid { get { return Instance != null && Instance.Visible && !Instance.Disposing && !Instance.IsDisposed; } }
        private Professionbuddy PB;
        private PropertyBag ProfilePropertyBag;
        private delegate void guiInvokeCB();

        // used to update GUI controls via other threads

        #region Initalize/update methods
        public MainForm() {
            Instance = this;
            PB = Professionbuddy.Instance;
            InitializeComponent();
            saveFileDialog.InitialDirectory = Professionbuddy.Instance.PluginPath + "\\Profiles";
            // used by the dev to display the 'Secret button', a button that dumps some debug info of the Task list.
            if (Environment.UserName == "highvoltz")
            {
                toolStripSecretButton.Visible = true;
                saveFileDialog.InitialDirectory = @"C:\Users\highvoltz\Desktop\Buddy\Projects\Professionbuddy\Professionbuddy\Profiles";
            }
        }

        private delegate void InitDelegate();
        public void Initialize() {
            InitTradeSkillTab();
            InitActionTree();
            PopulateActionGridView();
            if (PB.HasDataStoreAddon && !toolStripAddCombo.Items.Contains("Banker"))
                toolStripAddCombo.Items.Add("Banker");
            toolStripAddCombo.SelectedIndex = 0;
#if DLL
            toolStripOpen.Image = HighVoltz.Properties.Resources.OpenPL;
            toolStripSave.Image = HighVoltz.Properties.Resources.SaveHL;
            toolStripCopy.Image = HighVoltz.Properties.Resources.copy;
            toolStripCut.Image = HighVoltz.Properties.Resources.cut;
            toolStripPaste.Image = HighVoltz.Properties.Resources.paste_32x32;
            toolStripDelete.Image = HighVoltz.Properties.Resources.delete;
            toolStripAddBtn.Image = HighVoltz.Properties.Resources._112_RightArrowLong_Orange_32x32_72;
            toolStripMaterials.Image = HighVoltz.Properties.Resources.Notepad_32x32;
            toolStripHelp.Image = HighVoltz.Properties.Resources._109_AllAnnotations_Help_32x32_72;
            toolStripSettings.Image = HighVoltz.Properties.Resources.settings_48);
#else
            string imagePath = Path.Combine(PB.PluginPath, "Icons//");
            toolStripOpen.Image = Image.FromFile(imagePath + "OpenPL.bmp");
            toolStripSave.Image = Image.FromFile(imagePath + "SaveHL.bmp");
            toolStripCopy.Image = Image.FromFile(imagePath + "copy.png");
            toolStripCut.Image = Image.FromFile(imagePath + "cut.png");
            toolStripPaste.Image = Image.FromFile(imagePath + "paste_32x32.png");
            toolStripDelete.Image = Image.FromFile(imagePath + "delete.png");
            toolStripAddBtn.Image = Image.FromFile(imagePath + "112_RightArrowLong_Orange_32x32_72.png");
            toolStripMaterials.Image = Image.FromFile(imagePath + "Notepad_32x32.png");
            toolStripHelp.Image = Image.FromFile(imagePath + "109_AllAnnotations_Help_32x32_72.png");
            toolStripSettings.Image = Image.FromFile(imagePath + "settings_48.png");
#endif

            if (PB.ProfileSettings.Settings.Count == 0)
                toolStripSettings.Enabled = false;
            if (PB.TradeSkillList.Count > 0)
                TradeSkillTabControl.Visible = true;
            UpdateControls();
        }

        public void InitTradeSkillTab() {
            if (!IsValid)
                return;
            if (TradeSkillTabControl.InvokeRequired)
                TradeSkillTabControl.BeginInvoke(new guiInvokeCB(InitTradeSkillTabCallback));
            else
                InitTradeSkillTabCallback();
        }

        void InitTradeSkillTabCallback() {
            TradeSkillTabControl.SuspendLayout();
            TradeSkillTabControl.TabPages.Clear();
            for (int i = 0; i < PB.TradeSkillList.Count; i++)
            {
                TradeSkillTabControl.TabPages.Add(new TradeSkillListView(i));
            }
            TradeSkillTabControl.ResumeLayout();
        }

        bool IsChildNode(TreeNode parent, TreeNode child) {
            if ((child == null && parent != null) || child.Parent == null)
                return false;
            if (child.Parent == parent)
                return true;
            else
                return IsChildNode(parent, child.Parent);
        }

        void ToggleStart() {
            try
            {
                if (PB.IsRunning)
                {
                    PB.MySettings.IsRunning = PB.IsRunning = false;
                    PB.MySettings.Save();
                }
                else
                {
                    // reset all actions 
                    foreach (IPBComposite comp in PB.CurrentProfile.Branch.Children)
                    {
                        comp.Reset();
                    }
                    if (PB.CodeWasModified)
                    {
                        PB.GenorateDynamicCode();
                    }
                    PB.ProfileSettings.LoadDefaultValues();
                    PB.MySettings.IsRunning = PB.IsRunning = true;
                    PB.MySettings.Save();
                    if (!TreeRoot.IsRunning)
                        TreeRoot.Start();
                }
                UpdateControls();
            }
            catch (Exception ex) { Professionbuddy.Err(ex.ToString()); }
        }

        // locks/unlocks controls depending on if PB is running on not.
        public void UpdateControls() {
            if (!IsValid)
                return;
            if (this.InvokeRequired)
                this.BeginInvoke(new guiInvokeCB(UpdateControlsCallback));
            else
                UpdateControlsCallback();
        }

        void UpdateControlsCallback() {
            if (PB.IsRunning)
            {
                DisableControls();
                this.Text = string.Format("Profession Buddy - Running {0}",
                    !string.IsNullOrEmpty(PB.MySettings.LastProfile) ? "(" + Path.GetFileName(PB.MySettings.LastProfile) + ")" : "");
                toolStripStart.BackColor = Color.Green;
                toolStripStart.Text = "Running";
            }
            else
            {
                EnableControls();
                this.Text = string.Format("Profession Buddy - Stopped {0}",
                    !string.IsNullOrEmpty(PB.MySettings.LastProfile) ? "(" + Path.GetFileName(PB.MySettings.LastProfile) + ")" : "");
                toolStripStart.BackColor = Color.Red;
                toolStripStart.Text = "Stopped";
            }
        }

        void AddToActionTree(object action, TreeNode dest) {
            bool ignoreRoot = (copyAction & CopyPasteOperactions.IgnoreRoot) == CopyPasteOperactions.IgnoreRoot ? true : false;
            bool cloneActions = (copyAction & CopyPasteOperactions.Copy) == CopyPasteOperactions.Copy ? true : false;
            TreeNode newNode = null;
            if (action is TreeNode)
            {
                if (cloneActions)
                {
                    newNode = RecursiveCloning(((TreeNode)action));
                }
                else
                    newNode = (TreeNode)((TreeNode)action).Clone();
            }
            else if (action.GetType().GetInterface("IPBComposite") != null)
            {
                IPBComposite composite = (IPBComposite)action;
                newNode = new TreeNode(composite.Title);
                newNode.ForeColor = composite.Color;
                newNode.Tag = composite;
            }
            else
                return;
            ActionTree.SuspendLayout();
            if (dest != null)
            {
                int index = action is TreeNode && ((TreeNode)action).Parent == dest.Parent && ((TreeNode)action).Index < dest.Index ?
                    dest.Index + 1 : dest.Index;
                PrioritySelector ps = null;
                // If, While and SubRoutines are Decorators...
                if (!ignoreRoot && dest.Tag is Decorator)
                    ps = (PrioritySelector)((Decorator)dest.Tag).DecoratedChild;
                else
                    ps = (PrioritySelector)((Composite)dest.Tag).Parent;

                if ((dest.Tag is If || dest.Tag is SubRoutine) && !ignoreRoot)
                {
                    dest.Nodes.Add(newNode);
                    ps.AddChild((Composite)newNode.Tag);
                    if (!dest.IsExpanded)
                        dest.Expand();
                }
                else
                {
                    if (dest.Parent == null)
                    {
                        if (index >= ActionTree.Nodes.Count)
                            ActionTree.Nodes.Add(newNode);
                        else
                            ActionTree.Nodes.Insert(index, newNode);
                    }
                    else
                    {
                        if (index >= dest.Parent.Nodes.Count)
                            dest.Parent.Nodes.Add(newNode);
                        else
                            dest.Parent.Nodes.Insert(index, newNode);
                    }
                    if (index >= ps.Children.Count)
                        ps.AddChild((Composite)newNode.Tag);
                    else
                        ps.InsertChild(index, (Composite)newNode.Tag);
                }
            }
            else
            {
                ActionTree.Nodes.Add(newNode);
                PB.CurrentProfile.Branch.AddChild((Composite)newNode.Tag);
            }
            ActionTree.ResumeLayout();
        }

        TreeNode RecursiveCloning(TreeNode node) {
            IPBComposite newComp = (IPBComposite)(((IPBComposite)node.Tag).Clone());
            TreeNode newNode = new TreeNode(newComp.Title);
            newNode.ForeColor = newComp.Color;
            newNode.Tag = newComp;
            if (node.Nodes != null)
            {
                foreach (TreeNode child in node.Nodes)
                {
                    PrioritySelector ps = null;
                    // If, While and SubRoutine are Decorators.
                    if (newComp is Decorator)
                    {
                        ps = (PrioritySelector)((Decorator)newComp).DecoratedChild;

                        TreeNode newChildNode = RecursiveCloning(child);
                        ps.AddChild((Composite)newChildNode.Tag);
                        newNode.Nodes.Add(newChildNode);
                    }
                }
            }
            return newNode;
        }

        void DisableControls() {
            ActionTree.Enabled = false;
            toolStripAddBtn.Enabled = false;
            toolStripOpen.Enabled = false;
            toolStripDelete.Enabled = false;
            toolStripCopy.Enabled = false;
            toolStripCut.Enabled = false;
            toolStripPaste.Enabled = false;
            ActionGrid.Enabled = false;
        }
        void EnableControls() {
            ActionTree.Enabled = true;
            toolStripAddBtn.Enabled = true;
            toolStripOpen.Enabled = true;
            toolStripDelete.Enabled = true;
            toolStripCopy.Enabled = true;
            toolStripCut.Enabled = true;
            toolStripPaste.Enabled = true;
            ActionGrid.Enabled = true;
        }

        public void RefreshActionTree() {
            // Don't update ActionTree while PB is running to improve performance.
            if (PB.IsRunning || !IsValid)
                return;
            if (ActionTree.InvokeRequired)
                ActionTree.BeginInvoke(new guiInvokeCB(RefreshActionTreeCallback));
            else
                RefreshActionTreeCallback();
        }
        void RefreshActionTreeCallback() {
            ActionTree.SuspendLayout();
            foreach (TreeNode node in ActionTree.Nodes)
            {
                UdateTreeNode(node);
            }
            ActionTree.ResumeLayout();
        }

        void UdateTreeNode(TreeNode node) {
            IPBComposite comp = (IPBComposite)node.Tag;
            node.Text = comp.Title;
            node.ForeColor = comp.Color;
            if (node.Nodes != null)
            {
                foreach (TreeNode child in node.Nodes)
                {
                    UdateTreeNode(child);
                }
            }
        }

        public void InitActionTree() {
            if (!IsValid)
                return;
            if (ActionTree.InvokeRequired)
                ActionTree.BeginInvoke(new guiInvokeCB(InitActionTreeCallback));
            else
                InitActionTreeCallback();
        }
        public void InitActionTreeCallback() {
            ActionTree.SuspendLayout();
            int selectedIndex = -1;
            if (ActionTree.SelectedNode != null)
                selectedIndex = ActionTree.Nodes.IndexOf(ActionTree.SelectedNode);
            ActionTree.Nodes.Clear();
            foreach (IPBComposite composite in PB.CurrentProfile.Branch.Children)
            {
                TreeNode node = new TreeNode(composite.Title);
                node.ForeColor = composite.Color;
                node.Tag = composite;
                if (composite is Decorator)
                {
                    ActionTreeAddChildren((GroupComposite)((Decorator)composite).DecoratedChild, node);
                }
                ActionTree.Nodes.Add(node);
            }
            ActionTree.ExpandAll();
            if (selectedIndex != -1)
            {
                if (selectedIndex < ActionTree.Nodes.Count)
                    ActionTree.SelectedNode = ActionTree.Nodes[selectedIndex];
                else
                    ActionTree.SelectedNode = ActionTree.Nodes[ActionTree.Nodes.Count - 1];
            }
            ActionTree.ResumeLayout();
        }

        //void ActionTreeAddChildren(If ds, TreeNode node) {
        void ActionTreeAddChildren(GroupComposite ds, TreeNode node) {
            foreach (IPBComposite child in ds.Children)
            {
                TreeNode childNode = new TreeNode(child.Title);
                childNode.ForeColor = child.Color;
                childNode.Tag = child;
                // If, While and SubRoutine are Decorators.
                if (child is Decorator)
                {
                    ActionTreeAddChildren((GroupComposite)((Decorator)child).DecoratedChild, childNode);
                }
                node.Nodes.Add(childNode);
            }
        }

        public void RefreshTradeSkillTabs() {
            if (!IsValid)
                return;
            if (TradeSkillTabControl.InvokeRequired)
                TradeSkillTabControl.BeginInvoke(new guiInvokeCB(RefreshTradeSkillTabsCallback));
            else
                RefreshTradeSkillTabsCallback();
        }

        private void RefreshTradeSkillTabsCallback() {
            foreach (TradeSkillListView tv in TradeSkillTabControl.TabPages)
            {
                tv.TradeDataView.SuspendLayout();
                foreach (DataGridViewRow row in tv.TradeDataView.Rows)
                {
                    TradeSkillRecipeCell cell = (TradeSkillRecipeCell)row.Cells[0].Value;
                    row.Cells[1].Value = Util.CalculateRecipeRepeat(cell.Recipe);
                }
                tv.TradeDataView.ResumeLayout();
            }
        }
        void PopulateActionGridView() {
            ActionGridView.Rows.Clear();
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (Type type in asm.GetTypes())
            {
                if (type.GetInterface("IPBComposite") != null && !type.IsAbstract)
                {
                    IPBComposite pa = (IPBComposite)Activator.CreateInstance(type);
                    DataGridViewRow row = new DataGridViewRow();
                    DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                    cell.Value = pa.Name;
                    row.Cells.Add(cell);
                    row.Tag = pa;
                    row.Height = 16;
                    ActionGridView.Rows.Add(row);
                    row.DefaultCellStyle.ForeColor = pa.Color;
                    //row.DefaultCellStyle.SelectionBackColor = pa.Color;
                }
            }
        }
        #endregion

        #region Callbacks
        void ActionTree_DragDrop(object sender, DragEventArgs e) {
            copyAction = CopyPasteOperactions.Cut;

            if ((e.KeyState & 4) > 0) // shift key
                copyAction |= CopyPasteOperactions.IgnoreRoot;
            if ((e.KeyState & 8) > 0) // ctrl key
                copyAction |= CopyPasteOperactions.Copy;

            if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", false))
            {
                Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
                TreeNode dest = ((TreeView)sender).GetNodeAt(pt);
                TreeNode newNode = (TreeNode)e.Data.GetData("System.Windows.Forms.TreeNode");
                PasteAction(newNode, dest);
            }
            else if (e.Data.GetDataPresent("System.Windows.Forms.DataGridViewRow", false))
            {
                Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
                TreeNode dest = ((TreeView)sender).GetNodeAt(pt);
                DataGridViewRow row = (DataGridViewRow)e.Data.GetData("System.Windows.Forms.DataGridViewRow");
                if (row.Tag.GetType().GetInterface("IPBComposite") != null)
                {
                    IPBComposite pa = (IPBComposite)Activator.CreateInstance(row.Tag.GetType());
                    AddToActionTree(pa, dest);
                }
            }
        }

        void PasteAction(TreeNode source, TreeNode dest) {
            if (dest != source && (!IsChildNode(source, dest) || dest == null))
            {
                PrioritySelector ps = (PrioritySelector)((Composite)source.Tag).Parent;
                if ((copyAction & CopyPasteOperactions.Copy) != CopyPasteOperactions.Copy)
                    ps.Children.Remove((Composite)source.Tag);
                AddToActionTree(source, dest);
                if ((copyAction & CopyPasteOperactions.Copy) != CopyPasteOperactions.Copy) // ctrl key
                    source.Remove();
                copySource = null;// free any ref..
            }
        }

        void ActionTree_DragEnter(object sender, DragEventArgs e) {
            e.Effect = DragDropEffects.Move;
        }

        void ActionTree_ItemDrag(object sender, ItemDragEventArgs e) {
            this.DoDragDrop(e.Item, DragDropEffects.Move);
        }

        void ActionTree_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyData == Keys.Escape)
            {
                ActionTree.SelectedNode = null;
                e.Handled = true;
            }
            else if (e.KeyData == Keys.Delete)
            {
                if (ActionTree.SelectedNode != null)
                    RemoveSelectedNodes();
            }
        }
        private void ActionTree_AfterSelect(object sender, TreeViewEventArgs e) {
            if (!IsValid)
                return;
            IPBComposite comp = (IPBComposite)e.Node.Tag;
            if (comp != null && comp.Properties != null)
            {
                MainForm.Instance.ActionGrid.SelectedObject = comp.Properties;
            }
        }

        public void OnTradeSkillsLoadedEventHandler(object sender, EventArgs e) {
            // must create GUI elements on its parent thread
            if (this.IsHandleCreated)
                this.BeginInvoke(new InitDelegate(Initialize));
            else
            {
                this.HandleCreated += MainForm_HandleCreated;
            }
            PB.OnTradeSkillsLoaded -= OnTradeSkillsLoadedEventHandler;
        }

        void MainForm_HandleCreated(object sender, EventArgs e) {
            this.BeginInvoke(new InitDelegate(Initialize));
            this.HandleCreated -= MainForm_HandleCreated;
        }

        private void MainForm_Load(object sender, EventArgs e) {
            if (!PB.IsTradeSkillsLoaded)
            {
                PB.OnTradeSkillsLoaded -= OnTradeSkillsLoadedEventHandler;
                PB.OnTradeSkillsLoaded += OnTradeSkillsLoadedEventHandler;
            }
            else
                Initialize();
            if (PB.CodeWasModified)
                PB.GenorateDynamicCode();
        }

        private void MainForm_ResizeBegin(object sender, EventArgs e) {
            this.SuspendLayout();
        }

        private void MainForm_ResizeEnd(object sender, EventArgs e) {
            this.ResumeLayout();
        }


        private void StartButton_Click(object sender, EventArgs e) {
            ToggleStart();
        }


        private void ActionGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e) {
            if (ActionGrid.SelectedObject is CastSpellAction && ((CastSpellAction)ActionGrid.SelectedObject).IsRecipe)
            {
                CastSpellAction ca = (CastSpellAction)ActionGrid.SelectedObject;
                PB.UpdateMaterials();
                RefreshTradeSkillTabs();
            }
            if (PB.CodeWasModified)
                PB.GenorateDynamicCode();
            RefreshActionTree();
        }

        private void RemoveButton_Click(object sender, EventArgs e) {
            RemoveSelectedNodes();
        }

        void RemoveSelectedNodes() {
            if (ActionTree.SelectedNode != null)
            {
                Composite comp = (Composite)ActionTree.SelectedNode.Tag;
                ((PrioritySelector)comp.Parent).Children.Remove(comp);
                if (comp is CastSpellAction && ((CastSpellAction)comp).IsRecipe)
                {
                    PB.UpdateMaterials();
                    RefreshTradeSkillTabs();
                }
                if (ActionTree.SelectedNode.Parent != null)
                    ActionTree.SelectedNode.Parent.Nodes.RemoveAt(ActionTree.SelectedNode.Index);
                else
                    ActionTree.Nodes.RemoveAt(ActionTree.SelectedNode.Index);
            }
        }

        private void ActionGridView_MouseMove(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left)
            {
                this.ActionGridView.DoDragDrop(this.ActionGridView.CurrentRow, DragDropEffects.All);
            }
        }

        private void ActionGridView_SelectionChanged(object sender, EventArgs e) {
            if (ActionGridView.SelectedRows != null && ActionGridView.SelectedRows.Count > 0)
                HelpTextBox.Text = ((IPBComposite)ActionGridView.SelectedRows[0].Tag).Help;
        }

        private void toolStripOpen_Click(object sender, EventArgs e) {
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Professionbuddy.LoadProfile(openFileDialog.FileName);
                // check for a LoadProfileAction and load the profile to stop all the crying from the lazy noobs 
                LoadFirstProfileAction(PB.CurrentProfile.Branch);
                if (PB.ProfileSettings.Settings.Count > 0)
                    toolStripSettings.Enabled = true;
            }
        }

        bool LoadFirstProfileAction(Composite comp) {
            if (comp is LoadProfileAction &&
                ((LoadProfileAction)comp).ProfileType == LoadProfileAction.LoadProfileType.Honorbuddy)
            {
                ((LoadProfileAction)comp).Load();
                return true;
            }
            else if (comp is GroupComposite)
            {
                foreach (Composite c in ((GroupComposite)comp).Children)
                {
                    if (LoadFirstProfileAction(c))
                        return true;
                }
            }
            return false;
        }

        private void toolStripSave_Click(object sender, EventArgs e) {
            saveFileDialog.DefaultExt = "xml";
            saveFileDialog.FilterIndex = 1;
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                bool zip = Path.GetExtension(saveFileDialog.FileName).Equals(".package", StringComparison.InvariantCultureIgnoreCase);
                // if we are saving to a zip check if CurrentProfile.XmlPath is not blank/null and use it if not. 
                // otherwise use the selected zipname with xml ext.
                string xmlfile = zip ? (string.IsNullOrEmpty(PB.CurrentProfile.XmlPath) ?
                    Path.ChangeExtension(saveFileDialog.FileName, ".xml") : PB.CurrentProfile.XmlPath)
                    : saveFileDialog.FileName;
                Professionbuddy.Log("Packaging profile to {0}", saveFileDialog.FileName);
                PB.CurrentProfile.SaveXml(xmlfile);
                if (zip)
                    PB.CurrentProfile.CreatePackage(saveFileDialog.FileName, xmlfile);
                PB.MySettings.LastProfile = saveFileDialog.FileName;
                PB.MySettings.Save();
                UpdateControls();
            }
        }

        private void toolStripAddBtn_Click(object sender, EventArgs e) {
            List<IPBComposite> compositeList = new List<IPBComposite>();
            // if the tradeskill tab is selected
            if (MainTabControl.SelectedTab == TradeSkillTab)
            {
                TradeSkillListView tv = TradeSkillTabControl.SelectedTab as TradeSkillListView;
                if (tv.TradeDataView.SelectedRows == null)
                    return;

                DataGridViewSelectedRowCollection rowCollection = tv.TradeDataView.SelectedRows;
                foreach (DataGridViewRow row in rowCollection)
                {
                    TradeSkillRecipeCell cell = (TradeSkillRecipeCell)row.Cells[0].Value;
                    Recipe recipe = PB.TradeSkillList[tv.TradeIndex].Recipes[cell.RecipeID];
                    int repeat;
                    int.TryParse(toolStripAddNum.Text, out repeat);
                    CastSpellAction.RepeatCalculationType repeatType = CastSpellAction.RepeatCalculationType.Specific;
                    switch (toolStripAddCombo.SelectedIndex)
                    {
                        case 1:
                            repeatType = CastSpellAction.RepeatCalculationType.Craftable;
                            break;
                        case 2:
                            repeatType = CastSpellAction.RepeatCalculationType.Banker;
                            break;
                    }
                    CastSpellAction ca = new CastSpellAction(recipe, repeat, repeatType);
                    compositeList.Add(ca);
                }
            }
            else if (MainTabControl.SelectedTab == ActionsTab)
            {
                if (ActionGridView.SelectedRows != null)
                {
                    foreach (DataGridViewRow row in ActionGridView.SelectedRows)
                    {
                        IPBComposite pa = (IPBComposite)Activator.CreateInstance(row.Tag.GetType());
                        compositeList.Add(pa);
                    }
                }
            }
            copyAction = CopyPasteOperactions.Copy;
            foreach (IPBComposite composite in compositeList)
            {
                if (ActionTree.SelectedNode == null)
                    AddToActionTree(composite, null);
                else
                    AddToActionTree(composite, ActionTree.SelectedNode);
            }
            // now update the CanRepeatCount. 
            PB.UpdateMaterials();
            RefreshTradeSkillTabs();
        }

        private void toolStripDelete_Click(object sender, EventArgs e) {
            RemoveSelectedNodes();
        }

        private void toolStripStart_Click(object sender, EventArgs e) {
            ToggleStart();
        }

        private void Materials_Click(object sender, EventArgs e) {
            new MaterialListForm().ShowDialog();
        }

        private void toolStripHelp_Click(object sender, EventArgs e) {
            Form helpWindow = new Form();
            helpWindow.Height = 600;
            helpWindow.Width = 600;
            helpWindow.Text = "ProfessionBuddy Guide";
            RichTextBox helpView = new RichTextBox();
            helpView.Dock = DockStyle.Fill;
            helpView.ReadOnly = true;
#if DLL
            helpView.Rtf = HighVoltz.Properties.Resources.Guide;
#else
            helpView.LoadFile(Path.Combine(PB.PluginPath, "Guide.rtf"));
#endif
            helpWindow.Controls.Add(helpView);
            helpWindow.Show();
        }

        private void toolStripCopy_Click(object sender, EventArgs e) {
            copySource = ActionTree.SelectedNode;
            if (copySource != null)
                copyAction = CopyPasteOperactions.Copy;
        }

        private void toolStripPaste_Click(object sender, EventArgs e) {
            if (copySource != null && ActionTree.SelectedNode != null)
                PasteAction(copySource, ActionTree.SelectedNode);
        }

        private void toolStripCut_Click(object sender, EventArgs e) {
            copySource = ActionTree.SelectedNode;
            if (copySource != null)
                copyAction = CopyPasteOperactions.Cut;
        }

        private void toolStripSecretButton_Click(object sender, EventArgs e) {
            PrioritySelector ps = TreeRoot.Current.Root as PrioritySelector;
            int n = 0;
            Logging.Write("** BotBase **");
            foreach (var p in ps.Children)
            {
                // add alternating amount of spaces to the end of log entries to prevent spam filter from blocking it
                n = (n + 1) % 2;
                Logging.Write("[{0}] {1}", p.GetType(), new string(' ', n));
            }
            Logging.Write("** Profile Settings **");
            foreach (var kv in PB.ProfileSettings.Settings)
                Logging.Write("{0} {1}", kv.Key, kv.Value);
            Logging.Write("** ActionSelector **");
            printComposite(PB.CurrentProfile.Branch, 0);
            Logging.Write("** Material List **");
            foreach (var kv in PB.MaterialList)
                Logging.Write("Ingredient ID: {0} Amount required:{1}", kv.Key, kv.Value);
            Logging.Write("** DataStore **");
            foreach (var kv in PB.DataStore)
                Logging.Write("item ID: {0} Amount in bag/bank/ah/alts:{1}", kv.Key, kv.Value);
        }

        void printComposite(Composite comp, int cnt) {
            string name;
            if (comp.GetType().GetInterface("IPBComposite") != null)
                name = ((IPBComposite)comp).Title;
            else
                name = comp.GetType().ToString();
            if (comp is IPBComposite)
                Logging.Write("{0}{1} IsDone:{2} LastStatus:{3}", new string(' ', cnt * 4), ((IPBComposite)comp).Title, ((IPBComposite)comp).IsDone, comp.LastStatus);
            if (comp is GroupComposite)
            {
                foreach (Composite child in ((GroupComposite)comp).Children)
                {
                    printComposite(child, cnt + 1);
                }
            }
        }

        private void toolStripSettings_Click(object sender, EventArgs e) {
            Form settingWindow = new Form();
            settingWindow.Height = 300;
            settingWindow.Width = 300;
            settingWindow.Text = "Profile Settings";
            PropertyGrid pg = new PropertyGrid();
            pg.Dock = DockStyle.Fill;
            settingWindow.Controls.Add(pg);

            ProfilePropertyBag = new PropertyBag();
            foreach (var kv in PB.ProfileSettings.Settings)
            {
                string sum = PB.ProfileSettings.Summaries.ContainsKey(kv.Key) ?
                    PB.ProfileSettings.Summaries[kv.Key] : "";
                ProfilePropertyBag[kv.Key] = new MetaProp(kv.Key, kv.Value.GetType(),
                    new DescriptionAttribute(sum));
                ProfilePropertyBag[kv.Key].Value = kv.Value;
                ProfilePropertyBag[kv.Key].PropertyChanged += new EventHandler(MainForm_PropertyChanged);
            }
            pg.SelectedObject = ProfilePropertyBag;
            toolStripSettings.Enabled = false;
            settingWindow.Show();
            settingWindow.Disposed += new EventHandler(settingWindow_Disposed);
        }

        void settingWindow_Disposed(object sender, EventArgs e) {
            // cleanup 
            foreach (var kv in PB.ProfileSettings.Settings)
            {
                ProfilePropertyBag[kv.Key].PropertyChanged -= MainForm_PropertyChanged;
            }
            ((Form)sender).Disposed -= settingWindow_Disposed;
            toolStripSettings.Enabled = true;
        }

        void MainForm_PropertyChanged(object sender, EventArgs e) {
            PB.ProfileSettings[((MetaProp)sender).Name] = ((MetaProp)sender).Value;
        }

    }
        #endregion

    #region TradeSkillListView
    internal class TradeSkillListView : TabPage
    {
        public DataGridViewTextBoxColumn NameColumn { get; private set; }
        public DataGridViewTextBoxColumn CraftableColumn { get; private set; }
        public DataGridViewTextBoxColumn DifficultyColumn { get; private set; }
        public DataGridView TradeDataView { get; private set; }
        public TextBox FilterText { get; private set; }
        public ComboBox CategoryCombo { get; private set; }
        private TableLayoutPanel tabTableLayout;

        public TradeSkillListView()
            : this(0) {
        }

        public int TradeIndex {
            get { return index; }
        }

        private int index; // index to Professionbuddy.Instance.TradeSkillList

        public TradeSkillListView(int index) {
            this.index = index;
            // Filter TextBox
            FilterText = new TextBox();
            FilterText.Dock = DockStyle.Fill;
            // Category Combobox
            CategoryCombo = new ComboBox();
            CategoryCombo.Dock = DockStyle.Fill;
            // columns
            NameColumn = new DataGridViewTextBoxColumn();
            CraftableColumn = new DataGridViewTextBoxColumn();
            DifficultyColumn = new DataGridViewTextBoxColumn();
            NameColumn.HeaderText = "Name";
            CraftableColumn.HeaderText = "#";
            NameColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            CraftableColumn.MinimumWidth = 25;
            CraftableColumn.Width = 25;
            DifficultyColumn.MinimumWidth = 25;
            DifficultyColumn.Width = 25;
            // DataGridView
            TradeDataView = new DataGridView();
            TradeDataView.Dock = DockStyle.Fill;
            TradeDataView.AllowUserToAddRows = false;
            TradeDataView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            TradeDataView.RowHeadersVisible = false;
            TradeDataView.Columns.Add(NameColumn);
            TradeDataView.Columns.Add(CraftableColumn);
            TradeDataView.Columns.Add(DifficultyColumn);
            TradeDataView.AllowUserToResizeRows = false;
            TradeDataView.EditMode = DataGridViewEditMode.EditProgrammatically;
            TradeDataView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            TradeDataView.ColumnHeadersHeight = 21;
            TradeDataView.RowTemplate.Height = 16;
            //table layout
            tabTableLayout = new TableLayoutPanel();
            tabTableLayout.ColumnCount = 2;
            tabTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tabTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tabTableLayout.RowCount = 2;
            tabTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tabTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tabTableLayout.Controls.Add(FilterText, 0, 0);
            tabTableLayout.Controls.Add(CategoryCombo, 1, 0);
            tabTableLayout.Controls.Add(TradeDataView, 0, 1);
            tabTableLayout.Dock = DockStyle.Fill;
            tabTableLayout.SetColumnSpan(TradeDataView, 2);
            // tab
            this.Controls.Add(tabTableLayout);
            this.Text = Professionbuddy.Instance.TradeSkillList[index].Name;
            // populate the controls with data
            CategoryCombo.Items.Add(""); // blank line will show all headers...

            foreach (KeyValuePair<uint, Recipe> kv in Professionbuddy.Instance.TradeSkillList[index].Recipes)
            {
                if (!CategoryCombo.Items.Contains(kv.Value.Header))
                {
                    CategoryCombo.Items.Add(kv.Value.Header);
                }
                TradeDataView.Rows.Add(new TradeSkillRecipeCell(index, kv.Key), Util.CalculateRecipeRepeat(kv.Value),
                                       (int)kv.Value.Difficulty); // make color column sortable by dificulty..
            }
            TradeDataView_SelectionChanged(null, null);
            // hook events
            FilterText.TextChanged += new EventHandler(FilterText_TextChanged);
            CategoryCombo.SelectedValueChanged += new EventHandler(SectionCombo_SelectedValueChanged);
            TradeDataView.SelectionChanged += new EventHandler(TradeDataView_SelectionChanged);
            TradeDataView.CellFormatting += new DataGridViewCellFormattingEventHandler(TradeDataView_CellFormatting);
        }

        private void TradeDataView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e) {

            if (TradeDataView.Columns[e.ColumnIndex].HeaderText == "")
            {
                TradeSkillRecipeCell tsrc = TradeDataView.Rows[e.RowIndex].Cells[0].Value as TradeSkillRecipeCell;
                e.CellStyle.ForeColor = tsrc.Recipe.Color;
                e.CellStyle.BackColor = e.CellStyle.ForeColor;
                e.CellStyle.SelectionBackColor = e.CellStyle.ForeColor;
                e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
            }
        }

        private void TradeDataView_SelectionChanged(object sender, EventArgs e) {
            if (MainForm.IsValid)
            {
                MainForm.Instance.IngredientsView.Rows.Clear();
                if (TradeDataView.SelectedRows.Count > 0)
                {
                    TradeSkillRecipeCell cell = (TradeSkillRecipeCell)TradeDataView.SelectedRows[0].Cells[0].Value;
                    Recipe _recipe = Professionbuddy.Instance.TradeSkillList[index].Recipes[cell.RecipeID];
                    DataGridViewRow row = new DataGridViewRow();
                    foreach (Ingredient ingred in _recipe.Ingredients)
                    {
                        uint inBags = ingred.InBagsCount;
                        MainForm.Instance.IngredientsView.Rows.
                            Add(ingred.Name, ingred.Required, inBags, Util.GetBankItemCount(ingred.ID, inBags));
                        if (ingred.InBagsCount < ingred.Required)
                        {
                            MainForm.Instance.IngredientsView.Rows[MainForm.Instance.IngredientsView.Rows.Count - 1].
                                Cells[2].Style.SelectionBackColor = Color.Red;
                            MainForm.Instance.IngredientsView.Rows[MainForm.Instance.IngredientsView.Rows.Count - 1].
                                Cells[2].Style.ForeColor = Color.Red;
                        }
                        MainForm.Instance.IngredientsView.ClearSelection();
                    }
                }
            }
        }

        private void FilterText_TextChanged(object sender, EventArgs e) {
            filterTradeDateView();
        }

        private void SectionCombo_SelectedValueChanged(object sender, EventArgs e) {
            filterTradeDateView();
        }

        private void filterTradeDateView() {
            TradeDataView.Rows.Clear();
            string filter = FilterText.Text.ToUpper();
            bool noFilter = string.IsNullOrEmpty(FilterText.Text);
            bool showAllCategories = string.IsNullOrEmpty(CategoryCombo.Text);
            foreach (KeyValuePair<uint, Recipe> kv in Professionbuddy.Instance.TradeSkillList[index].Recipes)
            {
                if ((noFilter || kv.Value.Name.ToUpper().Contains(filter)) &&
                    (showAllCategories || kv.Value.Header == CategoryCombo.Text))
                {
                    TradeDataView.Rows.Add(new TradeSkillRecipeCell(index, kv.Key), kv.Value.CanRepeatNum,
                                           kv.Value.Color);
                }
            }
        }
    }

    // attached to the TradeSkillListView cell values

    internal class TradeSkillRecipeCell
    {
        public TradeSkillRecipeCell(int index, uint id) {
            TradeSkillIndex = index;
            RecipeID = id;
        }

        public string RecipeName {
            get { return Professionbuddy.Instance.TradeSkillList[TradeSkillIndex].Recipes[RecipeID].Name; }
        }

        public uint RecipeID { get; private set; }

        public Recipe Recipe {
            get { return Professionbuddy.Instance.TradeSkillList[TradeSkillIndex].Recipes[RecipeID]; }
        }

        public int TradeSkillIndex { get; private set; }

        public override string ToString() {
            return RecipeName;
        }
    }
    #endregion

}