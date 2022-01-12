﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Linq;
using Artemis.Core;
using Artemis.UI.Shared.Services.ProfileEditor;
using Artemis.UI.Shared.Services.ProfileEditor.Commands;
using Avalonia.Controls.Mixins;
using ReactiveUI;
using ReactiveUI.Validation.Helpers;

namespace Artemis.UI.Shared.Services.PropertyInput;

/// <summary>
///     Represents the base class for a property input view model that is used to edit layer properties
/// </summary>
/// <typeparam name="T">The type of property this input view model supports</typeparam>
public abstract class PropertyInputViewModel<T> : PropertyInputViewModel
{
    private T _dragStartValue;
    private bool _inputDragging;

    [AllowNull] private T _inputValue;

    private TimeSpan _time;

    /// <summary>
    ///     Creates a new instance of the <see cref="PropertyInputViewModel{T}" /> class
    /// </summary>
    protected PropertyInputViewModel(LayerProperty<T> layerProperty, IProfileEditorService profileEditorService, IPropertyInputService propertyInputService)
    {
        LayerProperty = layerProperty;
        ProfileEditorService = profileEditorService;
        PropertyInputService = propertyInputService;

        _inputValue = default!;
        _dragStartValue = default!;

        this.WhenActivated(d =>
        {
            ProfileEditorService.Time.Subscribe(t => _time = t).DisposeWith(d);
            UpdateInputValue();

            Observable.FromEventPattern<LayerPropertyEventArgs>(x => LayerProperty.Updated += x, x => LayerProperty.Updated -= x)
                .Subscribe(_ => UpdateInputValue())
                .DisposeWith(d);
            Observable.FromEventPattern<LayerPropertyEventArgs>(x => LayerProperty.CurrentValueSet += x, x => LayerProperty.CurrentValueSet -= x)
                .Subscribe(_ => UpdateInputValue())
                .DisposeWith(d);
            Observable.FromEventPattern<DataBindingEventArgs>(x => LayerProperty.DataBinding.DataBindingEnabled += x, x => LayerProperty.DataBinding.DataBindingEnabled -= x)
                .Subscribe(_ => UpdateDataBinding())
                .DisposeWith(d);
            Observable.FromEventPattern<DataBindingEventArgs>(x => LayerProperty.DataBinding.DataBindingDisabled += x, x => LayerProperty.DataBinding.DataBindingDisabled -= x)
                .Subscribe(_ => UpdateDataBinding())
                .DisposeWith(d);
        });
    }

    /// <summary>
    ///     Gets the layer property this view model is editing
    /// </summary>
    public LayerProperty<T> LayerProperty { get; }

    /// <summary>
    ///     Gets a boolean indicating whether the layer property should be enabled
    /// </summary>
    public bool IsEnabled => !LayerProperty.HasDataBinding;

    /// <summary>
    ///     Gets the profile editor service
    /// </summary>
    public IProfileEditorService ProfileEditorService { get; }

    /// <summary>
    ///     Gets the property input service
    /// </summary>
    public IPropertyInputService PropertyInputService { get; }

    /// <summary>
    ///     Gets or sets a boolean indicating whether the input is currently being dragged
    ///     <para>
    ///         Only applicable when using something like a <see cref="DraggableFloat" />, see
    ///         <see cref="InputDragStarted" /> and <see cref="InputDragEnded" />
    ///     </para>
    /// </summary>
    public bool InputDragging
    {
        get => _inputDragging;
        private set => this.RaiseAndSetIfChanged(ref _inputDragging, value);
    }

    /// <summary>
    ///     Gets or sets the input value
    /// </summary>
    [MaybeNull]
    public T InputValue
    {
        get => _inputValue;
        set
        {
            this.RaiseAndSetIfChanged(ref _inputValue, value);
            ApplyInputValue();
        }
    }

    internal override object InternalGuard { get; } = new();

    /// <summary>
    ///     Called by the view input drag has started
    ///     <para>
    ///         To use, add the following to DraggableFloat in your xaml: <c>DragStarted="{s:Action InputDragStarted}"</c>
    ///     </para>
    /// </summary>
    public void InputDragStarted(object sender, EventArgs e)
    {
        InputDragging = true;
        _dragStartValue = GetDragStartValue();
    }

    /// <summary>
    ///     Called by the view when input drag has ended
    ///     <para>
    ///         To use, add the following to DraggableFloat in your xaml: <c>DragEnded="{s:Action InputDragEnded}"</c>
    ///     </para>
    /// </summary>
    public void InputDragEnded(object sender, EventArgs e)
    {
        InputDragging = false;
        ProfileEditorService.ExecuteCommand(new UpdateLayerProperty<T>(LayerProperty, _inputValue, _dragStartValue, _time));
    }

    /// <summary>
    ///     Called when the input value has changed
    /// </summary>
    protected virtual void OnInputValueChanged()
    {
    }

    /// <summary>
    ///     Called when data bindings have been enabled or disabled on the layer property
    /// </summary>
    protected virtual void OnDataBindingsChanged()
    {
    }

    /// <summary>
    ///     Called when dragging starts to get the initial value before dragging begun
    /// </summary>
    /// <returns>The initial value before dragging begun</returns>
    protected virtual T? GetDragStartValue()
    {
        return InputValue;
    }

    /// <summary>
    ///     Applies the input value to the layer property using an <see cref="IProfileEditorCommand" />.
    /// </summary>
    protected virtual void ApplyInputValue()
    {
        if (InputDragging)
            ProfileEditorService.ChangeTime(_time);
        else if (ValidationContext.IsValid)
            ProfileEditorService.ExecuteCommand(new UpdateLayerProperty<T>(LayerProperty, _inputValue, _time));
    }

    private void UpdateInputValue()
    {
        // Avoid unnecessary UI updates and validator cycles
        if (_inputValue != null && _inputValue.Equals(LayerProperty.CurrentValue) || _inputValue == null && LayerProperty.CurrentValue == null)
            return;

        // Override the input value
        _inputValue = LayerProperty.CurrentValue;

        // Notify a change in the input value
        OnInputValueChanged();
        this.RaisePropertyChanged(nameof(InputValue));
    }

    private void UpdateDataBinding()
    {
        this.RaisePropertyChanged(nameof(IsEnabled));
        OnDataBindingsChanged();
    }
}

/// <summary>
///     For internal use only, implement <see cref="PropertyInputViewModel" /> instead.
/// </summary>
public abstract class PropertyInputViewModel : ReactiveValidationObject, IActivatableViewModel, IDisposable
{
    /// <summary>
    ///     Prevents this type being implemented directly, implement
    ///     <see cref="PropertyInputViewModel" /> instead.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    internal abstract object InternalGuard { get; }

    /// <summary>
    ///     Releases the unmanaged resources used by the object and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">
    ///     <see langword="true" /> to release both managed and unmanaged resources;
    ///     <see langword="false" /> to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }

    #region Implementation of IActivatableViewModel

    /// <inheritdoc />
    public ViewModelActivator Activator { get; } = new();

    #endregion

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}