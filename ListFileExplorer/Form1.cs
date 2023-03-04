using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Security;
using System.Runtime;
using System.Security.Policy;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Xml;
using System.Xml.Linq;

namespace ListFileExplorer
{
    public enum FileIcon { None = 0, Folder = 1 };

    public partial class Form1 : Form
    {
        private string currentDir = string.Empty;
        private bool showHidden, showSystem;
        private List<string> fileExts = new List<string>();

        public Form1(bool loadViewSettings = true)
        {
            InitializeComponent();
            if (loadViewSettings)
            {
                LoadQuickAccessFolders();
            }
            SetupListView();
            LoadViewSettings();          
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.MinimumSize = new Size(400, 300);

            UpdateMenuStrip();
            UpdateTitle();
            UpdateContextMenu();
            UpdateStatusStrip();
        }

        private bool SaveViewSettings()
        {
            const string path = "view_settings.xml";

            try
            {
                XmlDocument doc = new XmlDocument();
                XmlNode parentNode = doc.CreateElement("view_settings");

                XmlNode show_propNode = doc.CreateElement("show_prop");

                XmlAttribute show_hiddenAtt = doc.CreateAttribute("show_hidden");
                show_hiddenAtt.Value = showHidden.ToString();
                if (show_propNode.Attributes != null)
                    show_propNode.Attributes.Append(show_hiddenAtt);
                else return false;

                XmlAttribute show_systemAtt = doc.CreateAttribute("show_system");
                show_systemAtt.Value = showSystem.ToString();
                if (show_propNode.Attributes != null)
                    show_propNode.Attributes.Append(show_systemAtt);
                else return false;

                parentNode.AppendChild(show_propNode);
                doc.AppendChild(parentNode);

                XmlAttribute view_typeAtt = doc.CreateAttribute("view_type");
                view_typeAtt.Value = ((int)listView.View).ToString();
                if (parentNode.Attributes != null)
                    parentNode.Attributes.Append(view_typeAtt);
                else return false;

                doc.Save(path);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, $"Error while executing {nameof(SaveViewSettings)}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        private bool LoadViewSettings()
        {
            const string path = "view_settings.xml";

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                
                var list = doc.SelectNodes("//view_settings/show_prop");
                if (list == null)
                    return false;

                if (bool.TryParse(list[0]?.Attributes?["show_hidden"]?.Value, out bool showHidden))
                    this.showHidden = showHidden;
                if (bool.TryParse(list[0]?.Attributes?["show_system"]?.Value, out bool showSystem))
                    this.showSystem = showSystem;

                if (int.TryParse(doc.FirstChild?.Attributes?["view_type"]?.Value, out int intViewType))
                    listView.View = (View)intViewType;

                UpdateMenuStripView();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, $"Error while executing {nameof(LoadViewSettings)}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        private void LoadQuickAccessFolders()
        {
            List<string> folders = new List<string>()
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.MyComputer),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.History)
            };
            foreach (var ob in folders)
            {
                try
                {
                    DirectoryInfo dir = new DirectoryInfo(ob);
                    var item = quickAccessToolStripMenuItem.DropDownItems.Add(dir.Name);
                    item.Tag = dir;
                    item.Click += QuickAccessToolClick;
                }
                catch { }
            }
        }

        private void SetupListView()
        {
            listView.AllowDrop = true;
            listView.ShowItemToolTips = true;

            listView.Columns.Clear();
            listView.Columns.Add("Name", 180);
            listView.Columns.Add("Type", 96);
            listView.Columns.Add("Date Created", 128);
            listView.Columns.Add("Date Modified", 128);
            listView.Columns.Add("Size", 128);

            listView.ContextMenuStrip = listView_contextMenuStrip;

            var largeList = listView.LargeImageList = new ImageList();
            largeList.ImageSize = new Size(64, 64);

            var smallList = listView.SmallImageList = new ImageList();
            smallList.ImageSize = new Size(18, 18);
        }

        private void UpdateContextMenu()
        {
            bool isSelectedDirectory = listView.SelectedItems.Count == 1 ? (listView.SelectedItems[0].Tag is DirectoryInfo) : false;
            bool isSelectedOneItem = listView.SelectedItems.Count == 1;
            openInNewWindowToolStripMenuItem.Enabled = isSelectedDirectory;
            showInExplorerToolStripMenuItem1.Enabled = isSelectedOneItem;
            copyPathToolStripMenuItem1.Enabled = isSelectedOneItem;
            propertiesToolStripMenuItem1.Enabled = isSelectedOneItem;
            sizeAllColumnsToFitToolStripMenuItem.Enabled = listView.View == View.Details;
        }

        private void UpdateTitle()
        {
            if (string.IsNullOrWhiteSpace(currentDir))
                this.Text = Application.ProductName;
            else
                this.Text = currentDir + " - " + Application.ProductName;
        }

        private void UpdateStatusStrip()
        {
            currentDir_toolStripStatusLabel.Visible = true;
            if (listView.SelectedItems.Count == 1)
            {
                currentDir_toolStripStatusLabel.Text = ((FileSystemInfo)listView.SelectedItems[0].Tag).FullName;
                
                //if (listView.SelectedItems[0].Tag is DirectoryInfo dir)
                //{
                //    currentDir_toolStripStatusLabel.Text = dir.FullName;
                //    if (TryGetDirectoryLength(dir.FullName, out long size))
                //        size_toolStripStatusLabel.Text = "Size: " + size + " bytes";
                //    else
                //        size_toolStripStatusLabel.Visible = false;
                //    if (TryGetDirectoryDirectoriesCount(dir.FullName, out long count))
                //    {
                //        other_toolStripStatusLabel.Text = $"Contains: {count} directories, ";
                //        if (TryGetDirectoryFilesCount(dir.FullName, out count))
                //            other_toolStripStatusLabel.Text += $"{count} files";
                //        else
                //            other_toolStripStatusLabel.Text += $"- files";
                //    }
                //    else
                //        other_toolStripStatusLabel.Visible = false;
                //}
                //else if(listView.SelectedItems[0].Tag is FileInfo file)
                //{
                //    size_toolStripStatusLabel.Text = "Size: " + file.Length + " bytes";
                //    currentDir_toolStripStatusLabel.Text = file.FullName;
                //    other_toolStripStatusLabel.Visible = false;
                //}
            }
            else if(!string.IsNullOrWhiteSpace(currentDir))
            {
                currentDir_toolStripStatusLabel.Text = currentDir;

                //if (TryGetDirectoryLength(currentDir, out long size))
                //    size_toolStripStatusLabel.Text = "Size: " + size + " bytes";
                //else
                //    size_toolStripStatusLabel.Visible = false;
                //if (TryGetDirectoryDirectoriesCount(currentDir, out long count))
                //{
                //    other_toolStripStatusLabel.Text = $"Contains: {count} directories, ";
                //    if (TryGetDirectoryFilesCount(currentDir, out count))
                //        other_toolStripStatusLabel.Text += $"{count} files";
                //    else
                //        other_toolStripStatusLabel.Text += $"- files";
                //}
                //else
                //    other_toolStripStatusLabel.Visible = false;
            }
            else
            {
                currentDir_toolStripStatusLabel.Visible = false;
            }
        }

        private void UpdateMenuStrip()
        {
            UpdateMenuStripEdit();
            UpdateMenuStripView();
        }

        private void UpdateMenuStripView()
        {
            hiddenToolStripMenuItem.Checked = showHidden;
            systemToolStripMenuItem.Checked = showSystem;
            largeIconToolStripMenuItem.CheckState = CheckState.Unchecked;
            detailsToolStripMenuItem.CheckState = CheckState.Unchecked;
            smallIconToolStripMenuItem.CheckState = CheckState.Unchecked;
            listToolStripMenuItem.CheckState = CheckState.Unchecked;
            switch (listView.View)
            {
                case View.List:
                    listToolStripMenuItem.CheckState = CheckState.Indeterminate;
                    break;
                case View.Details:
                    detailsToolStripMenuItem.CheckState = CheckState.Indeterminate;
                    break;
                case View.LargeIcon:
                    largeIconToolStripMenuItem.CheckState = CheckState.Indeterminate;
                    break;
                case View.SmallIcon:
                    smallIconToolStripMenuItem.CheckState = CheckState.Indeterminate;
                    break;
            }
        }

        private void UpdateMenuStripEdit()
        {
            bool isSelectedDirectory = listView.SelectedItems.Count == 1 ? (listView.SelectedItems[0].Tag is DirectoryInfo) : false;
            bool isSelectedOneItem = listView.SelectedItems.Count == 1;
            openInNewWindowEditToolStripMenuItem.Enabled = isSelectedDirectory;
            showInExplorerToolStripMenuItem.Enabled = isSelectedOneItem;
            copyPathToolStripMenuItem1.Enabled = isSelectedOneItem;
            propertiesToolStripMenuItem.Enabled = isSelectedOneItem;
        }

        private void ResetIcons()
        {
            fileExts.Clear();
            fileExts.Add("none_icn");
            fileExts.Add("folder_icn");

            listView.LargeImageList.Images.Clear();
            listView.LargeImageList.Images.Add(new Bitmap("none_icn.png"));
            listView.LargeImageList.Images.Add(new Bitmap("folder_icn.png"));

            listView.SmallImageList.Images.Clear();
            listView.SmallImageList.Images.Add(new Bitmap("none_icn.png"));
            listView.SmallImageList.Images.Add(new Bitmap("folder_icn.png"));
        }

        private int GetFileIconIndex(FileSystemInfo info)
        {
            if(info.Attributes.HasFlag(FileAttributes.Directory))
                return (int)FileIcon.Folder;

            var index = fileExts.IndexOf(info.Extension);
            if (index != -1)
                return index;

            var icon = Icon.ExtractAssociatedIcon(info.FullName);
            if (icon == null)
                return (int)FileIcon.None;
            var bitmap = icon.ToBitmap();
            bitmap.MakeTransparent();

            listView.LargeImageList.Images.Add(bitmap);
            listView.SmallImageList.Images.Add(bitmap);

            fileExts.Add(info.Extension);

            return listView.LargeImageList.Images.Count - 1;
        }

        public bool OpenDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                MessageBox.Show($"Directory {path} does not exists", path, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            try
            {
                ResetIcons();
                listView.Items.Clear();
                currentDir = path;
                LoadFiles(path);
                LoadDirectories(path);
                currentDir_textBox.Text = path;

                UpdateStatusStrip();
                UpdateContextMenu();
                UpdateMenuStripEdit();
                UpdateTitle();

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while loading directory '{path}'\nError message: {ex.Message}\n{ex.InnerException?.Message}", path, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        private void LoadFiles(string dir)
        {
            foreach (var ob in Directory.GetFiles(dir))
            {
                FileInfo file = new FileInfo(ob);

                if (!(file.Attributes.HasFlag(FileAttributes.Hidden) && !showHidden) && !(file.Attributes.HasFlag(FileAttributes.System) && !showSystem))
                {
                    string name = Path.GetFileNameWithoutExtension(ob);
                    if (string.IsNullOrWhiteSpace(name))
                        name = Path.GetFileName(ob);

                    var item = listView.Items.Add(name);
                    item.Tag = file;
                    item.ImageIndex = GetFileIconIndex(file);

                    item.SubItems.Add(Path.GetExtension(ob));
                    item.SubItems.Add(file.CreationTime.ToShortTimeString());
                    item.SubItems.Add(file.LastWriteTime.ToShortTimeString());
                    item.SubItems.Add(file.Length.ToString() + " bytes");

                    item.ToolTipText = file.Attributes.ToString();
                    if ((file.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        item.BackColor = Color.LightGray;
                }   
            }
        }

        private void LoadDirectories(string dir)
        {
            foreach (var ob in Directory.GetDirectories(dir))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(ob);

                if (!(dirInfo.Attributes.HasFlag(FileAttributes.Hidden) && !showHidden) && !(dirInfo.Attributes.HasFlag(FileAttributes.System) && !showSystem))
                {
                    var item = listView.Items.Add(dirInfo.Name);
                    item.Tag = dirInfo;
                    item.ImageIndex = (int)FileIcon.Folder;

                    item.SubItems.Add("Folder");
                    item.SubItems.Add(dirInfo.CreationTime.ToShortTimeString());
                    item.SubItems.Add(dirInfo.LastWriteTime.ToShortTimeString());

                    if (TryGetDirectoryLength(dirInfo.FullName, out long size))
                        item.SubItems.Add(size.ToString() + " bytes");
                    else
                        item.SubItems.Add("- bytes");

                    item.ToolTipText = dirInfo.Attributes.ToString();
                    if ((dirInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        item.BackColor = Color.LightGray;
                }
            }
        }

        private bool TryGetDirectoryLength(string dir, out long count)
        {
            count = 0;
            if (!Directory.Exists(dir))
                return false;

            try
            {
                foreach (var ob in Directory.GetFiles(dir))
                {
                    count += ob.Length;
                }
                foreach (var ob in Directory.GetDirectories(dir))
                {
                    if (TryGetDirectoryLength(ob, out long temp))
                        count += temp;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private bool TryGetDirectoryDirectoriesCount(string dir, out long count)
        {
            count = 0;
            if (!Directory.Exists(dir))
                return false;

            string[] arr;
            try
            {
                arr = Directory.GetDirectories(dir);
                count = arr.LongLength;
            }
            catch
            {
                return false;
            }

            foreach (var ob in arr)
            {
                if (TryGetDirectoryDirectoriesCount(ob, out long temp))
                    count += temp;
            }

            return true;
        }

        private bool TryGetDirectoryFilesCount(string dir, out long count)
        {
            count = 0;
            if (!Directory.Exists(dir))
                return false;

            string[] arr;
            try
            {
                arr = Directory.GetDirectories(dir);
                count = Directory.GetFiles(dir).LongLength;
            }
            catch
            {
                return false;
            }

            foreach (var ob in arr)
            {
                if (TryGetDirectoryFilesCount(ob, out long temp))
                    count += temp;
            }

            return true;
        }

        public bool ShowSelectedItemsProperties()
        {
            if (listView.SelectedItems.Count != 1)
                return false;

            for(int i = 0; i < listView.SelectedItems.Count; i++)
            {
                if (listView.SelectedItems[i].Tag is FileInfo file)
                {
                    string message = $"Full name: {file.FullName}\nType: {file.Extension}\nDate created: {file.CreationTime.ToShortDateString()}\nDate modified: {file.LastWriteTime.ToShortDateString()}\nSize: {file.Length} bytes\nAttributes: {file.Attributes}";
                    MessageBox.Show(message, file.Name);
                }
                else if (listView.SelectedItems[i].Tag is DirectoryInfo dir)
                {
                    string message = $"Full name: {dir.FullName}\nType: Folder\n";
                    if (TryGetDirectoryDirectoriesCount(dir.FullName, out long count))
                    {
                        message += $"Contains: {count} directories, ";
                        if (TryGetDirectoryFilesCount(dir.FullName, out count))
                            message += $"{dir.GetFiles().Length} files\n";
                        else
                            message += "- files\n";
                    }
                    if (TryGetDirectoryLength(dir.FullName, out long size))
                        message += $"Size {size} bytes\n";
                    message += $"Date created: {dir.CreationTime.ToShortDateString()}\nDate modified: {dir.LastWriteTime.ToShortDateString()}\nAttributes: {dir.Attributes}";
                    MessageBox.Show(message, dir.Name);
                }
            }

            return true;
        }

        public bool CopyToClipboardSelectedItemPath()
        {
            if (listView.SelectedItems.Count != 1)
                return false;
            Clipboard.SetText(((FileSystemInfo)listView.SelectedItems[0].Tag).FullName);
            return true;
        }

        public bool ShowSelectedItemInExplorer()
        {
            if (listView.SelectedItems.Count != 1)
                return false;
            try
            {
                string path = ((FileSystemInfo)listView.SelectedItems[0].Tag).FullName;
                ProcessStartInfo info = new ProcessStartInfo()
                {
                    FileName = "explorer.exe",
                    Arguments = ("/select," + path)
                };
                Process.Start(info);
            }
            catch
            {
                return false;
            }
            return true;
        }

        private void QuickAccessToolClick(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                if(item.Tag is DirectoryInfo dir)
                {
                    OpenDirectory(dir.FullName);
                }
            }
        }

        private void listView_DoubleClick(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count != 1)
                return;
            if (listView.SelectedItems[0].Tag is DirectoryInfo dir)
            {
                OpenDirectory(dir.FullName);
            }
            else if (listView.SelectedItems[0].Tag is FileInfo)
            {
                ShowSelectedItemsProperties();
            }
        }

        private void listView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            listView.Columns[e.Column].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
        }

        private void sizeAllColumnsToFitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for(int i = 0; i < listView.Columns.Count; i++)
            {
                listView.Columns[i].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
            }
        }

        private void select_button_Click(object sender, EventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if(dialog.ShowDialog() == DialogResult.OK)
            {
                currentDir_textBox.Text = dialog.SelectedPath;
            }
        }

        private void open_button_Click(object sender, EventArgs e)
        {
            OpenDirectory(currentDir_textBox.Text);   
        }

        private void newWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //-----------------
            new Form1().Show();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                OpenDirectory(dialog.SelectedPath);
            }
        }

        private void openInNewWindowToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var form = new Form1();
                form.OpenDirectory(dialog.SelectedPath);
                form.Show();
            }
        }

        private void openInNewWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count != 1)
                return;
            if (listView.SelectedItems[0].Tag is DirectoryInfo dir)
            {
                var form = new Form1();
                form.OpenDirectory(dir.FullName);
                form.Show();
            }
        }

        private void showInExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSelectedItemInExplorer();
        }

        private void copyPathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyToClipboardSelectedItemPath();
        }

        private void propertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSelectedItemsProperties();
        }

        private void largeIconToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView.View = View.LargeIcon;
            UpdateMenuStripView();
            UpdateContextMenu();
            SaveViewSettings();
        }

        private void detailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView.View = View.Details;
            UpdateMenuStripView();
            UpdateContextMenu();
            SaveViewSettings();
        }

        private void smallIconToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView.View = View.SmallIcon;
            UpdateMenuStripView();
            UpdateContextMenu();
            SaveViewSettings();
        }

        private void listToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView.View = View.List;
            UpdateMenuStripView();
            UpdateContextMenu();
            SaveViewSettings();
        }

        private void listView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data == null)
                return;
            e.Effect = DragDropEffects.None;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var arr = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (arr.Any(i => !Directory.Exists(i)))
                    return;
            }
            e.Effect = DragDropEffects.Copy;
        }

        private void listView_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null)
                return;
            if (e.Effect != DragDropEffects.Copy)
                return;

            var arr = (string[])e.Data.GetData(DataFormats.FileDrop);

            if(arr.Length == 1)
            {
                OpenDirectory(arr[0]);
            }
            else
            {
                foreach (var ob in arr)
                {
                    var form = new Form1();
                    form.OpenDirectory(ob);
                    form.Show();
                }
            }
        }

        private void systemToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            showSystem = systemToolStripMenuItem.Checked;
            SaveViewSettings();
        }

        private void listView_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateContextMenu();
            UpdateMenuStripEdit();
            UpdateStatusStrip();
        }

        private void propertiesToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ShowSelectedItemsProperties();
        }

        private void currentDir_toolStripStatusLabel_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(currentDir_toolStripStatusLabel.Text);
        }

        private void openInNewWindowToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count != 1)
                return;
            if (listView.SelectedItems[0].Tag is DirectoryInfo dir)
            {
                var form = new Form1();
                form.OpenDirectory(dir.FullName);
                form.Show();
            }
        }

        private void showInExplorerToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ShowSelectedItemInExplorer();
        }

        private void copyPathToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            CopyToClipboardSelectedItemPath();
        }

        private void hiddenToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            showHidden = hiddenToolStripMenuItem.Checked;
            SaveViewSettings();
        }
    }
}