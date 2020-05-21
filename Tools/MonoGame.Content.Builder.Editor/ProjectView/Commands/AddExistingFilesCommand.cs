// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eto.Forms;
using MonoGame.Tools.Pipeline;

namespace MonoGame.Content.Builder.Editor.ProjectView
{
    public class AddExistingFilesCommand : ProjectExplorerCommand
    {
        public override (int groupIndex, int index) Index => (10, 20);

        public override string Category => "Add";

        public override bool GetIsActive(List<IProjectItem> items)
        {
            return items.Count == 1 && (items[0] is DirectoryItem || items[0] is PipelineProject);
        }

        public override string GetName(List<IProjectItem> items)
        {
            return "Add Existing Files...";
        }

        public override async void Clicked(ProjectExplorer projectExplorer, List<TreeGridItem> treeItems, List<IProjectItem> items)
        {
            var basePath = items[0] is PipelineProject ? string.Empty : items[0].OriginalPath;
            var allFileFilter = new FileFilter("All Files (*.*)", new[] { ".*" });

            var dialog = new OpenFileDialog();
            dialog.Directory = new Uri(PipelineController.Instance.GetFullPath(basePath));
            dialog.MultiSelect = true;
            dialog.Filters.Add(allFileFilter);
            dialog.CurrentFilter = allFileFilter;

            if (dialog.ShowDialog(projectExplorer) != DialogResult.Ok)
                return;

            var filePaths = new List<string>();
            var isInProjectFolder = !PipelineController.Instance.GetRelativePath(dialog.Filenames.ElementAt(0)).Contains("..");

            if (isInProjectFolder)
            {
                filePaths = dialog.Filenames.ToList();
            }
            else
            {
                var progressDialog = new FileProgressDialog(() =>
                {
                    foreach (var filePath in dialog.Filenames)
                    {
                        var relativeDestPath = Path.Combine(basePath, Path.GetFileName(filePath));
                        var destPath = PipelineController.Instance.GetFullPath(relativeDestPath);

                        filePaths.Add(destPath);
                        File.Copy(filePath, destPath);
                    }
                });
                await progressDialog.ShowModalAsync(projectExplorer);

                if (!progressDialog.IsSuccess)
                    return;
            }

            projectExplorer.AddFiles(filePaths);
            projectExplorer.TreeView.ReloadData();
        }
    }
}
