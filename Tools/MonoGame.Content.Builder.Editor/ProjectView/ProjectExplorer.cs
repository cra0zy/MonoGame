// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using MonoGame.Tools.Pipeline;
using OpaqueDataDictionary = Microsoft.Xna.Framework.Content.Pipeline.OpaqueDataDictionary;

namespace MonoGame.Content.Builder.Editor.ProjectView
{
    public partial class ProjectExplorer : Pad
    {
        private PipelineProject _project;
        private TreeGridItem _itemBase, _itemRoot;
        private Image _iconRoot, _iconFolder, _iconFile;
        private List<ProjectExplorerCommand> _commands;

        public ProjectExplorer()
        {
            InitializeComponent();

            _iconRoot = Bitmap.FromResource("TreeView.Root.png").WithSize(16, 16);
            _iconFolder = Bitmap.FromResource("TreeView.Folder.png").WithSize(16, 16);
            _iconFile = Bitmap.FromResource("TreeView.File.png").WithSize(16, 16);

            _commands = new List<ProjectExplorerCommand>();

            foreach (var t in typeof(ProjectExplorer).Assembly.GetTypes())
            {
                if (!t.IsAbstract && typeof(ProjectExplorerCommand).IsAssignableFrom(t))
                {
                    var cmd = (ProjectExplorerCommand)Activator.CreateInstance(t);
                    cmd.Init(this);

                    _commands.Add(cmd);
                }
            }

            _commands.Sort((ProjectExplorerCommand x, ProjectExplorerCommand y) =>
            {
                var xindex = x.Index;
                var yindex = y.Index;

                if (xindex.groupIndex != yindex.groupIndex)
                    return xindex.groupIndex - yindex.groupIndex;

                return xindex.index - yindex.index;
            });

            _treeView.CellEditing += TreeView_CellEditing;
            _treeView.CellEdited += TreeView_CellEdited;
            _treeView.SelectionChanged += TreeView_SelectionChanged;
            _treeView.ContextMenu.Opening += TreeView_SelectionChanged;
        }

        public TreeGridView TreeView => _treeView;

        public TreeGridItem TreeRoot => _itemRoot;

        private void TreeView_CellEditing(object sender, GridViewCellEventArgs e)
        {
            var item = e.Item as TreeGridItem;
            
            if (item.GetValue(2) is PipelineProject)
                _treeView.CancelEdit();
        }

        private async void TreeView_CellEdited(object sender, GridViewCellEventArgs e)
        {
            var item = e.Item as TreeGridItem;
            var newFileName = item.GetValue(1).ToString();
            var projectItem = item.GetValue(2) as IProjectItem;

            if (projectItem is PipelineProject || projectItem.Name == newFileName)
                return;

            // If original path is not the same as destination path
            // than the item is a link, and now move operation needs to happen
            if (projectItem.OriginalPath == projectItem.DestinationPath)
            {
                var progressDialog = new FileProgressDialog(() =>
                {
                    var location = PipelineController.Instance.GetFullPath(projectItem.Location);
                    var originalPath = Path.Combine(location, projectItem.Name);
                    var newPath = Path.Combine(location, newFileName);

                    if (projectItem is ContentItem)
                        File.Move(originalPath, newPath);
                    else if (projectItem is DirectoryItem)
                        Directory.Move(originalPath, newPath);
                    else
                        throw new Exception("How did this happen?");
                });

                await progressDialog.ShowModalAsync(this);

                if (!progressDialog.IsSuccess)
                {
                    item.SetValue(1, projectItem.Name);
                    _treeView.ReloadData();
                    return;
                }

                projectItem.OriginalPath = projectItem.Location + "/" + newFileName;
            }

            projectItem.DestinationPath = projectItem.Location + "/" + newFileName;
        }

        private void TreeView_SelectionChanged(object sender, EventArgs e)
        {
            var items = new List<IProjectItem>();
            var treeItems = new List<TreeGridItem>();
            var dic = new Dictionary<string, MenuItemCollection>();

            foreach (TreeGridItem selected in _treeView.SelectedItems)
            {
                if (selected.GetValue(2) is IProjectItem item)
                {
                    items.Add(item);
                }

                treeItems.Add(selected);
            }

            Tools.Pipeline.PropertyGrid.Instance.SetObjects(items);

            // Populate context menu

            _treeView.ContextMenu.Items.Clear();

            var lastGroupNum = -1;

            foreach (var item in _commands)
            {
                if (!item.CheckIsActive(items, treeItems))
                    continue;

                // Add menu separator

                var groupIndex = item.Index.groupIndex;
                if (groupIndex > lastGroupNum)
                {
                    if (lastGroupNum != -1)
                        _treeView.ContextMenu.Items.Add(new SeparatorMenuItem());
                    lastGroupNum = groupIndex;
                }

                // Add menu item

                if (!string.IsNullOrWhiteSpace(item.Category))
                {
                    if (!dic.TryGetValue(item.Category, out MenuItemCollection menu))
                    {
                        var buttonMenuItem = new ButtonMenuItem();
                        buttonMenuItem.Text = item.Category;
                        _treeView.ContextMenu.Items.Add(buttonMenuItem);

                        dic[item.Category] = buttonMenuItem.Items;
                    }

                    dic[item.Category].Add(item.CreateMenuItem());
                }
                else
                {
                    _treeView.ContextMenu.Items.Add(item.CreateMenuItem());
                }
            }
        }

        public void Open(PipelineProject project)
        {
            _project = project;

            _itemBase = new TreeGridItem();

            _itemRoot = new TreeGridItem();
            _itemRoot.Expanded = true;
            _itemRoot.SetValue(0, _iconRoot);
            _itemRoot.SetValue(1, project.Name);
            _itemRoot.SetValue(2, project);
            _itemBase.Children.Add(_itemRoot);

            foreach (var contentItem in project.ContentItems)
                AddItem(_itemRoot, contentItem, contentItem.DestinationPath);

            _treeView.DataStore = _itemBase;
            _treeView.ReloadData();
        }

        public TreeGridItem AddItem(TreeGridItem root, IProjectItem projectItem, string filePath)
        {
            var split = filePath.Split('/', '\\');
            var rootItem = root.GetValue(2) as IProjectItem;
            var rootItemPath = rootItem is PipelineProject ? string.Empty : rootItem.OriginalPath;
            var findItem = root.Children.FirstOrDefault(p =>
                p is TreeGridItem pitem && 
                pitem.GetValue(1).ToString() == split[0]
            ) as TreeGridItem;

            if (findItem == null)
            {
                findItem = new TreeGridItem();
                findItem.SetValue(1, split[0]);

                if (split.Length == 1 && projectItem is ContentItem)
                {
                    var originalFilePath = PipelineController.Instance.GetFullPath(projectItem.OriginalPath);
                    var link = projectItem.OriginalPath != projectItem.DestinationPath;

                    findItem.SetValue(0, Global.GetEtoFileIcon(originalFilePath, link));
                    findItem.SetValue(2, projectItem);
                }
                else
                {
                    findItem.SetValue(0, Global.GetEtoDirectoryIcon());
                    findItem.SetValue(2, split.Length == 1 ? projectItem : new DirectoryItem(split[0], rootItemPath));
                }

                root.Children.Add(findItem);
                SortItem(root);
            }

            if (split.Length > 1)
                AddItem(findItem, projectItem, string.Join('/', split, 1, split.Length - 1));

            return findItem;
        }

        public void AddFiles(List<string> filePaths)
        {
            foreach (var filePath in filePaths)
            {
                var relativePath = PipelineController.Instance.GetRelativePath(filePath);

                if (Directory.Exists(filePath))
                {
                    var dirItem = new DirectoryItem(relativePath);

                    AddItem(TreeRoot, dirItem, relativePath);
                }
                else
                {
                    var contentItem = new ContentItem();
                    contentItem.Observer = PipelineController.Instance;
                    contentItem.ProcessorParams = new OpaqueDataDictionary();
                    contentItem.OriginalPath = relativePath;
                    contentItem.DestinationPath = relativePath;
                    contentItem.ResolveTypes();

                    AddItem(TreeRoot, contentItem, relativePath);
                }
            }
        }

        private void SortItem(TreeGridItem item)
        {
            item.Children.Sort((s1, s2) =>
            {
                var s1Item = s1 as TreeGridItem;
                var s2Item = s2 as TreeGridItem;
                var s1Dir = s1Item.GetValue(2) is DirectoryItem;
                var s2Dir = s2Item.GetValue(2) is DirectoryItem;

                if (s1Dir == s2Dir)
                {
                    return s1Item.GetValue(1).ToString().CompareTo(s2Item.GetValue(1).ToString());
                }

                return s2Dir ? 1 : -1;
            });
        }
    }
}
