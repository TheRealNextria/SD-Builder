
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
// Saroo listmaker (mirrors Xstation)
        private System.Windows.Forms.ListView lvSrList;
        private System.Windows.Forms.ColumnHeader colPathSR;
        private System.Windows.Forms.ColumnHeader colStatusSR;
        private System.Windows.Forms.FlowLayoutPanel sTop;
        private System.Windows.Forms.Label lblSrMode;
        private System.Windows.Forms.RadioButton rbSrFiles;
        private System.Windows.Forms.RadioButton rbSrDirs;
        private System.Windows.Forms.Button btnSrChoose;
        private System.Windows.Forms.Button btnSrCheckAll;
        private System.Windows.Forms.Button btnSrUncheckAll;
        private System.Windows.Forms.Button btnSrStart;
        private System.Windows.Forms.StatusStrip statusStripSR;
        private System.Windows.Forms.ToolStripStatusLabel lblStatusSR;
        private System.Windows.Forms.ToolStripStatusLabel lblSepSR1;
        private System.Windows.Forms.ToolStripStatusLabel lblSpacerSR;
        private System.Windows.Forms.ToolStripStatusLabel lblCountSR;
        private System.Windows.Forms.ToolStripStatusLabel lblSepSR2;
        private System.Windows.Forms.ToolStripStatusLabel lblSizeSR;
// Saroo
        private Button btnSarooFw;
        private ComboBox cmbSarooList;
        private Button btnSarooRefreshLists;
        private Button btnSarooOpenLists;
        private Button btnSarooStart;

        
        // Gamecube listmaker (mirrors Xstation)
        private System.Windows.Forms.ListView lvGcList;
        private System.Windows.Forms.ColumnHeader colPathGC;
        private System.Windows.Forms.ColumnHeader colStatusGC;
        private System.Windows.Forms.FlowLayoutPanel gcTop;
        private System.Windows.Forms.Label lblGcMode;
        private System.Windows.Forms.RadioButton rbGcFiles;
        private System.Windows.Forms.RadioButton rbGcDirs;
        private System.Windows.Forms.Button btnGcChoose;
        private System.Windows.Forms.Button btnGcCheckAll;
        private System.Windows.Forms.Button btnGcUncheckAll;
        private System.Windows.Forms.Button btnGcStart;
        private System.Windows.Forms.StatusStrip statusStripGC;
        private System.Windows.Forms.ToolStripStatusLabel lblStatusGC;
        private System.Windows.Forms.ToolStripStatusLabel lblSepGC1;
        private System.Windows.Forms.ToolStripStatusLabel lblSpacerGC;
        private System.Windows.Forms.ToolStripStatusLabel lblCountGC;
        private System.Windows.Forms.ToolStripStatusLabel lblSepGC2;
        private System.Windows.Forms.ToolStripStatusLabel lblSizeGC;
    // Gamecube
        private Button btnGamecubeCheats;
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
                private System.Windows.Forms.RadioButton rbMixed;
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
        private CheckBox chkEjectPrompt;
        private NumericUpDown numCopy;
        private NumericUpDown numOW;
        private ComboBox cmbAuto;
        private Button btnSaveSettings;
        private System.Windows.Forms.ColumnHeader colPathLM;
        private System.Windows.Forms.ColumnHeader colStatusLM;
        private System.Windows.Forms.ColumnHeader colPathXS;
        private System.Windows.Forms.ColumnHeader colStatusXS;

        private void InitializeComponent()
        {
            rbMixed = new RadioButton();
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
            lvXsList = new ListView();
            colPathXS = new ColumnHeader();
            colStatusXS = new ColumnHeader();
            xsTop = new FlowLayoutPanel();
            lblXsMode = new Label();
            rbXsFiles = new RadioButton();
            rbXsDirs = new RadioButton();
            btnXsChoose = new Button();
            btnXsCheckAll = new Button();
            btnXsUncheckAll = new Button();
            btnXsStart = new Button();
            statusStripXS = new StatusStrip();
            lblStatusXS = new ToolStripStatusLabel();
            lblSepXS1 = new ToolStripStatusLabel();
            lblSpacerXS = new ToolStripStatusLabel();
            lblCountXS = new ToolStripStatusLabel();
            lblSepXS2 = new ToolStripStatusLabel();
            lblSizeXS = new ToolStripStatusLabel();
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
            lvSrList = new ListView();
            colPathSR = new ColumnHeader();
            colStatusSR = new ColumnHeader();
            sTop = new FlowLayoutPanel();
            lblSrMode = new Label();
            rbSrFiles = new RadioButton();
            rbSrDirs = new RadioButton();
            btnSrChoose = new Button();
            btnSrCheckAll = new Button();
            btnSrUncheckAll = new Button();
            btnSrStart = new Button();
            statusStripSR = new StatusStrip();
            lblStatusSR = new ToolStripStatusLabel();
            lblSepSR1 = new ToolStripStatusLabel();
            lblSpacerSR = new ToolStripStatusLabel();
            lblCountSR = new ToolStripStatusLabel();
            lblSepSR2 = new ToolStripStatusLabel();
            lblSizeSR = new ToolStripStatusLabel();
            tabGamecube = new TabPage();
            gRoot = new TableLayoutPanel();
            gRow = new FlowLayoutPanel();
            btnGamecubeFw = new Button();
            btnGamecubeCheats = new Button();
            lblList3 = new Label();
            cmbGamecubeList = new ComboBox();
            gListBtns = new FlowLayoutPanel();
            btnGamecubeRefreshLists = new Button();
            btnGamecubeOpenLists = new Button();
            btnGamecubeStart = new Button();
            gFill = new Panel();
            lvGcList = new ListView();
            colPathGC = new ColumnHeader();
            colStatusGC = new ColumnHeader();
            gcTop = new FlowLayoutPanel();
            lblGcMode = new Label();
            rbGcFiles = new RadioButton();
            rbGcDirs = new RadioButton();
            btnGcChoose = new Button();
            btnGcCheckAll = new Button();
            btnGcUncheckAll = new Button();
            btnGcStart = new Button();
            statusStripGC = new StatusStrip();
            lblStatusGC = new ToolStripStatusLabel();
            lblSepGC1 = new ToolStripStatusLabel();
            lblSpacerGC = new ToolStripStatusLabel();
            lblCountGC = new ToolStripStatusLabel();
            lblSepGC2 = new ToolStripStatusLabel();
            lblSizeGC = new ToolStripStatusLabel();
            tabSC64 = new TabPage();
            scRoot = new TableLayoutPanel();
            scRow = new FlowLayoutPanel();
            btnSC64Fw = new Button();
            flowLayoutPanel1 = new FlowLayoutPanel();
            btnSC64Install64DD = new Button();
            btnSC64InstallEmulators = new Button();
            lblList4 = new Label();
            cmbSC64List = new ComboBox();
            scListBtns = new FlowLayoutPanel();
            btnSC64RefreshLists = new Button();
            btnSC64OpenLists = new Button();
            btnSC64Start = new Button();
            scExtras = new FlowLayoutPanel();
            scFill = new Panel();
            statusStripSC = new StatusStrip();
            lblStatusSC = new ToolStripStatusLabel();
            lblSepSC1 = new ToolStripStatusLabel();
            lblSpacerSC = new ToolStripStatusLabel();
            lblCountSC = new ToolStripStatusLabel();
            lblSepSC2 = new ToolStripStatusLabel();
            lblSizeSC = new ToolStripStatusLabel();
            lvScList = new ListView();
            colPathSC = new ColumnHeader();
            colStatusSC = new ColumnHeader();
            scTop = new FlowLayoutPanel();
            lblScMode = new Label();
            rbScFiles = new RadioButton();
            rbScDirs = new RadioButton();
            btnScChoose = new Button();
            btnScCheckAll = new Button();
            btnScUncheckAll = new Button();
            btnScStart = new Button();
            tabListmaker = new TabPage();
            lvList = new ListView();
            colPathLM = new ColumnHeader();
            colStatusLM = new ColumnHeader();
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
            btnClearLM = new Button();
            btnOpenGameListsLM = new Button();
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
            chkEjectPrompt = new CheckBox();
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
            xFill.SuspendLayout();
            xsTop.SuspendLayout();
            statusStripXS.SuspendLayout();
            tabSaroo.SuspendLayout();
            sRoot.SuspendLayout();
            sRow.SuspendLayout();
            sListBtns.SuspendLayout();
            sFill.SuspendLayout();
            sTop.SuspendLayout();
            statusStripSR.SuspendLayout();
            tabGamecube.SuspendLayout();
            gRoot.SuspendLayout();
            gRow.SuspendLayout();
            gListBtns.SuspendLayout();
            gFill.SuspendLayout();
            gcTop.SuspendLayout();
            statusStripGC.SuspendLayout();
            tabSC64.SuspendLayout();
            scRoot.SuspendLayout();
            scRow.SuspendLayout();
            flowLayoutPanel1.SuspendLayout();
            scListBtns.SuspendLayout();
            scFill.SuspendLayout();
            statusStripSC.SuspendLayout();
            scTop.SuspendLayout();
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
            // rbMixed
            // 
            rbMixed.Location = new Point(256, 12);
            rbMixed.Margin = new Padding(6, 4, 3, 3);
            rbMixed.Name = "rbMixed";
            rbMixed.Size = new Size(60, 23);
            rbMixed.TabIndex = 0;
            rbMixed.TabStop = true;
            rbMixed.Text = "Mixed";
            rbMixed.UseVisualStyleBackColor = true;
            // 
            // topPanel
            // 
            topPanel.Controls.Add(driveRow);
            topPanel.Controls.Add(lblVersion);
            topPanel.Dock = DockStyle.Top;
            topPanel.Location = new Point(0, 0);
            topPanel.Name = "topPanel";
            topPanel.Padding = new Padding(10, 8, 10, 8);
            topPanel.Size = new Size(1169, 60);
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
            driveRow.Location = new Point(10, 8);
            driveRow.Margin = new Padding(0);
            driveRow.MinimumSize = new Size(1000, 36);
            driveRow.Name = "driveRow";
            driveRow.Size = new Size(1149, 36);
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
            cmbDrives.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cmbDrives.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDrives.Location = new Point(110, 3);
            cmbDrives.Name = "cmbDrives";
            cmbDrives.Size = new Size(260, 23);
            cmbDrives.TabIndex = 1;
            // 
            // btnRefresh
            // 
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.Location = new Point(381, 0);
            btnRefresh.Margin = new Padding(8, 0, 0, 0);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(80, 28);
            btnRefresh.TabIndex = 2;
            btnRefresh.Text = "Refresh";
            // 
            // btnStop
            // 
            btnStop.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnStop.Enabled = false;
            btnStop.Location = new Point(473, 0);
            btnStop.Margin = new Padding(12, 0, 0, 0);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(80, 28);
            btnStop.TabIndex = 3;
            btnStop.Text = "Stop";
            // 
            // btnOpen
            // 
            btnOpen.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOpen.Location = new Point(561, 0);
            btnOpen.Margin = new Padding(8, 0, 0, 0);
            btnOpen.Name = "btnOpen";
            btnOpen.Size = new Size(130, 28);
            btnOpen.TabIndex = 4;
            btnOpen.Text = "Open in Explorer";
            // 
            // btnEject
            // 
            btnEject.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnEject.Location = new Point(699, 0);
            btnEject.Margin = new Padding(8, 0, 0, 0);
            btnEject.Name = "btnEject";
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
            tabs.Size = new Size(1169, 380);
            tabs.TabIndex = 0;
            // 
            // tabXstation
            // 
            tabXstation.Controls.Add(xRoot);
            tabXstation.Location = new Point(4, 24);
            tabXstation.Name = "tabXstation";
            tabXstation.Size = new Size(1161, 352);
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
            xRoot.Size = new Size(1161, 352);
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
            xRow.Size = new Size(1155, 96);
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
            xListBtns.Size = new Size(186, 80);
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
            btnXstationOpenLists.Location = new Point(3, 43);
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
            xFill.AutoScroll = true;
            xFill.Controls.Add(lvXsList);
            xFill.Controls.Add(xsTop);
            xFill.Controls.Add(statusStripXS);
            xFill.Dock = DockStyle.Fill;
            xFill.Location = new Point(3, 105);
            xFill.Name = "xFill";
            xFill.Size = new Size(1155, 244);
            xFill.TabIndex = 1;
            // 
            // lvXsList
            // 
            lvXsList.CheckBoxes = true;
            lvXsList.Columns.AddRange(new ColumnHeader[] { colPathXS, colStatusXS });
            lvXsList.Dock = DockStyle.Fill;
            lvXsList.FullRowSelect = true;
            lvXsList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lvXsList.Location = new Point(0, 41);
            lvXsList.Name = "lvXsList";
            lvXsList.Size = new Size(1155, 181);
            lvXsList.TabIndex = 0;
            lvXsList.UseCompatibleStateImageBehavior = false;
            lvXsList.View = View.Details;
            // 
            // colPathXS
            // 
            colPathXS.Text = "Path";
            colPathXS.Width = 500;
            // 
            // colStatusXS
            // 
            colStatusXS.Text = "Status";
            colStatusXS.Width = 160;
            // 
            // xsTop
            // 
            xsTop.AutoSize = true;
            xsTop.Controls.Add(lblXsMode);
            xsTop.Controls.Add(rbXsFiles);
            xsTop.Controls.Add(rbXsDirs);
            xsTop.Controls.Add(btnXsChoose);
            xsTop.Controls.Add(btnXsCheckAll);
            xsTop.Controls.Add(btnXsUncheckAll);
            xsTop.Controls.Add(btnXsStart);
            xsTop.Dock = DockStyle.Top;
            xsTop.Location = new Point(0, 0);
            xsTop.Margin = new Padding(10);
            xsTop.Name = "xsTop";
            xsTop.Size = new Size(1155, 41);
            xsTop.TabIndex = 1;
            xsTop.WrapContents = false;
            // 
            // lblXsMode
            // 
            lblXsMode.AutoSize = true;
            lblXsMode.Location = new Point(6, 13);
            lblXsMode.Margin = new Padding(6, 13, 8, 0);
            lblXsMode.Name = "lblXsMode";
            lblXsMode.Size = new Size(41, 15);
            lblXsMode.TabIndex = 0;
            lblXsMode.Text = "Mode:";
            // 
            // rbXsFiles
            // 
            rbXsFiles.AutoSize = true;
            rbXsFiles.Checked = true;
            rbXsFiles.Location = new Point(55, 11);
            rbXsFiles.Margin = new Padding(0, 11, 8, 0);
            rbXsFiles.Name = "rbXsFiles";
            rbXsFiles.Size = new Size(48, 19);
            rbXsFiles.TabIndex = 1;
            rbXsFiles.TabStop = true;
            rbXsFiles.Text = "Files";
            // 
            // rbXsDirs
            // 
            rbXsDirs.AutoSize = true;
            rbXsDirs.Location = new Point(111, 11);
            rbXsDirs.Margin = new Padding(0, 11, 12, 0);
            rbXsDirs.Name = "rbXsDirs";
            rbXsDirs.Size = new Size(81, 19);
            rbXsDirs.TabIndex = 2;
            rbXsDirs.Text = "Directories";
            // 
            // btnXsChoose
            // 
            btnXsChoose.AutoSize = true;
            btnXsChoose.Location = new Point(204, 8);
            btnXsChoose.Margin = new Padding(0, 8, 8, 8);
            btnXsChoose.Name = "btnXsChoose";
            btnXsChoose.Size = new Size(75, 25);
            btnXsChoose.TabIndex = 3;
            btnXsChoose.Text = "Choose…";
            // 
            // btnXsCheckAll
            // 
            btnXsCheckAll.AutoSize = true;
            btnXsCheckAll.Location = new Point(287, 8);
            btnXsCheckAll.Margin = new Padding(0, 8, 8, 8);
            btnXsCheckAll.Name = "btnXsCheckAll";
            btnXsCheckAll.Size = new Size(75, 25);
            btnXsCheckAll.TabIndex = 4;
            btnXsCheckAll.Text = "Check All";
            // 
            // btnXsUncheckAll
            // 
            btnXsUncheckAll.AutoSize = true;
            btnXsUncheckAll.Location = new Point(370, 8);
            btnXsUncheckAll.Margin = new Padding(0, 8, 8, 8);
            btnXsUncheckAll.Name = "btnXsUncheckAll";
            btnXsUncheckAll.Size = new Size(80, 25);
            btnXsUncheckAll.TabIndex = 5;
            btnXsUncheckAll.Text = "Uncheck All";
            // 
            // btnXsStart
            // 
            btnXsStart.AutoSize = true;
            btnXsStart.Location = new Point(458, 8);
            btnXsStart.Margin = new Padding(0, 8, 8, 8);
            btnXsStart.Name = "btnXsStart";
            btnXsStart.Size = new Size(98, 25);
            btnXsStart.TabIndex = 6;
            btnXsStart.Text = "Start (Checked)";
            // 
            // statusStripXS
            // 
            statusStripXS.Items.AddRange(new ToolStripItem[] { lblStatusXS, lblSepXS1, lblSpacerXS, lblCountXS, lblSepXS2, lblSizeXS });
            statusStripXS.Location = new Point(0, 222);
            statusStripXS.Name = "statusStripXS";
            statusStripXS.Size = new Size(1155, 22);
            statusStripXS.SizingGrip = false;
            statusStripXS.TabIndex = 2;
            // 
            // lblStatusXS
            // 
            lblStatusXS.Name = "lblStatusXS";
            lblStatusXS.Size = new Size(42, 17);
            lblStatusXS.Text = "Ready.";
            // 
            // lblSepXS1
            // 
            lblSepXS1.Name = "lblSepXS1";
            lblSepXS1.Size = new Size(16, 17);
            lblSepXS1.Text = " | ";
            // 
            // lblSpacerXS
            // 
            lblSpacerXS.Name = "lblSpacerXS";
            lblSpacerXS.Size = new Size(952, 17);
            lblSpacerXS.Spring = true;
            // 
            // lblCountXS
            // 
            lblCountXS.Name = "lblCountXS";
            lblCountXS.Size = new Size(65, 17);
            lblCountXS.Text = "Checked: 0";
            // 
            // lblSepXS2
            // 
            lblSepXS2.Name = "lblSepXS2";
            lblSepXS2.Size = new Size(16, 17);
            lblSepXS2.Text = " | ";
            // 
            // lblSizeXS
            // 
            lblSizeXS.Name = "lblSizeXS";
            lblSizeXS.Size = new Size(49, 17);
            lblSizeXS.Text = "Size: 0 B";
            // 
            // tabSaroo
            // 
            tabSaroo.Controls.Add(sRoot);
            tabSaroo.Location = new Point(4, 24);
            tabSaroo.Name = "tabSaroo";
            tabSaroo.Size = new Size(1161, 352);
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
            sRoot.Size = new Size(1161, 352);
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
            sRow.Size = new Size(1155, 96);
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
            sListBtns.Size = new Size(186, 80);
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
            btnSarooOpenLists.Location = new Point(3, 43);
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
            sFill.AutoScroll = true;
            sFill.Controls.Add(lvSrList);
            sFill.Controls.Add(sTop);
            sFill.Controls.Add(statusStripSR);
            sFill.Dock = DockStyle.Fill;
            sFill.Location = new Point(3, 105);
            sFill.Name = "sFill";
            sFill.Size = new Size(1155, 244);
            sFill.TabIndex = 1;
            // 
            // lvSrList
            // 
            lvSrList.CheckBoxes = true;
            lvSrList.Columns.AddRange(new ColumnHeader[] { colPathSR, colStatusSR });
            lvSrList.Dock = DockStyle.Fill;
            lvSrList.FullRowSelect = true;
            lvSrList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lvSrList.Location = new Point(0, 41);
            lvSrList.Name = "lvSrList";
            lvSrList.Size = new Size(1155, 181);
            lvSrList.TabIndex = 0;
            lvSrList.UseCompatibleStateImageBehavior = false;
            lvSrList.View = View.Details;
            // 
            // colPathSR
            // 
            colPathSR.Text = "Path";
            colPathSR.Width = 500;
            // 
            // colStatusSR
            // 
            colStatusSR.Text = "Status";
            colStatusSR.Width = 160;
            // 
            // sTop
            // 
            sTop.AutoSize = true;
            sTop.Controls.Add(lblSrMode);
            sTop.Controls.Add(rbSrFiles);
            sTop.Controls.Add(rbSrDirs);
            sTop.Controls.Add(btnSrChoose);
            sTop.Controls.Add(btnSrCheckAll);
            sTop.Controls.Add(btnSrUncheckAll);
            sTop.Controls.Add(btnSrStart);
            sTop.Dock = DockStyle.Top;
            sTop.Location = new Point(0, 0);
            sTop.Margin = new Padding(10);
            sTop.Name = "sTop";
            sTop.Size = new Size(1155, 41);
            sTop.TabIndex = 1;
            sTop.WrapContents = false;
            // 
            // lblSrMode
            // 
            lblSrMode.AutoSize = true;
            lblSrMode.Location = new Point(6, 13);
            lblSrMode.Margin = new Padding(6, 13, 8, 0);
            lblSrMode.Name = "lblSrMode";
            lblSrMode.Size = new Size(41, 15);
            lblSrMode.TabIndex = 0;
            lblSrMode.Text = "Mode:";
            // 
            // rbSrFiles
            // 
            rbSrFiles.AutoSize = true;
            rbSrFiles.Checked = true;
            rbSrFiles.Location = new Point(55, 11);
            rbSrFiles.Margin = new Padding(0, 11, 8, 0);
            rbSrFiles.Name = "rbSrFiles";
            rbSrFiles.Size = new Size(48, 19);
            rbSrFiles.TabIndex = 1;
            rbSrFiles.TabStop = true;
            rbSrFiles.Text = "Files";
            // 
            // rbSrDirs
            // 
            rbSrDirs.AutoSize = true;
            rbSrDirs.Location = new Point(111, 11);
            rbSrDirs.Margin = new Padding(0, 11, 12, 0);
            rbSrDirs.Name = "rbSrDirs";
            rbSrDirs.Size = new Size(81, 19);
            rbSrDirs.TabIndex = 2;
            rbSrDirs.Text = "Directories";
            // 
            // btnSrChoose
            // 
            btnSrChoose.AutoSize = true;
            btnSrChoose.Location = new Point(204, 8);
            btnSrChoose.Margin = new Padding(0, 8, 8, 8);
            btnSrChoose.Name = "btnSrChoose";
            btnSrChoose.Size = new Size(75, 25);
            btnSrChoose.TabIndex = 3;
            btnSrChoose.Text = "Choose…";
            // 
            // btnSrCheckAll
            // 
            btnSrCheckAll.AutoSize = true;
            btnSrCheckAll.Location = new Point(287, 8);
            btnSrCheckAll.Margin = new Padding(0, 8, 8, 8);
            btnSrCheckAll.Name = "btnSrCheckAll";
            btnSrCheckAll.Size = new Size(75, 25);
            btnSrCheckAll.TabIndex = 4;
            btnSrCheckAll.Text = "Check All";
            // 
            // btnSrUncheckAll
            // 
            btnSrUncheckAll.AutoSize = true;
            btnSrUncheckAll.Location = new Point(370, 8);
            btnSrUncheckAll.Margin = new Padding(0, 8, 8, 8);
            btnSrUncheckAll.Name = "btnSrUncheckAll";
            btnSrUncheckAll.Size = new Size(80, 25);
            btnSrUncheckAll.TabIndex = 5;
            btnSrUncheckAll.Text = "Uncheck All";
            // 
            // btnSrStart
            // 
            btnSrStart.AutoSize = true;
            btnSrStart.Location = new Point(458, 8);
            btnSrStart.Margin = new Padding(0, 8, 8, 8);
            btnSrStart.Name = "btnSrStart";
            btnSrStart.Size = new Size(98, 25);
            btnSrStart.TabIndex = 6;
            btnSrStart.Text = "Start (Checked)";
            // 
            // statusStripSR
            // 
            statusStripSR.Items.AddRange(new ToolStripItem[] { lblStatusSR, lblSepSR1, lblSpacerSR, lblCountSR, lblSepSR2, lblSizeSR });
            statusStripSR.Location = new Point(0, 222);
            statusStripSR.Name = "statusStripSR";
            statusStripSR.Size = new Size(1155, 22);
            statusStripSR.SizingGrip = false;
            statusStripSR.TabIndex = 2;
            // 
            // lblStatusSR
            // 
            lblStatusSR.Name = "lblStatusSR";
            lblStatusSR.Size = new Size(42, 17);
            lblStatusSR.Text = "Ready.";
            // 
            // lblSepSR1
            // 
            lblSepSR1.Name = "lblSepSR1";
            lblSepSR1.Size = new Size(16, 17);
            lblSepSR1.Text = " | ";
            // 
            // lblSpacerSR
            // 
            lblSpacerSR.Name = "lblSpacerSR";
            lblSpacerSR.Size = new Size(952, 17);
            lblSpacerSR.Spring = true;
            // 
            // lblCountSR
            // 
            lblCountSR.Name = "lblCountSR";
            lblCountSR.Size = new Size(65, 17);
            lblCountSR.Text = "Checked: 0";
            // 
            // lblSepSR2
            // 
            lblSepSR2.Name = "lblSepSR2";
            lblSepSR2.Size = new Size(16, 17);
            lblSepSR2.Text = " | ";
            // 
            // lblSizeSR
            // 
            lblSizeSR.Name = "lblSizeSR";
            lblSizeSR.Size = new Size(49, 17);
            lblSizeSR.Text = "Size: 0 B";
            // 
            // tabGamecube
            // 
            tabGamecube.Controls.Add(gRoot);
            tabGamecube.Location = new Point(4, 24);
            tabGamecube.Name = "tabGamecube";
            tabGamecube.Size = new Size(1161, 352);
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
            gRoot.Size = new Size(1161, 352);
            gRoot.TabIndex = 0;
            // 
            // gRow
            // 
            gRow.AutoSize = true;
            gRow.Controls.Add(btnGamecubeFw);
            gRow.Controls.Add(btnGamecubeCheats);
            gRow.Controls.Add(lblList3);
            gRow.Controls.Add(cmbGamecubeList);
            gRow.Controls.Add(gListBtns);
            gRow.Controls.Add(btnGamecubeStart);
            gRow.Dock = DockStyle.Fill;
            gRow.Location = new Point(3, 3);
            gRow.Name = "gRow";
            gRow.Padding = new Padding(8);
            gRow.Size = new Size(1155, 96);
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
            // btnGamecubeCheats
            // 
            btnGamecubeCheats.Location = new Point(257, 11);
            btnGamecubeCheats.Name = "btnGamecubeCheats";
            btnGamecubeCheats.Size = new Size(160, 34);
            btnGamecubeCheats.TabIndex = 1;
            btnGamecubeCheats.Text = "Download Cheat Files";
            // 
            // lblList3
            // 
            lblList3.AutoSize = true;
            lblList3.Location = new Point(432, 16);
            lblList3.Margin = new Padding(12, 8, 6, 0);
            lblList3.Name = "lblList3";
            lblList3.Size = new Size(28, 15);
            lblList3.TabIndex = 1;
            lblList3.Text = "List:";
            // 
            // cmbGamecubeList
            // 
            cmbGamecubeList.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbGamecubeList.Location = new Point(469, 11);
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
            gListBtns.Location = new Point(840, 8);
            gListBtns.Margin = new Padding(8, 0, 0, 0);
            gListBtns.Name = "gListBtns";
            gListBtns.Size = new Size(186, 80);
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
            btnGamecubeOpenLists.Location = new Point(3, 43);
            btnGamecubeOpenLists.Name = "btnGamecubeOpenLists";
            btnGamecubeOpenLists.Size = new Size(180, 34);
            btnGamecubeOpenLists.TabIndex = 2;
            btnGamecubeOpenLists.Text = "Open GameLists Folder";
            // 
            // btnGamecubeStart
            // 
            btnGamecubeStart.Enabled = false;
            btnGamecubeStart.Location = new Point(1029, 11);
            btnGamecubeStart.Name = "btnGamecubeStart";
            btnGamecubeStart.Size = new Size(100, 34);
            btnGamecubeStart.TabIndex = 4;
            btnGamecubeStart.Text = "Start";
            // 
            // gFill
            // 
            gFill.AutoScroll = true;
            gFill.Controls.Add(lvGcList);
            gFill.Controls.Add(gcTop);
            gFill.Controls.Add(statusStripGC);
            gFill.Dock = DockStyle.Fill;
            gFill.Location = new Point(3, 105);
            gFill.Name = "gFill";
            gFill.Size = new Size(1155, 244);
            gFill.TabIndex = 1;
            // 
            // lvGcList
            // 
            lvGcList.CheckBoxes = true;
            lvGcList.Columns.AddRange(new ColumnHeader[] { colPathGC, colStatusGC });
            lvGcList.Dock = DockStyle.Fill;
            lvGcList.FullRowSelect = true;
            lvGcList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lvGcList.Location = new Point(0, 41);
            lvGcList.Name = "lvGcList";
            lvGcList.Size = new Size(1155, 181);
            lvGcList.TabIndex = 0;
            lvGcList.UseCompatibleStateImageBehavior = false;
            lvGcList.View = View.Details;
            // 
            // colPathGC
            // 
            colPathGC.Text = "Path";
            colPathGC.Width = 1200;
            // 
            // colStatusGC
            // 
            colStatusGC.Text = "Status";
            colStatusGC.Width = 160;
            // 
            // gcTop
            // 
            gcTop.AutoSize = true;
            gcTop.Controls.Add(lblGcMode);
            gcTop.Controls.Add(rbGcFiles);
            gcTop.Controls.Add(rbGcDirs);
            gcTop.Controls.Add(btnGcChoose);
            gcTop.Controls.Add(btnGcCheckAll);
            gcTop.Controls.Add(btnGcUncheckAll);
            gcTop.Controls.Add(btnGcStart);
            gcTop.Dock = DockStyle.Top;
            gcTop.Location = new Point(0, 0);
            gcTop.Margin = new Padding(10);
            gcTop.Name = "gcTop";
            gcTop.Size = new Size(1155, 41);
            gcTop.TabIndex = 1;
            gcTop.WrapContents = false;
            // 
            // lblGcMode
            // 
            lblGcMode.AutoSize = true;
            lblGcMode.Location = new Point(6, 13);
            lblGcMode.Margin = new Padding(6, 13, 8, 0);
            lblGcMode.Name = "lblGcMode";
            lblGcMode.Size = new Size(41, 15);
            lblGcMode.TabIndex = 0;
            lblGcMode.Text = "Mode:";
            // 
            // rbGcFiles
            // 
            rbGcFiles.AutoSize = true;
            rbGcFiles.Checked = true;
            rbGcFiles.Location = new Point(55, 11);
            rbGcFiles.Margin = new Padding(0, 11, 8, 0);
            rbGcFiles.Name = "rbGcFiles";
            rbGcFiles.Size = new Size(48, 19);
            rbGcFiles.TabIndex = 1;
            rbGcFiles.TabStop = true;
            rbGcFiles.Text = "Files";
            rbGcFiles.UseVisualStyleBackColor = true;
            // 
            // rbGcDirs
            // 
            rbGcDirs.AutoSize = true;
            rbGcDirs.Location = new Point(111, 11);
            rbGcDirs.Margin = new Padding(0, 11, 12, 0);
            rbGcDirs.Name = "rbGcDirs";
            rbGcDirs.Size = new Size(81, 19);
            rbGcDirs.TabIndex = 2;
            rbGcDirs.Text = "Directories";
            rbGcDirs.UseVisualStyleBackColor = true;
            // 
            // btnGcChoose
            // 
            btnGcChoose.AutoSize = true;
            btnGcChoose.Location = new Point(204, 8);
            btnGcChoose.Margin = new Padding(0, 8, 8, 8);
            btnGcChoose.Name = "btnGcChoose";
            btnGcChoose.Size = new Size(75, 25);
            btnGcChoose.TabIndex = 3;
            btnGcChoose.Text = "Choose…";
            btnGcChoose.UseVisualStyleBackColor = true;
            // 
            // btnGcCheckAll
            // 
            btnGcCheckAll.Location = new Point(287, 8);
            btnGcCheckAll.Margin = new Padding(0, 8, 8, 8);
            btnGcCheckAll.Name = "btnGcCheckAll";
            btnGcCheckAll.Size = new Size(75, 25);
            btnGcCheckAll.TabIndex = 4;
            btnGcCheckAll.Text = "Check All";
            btnGcCheckAll.UseVisualStyleBackColor = true;
            btnGcCheckAll.Click += btnGcCheckAll_Click;
            // 
            // btnGcUncheckAll
            // 
            btnGcUncheckAll.AutoSize = true;
            btnGcUncheckAll.Location = new Point(370, 8);
            btnGcUncheckAll.Margin = new Padding(0, 8, 8, 8);
            btnGcUncheckAll.Name = "btnGcUncheckAll";
            btnGcUncheckAll.Size = new Size(80, 25);
            btnGcUncheckAll.TabIndex = 5;
            btnGcUncheckAll.Text = "Uncheck All";
            btnGcUncheckAll.UseVisualStyleBackColor = true;
            // 
            // btnGcStart
            // 
            btnGcStart.AutoSize = true;
            btnGcStart.Location = new Point(458, 8);
            btnGcStart.Margin = new Padding(0, 8, 8, 8);
            btnGcStart.Name = "btnGcStart";
            btnGcStart.Size = new Size(98, 25);
            btnGcStart.TabIndex = 6;
            btnGcStart.Text = "Start (Checked)";
            btnGcStart.UseVisualStyleBackColor = true;
            // 
            // statusStripGC
            // 
            statusStripGC.Items.AddRange(new ToolStripItem[] { lblStatusGC, lblSepGC1, lblSpacerGC, lblCountGC, lblSepGC2, lblSizeGC });
            statusStripGC.Location = new Point(0, 222);
            statusStripGC.Name = "statusStripGC";
            statusStripGC.Size = new Size(1155, 22);
            statusStripGC.TabIndex = 2;
            statusStripGC.Text = "statusStripGC";
            // 
            // lblStatusGC
            // 
            lblStatusGC.Name = "lblStatusGC";
            lblStatusGC.Size = new Size(42, 17);
            lblStatusGC.Text = "Ready.";
            // 
            // lblSepGC1
            // 
            lblSepGC1.Name = "lblSepGC1";
            lblSepGC1.Size = new Size(10, 17);
            lblSepGC1.Text = "|";
            // 
            // lblSpacerGC
            // 
            lblSpacerGC.Name = "lblSpacerGC";
            lblSpacerGC.Size = new Size(964, 17);
            lblSpacerGC.Spring = true;
            lblSpacerGC.Text = " ";
            // 
            // lblCountGC
            // 
            lblCountGC.Name = "lblCountGC";
            lblCountGC.Size = new Size(65, 17);
            lblCountGC.Text = "Checked: 0";
            // 
            // lblSepGC2
            // 
            lblSepGC2.Name = "lblSepGC2";
            lblSepGC2.Size = new Size(10, 17);
            lblSepGC2.Text = "|";
            // 
            // lblSizeGC
            // 
            lblSizeGC.Name = "lblSizeGC";
            lblSizeGC.Size = new Size(49, 17);
            lblSizeGC.Text = "Size: 0 B";
            // 
            // tabSC64
            // 
            tabSC64.Controls.Add(scRoot);
            tabSC64.Location = new Point(4, 24);
            tabSC64.Name = "tabSC64";
            tabSC64.Size = new Size(1161, 352);
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
            scRoot.Size = new Size(1161, 352);
            scRoot.TabIndex = 0;
            // 
            // scRow
            // 
            scRow.AutoSize = true;
            scRow.Controls.Add(btnSC64Fw);
            scRow.Controls.Add(flowLayoutPanel1);
            scRow.Controls.Add(lblList4);
            scRow.Controls.Add(cmbSC64List);
            scRow.Controls.Add(scListBtns);
            scRow.Controls.Add(btnSC64Start);
            scRow.Controls.Add(scExtras);
            scRow.Dock = DockStyle.Fill;
            scRow.Location = new Point(3, 3);
            scRow.Name = "scRow";
            scRow.Padding = new Padding(8);
            scRow.Size = new Size(1155, 96);
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
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.AutoSize = true;
            flowLayoutPanel1.Controls.Add(btnSC64Install64DD);
            flowLayoutPanel1.Controls.Add(btnSC64InstallEmulators);
            flowLayoutPanel1.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel1.Location = new Point(254, 8);
            flowLayoutPanel1.Margin = new Padding(0);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(166, 80);
            flowLayoutPanel1.TabIndex = 4;
            // 
            // btnSC64Install64DD
            // 
            btnSC64Install64DD.Location = new Point(3, 3);
            btnSC64Install64DD.Name = "btnSC64Install64DD";
            btnSC64Install64DD.Size = new Size(160, 34);
            btnSC64Install64DD.TabIndex = 0;
            btnSC64Install64DD.Text = "Install 64DD IPL";
            btnSC64Install64DD.Click += btnSC64Install64DD_Click;
            // 
            // btnSC64InstallEmulators
            // 
            btnSC64InstallEmulators.Location = new Point(3, 43);
            btnSC64InstallEmulators.Name = "btnSC64InstallEmulators";
            btnSC64InstallEmulators.Size = new Size(160, 34);
            btnSC64InstallEmulators.TabIndex = 1;
            btnSC64InstallEmulators.Text = "Install Emulators";
            // 
            // lblList4
            // 
            lblList4.AutoSize = true;
            lblList4.Location = new Point(432, 16);
            lblList4.Margin = new Padding(12, 8, 6, 0);
            lblList4.Name = "lblList4";
            lblList4.Size = new Size(28, 15);
            lblList4.TabIndex = 1;
            lblList4.Text = "List:";
            // 
            // cmbSC64List
            // 
            cmbSC64List.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSC64List.Location = new Point(469, 11);
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
            scListBtns.Location = new Point(840, 8);
            scListBtns.Margin = new Padding(8, 0, 0, 0);
            scListBtns.Name = "scListBtns";
            scListBtns.Size = new Size(186, 80);
            scListBtns.TabIndex = 3;
            // 
            // btnSC64RefreshLists
            // 
            btnSC64RefreshLists.Location = new Point(3, 3);
            btnSC64RefreshLists.Name = "btnSC64RefreshLists";
            btnSC64RefreshLists.Size = new Size(114, 34);
            btnSC64RefreshLists.TabIndex = 0;
            btnSC64RefreshLists.Text = "Refresh lists";
            // 
            // btnSC64OpenLists
            // 
            btnSC64OpenLists.Location = new Point(3, 43);
            btnSC64OpenLists.Name = "btnSC64OpenLists";
            btnSC64OpenLists.Size = new Size(180, 34);
            btnSC64OpenLists.TabIndex = 2;
            btnSC64OpenLists.Text = "Open GameLists Folder";
            // 
            // btnSC64Start
            // 
            btnSC64Start.Enabled = false;
            btnSC64Start.Location = new Point(1029, 11);
            btnSC64Start.Name = "btnSC64Start";
            btnSC64Start.Size = new Size(100, 34);
            btnSC64Start.TabIndex = 4;
            btnSC64Start.Text = "Start";
            // 
            // scExtras
            // 
            scExtras.AutoSize = true;
            scExtras.FlowDirection = FlowDirection.TopDown;
            scExtras.Location = new Point(1140, 8);
            scExtras.Margin = new Padding(8, 0, 0, 0);
            scExtras.Name = "scExtras";
            scExtras.Size = new Size(0, 0);
            scExtras.TabIndex = 5;
            scExtras.WrapContents = false;
            // 
            // scFill
            // 
            scFill.AutoScroll = true;
            scFill.Controls.Add(statusStripSC);
            scFill.Controls.Add(lvScList);
            scFill.Controls.Add(scTop);
            scFill.Dock = DockStyle.Fill;
            scFill.Location = new Point(3, 105);
            scFill.Name = "scFill";
            scFill.Size = new Size(1155, 244);
            scFill.TabIndex = 1;
            // 
            // statusStripSC
            // 
            statusStripSC.Items.AddRange(new ToolStripItem[] { lblStatusSC, lblSepSC1, lblSpacerSC, lblCountSC, lblSepSC2, lblSizeSC });
            statusStripSC.Location = new Point(0, 222);
            statusStripSC.Name = "statusStripSC";
            statusStripSC.Size = new Size(1155, 22);
            statusStripSC.TabIndex = 3;
            // 
            // lblStatusSC
            // 
            lblStatusSC.Name = "lblStatusSC";
            lblStatusSC.Size = new Size(42, 17);
            lblStatusSC.Text = "Ready.";
            // 
            // lblSepSC1
            // 
            lblSepSC1.Name = "lblSepSC1";
            lblSepSC1.Size = new Size(16, 17);
            lblSepSC1.Text = " | ";
            // 
            // lblSpacerSC
            // 
            lblSpacerSC.Name = "lblSpacerSC";
            lblSpacerSC.Size = new Size(958, 17);
            lblSpacerSC.Spring = true;
            // 
            // lblCountSC
            // 
            lblCountSC.Name = "lblCountSC";
            lblCountSC.Size = new Size(65, 17);
            lblCountSC.Text = "Checked: 0";
            // 
            // lblSepSC2
            // 
            lblSepSC2.Name = "lblSepSC2";
            lblSepSC2.Size = new Size(10, 17);
            lblSepSC2.Text = "|";
            // 
            // lblSizeSC
            // 
            lblSizeSC.Name = "lblSizeSC";
            lblSizeSC.Size = new Size(49, 17);
            lblSizeSC.Text = "Size: 0 B";
            // 
            // lvScList
            // 
            lvScList.CheckBoxes = true;
            lvScList.Columns.AddRange(new ColumnHeader[] { colPathSC, colStatusSC });
            lvScList.Dock = DockStyle.Fill;
            lvScList.FullRowSelect = true;
            lvScList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lvScList.Location = new Point(0, 41);
            lvScList.Name = "lvScList";
            lvScList.Size = new Size(1155, 203);
            lvScList.TabIndex = 2;
            lvScList.UseCompatibleStateImageBehavior = false;
            lvScList.View = View.Details;
            // 
            // colPathSC
            // 
            colPathSC.Text = "Path";
            colPathSC.Width = 500;
            // 
            // colStatusSC
            // 
            colStatusSC.Text = "Status";
            colStatusSC.Width = 160;
            // 
            // scTop
            // 
            scTop.AutoSize = true;
            scTop.Controls.Add(lblScMode);
            scTop.Controls.Add(rbScFiles);
            scTop.Controls.Add(rbScDirs);
            scTop.Controls.Add(btnScChoose);
            scTop.Controls.Add(btnScCheckAll);
            scTop.Controls.Add(btnScUncheckAll);
            scTop.Controls.Add(btnScStart);
            scTop.Dock = DockStyle.Top;
            scTop.Location = new Point(0, 0);
            scTop.Margin = new Padding(10);
            scTop.Name = "scTop";
            scTop.Size = new Size(1155, 41);
            scTop.TabIndex = 1;
            scTop.WrapContents = false;
            // 
            // lblScMode
            // 
            lblScMode.AutoSize = true;
            lblScMode.Location = new Point(6, 13);
            lblScMode.Margin = new Padding(6, 13, 8, 0);
            lblScMode.Name = "lblScMode";
            lblScMode.Size = new Size(41, 15);
            lblScMode.TabIndex = 0;
            lblScMode.Text = "Mode:";
            // 
            // rbScFiles
            // 
            rbScFiles.AutoSize = true;
            rbScFiles.Checked = true;
            rbScFiles.Location = new Point(55, 11);
            rbScFiles.Margin = new Padding(0, 11, 8, 0);
            rbScFiles.Name = "rbScFiles";
            rbScFiles.Size = new Size(48, 19);
            rbScFiles.TabIndex = 1;
            rbScFiles.TabStop = true;
            rbScFiles.Text = "Files";
            rbScFiles.UseVisualStyleBackColor = true;
            // 
            // rbScDirs
            // 
            rbScDirs.AutoSize = true;
            rbScDirs.Location = new Point(111, 11);
            rbScDirs.Margin = new Padding(0, 11, 12, 0);
            rbScDirs.Name = "rbScDirs";
            rbScDirs.Size = new Size(81, 19);
            rbScDirs.TabIndex = 2;
            rbScDirs.Text = "Directories";
            rbScDirs.UseVisualStyleBackColor = true;
            // 
            // btnScChoose
            // 
            btnScChoose.AutoSize = true;
            btnScChoose.Location = new Point(204, 8);
            btnScChoose.Margin = new Padding(0, 8, 8, 8);
            btnScChoose.Name = "btnScChoose";
            btnScChoose.Size = new Size(75, 25);
            btnScChoose.TabIndex = 3;
            btnScChoose.Text = "Choose…";
            // 
            // btnScCheckAll
            // 
            btnScCheckAll.AutoSize = true;
            btnScCheckAll.Location = new Point(287, 8);
            btnScCheckAll.Margin = new Padding(0, 8, 8, 8);
            btnScCheckAll.Name = "btnScCheckAll";
            btnScCheckAll.Size = new Size(75, 25);
            btnScCheckAll.TabIndex = 4;
            btnScCheckAll.Text = "Check All";
            // 
            // btnScUncheckAll
            // 
            btnScUncheckAll.AutoSize = true;
            btnScUncheckAll.Location = new Point(370, 8);
            btnScUncheckAll.Margin = new Padding(0, 8, 8, 8);
            btnScUncheckAll.Name = "btnScUncheckAll";
            btnScUncheckAll.Size = new Size(80, 25);
            btnScUncheckAll.TabIndex = 5;
            btnScUncheckAll.Text = "Uncheck All";
            // 
            // btnScStart
            // 
            btnScStart.AutoSize = true;
            btnScStart.Location = new Point(458, 8);
            btnScStart.Margin = new Padding(0, 8, 8, 8);
            btnScStart.Name = "btnScStart";
            btnScStart.Size = new Size(98, 25);
            btnScStart.TabIndex = 6;
            btnScStart.Text = "Start (Checked)";
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
            tabListmaker.Size = new Size(1161, 352);
            tabListmaker.TabIndex = 4;
            tabListmaker.Text = "Listmaker";
            // 
            // lvList
            // 
            lvList.CheckBoxes = true;
            lvList.Columns.AddRange(new ColumnHeader[] { colPathLM, colStatusLM });
            lvList.Dock = DockStyle.Fill;
            lvList.FullRowSelect = true;
            lvList.Location = new Point(0, 93);
            lvList.Name = "lvList";
            lvList.Size = new Size(1161, 190);
            lvList.TabIndex = 0;
            lvList.UseCompatibleStateImageBehavior = false;
            lvList.View = View.Details;
            // 
            // colPathLM
            // 
            colPathLM.Text = "Path";
            colPathLM.Width = 500;
            // 
            // colStatusLM
            // 
            colStatusLM.Text = "Status";
            colStatusLM.Width = 160;
            // 
            // lmTop2
            // 
            lmTop2.AutoSize = true;
            lmTop2.Controls.Add(lblMode);
            lmTop2.Controls.Add(rbFiles);
            lmTop2.Controls.Add(rbDirs);
            lmTop2.Controls.Add(rbMixed);
            lmTop2.Dock = DockStyle.Top;
            lmTop2.Location = new Point(0, 47);
            lmTop2.Name = "lmTop2";
            lmTop2.Padding = new Padding(8);
            lmTop2.Size = new Size(1161, 46);
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
            rbFiles.Size = new Size(75, 24);
            rbFiles.TabIndex = 1;
            rbFiles.TabStop = true;
            rbFiles.Text = "Files";
            // 
            // rbDirs
            // 
            rbDirs.Location = new Point(143, 11);
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
            lmTop1.Controls.Add(btnClearLM);
            lmTop1.Controls.Add(btnOpenGameListsLM);
            lmTop1.Dock = DockStyle.Top;
            lmTop1.Location = new Point(0, 0);
            lmTop1.Name = "lmTop1";
            lmTop1.Padding = new Padding(8);
            lmTop1.Size = new Size(1161, 47);
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
            lblFilter.Size = new Size(142, 21);
            lblFilter.TabIndex = 1;
            lblFilter.Text = " Find a title (e.g. Zelda):";
            // 
            // txtFilter
            // 
            txtFilter.Location = new Point(240, 11);
            txtFilter.Name = "txtFilter";
            txtFilter.Size = new Size(280, 23);
            txtFilter.TabIndex = 2;
            txtFilter.Text = "*.*";
            // 
            // chkRecursive
            // 
            chkRecursive.Location = new Point(526, 11);
            chkRecursive.Name = "chkRecursive";
            chkRecursive.Size = new Size(104, 24);
            chkRecursive.TabIndex = 3;
            chkRecursive.Text = "Include subfolders";
            chkRecursive.Visible = false;
            // 
            // btnRefreshLM
            // 
            btnRefreshLM.AutoSize = true;
            btnRefreshLM.Location = new Point(636, 11);
            btnRefreshLM.Name = "btnRefreshLM";
            btnRefreshLM.Size = new Size(82, 25);
            btnRefreshLM.TabIndex = 4;
            btnRefreshLM.Text = "Clear Search";
            // 
            // btnClearLM
            // 
            btnClearLM.Location = new Point(724, 11);
            btnClearLM.Name = "btnClearLM";
            btnClearLM.Size = new Size(75, 25);
            btnClearLM.TabIndex = 999;
            btnClearLM.Text = "Clear All";
            // 
            // btnOpenGameListsLM
            // 
            btnOpenGameListsLM.AutoSize = true;
            btnOpenGameListsLM.Location = new Point(805, 11);
            btnOpenGameListsLM.Name = "btnOpenGameListsLM";
            btnOpenGameListsLM.Size = new Size(139, 25);
            btnOpenGameListsLM.TabIndex = 1000;
            btnOpenGameListsLM.Text = "Open GameLists Folder";
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
            lmBottom.Size = new Size(1161, 47);
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
            statusStripLM.Size = new Size(1161, 22);
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
            lblSpacerLM.Size = new Size(744, 17);
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
            tabSettings.Size = new Size(1161, 352);
            tabSettings.TabIndex = 5;
            tabSettings.Text = "Settings";
            // 
            // setPanel
            // 
            setPanel.AutoScroll = true;
            setPanel.Controls.Add(lblDriveList);
            setPanel.Controls.Add(chkOnlyRemovable);
            setPanel.Controls.Add(chkEjectPrompt);
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
            setPanel.Size = new Size(1161, 352);
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
            chkOnlyRemovable.Location = new Point(13, 28);
            chkOnlyRemovable.Name = "chkOnlyRemovable";
            chkOnlyRemovable.Size = new Size(226, 19);
            chkOnlyRemovable.TabIndex = 1;
            chkOnlyRemovable.Text = "Only show removable drives (USB/SD)";
            // 
            // chkEjectPrompt
            // 
            chkEjectPrompt.AutoSize = true;
            chkEjectPrompt.Location = new Point(13, 53);
            chkEjectPrompt.Name = "chkEjectPrompt";
            chkEjectPrompt.Size = new Size(147, 19);
            chkEjectPrompt.TabIndex = 9;
            chkEjectPrompt.Text = "Show post-run prompt";
            chkEjectPrompt.UseVisualStyleBackColor = true;
            // 
            // lblCopyTO
            // 
            lblCopyTO.AutoSize = true;
            lblCopyTO.Location = new Point(10, 87);
            lblCopyTO.Margin = new Padding(0, 12, 0, 0);
            lblCopyTO.Name = "lblCopyTO";
            lblCopyTO.Size = new Size(137, 15);
            lblCopyTO.TabIndex = 2;
            lblCopyTO.Text = "Copy timeout (seconds):";
            // 
            // numCopy
            // 
            numCopy.Location = new Point(13, 105);
            numCopy.Maximum = new decimal(new int[] { 999, 0, 0, 0 });
            numCopy.Name = "numCopy";
            numCopy.Size = new Size(80, 23);
            numCopy.TabIndex = 3;
            numCopy.Value = new decimal(new int[] { 20, 0, 0, 0 });
            // 
            // lblOWTO
            // 
            lblOWTO.AutoSize = true;
            lblOWTO.Location = new Point(13, 131);
            lblOWTO.Name = "lblOWTO";
            lblOWTO.Size = new Size(179, 15);
            lblOWTO.TabIndex = 4;
            lblOWTO.Text = "Overwrite countdown (seconds):";
            // 
            // numOW
            // 
            numOW.Location = new Point(13, 149);
            numOW.Maximum = new decimal(new int[] { 999, 0, 0, 0 });
            numOW.Name = "numOW";
            numOW.Size = new Size(80, 23);
            numOW.TabIndex = 5;
            numOW.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // lblAuto
            // 
            lblAuto.AutoSize = true;
            lblAuto.Location = new Point(13, 175);
            lblAuto.Name = "lblAuto";
            lblAuto.Size = new Size(124, 15);
            lblAuto.TabIndex = 6;
            lblAuto.Text = "Overwrite auto action:";
            // 
            // cmbAuto
            // 
            cmbAuto.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbAuto.Location = new Point(13, 193);
            cmbAuto.Name = "cmbAuto";
            cmbAuto.Size = new Size(80, 23);
            cmbAuto.TabIndex = 7;
            // 
            // btnSaveSettings
            // 
            btnSaveSettings.Location = new Point(10, 231);
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
            grpLog.Size = new Size(1169, 260);
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
            txtLog.Size = new Size(1163, 238);
            txtLog.TabIndex = 0;
            txtLog.WordWrap = false;
            // 
            // MainForm
            // 
            ClientSize = new Size(1169, 700);
            Controls.Add(tabs);
            Controls.Add(topPanel);
            Controls.Add(grpLog);
            MinimumSize = new Size(1175, 680);
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
            xFill.ResumeLayout(false);
            xFill.PerformLayout();
            xsTop.ResumeLayout(false);
            xsTop.PerformLayout();
            statusStripXS.ResumeLayout(false);
            statusStripXS.PerformLayout();
            tabSaroo.ResumeLayout(false);
            sRoot.ResumeLayout(false);
            sRoot.PerformLayout();
            sRow.ResumeLayout(false);
            sRow.PerformLayout();
            sListBtns.ResumeLayout(false);
            sFill.ResumeLayout(false);
            sFill.PerformLayout();
            sTop.ResumeLayout(false);
            sTop.PerformLayout();
            statusStripSR.ResumeLayout(false);
            statusStripSR.PerformLayout();
            tabGamecube.ResumeLayout(false);
            gRoot.ResumeLayout(false);
            gRoot.PerformLayout();
            gRow.ResumeLayout(false);
            gRow.PerformLayout();
            gListBtns.ResumeLayout(false);
            gFill.ResumeLayout(false);
            gFill.PerformLayout();
            gcTop.ResumeLayout(false);
            gcTop.PerformLayout();
            statusStripGC.ResumeLayout(false);
            statusStripGC.PerformLayout();
            tabSC64.ResumeLayout(false);
            scRoot.ResumeLayout(false);
            scRoot.PerformLayout();
            scRow.ResumeLayout(false);
            scRow.PerformLayout();
            flowLayoutPanel1.ResumeLayout(false);
            scListBtns.ResumeLayout(false);
            scFill.ResumeLayout(false);
            scFill.PerformLayout();
            statusStripSC.ResumeLayout(false);
            statusStripSC.PerformLayout();
            scTop.ResumeLayout(false);
            scTop.PerformLayout();
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

        // Xstation - Listmaker-style controls
        private System.Windows.Forms.FlowLayoutPanel xsTop;
        private System.Windows.Forms.Label lblXsMode;
        private System.Windows.Forms.RadioButton rbXsFiles;
        private System.Windows.Forms.RadioButton rbXsDirs;
        private System.Windows.Forms.Button btnXsChoose;
        private System.Windows.Forms.Button btnXsCheckAll;
        private System.Windows.Forms.Button btnXsUncheckAll;
        private System.Windows.Forms.Button btnXsStart;
        private System.Windows.Forms.ListView lvXsList;

        // Xstation status bar
        private System.Windows.Forms.StatusStrip statusStripXS;

private System.Windows.Forms.StatusStrip statusStripSC;
private System.Windows.Forms.ToolStripStatusLabel lblStatusSC;
private System.Windows.Forms.ToolStripStatusLabel lblSepSC1;
private System.Windows.Forms.ToolStripStatusLabel lblSpacerSC;
private System.Windows.Forms.ToolStripStatusLabel lblCountSC;
private System.Windows.Forms.ToolStripStatusLabel lblSepSC2;
private System.Windows.Forms.ToolStripStatusLabel lblSizeSC;
private System.Windows.Forms.ListView lvScList;
private System.Windows.Forms.ColumnHeader colPathSC;
private System.Windows.Forms.ColumnHeader colStatusSC;
private System.Windows.Forms.Button btnScChoose;
private System.Windows.Forms.Button btnScCheckAll;
private System.Windows.Forms.Button btnScUncheckAll;
private System.Windows.Forms.Button btnScStart;
private System.Windows.Forms.Label lblScMode;
private System.Windows.Forms.RadioButton rbScFiles;
private System.Windows.Forms.RadioButton rbScDirs;
private System.Windows.Forms.FlowLayoutPanel scTop;
        private System.Windows.Forms.ToolStripStatusLabel lblStatusXS;
        private System.Windows.Forms.ToolStripStatusLabel lblSepXS1;
        private System.Windows.Forms.ToolStripStatusLabel lblSpacerXS;
        private System.Windows.Forms.ToolStripStatusLabel lblCountXS;
        private System.Windows.Forms.ToolStripStatusLabel lblSepXS2;
        private System.Windows.Forms.ToolStripStatusLabel lblSizeXS;
        private System.Windows.Forms.Button btnClearLM;
        private System.Windows.Forms.Button btnOpenGameListsLM;

        private FlowLayoutPanel flowLayoutPanel1;
    }
            
}