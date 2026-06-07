using System.IO;
using System.Windows.Input;
using GameManager.App.Commands;
using GameManager.App.Models;
using GameManager.App.Services;

namespace GameManager.App.ViewModels;

public sealed class AddGameViewModel : ViewModelBase
{
    private readonly IFilePickerService filePickerService;
    private readonly Action<AddGameRequest> save;
    private readonly RelayCommand saveCommand;
    private string gameName = string.Empty;
    private string executablePath = string.Empty;
    private string gameRootPath = string.Empty;
    private string savePath = string.Empty;
    private string coverImagePath = string.Empty;
    private string launchArguments = string.Empty;
    private bool runAsAdministrator;

    public AddGameViewModel(IFilePickerService filePickerService, Action<AddGameRequest> save, Action cancel)
        : this(filePickerService, save, cancel, null, "保存")
    {
    }

    public AddGameViewModel(
        IFilePickerService filePickerService,
        Action<AddGameRequest> save,
        Action cancel,
        AddGameRequest? initialValues,
        string submitButtonText)
    {
        this.filePickerService = filePickerService;
        this.save = save;
        SubmitButtonText = submitButtonText;

        if (initialValues is not null)
        {
            gameName = initialValues.Name;
            executablePath = initialValues.ExecutablePath;
            gameRootPath = initialValues.GameRootPath;
            savePath = initialValues.SavePath;
            coverImagePath = initialValues.CoverImagePath ?? string.Empty;
            launchArguments = initialValues.LaunchArguments;
            runAsAdministrator = initialValues.RunAsAdministrator;
        }

        BrowseExecutableCommand = new RelayCommand(_ => BrowseExecutable());
        BrowseGameRootFolderCommand = new RelayCommand(_ => BrowseGameRootFolder());
        BrowseSaveFolderCommand = new RelayCommand(_ => BrowseSaveFolder());
        BrowseCoverImageCommand = new RelayCommand(_ => BrowseCoverImage());
        CancelCommand = new RelayCommand(_ => cancel());
        saveCommand = new RelayCommand(_ => Save(), _ => CanSave());
        SaveCommand = saveCommand;
    }

    public string SubmitButtonText { get; }

    public string GameName
    {
        get => gameName;
        set
        {
            if (SetProperty(ref gameName, value))
            {
                saveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ExecutablePath
    {
        get => executablePath;
        set
        {
            if (SetProperty(ref executablePath, value))
            {
                saveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string GameRootPath
    {
        get => gameRootPath;
        set
        {
            if (SetProperty(ref gameRootPath, value))
            {
                saveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SavePath
    {
        get => savePath;
        set
        {
            if (SetProperty(ref savePath, value))
            {
                saveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CoverImagePath
    {
        get => coverImagePath;
        set => SetProperty(ref coverImagePath, value);
    }

    public string LaunchArguments
    {
        get => launchArguments;
        set => SetProperty(ref launchArguments, value);
    }

    public bool RunAsAdministrator
    {
        get => runAsAdministrator;
        set => SetProperty(ref runAsAdministrator, value);
    }

    public ICommand BrowseExecutableCommand { get; }

    public ICommand BrowseGameRootFolderCommand { get; }

    public ICommand BrowseSaveFolderCommand { get; }

    public ICommand BrowseCoverImageCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand SaveCommand { get; }

    private void BrowseExecutable()
    {
        var path = filePickerService.PickExecutableFile();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        ExecutablePath = path;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && string.IsNullOrWhiteSpace(GameRootPath))
        {
            GameRootPath = directory;
        }

        if (string.IsNullOrWhiteSpace(GameName))
        {
            GameName = Path.GetFileNameWithoutExtension(path);
        }
    }

    private void BrowseGameRootFolder()
    {
        var path = filePickerService.PickFolder("选择游戏目录");
        if (!string.IsNullOrWhiteSpace(path))
        {
            GameRootPath = path;
        }
    }

    private void BrowseSaveFolder()
    {
        var path = filePickerService.PickFolder("选择存档目录");
        if (!string.IsNullOrWhiteSpace(path))
        {
            SavePath = path;
        }
    }

    private void BrowseCoverImage()
    {
        var path = filePickerService.PickCoverImage();
        if (!string.IsNullOrWhiteSpace(path))
        {
            CoverImagePath = path;
        }
    }

    private void Save()
    {
        if (!CanSave())
        {
            return;
        }

        save(new AddGameRequest(
            GameName.Trim(),
            ExecutablePath.Trim(),
            GameRootPath.Trim(),
            SavePath.Trim(),
            string.IsNullOrWhiteSpace(CoverImagePath) ? null : CoverImagePath.Trim(),
            LaunchArguments.Trim(),
            RunAsAdministrator));
    }

    private bool CanSave()
    {
        return !string.IsNullOrWhiteSpace(GameName)
            && !string.IsNullOrWhiteSpace(ExecutablePath)
            && !string.IsNullOrWhiteSpace(GameRootPath)
            && !string.IsNullOrWhiteSpace(SavePath);
    }
}
