
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SDBuilderWin
{
    public sealed partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        // Top bar controls
        private Panel topPanel;
        private Label lblVersion;
        private ComboBox cmbDrives;
        private Button btnRefresh;
        private Button btnStop;
        private Button btnOpen;
        private Button btnEject;

        // Body
        private TabControl tabs;
        private GroupBox grpLog;
        private TextBox txtLog;

        // Tabs
        private TabPage tabXstation;
        private TabPage tabSaroo;
        private TabPage tabGamecube;
        private TabPage tabSC64;
        private TabPage tabListmaker;
        private TabPage tabSettings;

        // Xstation
        private Button btnXstationFw;
        private ComboBox cmbXstationList;
        private Button btnXstationRefreshLists;
        private Button btnXstationOpenLists;
        private Button btnXstationStart;

        // Saroo
        private Button btnSarooFw;
        private ComboBox cmbSarooList;
        private Button btnSarooRefreshLists;
        private Button btnSarooOpenLists;
        private Button btnSarooStart;

        // Gamecube
        private Button btnGamecubeFw;
        private ComboBox cmbGamecubeList;
        private Button btnGamecubeRefreshLists;
        private Button btnGamecubeOpenLists;
        private Button btnGamecubeStart;

        // Summercart64
        private Button btnSC64Fw;
        private ComboBox cmbSC64List;
        private Button btnSC64RefreshLists;
        private Button btnSC64OpenLists;
        private Button btnSC64Start;
        private Button btnSC64Install64DD;
        private Button btnSC64InstallEmulators;

        // Listmaker
        private Button btnChooseFolder;
        private TextBox txtFilter;
        private CheckBox chkRecursive;
        private Button btnRefreshLM;
        private RadioButton rbFiles;
        private RadioButton rbDirs;
        private ListView lvList;
        private Button btnCheckAll;
        private Button btnUncheckAll;
        private Button btnDeleteSelected;
        private CheckBox chkAutoValidate;
        private Button btnValidateNow;
        private Button btnOpenTxt;
        private Button btnSaveTxt;
        private StatusStrip statusStripLM;
        private ToolStripStatusLabel lblStatusLM;
        private ToolStripStatusLabel lblSepLM1;
        private ToolStripStatusLabel lblSpacerLM;
        private ToolStripStatusLabel lblCountLM;
        private ToolStripStatusLabel lblSepLM2;
        private ToolStripStatusLabel lblSizeLM;

        // Settings
        private CheckBox chkOnlyRemovable;
        private NumericUpDown numCopy;
        private NumericUpDown numOW;
        private ComboBox cmbAuto;
        private Button btnSaveSettings;

        private void InitializeComponent()
        {
            topPanel = new Panel();
            driveRow = new FlowLayoutPanel();
            lblDrive = new Label();
            cmbDrives = new ComboBox();
            btnRefresh = new Button();
            btnStop = new Button();
            btnOpen = new Button();
            btnEject = new Button();
            lblVersion = new Label();
            tabs = new TabControl();
            tabXstation = new TabPage();
            xRoot = new TableLayoutPanel();
            xRow = new FlowLayoutPanel();
            btnXstationFw = new Button();
            lblList1 = new Label();
            cmbXstationList = new ComboBox();
            xListBtns = new FlowLayoutPanel();
            btnXstationRefreshLists = new Button();
            btnXstationOpenLists = new Button();
            btnXstationStart = new Button();
            xFill = new Panel();
            tabSaroo = new TabPage();
            sRoot = new TableLayoutPanel();
            sRow = new FlowLayoutPanel();
            btnSarooFw = new Button();
            lblList2 = new Label();
            cmbSarooList = new ComboBox();
            sListBtns = new FlowLayoutPanel();
            btnSarooRefreshLists = new Button();
            btnSarooOpenLists = new Button();
            btnSarooStart = new Button();
            sFill = new Panel();
            tabGamecube = new TabPage();
            gRoot = new TableLayoutPanel();
            gRow = new FlowLayoutPanel();
            btnGamecubeFw = new Button();
            lblList3 = new Label();
            cmbGamecubeList = new ComboBox();
            gListBtns = new FlowLayoutPanel();
            btnGamecubeRefreshLists = new Button();
            btnGamecubeOpenLists = new Button();
            btnGamecubeStart = new Button();
            gFill = new Panel();
            tabSC64 = new TabPage();
            scRoot = new TableLayoutPanel();
            scRow = new FlowLayoutPanel();
            btnSC64Fw = new Button();
            lblList4 = new Label();
            cmbSC64List = new ComboBox();
            scListBtns = new FlowLayoutPanel();
            btnSC64RefreshLists = new Button();
            btnSC64OpenLists = new Button();
            btnSC64Start = new Button();
            scExtras = new FlowLayoutPanel();
            btnSC64Install64DD = new Button();
            btnSC64InstallEmulators = new Button();
            scFill = new Panel();
            tabListmaker = new TabPage();
            lvList = new ListView();
            lmTop2 = new FlowLayoutPanel();
            lblMode = new Label();
            rbFiles = new RadioButton();
            rbDirs = new RadioButton();
            lmTop1 = new FlowLayoutPanel();
            btnChooseFolder = new Button();
            lblFilter = new Label();
            txtFilter = new TextBox();
            chkRecursive = new CheckBox();
            btnRefreshLM = new Button();
            lmBottom = new FlowLayoutPanel();
            btnCheckAll = new Button();
            btnUncheckAll = new Button();
            btnDeleteSelected = new Button();
            lmSpacer1 = new Label();
            chkAutoValidate = new CheckBox();
            btnValidateNow = new Button();
            lmSpacer2 = new Label();
            btnOpenTxt = new Button();
            btnSaveTxt = new Button();
            statusStripLM = new StatusStrip();
            lblStatusLM = new ToolStripStatusLabel();
            lblSepLM1 = new ToolStripStatusLabel();
            lblSpacerLM = new ToolStripStatusLabel();
            lblCountLM = new ToolStripStatusLabel();
            lblSepLM2 = new ToolStripStatusLabel();
            lblSizeLM = new ToolStripStatusLabel();
            tabSettings = new TabPage();
            setPanel = new FlowLayoutPanel();
            lblDriveList = new Label();
            chkOnlyRemovable = new CheckBox();
            lblCopyTO = new Label();
            numCopy = new NumericUpDown();
            lblOWTO = new Label();
            numOW = new NumericUpDown();
            lblAuto = new Label();
            cmbAuto = new ComboBox();
            btnSaveSettings = new Button();
            grpLog = new GroupBox();
            txtLog = new TextBox();
            topPanel.SuspendLayout();
            driveRow.SuspendLayout();
            tabs.SuspendLayout();
            tabXstation.SuspendLayout();
            xRoot.SuspendLayout();
            xRow.SuspendLayout();
            xListBtns.SuspendLayout();
            tabSaroo.SuspendLayout();
            sRoot.SuspendLayout();
            sRow.SuspendLayout();
            sListBtns.SuspendLayout();
            tabGamecube.SuspendLayout();
            gRoot.SuspendLayout();
            gRow.SuspendLayout();
            gListBtns.SuspendLayout();
            tabSC64.SuspendLayout();
            scRoot.SuspendLayout();
            scRow.SuspendLayout();
            scListBtns.SuspendLayout();
            scFill.SuspendLayout();
            tabListmaker.SuspendLayout();
            lmTop2.SuspendLayout();
            lmTop1.SuspendLayout();
            lmBottom.SuspendLayout();
            statusStripLM.SuspendLayout();
            tabSettings.SuspendLayout();
            setPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numCopy).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numOW).BeginInit();
            grpLog.SuspendLayout();
            SuspendLayout();
            // 
            // topPanel
            // 
            topPanel.Controls.Add(driveRow);
            topPanel.Controls.Add(lblVersion);
            topPanel.Dock = DockStyle.Top;
            topPanel.Location = new Point(0, 0);
topPanel.AutoSize = false;
            topPanel.Height = 56;

            topPanel.Name = "topPanel";
            topPanel.Padding = new Padding(10, 8, 10, 8);
            topPanel.Size = new Size(1000, 60);
            topPanel.TabIndex = 2;
            // 
            // driveRow
            // 

            driveRow.Controls.Add(lblDrive);
            driveRow.Controls.Add(cmbDrives);
            driveRow.Controls.Add(btnRefresh);
            driveRow.Controls.Add(btnStop);
            driveRow.Controls.Add(btnOpen);
            driveRow.Controls.Add(btnEject);
            driveRow.Dock = DockStyle.Top;

            driveRow.AutoSize = false;
            driveRow.Height = 36;
            driveRow.MinimumSize = new Size(0, 36);

driveRow.Location = new Point(10, 8);
            driveRow.Margin = new Padding(0);
            driveRow.Name = "driveRow";
            driveRow.Size = new Size(980, 29);
            driveRow.TabIndex = 0;
            driveRow.WrapContents = false;
            // 
            // lblDrive
            // 
            lblDrive.AutoSize = true;
            lblDrive.Location = new Point(0, 6);
            lblDrive.Margin = new Padding(0, 6, 8, 0);
            lblDrive.Name = "lblDrive";
            lblDrive.Size = new Size(99, 15);
            lblDrive.TabIndex = 0;
            lblDrive.Text = "Destination drive:";
            // 
            // cmbDrives
            // 
            cmbDrives.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDrives.Location = new Point(110, 3);
            cmbDrives.Name = "cmbDrives";
            cmbDrives.Size = new Size(260, 23);
            cmbDrives.TabIndex = 1;
            // 
            // btnRefresh
            // 
            btnRefresh.Location = new Point(381, 0);
            btnRefresh.Margin = new Padding(8, 0, 0, 0);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.AutoSize = false;

btnRefresh.Size = new Size(80, 28);
            btnRefresh.TabIndex = 2;
            btnRefresh.Text = "Refresh";
            // 
            // btnStop
            // 
            btnStop.Enabled = false;
            btnStop.Location = new Point(473, 0);
            btnStop.Margin = new Padding(12, 0, 0, 0);
            btnStop.Name = "btnStop";
            btnStop.AutoSize = false;

btnStop.Size = new Size(80, 28);
            btnStop.TabIndex = 3;
            btnStop.Text = "Stop";
            // 
            // btnOpen
            // 
            btnOpen.Location = new Point(561, 0);
            btnOpen.Margin = new Padding(8, 0, 0, 0);
            btnOpen.Name = "btnOpen";
            btnOpen.AutoSize = false;

btnOpen.Size = new Size(130, 28);
            btnOpen.TabIndex = 4;
            btnOpen.Text = "Open in Explorer";
            // 
            // btnEject
            // 
            btnEject.Location = new Point(699, 0);
            btnEject.Margin = new Padding(8, 0, 0, 0);
            btnEject.Name = "btnEject";
            btnEject.AutoSize = false;

btnEject.Size = new Size(110, 28);
            btnEject.TabIndex = 5;
            btnEject.Text = "Eject safely";
            // 
            // lblVersion
            // 
            lblVersion.AutoSize = true;
            lblVersion.ForeColor = Color.DimGray;
            lblVersion.Location = new Point(12, 8);
            lblVersion.Name = "lblVersion";
            lblVersion.Size = new Size(0, 15);
            lblVersion.TabIndex = 1;
            // 
            // tabs
            // 
            tabs.Controls.Add(tabXstation);
            tabs.Controls.Add(tabSaroo);
            tabs.Controls.Add(tabGamecube);
            tabs.Controls.Add(tabSC64);
            tabs.Controls.Add(tabListmaker);
            tabs.Controls.Add(tabSettings);
            tabs.Dock = DockStyle.Fill;
            tabs.Location = new Point(0, 60);
            tabs.Name = "tabs";
            tabs.SelectedIndex = 0;
            tabs.Size = new Size(1000, 380);
            tabs.TabIndex = 0;
            // 
            // tabXstation
            // 
            tabXstation.Controls.Add(xRoot);
            tabXstation.Location = new Point(4, 24);
            tabXstation.Name = "tabXstation";
            tabXstation.Size = new Size(992, 352);
            tabXstation.TabIndex = 0;
            tabXstation.Text = "Xstation";
            // 
            // xRoot
            // 
            xRoot.ColumnCount = 1;
            xRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            xRoot.Controls.Add(xRow, 0, 0);
            xRoot.Controls.Add(xFill, 0, 1);
            xRoot.Dock = DockStyle.Fill;
            xRoot.Location = new Point(0, 0);
            xRoot.Name = "xRoot";
            xRoot.RowCount = 2;
            xRoot.RowStyles.Add(new RowStyle());
            xRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            xRoot.Size = new Size(992, 352);
            xRoot.TabIndex = 0;
            // 
            // xRow
            // 
            xRow.AutoSize = true;
            xRow.Controls.Add(btnXstationFw);
            xRow.Controls.Add(lblList1);
            xRow.Controls.Add(cmbXstationList);
            xRow.Controls.Add(xListBtns);
            xRow.Controls.Add(btnXstationStart);
            xRow.Dock = DockStyle.Fill;
            xRow.Location = new Point(3, 3);
            xRow.Name = "xRow";
            xRow.Padding = new Padding(8);
            xRow.Size = new Size(986, 119);
            xRow.TabIndex = 0;
            xRow.WrapContents = false;
            // 
            // btnXstationFw
            // 
            btnXstationFw.Location = new Point(11, 11);
            btnXstationFw.Name = "btnXstationFw";
            btnXstationFw.Size = new Size(240, 34);
            btnXstationFw.TabIndex = 0;
            btnXstationFw.Text = "Check/Download firmware";
            // 
            // lblList1
            // 
            lblList1.AutoSize = true;
            lblList1.Location = new Point(266, 16);
            lblList1.Margin = new Padding(12, 8, 6, 0);
            lblList1.Name = "lblList1";
            lblList1.Size = new Size(28, 15);
            lblList1.TabIndex = 1;
            lblList1.Text = "List:";
            // 
            // cmbXstationList
            // 
            cmbXstationList.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbXstationList.Location = new Point(303, 11);
            cmbXstationList.Name = "cmbXstationList";
            cmbXstationList.Size = new Size(360, 23);
            cmbXstationList.TabIndex = 2;
            // 
            // xListBtns
            // 
            xListBtns.AutoSize = true;
            xListBtns.Controls.Add(btnXstationRefreshLists);
            xListBtns.Controls.Add(btnXstationOpenLists);
            xListBtns.FlowDirection = FlowDirection.TopDown;
            xListBtns.Location = new Point(674, 8);
            xListBtns.Margin = new Padding(8, 0, 0, 0);
            xListBtns.Name = "xListBtns";

            xListBtns.TabIndex = 3;
            // 
            // btnXstationRefreshLists
            // 
            btnXstationRefreshLists.Location = new Point(3, 3);
            btnXstationRefreshLists.Name = "btnXstationRefreshLists";
            btnXstationRefreshLists.Size = new Size(120, 34);
            btnXstationRefreshLists.TabIndex = 0;
            btnXstationRefreshLists.Text = "Refresh lists";
            // 
            // btnXstationOpenLists
            // 
            btnXstationOpenLists.Location = new Point(3, 66);
            btnXstationOpenLists.Name = "btnXstationOpenLists";
            btnXstationOpenLists.Size = new Size(180, 34);
            btnXstationOpenLists.TabIndex = 2;
            btnXstationOpenLists.Text = "Open GameLists Folder";
            // 
            // btnXstationStart
            // 
            btnXstationStart.Enabled = false;
            btnXstationStart.Location = new Point(863, 11);
            btnXstationStart.Name = "btnXstationStart";
            btnXstationStart.Size = new Size(100, 34);
            btnXstationStart.TabIndex = 4;
            btnXstationStart.Text = "Start";
            // 
            // xFill
            // 
            xFill.Dock = DockStyle.Fill;
            xFill.Location = new Point(3, 128);
            xFill.Name = "xFill";
            xFill.Size = new Size(986, 221);
            xFill.TabIndex = 1;
            // 
            // tabSaroo
            // 
            tabSaroo.Controls.Add(sRoot);
            tabSaroo.Location = new Point(4, 24);
            tabSaroo.Name = "tabSaroo";
            tabSaroo.Size = new Size(992, 352);
            tabSaroo.TabIndex = 1;
            tabSaroo.Text = "Saroo";
            // 
            // sRoot
            // 
            sRoot.ColumnCount = 1;
            sRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            sRoot.Controls.Add(sRow, 0, 0);
            sRoot.Controls.Add(sFill, 0, 1);
            sRoot.Dock = DockStyle.Fill;
            sRoot.Location = new Point(0, 0);
            sRoot.Name = "sRoot";
            sRoot.RowCount = 2;
            sRoot.RowStyles.Add(new RowStyle());
            sRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            sRoot.Size = new Size(992, 352);
            sRoot.TabIndex = 0;
            // 
            // sRow
            // 
            sRow.AutoSize = true;
            sRow.Controls.Add(btnSarooFw);
            sRow.Controls.Add(lblList2);
            sRow.Controls.Add(cmbSarooList);
            sRow.Controls.Add(sListBtns);
            sRow.Controls.Add(btnSarooStart);
            sRow.Dock = DockStyle.Fill;
            sRow.Location = new Point(3, 3);
            sRow.Name = "sRow";
            sRow.Padding = new Padding(8);
            sRow.Size = new Size(986, 119);
            sRow.TabIndex = 0;
            sRow.WrapContents = false;
            // 
            // btnSarooFw
            // 
            btnSarooFw.Location = new Point(11, 11);
            btnSarooFw.Name = "btnSarooFw";
            btnSarooFw.Size = new Size(240, 34);
            btnSarooFw.TabIndex = 0;
            btnSarooFw.Text = "Check/Download firmware";
            // 
            // lblList2
            // 
            lblList2.AutoSize = true;
            lblList2.Location = new Point(266, 16);
            lblList2.Margin = new Padding(12, 8, 6, 0);
            lblList2.Name = "lblList2";
            lblList2.Size = new Size(28, 15);
            lblList2.TabIndex = 1;
            lblList2.Text = "List:";
            // 
            // cmbSarooList
            // 
            cmbSarooList.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSarooList.Location = new Point(303, 11);
            cmbSarooList.Name = "cmbSarooList";
            cmbSarooList.Size = new Size(360, 23);
            cmbSarooList.TabIndex = 2;
            // 
            // sListBtns
            // 
            sListBtns.AutoSize = true;
            sListBtns.Controls.Add(btnSarooRefreshLists);
            sListBtns.Controls.Add(btnSarooOpenLists);
            sListBtns.FlowDirection = FlowDirection.TopDown;
            sListBtns.Location = new Point(674, 8);
            sListBtns.Margin = new Padding(8, 0, 0, 0);
            sListBtns.Name = "sListBtns";

            sListBtns.TabIndex = 3;
            // 
            // btnSarooRefreshLists
            // 
            btnSarooRefreshLists.Location = new Point(3, 3);
            btnSarooRefreshLists.Name = "btnSarooRefreshLists";
            btnSarooRefreshLists.Size = new Size(120, 34);
            btnSarooRefreshLists.TabIndex = 0;
            btnSarooRefreshLists.Text = "Refresh lists";
            // 
            // btnSarooOpenLists
            // 
            btnSarooOpenLists.Location = new Point(3, 66);
            btnSarooOpenLists.Name = "btnSarooOpenLists";
            btnSarooOpenLists.Size = new Size(180, 34);
            btnSarooOpenLists.TabIndex = 2;
            btnSarooOpenLists.Text = "Open GameLists Folder";
            // 
            // btnSarooStart
            // 
            btnSarooStart.Enabled = false;
            btnSarooStart.Location = new Point(863, 11);
            btnSarooStart.Name = "btnSarooStart";
            btnSarooStart.Size = new Size(100, 34);
            btnSarooStart.TabIndex = 4;
            btnSarooStart.Text = "Start";
            // 
            // sFill
            // 
            sFill.Dock = DockStyle.Fill;
            sFill.Location = new Point(3, 128);
            sFill.Name = "sFill";
            sFill.Size = new Size(986, 221);
            sFill.TabIndex = 1;
            // 
            // tabGamecube
            // 
            tabGamecube.Controls.Add(gRoot);
            tabGamecube.Location = new Point(4, 24);
            tabGamecube.Name = "tabGamecube";
            tabGamecube.Size = new Size(992, 352);
            tabGamecube.TabIndex = 2;
            tabGamecube.Text = "Gamecube";
            // 
            // gRoot
            // 
            gRoot.ColumnCount = 1;
            gRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            gRoot.Controls.Add(gRow, 0, 0);
            gRoot.Controls.Add(gFill, 0, 1);
            gRoot.Dock = DockStyle.Fill;
            gRoot.Location = new Point(0, 0);
            gRoot.Name = "gRoot";
            gRoot.RowCount = 2;
            gRoot.RowStyles.Add(new RowStyle());
            gRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            gRoot.Size = new Size(992, 352);
            gRoot.TabIndex = 0;
            // 
            // gRow
            // 
            gRow.AutoSize = true;
            gRow.Controls.Add(btnGamecubeFw);
            gRow.Controls.Add(lblList3);
            gRow.Controls.Add(cmbGamecubeList);
            gRow.Controls.Add(gListBtns);
            gRow.Controls.Add(btnGamecubeStart);
            gRow.Dock = DockStyle.Fill;
            gRow.Location = new Point(3, 3);
            gRow.Name = "gRow";
            gRow.Padding = new Padding(8);
            gRow.Size = new Size(986, 119);
            gRow.TabIndex = 0;
            gRow.WrapContents = false;
            // 
            // btnGamecubeFw
            // 
            btnGamecubeFw.Location = new Point(11, 11);
            btnGamecubeFw.Name = "btnGamecubeFw";
            btnGamecubeFw.Size = new Size(240, 34);
            btnGamecubeFw.TabIndex = 0;
            btnGamecubeFw.Text = "Check/Download firmware";
            // 
            // lblList3
            // 
            lblList3.AutoSize = true;
            lblList3.Location = new Point(266, 16);
            lblList3.Margin = new Padding(12, 8, 6, 0);
            lblList3.Name = "lblList3";
            lblList3.Size = new Size(28, 15);
            lblList3.TabIndex = 1;
            lblList3.Text = "List:";
            // 
            // cmbGamecubeList
            // 
            cmbGamecubeList.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbGamecubeList.Location = new Point(303, 11);
            cmbGamecubeList.Name = "cmbGamecubeList";
            cmbGamecubeList.Size = new Size(360, 23);
            cmbGamecubeList.TabIndex = 2;
            // 
            // gListBtns
            // 
            gListBtns.AutoSize = true;
            gListBtns.Controls.Add(btnGamecubeRefreshLists);
            gListBtns.Controls.Add(btnGamecubeOpenLists);
            gListBtns.FlowDirection = FlowDirection.TopDown;
            gListBtns.Location = new Point(674, 8);
            gListBtns.Margin = new Padding(8, 0, 0, 0);
            gListBtns.Name = "gListBtns";

            gListBtns.TabIndex = 3;
            // 
            // btnGamecubeRefreshLists
            // 
            btnGamecubeRefreshLists.Location = new Point(3, 3);
            btnGamecubeRefreshLists.Name = "btnGamecubeRefreshLists";
            btnGamecubeRefreshLists.Size = new Size(120, 34);
            btnGamecubeRefreshLists.TabIndex = 0;
            btnGamecubeRefreshLists.Text = "Refresh lists";
            // 
            // btnGamecubeOpenLists
            // 
            btnGamecubeOpenLists.Location = new Point(3, 66);
            btnGamecubeOpenLists.Name = "btnGamecubeOpenLists";
            btnGamecubeOpenLists.Size = new Size(180, 34);
            btnGamecubeOpenLists.TabIndex = 2;
            btnGamecubeOpenLists.Text = "Open GameLists Folder";
            // 
            // btnGamecubeStart
            // 
            btnGamecubeStart.Enabled = false;
            btnGamecubeStart.Location = new Point(863, 11);
            btnGamecubeStart.Name = "btnGamecubeStart";
            btnGamecubeStart.Size = new Size(100, 34);
            btnGamecubeStart.TabIndex = 4;
            btnGamecubeStart.Text = "Start";
            // 
            // gFill
            // 
            gFill.Dock = DockStyle.Fill;
            gFill.Location = new Point(3, 128);
            gFill.Name = "gFill";
            gFill.Size = new Size(986, 221);
            gFill.TabIndex = 1;
            // 
            // tabSC64
            // 
            tabSC64.Controls.Add(scRoot);
            tabSC64.Location = new Point(4, 24);
            tabSC64.Name = "tabSC64";
            tabSC64.Size = new Size(992, 352);
            tabSC64.TabIndex = 3;
            tabSC64.Text = "Summercart64";
            // 
            // scRoot
            // 
            scRoot.ColumnCount = 1;
            scRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            scRoot.Controls.Add(scRow, 0, 0);
            scRoot.Controls.Add(scFill, 0, 1);
            scRoot.Dock = DockStyle.Fill;
            scRoot.Location = new Point(0, 0);
            scRoot.Name = "scRoot";
            scRoot.RowCount = 2;
            scRoot.RowStyles.Add(new RowStyle());
            scRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            scRoot.Size = new Size(992, 352);
            scRoot.TabIndex = 0;
            // 
            // scRow
            // 
            scRow.AutoSize = true;
            scRow.Controls.Add(btnSC64Fw);
            scRow.Controls.Add(lblList4);
            scRow.Controls.Add(cmbSC64List);
            scRow.Controls.Add(scListBtns);
            scRow.Controls.Add(btnSC64Start);
            scRow.Controls.Add(scExtras);
            scRow.Dock = DockStyle.Fill;
            scRow.Location = new Point(3, 3);
            scRow.Name = "scRow";
            scRow.Padding = new Padding(8);
            scRow.Size = new Size(986, 119);
            scRow.TabIndex = 0;
            scRow.WrapContents = false;
            // 
            // btnSC64Fw
            // 
            btnSC64Fw.Location = new Point(11, 11);
            btnSC64Fw.Name = "btnSC64Fw";
            btnSC64Fw.Size = new Size(240, 34);
            btnSC64Fw.TabIndex = 0;
            btnSC64Fw.Text = "Check/Download firmware";
            // 
            // lblList4
            // 
            lblList4.AutoSize = true;
            lblList4.Location = new Point(266, 16);
            lblList4.Margin = new Padding(12, 8, 6, 0);
            lblList4.Name = "lblList4";
            lblList4.Size = new Size(28, 15);
            lblList4.TabIndex = 1;
            lblList4.Text = "List:";
            // 
            // cmbSC64List
            // 
            cmbSC64List.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSC64List.Location = new Point(303, 11);
            cmbSC64List.Name = "cmbSC64List";
            cmbSC64List.Size = new Size(360, 23);
            cmbSC64List.TabIndex = 2;
            // 
            // scListBtns
            // 
            scListBtns.AutoSize = true;
            scListBtns.Controls.Add(btnSC64RefreshLists);
            scListBtns.Controls.Add(btnSC64OpenLists);
            scListBtns.FlowDirection = FlowDirection.TopDown;
            scListBtns.Location = new Point(674, 8);
            scListBtns.Margin = new Padding(8, 0, 0, 0);
            scListBtns.Name = "scListBtns";

            scListBtns.TabIndex = 3;
            // 
            // btnSC64RefreshLists
            // 
            btnSC64RefreshLists.Location = new Point(3, 3);
            btnSC64RefreshLists.Name = "btnSC64RefreshLists";
            btnSC64RefreshLists.Size = new Size(120, 34);
            btnSC64RefreshLists.TabIndex = 0;
            btnSC64RefreshLists.Text = "Refresh lists";
            // 
            // btnSC64OpenLists
            // 
            btnSC64OpenLists.Location = new Point(3, 66);
            btnSC64OpenLists.Name = "btnSC64OpenLists";
            btnSC64OpenLists.Size = new Size(180, 34);
            btnSC64OpenLists.TabIndex = 2;
            btnSC64OpenLists.Text = "Open GameLists Folder";
            // 
            // btnSC64Start
            // 
            btnSC64Start.Enabled = false;
            btnSC64Start.Location = new Point(863, 11);
            btnSC64Start.Name = "btnSC64Start";
            btnSC64Start.Size = new Size(100, 34);
            btnSC64Start.TabIndex = 4;
            btnSC64Start.Text = "Start";
            // 
            // scExtras
            // 
            scExtras.AutoSize = true;
            scExtras.FlowDirection = FlowDirection.TopDown;
            scExtras.Location = new Point(974, 8);
            scExtras.Margin = new Padding(8, 0, 0, 0);
            scExtras.Name = "scExtras";

            scExtras.TabIndex = 5;
            scExtras.WrapContents = false;
            // 
            // btnSC64Install64DD
            // 
            btnSC64Install64DD.Location = new Point(24, 44);
            btnSC64Install64DD.Name = "btnSC64Install64DD";
            btnSC64Install64DD.Size = new Size(160, 34);
            btnSC64Install64DD.TabIndex = 0;
            btnSC64Install64DD.Text = "Install 64DD IPL";
            // 
            // btnSC64InstallEmulators
            // 
            btnSC64InstallEmulators.Location = new Point(24, 93);
            btnSC64InstallEmulators.Name = "btnSC64InstallEmulators";
            btnSC64InstallEmulators.Size = new Size(160, 34);
            btnSC64InstallEmulators.TabIndex = 1;
            btnSC64InstallEmulators.Text = "Install Emulators";
            // 
            // scFill
            // 
            scFill.Controls.Add(btnSC64InstallEmulators);
            scFill.Controls.Add(btnSC64Install64DD);
            scFill.Dock = DockStyle.Fill;
            scFill.Location = new Point(3, 128);
            scFill.Name = "scFill";
            scFill.Size = new Size(986, 221);
            scFill.TabIndex = 1;
            // 
            // tabListmaker
            // 
            tabListmaker.Controls.Add(lvList);
            tabListmaker.Controls.Add(lmTop2);
            tabListmaker.Controls.Add(lmTop1);
            tabListmaker.Controls.Add(lmBottom);
            tabListmaker.Controls.Add(statusStripLM);
            tabListmaker.Location = new Point(4, 24);
            tabListmaker.Name = "tabListmaker";
            tabListmaker.Size = new Size(992, 352);
            tabListmaker.TabIndex = 4;
            tabListmaker.Text = "Listmaker";
            // 
            // lvList
            // 
            lvList.CheckBoxes = true;
            lvList.Dock = DockStyle.Fill;
            lvList.FullRowSelect = true;
            lvList.Location = new Point(0, 92);
            lvList.Name = "lvList";
            lvList.Size = new Size(992, 191);
            lvList.TabIndex = 0;
            lvList.UseCompatibleStateImageBehavior = false;
            lvList.View = View.Details;
            // 
            // lmTop2
            // 
            lmTop2.AutoSize = true;
            lmTop2.Controls.Add(lblMode);
            lmTop2.Controls.Add(rbFiles);
            lmTop2.Controls.Add(rbDirs);
            lmTop2.Dock = DockStyle.Top;
            lmTop2.Location = new Point(0, 46);
            lmTop2.Name = "lmTop2";
            lmTop2.Padding = new Padding(8);
            lmTop2.Size = new Size(992, 46);
            lmTop2.TabIndex = 1;
            // 
            // lblMode
            // 
            lblMode.AutoSize = true;
            lblMode.Location = new Point(11, 8);
            lblMode.Name = "lblMode";
            lblMode.Padding = new Padding(0, 6, 4, 0);
            lblMode.Size = new Size(45, 21);
            lblMode.TabIndex = 0;
            lblMode.Text = "Mode:";
            // 
            // rbFiles
            // 
            rbFiles.Checked = true;
            rbFiles.Location = new Point(62, 11);
            rbFiles.Name = "rbFiles";
            rbFiles.Size = new Size(104, 24);
            rbFiles.TabIndex = 1;
            rbFiles.TabStop = true;
            rbFiles.Text = "Files";
            // 
            // rbDirs
            // 
            rbDirs.Location = new Point(172, 11);
            rbDirs.Name = "rbDirs";
            rbDirs.Size = new Size(104, 24);
            rbDirs.TabIndex = 2;
            rbDirs.Text = "Directories";
            // 
            // lmTop1
            // 
            lmTop1.AutoSize = true;
            lmTop1.Controls.Add(btnChooseFolder);
            lmTop1.Controls.Add(lblFilter);
            lmTop1.Controls.Add(txtFilter);
            lmTop1.Controls.Add(chkRecursive);
            lmTop1.Controls.Add(btnRefreshLM);
            lmTop1.Dock = DockStyle.Top;
            lmTop1.Location = new Point(0, 0);
            lmTop1.Name = "lmTop1";
            lmTop1.Padding = new Padding(8);
            lmTop1.Size = new Size(992, 46);
            lmTop1.TabIndex = 2;
            // 
            // btnChooseFolder
            // 
            btnChooseFolder.Location = new Point(11, 11);
            btnChooseFolder.Name = "btnChooseFolder";
            btnChooseFolder.Size = new Size(75, 23);
            btnChooseFolder.TabIndex = 0;
            btnChooseFolder.Text = "Choose Folder...";
            // 
            // lblFilter
            // 
            lblFilter.AutoSize = true;
            lblFilter.Location = new Point(92, 8);
            lblFilter.Name = "lblFilter";
            lblFilter.Padding = new Padding(8, 6, 4, 0);
            lblFilter.Size = new Size(204, 21);
            lblFilter.TabIndex = 1;
            lblFilter.Text = "  Filter (e.g. *.iso;*.gcm or *Zelda*): ";
            // 
            // txtFilter
            // 
            txtFilter.Location = new Point(302, 11);
            txtFilter.Name = "txtFilter";
            txtFilter.Size = new Size(280, 23);
            txtFilter.TabIndex = 2;
            txtFilter.Text = "*.*";
            // 
            // chkRecursive
            // 
            chkRecursive.Location = new Point(588, 11);
            chkRecursive.Name = "chkRecursive";
            chkRecursive.Size = new Size(104, 24);
            chkRecursive.TabIndex = 3;
            chkRecursive.Text = "Include subfolders";
                        chkRecursive.Visible = false;
// 
            // btnRefreshLM
            // 
            btnRefreshLM.Location = new Point(698, 11);
            btnRefreshLM.Name = "btnRefreshLM";
            btnRefreshLM.Size = new Size(75, 23);
            btnRefreshLM.TabIndex = 4;
            btnRefreshLM.Text = "Refresh";
            // 
            // lmBottom
            // 
            lmBottom.AutoSize = true;
            lmBottom.Controls.Add(btnCheckAll);
            lmBottom.Controls.Add(btnUncheckAll);
            lmBottom.Controls.Add(btnDeleteSelected);
            lmBottom.Controls.Add(lmSpacer1);
            lmBottom.Controls.Add(chkAutoValidate);
            lmBottom.Controls.Add(btnValidateNow);
            lmBottom.Controls.Add(lmSpacer2);
            lmBottom.Controls.Add(btnOpenTxt);
            lmBottom.Controls.Add(btnSaveTxt);
            lmBottom.Dock = DockStyle.Bottom;
            lmBottom.Location = new Point(0, 283);
            lmBottom.Name = "lmBottom";
            lmBottom.Padding = new Padding(8);
            lmBottom.Size = new Size(992, 47);
            lmBottom.TabIndex = 3;
            // 
            // btnCheckAll
            // 
            btnCheckAll.AutoSize = true;
            btnCheckAll.Location = new Point(11, 11);
            btnCheckAll.Name = "btnCheckAll";
            btnCheckAll.Size = new Size(75, 25);
            btnCheckAll.TabIndex = 0;
            btnCheckAll.Text = "Check All";
            // 
            // btnUncheckAll
            // 
            btnUncheckAll.AutoSize = true;
            btnUncheckAll.Location = new Point(92, 11);
            btnUncheckAll.Name = "btnUncheckAll";
            btnUncheckAll.Size = new Size(80, 25);
            btnUncheckAll.TabIndex = 1;
            btnUncheckAll.Text = "Uncheck All";
            // 
            // btnDeleteSelected
            // 
            btnDeleteSelected.Location = new Point(178, 11);
            btnDeleteSelected.Name = "btnDeleteSelected";
            btnDeleteSelected.Size = new Size(75, 25);
            btnDeleteSelected.TabIndex = 2;
            btnDeleteSelected.Text = "Delete Selected";
            // 
            // lmSpacer1
            // 
            lmSpacer1.AutoSize = true;
            lmSpacer1.Location = new Point(259, 8);
            lmSpacer1.Name = "lmSpacer1";
            lmSpacer1.Size = new Size(16, 15);
            lmSpacer1.TabIndex = 3;
            lmSpacer1.Text = "   ";
            // 
            // chkAutoValidate
            // 
            chkAutoValidate.Location = new Point(281, 11);
            chkAutoValidate.Name = "chkAutoValidate";
            chkAutoValidate.Size = new Size(104, 24);
            chkAutoValidate.TabIndex = 4;
            chkAutoValidate.Text = "Auto-validate paths";
            // 
            // btnValidateNow
            // 
            btnValidateNow.Location = new Point(391, 11);
            btnValidateNow.Name = "btnValidateNow";
            btnValidateNow.Size = new Size(75, 23);
            btnValidateNow.TabIndex = 5;
            btnValidateNow.Text = "Validate Now";
            // 
            // lmSpacer2
            // 
            lmSpacer2.AutoSize = true;
            lmSpacer2.Location = new Point(472, 8);
            lmSpacer2.Name = "lmSpacer2";
            lmSpacer2.Size = new Size(16, 15);
            lmSpacer2.TabIndex = 6;
            lmSpacer2.Text = "   ";
            // 
            // btnOpenTxt
            // 
            btnOpenTxt.Location = new Point(494, 11);
            btnOpenTxt.Name = "btnOpenTxt";
            btnOpenTxt.Size = new Size(75, 23);
            btnOpenTxt.TabIndex = 7;
            btnOpenTxt.Text = "Open .txt...";
            // 
            // btnSaveTxt
            // 
            btnSaveTxt.Location = new Point(575, 11);
            btnSaveTxt.Name = "btnSaveTxt";
            btnSaveTxt.Size = new Size(75, 23);
            btnSaveTxt.TabIndex = 8;
            btnSaveTxt.Text = "Save checked...";
            // 
            // statusStripLM
            // 
            statusStripLM.Items.AddRange(new ToolStripItem[] { lblStatusLM, lblSepLM1, lblSpacerLM, lblCountLM, lblSepLM2, lblSizeLM });
            statusStripLM.Location = new Point(0, 330);
            statusStripLM.Name = "statusStripLM";
            statusStripLM.Size = new Size(992, 22);
            statusStripLM.TabIndex = 4;
            // 
            // lblStatusLM
            // 
            lblStatusLM.Name = "lblStatusLM";
            lblStatusLM.Size = new Size(232, 17);
            lblStatusLM.Text = "Choose a folder or open a .txt file to begin.";
            // 
            // lblSepLM1
            // 
            lblSepLM1.Name = "lblSepLM1";
            lblSepLM1.Size = new Size(28, 17);
            lblSepLM1.Text = "   |   ";
            // 
            // lblSpacerLM
            // 
            lblSpacerLM.Name = "lblSpacerLM";
            lblSpacerLM.Size = new Size(575, 17);
            lblSpacerLM.Spring = true;
            // 
            // lblCountLM
            // 
            lblCountLM.Name = "lblCountLM";
            lblCountLM.Size = new Size(65, 17);
            lblCountLM.Text = "Checked: 0";
            // 
            // lblSepLM2
            // 
            lblSepLM2.Name = "lblSepLM2";
            lblSepLM2.Size = new Size(28, 17);
            lblSepLM2.Text = "   |   ";
            // 
            // lblSizeLM
            // 
            lblSizeLM.Name = "lblSizeLM";
            lblSizeLM.Size = new Size(49, 17);
            lblSizeLM.Text = "Size: 0 B";
            // 
            // tabSettings
            // 
            tabSettings.Controls.Add(setPanel);
            tabSettings.Location = new Point(4, 24);
            tabSettings.Name = "tabSettings";
            tabSettings.Size = new Size(192, 72);
            tabSettings.TabIndex = 5;
            tabSettings.Text = "Settings";
            // 
            // setPanel
            // 
            setPanel.AutoScroll = true;
            setPanel.Controls.Add(lblDriveList);
            setPanel.Controls.Add(chkOnlyRemovable);
            setPanel.Controls.Add(lblCopyTO);
            setPanel.Controls.Add(numCopy);
            setPanel.Controls.Add(lblOWTO);
            setPanel.Controls.Add(numOW);
            setPanel.Controls.Add(lblAuto);
            setPanel.Controls.Add(cmbAuto);
            setPanel.Controls.Add(btnSaveSettings);
            setPanel.Dock = DockStyle.Fill;
            setPanel.FlowDirection = FlowDirection.TopDown;
            setPanel.Location = new Point(0, 0);
            setPanel.Name = "setPanel";
            setPanel.Padding = new Padding(10);
            setPanel.Size = new Size(192, 72);
            setPanel.TabIndex = 0;
            // 
            // lblDriveList
            // 
            lblDriveList.AutoSize = true;
            lblDriveList.Location = new Point(13, 10);
            lblDriveList.Name = "lblDriveList";
            lblDriveList.Size = new Size(55, 15);
            lblDriveList.TabIndex = 0;
            lblDriveList.Text = "Drive list:";
            // 
            // chkOnlyRemovable
            // 
            chkOnlyRemovable.AutoSize = true;
            chkOnlyRemovable.Checked = true;
            chkOnlyRemovable.CheckState = CheckState.Checked;
            chkOnlyRemovable.Location = new Point(74, 13);
            chkOnlyRemovable.Name = "chkOnlyRemovable";
            chkOnlyRemovable.Size = new Size(226, 19);
            chkOnlyRemovable.TabIndex = 1;
            chkOnlyRemovable.Text = "Only show removable drives (USB/SD)";
            // 
            // lblCopyTO
            // 
            lblCopyTO.AutoSize = true;
            lblCopyTO.Location = new Point(303, 22);
            lblCopyTO.Margin = new Padding(0, 12, 0, 0);
            lblCopyTO.Name = "lblCopyTO";
            lblCopyTO.Size = new Size(137, 15);
            lblCopyTO.TabIndex = 2;
            lblCopyTO.Text = "Copy timeout (seconds):";
            // 
            // numCopy
            // 
            numCopy.Location = new Point(443, 13);
            numCopy.Maximum = new decimal(new int[] { 999, 0, 0, 0 });
            numCopy.Name = "numCopy";
            numCopy.Size = new Size(80, 23);
            numCopy.TabIndex = 3;
            numCopy.Value = new decimal(new int[] { 20, 0, 0, 0 });
            // 
            // lblOWTO
            // 
            lblOWTO.AutoSize = true;
            lblOWTO.Location = new Point(529, 10);
            lblOWTO.Name = "lblOWTO";
            lblOWTO.Size = new Size(179, 15);
            lblOWTO.TabIndex = 4;
            lblOWTO.Text = "Overwrite countdown (seconds):";
            // 
            // numOW
            // 
            numOW.Location = new Point(714, 13);
            numOW.Maximum = new decimal(new int[] { 999, 0, 0, 0 });
            numOW.Name = "numOW";
            numOW.Size = new Size(80, 23);
            numOW.TabIndex = 5;
            numOW.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // lblAuto
            // 
            lblAuto.AutoSize = true;
            lblAuto.Location = new Point(800, 10);
            lblAuto.Name = "lblAuto";
            lblAuto.Size = new Size(124, 15);
            lblAuto.TabIndex = 6;
            lblAuto.Text = "Overwrite auto action:";
            // 
            // cmbAuto
            // 
            cmbAuto.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbAuto.Location = new Point(930, 13);
            cmbAuto.Name = "cmbAuto";
            cmbAuto.Size = new Size(80, 23);
            cmbAuto.TabIndex = 7;
            // 
            // btnSaveSettings
            // 
            btnSaveSettings.Location = new Point(1013, 22);
            btnSaveSettings.Margin = new Padding(0, 12, 0, 0);
            btnSaveSettings.Name = "btnSaveSettings";
            btnSaveSettings.Size = new Size(140, 30);
            btnSaveSettings.TabIndex = 8;
            btnSaveSettings.Text = "Save Settings";
            // 
            // grpLog
            // 
            grpLog.Controls.Add(txtLog);
            grpLog.Dock = DockStyle.Bottom;
            grpLog.Location = new Point(0, 440);
            grpLog.Name = "grpLog";
            grpLog.Size = new Size(1000, 260);
            grpLog.TabIndex = 1;
            grpLog.TabStop = false;
            grpLog.Text = "Log";
            // 
            // txtLog
            // 
            txtLog.Dock = DockStyle.Fill;
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.Location = new Point(3, 19);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Size = new Size(994, 238);
            txtLog.TabIndex = 0;
            txtLog.WordWrap = false;
            // 
            // MainForm
            // 
            ClientSize = new Size(1000, 700);
			Controls.Add(tabs);
			Controls.Add(topPanel);
			Controls.Add(grpLog);
            MinimumSize = new Size(900, 600);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "SD-Builder (GUI)";
            topPanel.ResumeLayout(false);
            topPanel.PerformLayout();
            driveRow.ResumeLayout(false);
            driveRow.PerformLayout();
            tabs.ResumeLayout(false);
            tabXstation.ResumeLayout(false);
            xRoot.ResumeLayout(false);
            xRoot.PerformLayout();
            xRow.ResumeLayout(false);
            xRow.PerformLayout();
            xListBtns.ResumeLayout(false);
            tabSaroo.ResumeLayout(false);
            sRoot.ResumeLayout(false);
            sRoot.PerformLayout();
            sRow.ResumeLayout(false);
            sRow.PerformLayout();
            sListBtns.ResumeLayout(false);
            tabGamecube.ResumeLayout(false);
            gRoot.ResumeLayout(false);
            gRoot.PerformLayout();
            gRow.ResumeLayout(false);
            gRow.PerformLayout();
            gListBtns.ResumeLayout(false);
            tabSC64.ResumeLayout(false);
            scRoot.ResumeLayout(false);
            scRoot.PerformLayout();
            scRow.ResumeLayout(false);
            scRow.PerformLayout();
            scListBtns.ResumeLayout(false);
            scFill.ResumeLayout(false);
            tabListmaker.ResumeLayout(false);
            tabListmaker.PerformLayout();
            lmTop2.ResumeLayout(false);
            lmTop2.PerformLayout();
            lmTop1.ResumeLayout(false);
            lmTop1.PerformLayout();
            lmBottom.ResumeLayout(false);
            lmBottom.PerformLayout();
            statusStripLM.ResumeLayout(false);
            statusStripLM.PerformLayout();
            tabSettings.ResumeLayout(false);
            setPanel.ResumeLayout(false);
            setPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numCopy).EndInit();
            ((System.ComponentModel.ISupportInitialize)numOW).EndInit();
            grpLog.ResumeLayout(false);
            grpLog.PerformLayout();
            ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
        private FlowLayoutPanel driveRow;
        private Label lblDrive;
        private TableLayoutPanel xRoot;
        private FlowLayoutPanel xRow;
        private Label lblList1;
        private FlowLayoutPanel xListBtns;
        private Panel xFill;
        private TableLayoutPanel sRoot;
        private FlowLayoutPanel sRow;
        private Label lblList2;
        private FlowLayoutPanel sListBtns;
        private Panel sFill;
        private TableLayoutPanel gRoot;
        private FlowLayoutPanel gRow;
        private Label lblList3;
        private FlowLayoutPanel gListBtns;
        private Panel gFill;
        private TableLayoutPanel scRoot;
        private FlowLayoutPanel scRow;
        private Label lblList4;
        private FlowLayoutPanel scListBtns;
        private FlowLayoutPanel scExtras;
        private Panel scFill;
        private FlowLayoutPanel lmTop2;
        private Label lblMode;
        private FlowLayoutPanel lmTop1;
        private Label lblFilter;
        private FlowLayoutPanel lmBottom;
        private Label lmSpacer1;
        private Label lmSpacer2;
        private FlowLayoutPanel setPanel;
        private Label lblDriveList;
        private Label lblCopyTO;
        private Label lblOWTO;
        private Label lblAuto;
    }
}