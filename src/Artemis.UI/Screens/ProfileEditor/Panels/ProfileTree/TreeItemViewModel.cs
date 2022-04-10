﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Artemis.Core;
using Artemis.Core.Services;
using Artemis.UI.Ninject.Factories;
using Artemis.UI.Shared;
using Artemis.UI.Shared.Services;
using Artemis.UI.Shared.Services.ProfileEditor;
using Artemis.UI.Shared.Services.ProfileEditor.Commands;
using ReactiveUI;

namespace Artemis.UI.Screens.ProfileEditor.ProfileTree;

public abstract class TreeItemViewModel : ActivatableViewModelBase
{
    private readonly IProfileEditorService _profileEditorService;
    private readonly IProfileEditorVmFactory _profileEditorVmFactory;
    private readonly IWindowService _windowService;
    private RenderProfileElement? _currentProfileElement;
    private bool _isExpanded;
    private ProfileElement? _profileElement;
    private string? _renameValue;
    private bool _renaming;

    protected TreeItemViewModel(TreeItemViewModel? parent, ProfileElement? profileElement,
        IWindowService windowService,
        IProfileEditorService profileEditorService,
        IRgbService rgbService,
        ILayerBrushService layerBrushService,
        IProfileEditorVmFactory profileEditorVmFactory)
    {
        _windowService = windowService;
        _profileEditorService = profileEditorService;
        _profileEditorVmFactory = profileEditorVmFactory;

        Parent = parent;
        ProfileElement = profileElement;

        AddLayer = ReactiveCommand.Create(() =>
        {
            if (ProfileElement is Layer targetLayer)
            {
                Layer layer = new(targetLayer.Parent, targetLayer.GetNewLayerName());
                layerBrushService.ApplyDefaultBrush(layer);

                layer.AddLeds(rgbService.EnabledDevices.SelectMany(d => d.Leds));
                profileEditorService.ExecuteCommand(new AddProfileElement(layer, targetLayer.Parent, targetLayer.Order));
            }
            else if (ProfileElement != null)
            {
                Layer layer = new(ProfileElement, ProfileElement.GetNewLayerName());
                layerBrushService.ApplyDefaultBrush(layer);

                layer.AddLeds(rgbService.EnabledDevices.SelectMany(d => d.Leds));
                profileEditorService.ExecuteCommand(new AddProfileElement(layer, ProfileElement, 0));
            }
        });

        AddFolder = ReactiveCommand.Create(() =>
        {
            if (ProfileElement is Layer targetLayer)
                profileEditorService.ExecuteCommand(new AddProfileElement(new Folder(targetLayer.Parent, targetLayer.Parent.GetNewFolderName()), targetLayer.Parent, targetLayer.Order));
            else if (ProfileElement != null)
                profileEditorService.ExecuteCommand(new AddProfileElement(new Folder(ProfileElement, ProfileElement.GetNewFolderName()), ProfileElement, 0));
        });

        Rename = ReactiveCommand.Create(() =>
        {
            Renaming = true;
            RenameValue = ProfileElement?.Name;
        });

        Duplicate = ReactiveCommand.Create(() => throw new NotImplementedException());
        Copy = ReactiveCommand.Create(() => throw new NotImplementedException());
        Paste = ReactiveCommand.Create(() => throw new NotImplementedException());

        Delete = ReactiveCommand.Create(() =>
        {
            if (ProfileElement is RenderProfileElement renderProfileElement)
                profileEditorService.ExecuteCommand(new RemoveProfileElement(renderProfileElement));
        });

        this.WhenActivated(d =>
        {
            _profileEditorService.ProfileElement.Subscribe(element => _currentProfileElement = element).DisposeWith(d);
            SubscribeToProfileElement(d);
            CreateTreeItems();
        });
    }

    public ProfileElement? ProfileElement
    {
        get => _profileElement;
        set => RaiseAndSetIfChanged(ref _profileElement, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public bool Renaming
    {
        get => _renaming;
        set => RaiseAndSetIfChanged(ref _renaming, value);
    }

    public TreeItemViewModel? Parent { get; set; }
    public ObservableCollection<TreeItemViewModel> Children { get; } = new();

    public ReactiveCommand<Unit, Unit> AddLayer { get; }
    public ReactiveCommand<Unit, Unit> AddFolder { get; }
    public ReactiveCommand<Unit, Unit> Rename { get; }
    public ReactiveCommand<Unit, Unit> Duplicate { get; }
    public ReactiveCommand<Unit, Unit> Copy { get; }
    public ReactiveCommand<Unit, Unit> Paste { get; }
    public ReactiveCommand<Unit, Unit> Delete { get; }
    public abstract bool SupportsChildren { get; }

    public string? RenameValue
    {
        get => _renameValue;
        set => RaiseAndSetIfChanged(ref _renameValue, value);
    }

    public async Task ShowBrokenStateExceptions()
    {
        if (ProfileElement == null)
            return;

        List<IBreakableModel> broken = ProfileElement.GetBrokenHierarchy().Where(b => b.BrokenStateException != null).ToList();

        foreach (IBreakableModel current in broken)
        {
            _windowService.ShowExceptionDialog($"{current.BrokenDisplayName} - {current.BrokenState}", current.BrokenStateException!);
            if (broken.Last() != current)
                if (!await _windowService.ShowConfirmContentDialog("Broken state", "Do you want to view the next exception?"))
                    return;
        }
    }

    public void SubmitRename()
    {
        if (ProfileElement == null)
        {
            Renaming = false;
            return;
        }

        _profileEditorService.ExecuteCommand(new RenameProfileElement(ProfileElement, RenameValue));
        Renaming = false;
    }

    public void CancelRename()
    {
        Renaming = false;
    }

    public void InsertElement(TreeItemViewModel elementViewModel, int targetIndex)
    {
        if (elementViewModel.Parent == this && Children.IndexOf(elementViewModel) == targetIndex)
            return;

        if (ProfileElement != null && elementViewModel.ProfileElement != null)
            _profileEditorService.ExecuteCommand(new MoveProfileElement(ProfileElement, elementViewModel.ProfileElement, targetIndex));
    }

    protected void SubscribeToProfileElement(CompositeDisposable d)
    {
        if (ProfileElement == null)
            return;

        Observable.FromEventPattern<ProfileElementEventArgs>(x => ProfileElement.ChildAdded += x, x => ProfileElement.ChildAdded -= x)
            .Subscribe(c => AddTreeItemIfMissing(c.EventArgs.ProfileElement)).DisposeWith(d);
        Observable.FromEventPattern<ProfileElementEventArgs>(x => ProfileElement.ChildRemoved += x, x => ProfileElement.ChildRemoved -= x)
            .Subscribe(c => RemoveTreeItemsIfFound(c.Sender, c.EventArgs.ProfileElement)).DisposeWith(d);
    }

    protected void RemoveTreeItemsIfFound(object? sender, ProfileElement profileElement)
    {
        List<TreeItemViewModel> toRemove = Children.Where(t => t.ProfileElement == profileElement).ToList();
        foreach (TreeItemViewModel treeItemViewModel in toRemove)
            Children.Remove(treeItemViewModel);

        if (_currentProfileElement != profileElement)
            return;

        // Find a good candidate for a new selection, preferring the next sibling, falling back to the previous sibling and finally the parent
        ProfileElement? parent = sender as ProfileElement;
        ProfileElement? newSelection = null;
        if (parent != null)
        {
            newSelection = parent.Children.FirstOrDefault(c => c.Order == profileElement.Order) ?? parent.Children.FirstOrDefault(c => c.Order == profileElement.Order - 1);
            if (newSelection == null && parent is Folder {IsRootFolder: false})
                newSelection = parent;
        }

        _profileEditorService.ChangeCurrentProfileElement(newSelection as RenderProfileElement);
    }

    protected void AddTreeItemIfMissing(ProfileElement profileElement)
    {
        if (Children.Any(t => t.ProfileElement == profileElement))
            return;

        if (profileElement is Folder folder)
            Children.Insert(folder.Parent.Children.IndexOf(folder), _profileEditorVmFactory.FolderTreeItemViewModel(this, folder));
        else if (profileElement is Layer layer)
            Children.Insert(layer.Parent.Children.IndexOf(layer), _profileEditorVmFactory.LayerTreeItemViewModel(this, layer));

        // Select the newly added element
        if (profileElement is RenderProfileElement renderProfileElement)
            _profileEditorService.ChangeCurrentProfileElement(renderProfileElement);
    }

    protected void CreateTreeItems()
    {
        if (Children.Any())
            Children.Clear();

        if (ProfileElement == null)
            return;

        foreach (ProfileElement profileElement in ProfileElement.Children)
        {
            if (profileElement is Folder folder)
                Children.Add(_profileEditorVmFactory.FolderTreeItemViewModel(this, folder));
            else if (profileElement is Layer layer)
                Children.Add(_profileEditorVmFactory.LayerTreeItemViewModel(this, layer));
        }
    }
}