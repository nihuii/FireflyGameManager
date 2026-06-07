using System.IO;
using Microsoft.Win32;

namespace GameManager.App.Services;

public sealed class WpfFilePickerService : IFilePickerService
{
    public string? PickExecutableFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择游戏启动文件",
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PickCoverImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择游戏封面图片",
            Filter = "图片文件 (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickWallpaperImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择背景壁纸",
            Filter = "图片文件 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSaveBackupFile(string initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择存档备份文件",
            Filter = "ZIP 备份文件 (*.zip)|*.zip|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        if (Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickExportArchivePath()
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出 Firefly 本地数据",
            Filter = "ZIP 数据包 (*.zip)|*.zip",
            AddExtension = true,
            DefaultExt = ".zip",
            FileName = $"FireflyGameManager-{DateTime.Now:yyyyMMdd-HHmm}.zip"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickImportArchiveFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入 Firefly 本地数据",
            Filter = "ZIP 数据包 (*.zip)|*.zip",
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
