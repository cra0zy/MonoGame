// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Collections.Generic;
using System.IO;
using Eto.Forms;
using MonoGame.Tools.Pipeline;

namespace MonoGame.Content.Builder.Editor.ProjectView
{
    public class AddNewFolderCommand : ProjectExplorerCommand
    {
        public override (int groupIndex, int index) Index => (10, 10);

        public override string Category => "Add";

        public override bool GetIsActive(List<IProjectItem> items)
        {
            return items.Count == 1 && (items[0] is DirectoryItem || items[0] is PipelineProject);
        }

        public override string GetName(List<IProjectItem> items)
        {
            return "Add New Folder...";
        }

        public override async void Clicked(ProjectExplorer projectExplorer, List<TreeGridItem> treeItems, List<IProjectItem> items)
        {
            var dialog = new NewFolderDialog();
            await dialog.ShowModalAsync(projectExplorer);
            
            if (dialog.Result != DialogResult.Ok)
                return;

            var baseRelativePath = items[0] is PipelineProject ? string.Empty : items[0].OriginalPath;
            var dirPath = Path.Combine(PipelineController.Instance.GetFullPath(baseRelativePath), dialog.Text);

            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            Eto.Forms.Application.Instance.Invoke(() =>
            {
                projectExplorer.AddItem(treeItems[0], new DirectoryItem(dialog.Text, baseRelativePath), dialog.Text);
                treeItems[0].Expanded = true;
                projectExplorer.TreeView.ReloadData();
            });
        }
    }
}
