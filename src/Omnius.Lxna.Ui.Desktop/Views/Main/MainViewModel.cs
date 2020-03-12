using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Lxna.Gui.Desktop.Core.Contents;
using Lxna.Gui.Desktop.Models;
using Omnius.Core;
using Omnius.Core.Extensions;
using Omnius.Core.Network;
using Omnius.Lxna.Service;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;

namespace Omnius.Lxna.Ui.Desktop.Views.Main
{
    public sealed class MainViewModel : AsyncDisposableBase
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly IThumbnailGenerator _thumbnailGenerator;

        private Task? _loadTask;
        private Task? _rotateTask;
        private CancellationTokenSource? _cancellationTokenSource;

        private readonly ObservableCollection<DirectoryModel> _rootDirectoryModels = new ObservableCollection<DirectoryModel>();
        private readonly ObservableCollection<ItemModel> _currentItemModels = new ObservableCollection<ItemModel>();
        private readonly ConcurrentDictionary<ItemViewModel, int> _shownItemViewModelMap = new ConcurrentDictionary<ItemViewModel, int>();

        private readonly CompositeDisposable _disposable = new CompositeDisposable();

        private readonly AsyncLock _asyncLock = new AsyncLock();

        public MainViewModel(IThumbnailGenerator thumbnailGenerator)
        {
            _thumbnailGenerator = thumbnailGenerator;

            this.RootDirectories = _rootDirectoryModels.ToReadOnlyReactiveCollection(n => new DirectoryViewModel(null, n)).AddTo(_disposable);
            this.SelectedDirectory = new ReactiveProperty<DirectoryViewModel>().AddTo(_disposable);
            this.SelectedDirectory.Subscribe(n => { if (n != null) { this.TreeView_SelectionChanged(n); } }).AddTo(_disposable);
            this.CurrentItems = _currentItemModels.ToReadOnlyReactiveCollection(n => new ItemViewModel(n)).AddTo(_disposable);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (var drive in Directory.GetLogicalDrives())
                {
                    if (!OmniPath.Windows.TryEncoding(drive, out var omniPath))
                    {
                        continue;
                    }

                    var model = new DirectoryModel(omniPath);
                    _rootDirectoryModels.Add(model);
                }
            }
        }

        protected override async ValueTask OnDisposeAsync()
        {
            using (await _asyncLock.LockAsync())
            {
                _cancellationTokenSource?.Cancel();
                if (_loadTask != null) await _loadTask;
                if (_rotateTask != null) await _rotateTask;

                _disposable.Dispose();
            }
        }

        public ReadOnlyReactiveCollection<DirectoryViewModel> RootDirectories { get; }
        public ReactiveProperty<DirectoryViewModel> SelectedDirectory { get; }
        public ReadOnlyReactiveCollection<ItemViewModel> CurrentItems { get; }

        public void NotifyDoubleTapped(object item)
        {
            var path = ((ItemViewModel)item).Model.Path.ToCurrentPlatformPath();

            var process = new Process();
            process.StartInfo.FileName = path;
            process.StartInfo.UseShellExecute = true;
            process.Start();
        }

        public void NotifyItemPrepared(object item, int index)
        {
            _shownItemViewModelMap[(ItemViewModel)item] = index;
        }

        public void NotifyItemIndexChanged(object item, int oldIndex, int newIndex)
        {
            _shownItemViewModelMap[(ItemViewModel)item] = newIndex;
        }

        public void NotifyItemClearing(object item)
        {
            _shownItemViewModelMap.TryRemove((ItemViewModel)item, out var _);
        }

        private async Task LoadThread(CancellationToken cancellationToken = default)
        {
            try
            {
                var itemViewModels = new List<ItemViewModel>();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    itemViewModels.AddRange(this.CurrentItems);
                });

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (itemViewModels.Count == 0)
                    {
                        return;
                    }

                    var targetItemViewModel = itemViewModels
                        .OrderBy(n => _shownItemViewModelMap.ContainsKey(n) ? 0 : 1)
                        .Where(n => n.Thumbnail.Value == null)
                        .First();

                    var options = new ThumbnailGeneratorGetThumbnailOptions(256, 256, ThumbnailFormatType.Png, ThumbnailResizeType.Pad, TimeSpan.FromSeconds(5), 30);
                    var result = await _thumbnailGenerator.GetThumbnailAsync(targetItemViewModel.Model.Path, options, cancellationToken);

                    if (result.Status == ThumbnailGeneratorResultStatus.Succeeded)
                    {
                        await targetItemViewModel.Model.SetThumbnailAsync(result.Contents);
                    }

                    foreach (var content in result.Contents)
                    {
                        content.Dispose();
                    }

                    itemViewModels.Remove(targetItemViewModel);
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }

        private async Task RotateThread(CancellationToken cancellationToken = default)
        {
            try
            {
                var itemViewModels = new List<ItemViewModel>();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    itemViewModels.AddRange(this.CurrentItems);
                });

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken);

                    foreach (var viewModel in itemViewModels)
                    {
                        await viewModel.Model.RotateThumbnailAsync();
                    }
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }

        private void TreeView_SelectionChanged(DirectoryViewModel selectedDirectory)
        {
            this.RefreshTree(selectedDirectory);
        }

        private async void RefreshTree(DirectoryViewModel selectedDirectory)
        {
            using (await _asyncLock.LockAsync())
            {
                // 古い描画タスクを終了する
                {
                    _cancellationTokenSource?.Cancel();
                    if (_loadTask != null) await _loadTask;
                    if (_rotateTask != null) await _rotateTask;
                }

                try
                {
                    var oldModels = _currentItemModels.ToArray();
                    _currentItemModels.Clear();

                    foreach (var model in oldModels)
                    {
                        model.Dispose();
                    }

                    var tempList = Directory.GetFiles(selectedDirectory.Model.Path.ToCurrentPlatformPath()).ToList();
                    tempList.Sort();

                    foreach (var filePath in tempList)
                    {
                        _currentItemModels.Add(new ItemModel(OmniPath.FromCurrentPlatformPath(filePath)));
                    }
                }
                catch (UnauthorizedAccessException)
                {

                }
                catch (Exception e)
                {
                    _logger.Error(e);
                }

                // 新しい描画タスクを開始する
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    _loadTask = this.LoadThread(_cancellationTokenSource.Token);
                    _rotateTask = this.RotateThread(_cancellationTokenSource.Token);
                }
            }
        }
    }
}

