using System;
using System.Collections.Generic;
using System.Linq;
using Artemis.Core.LayerEffects;
using Artemis.Storage.Entities.Profile;
using Artemis.Storage.Entities.Profile.Abstract;
using Newtonsoft.Json;
using SkiaSharp;

namespace Artemis.Core
{
    /// <summary>
    ///     Represents a folder in a <see cref="Profile" />
    /// </summary>
    public sealed class Folder : RenderProfileElement
    {
        /// <summary>
        ///     Creates a new instance of the <see cref="Folder" /> class and adds itself to the child collection of the provided
        ///     <paramref name="parent" />
        /// </summary>
        /// <param name="parent">The parent of the folder</param>
        /// <param name="name">The name of the folder</param>
        public Folder(ProfileElement parent, string name)
        {
            FolderEntity = new FolderEntity();
            EntityId = Guid.NewGuid();

            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Profile = Parent.Profile;
            Name = name;
            Enabled = true;
            Renderer = new Renderer();

            _layerEffects = new List<BaseLayerEffect>();
            _expandedPropertyGroups = new List<string>();

            Parent.AddChild(this);
        }

        internal Folder(Profile profile, ProfileElement parent, FolderEntity folderEntity)
        {
            FolderEntity = folderEntity;
            EntityId = folderEntity.Id;

            Profile = profile;
            Parent = parent;
            Name = folderEntity.Name;
            Enabled = folderEntity.Enabled;
            Order = folderEntity.Order;
            Renderer = new Renderer();

            _layerEffects = new List<BaseLayerEffect>();
            _expandedPropertyGroups = new List<string>();

            Load();
        }

        /// <summary>
        ///     Gets a boolean indicating whether this folder is at the root of the profile tree
        /// </summary>
        public bool IsRootFolder => Parent == Profile;

        /// <summary>
        ///     Gets the longest timeline of all this folders children
        /// </summary>
        public Timeline LongestChildTimeline { get; private set; }

        internal FolderEntity FolderEntity { get; set; }

        internal override RenderElementEntity RenderElementEntity => FolderEntity;

        internal Renderer Renderer { get; }

        /// <inheritdoc />
        public override List<ILayerProperty> GetAllLayerProperties()
        {
            List<ILayerProperty> result = new List<ILayerProperty>();
            foreach (BaseLayerEffect layerEffect in LayerEffects)
                if (layerEffect.BaseProperties != null)
                    result.AddRange(layerEffect.BaseProperties.GetAllLayerProperties());

            return result;
        }

        public override void Update(double deltaTime)
        {
            if (Disposed)
                throw new ObjectDisposedException("Folder");

            if (!Enabled)
                return;

            UpdateDisplayCondition();
            UpdateTimeline(deltaTime);

            foreach (ProfileElement child in Children)
                child.Update(deltaTime);
        }

        /// <inheritdoc />
        public override void Reset()
        {
            DisplayConditionMet = false;
            Timeline.JumpToStart();

            foreach (ProfileElement child in Children)
                child.Reset();
        }

        /// <inheritdoc />
        public override void AddChild(ProfileElement child, int? order = null)
        {
            if (Disposed)
                throw new ObjectDisposedException("Folder");

            base.AddChild(child, order);
            CalculateRenderProperties();
        }

        /// <inheritdoc />
        public override void RemoveChild(ProfileElement child)
        {
            if (Disposed)
                throw new ObjectDisposedException("Folder");

            base.RemoveChild(child);
            CalculateRenderProperties();
        }

        /// <summary>
        ///     Creates a deep copy of the layer
        /// </summary>
        /// <returns>The newly created copy</returns>
        public Folder CreateCopy()
        {
            FolderEntity entityCopy = JsonConvert.DeserializeObject<FolderEntity>(JsonConvert.SerializeObject(FolderEntity));
            entityCopy.Id = Guid.NewGuid();

            return new Folder(Profile, Parent, entityCopy);
        }

        public override string ToString()
        {
            return $"[Folder] {nameof(Name)}: {Name}, {nameof(Order)}: {Order}";
        }

        public void CalculateRenderProperties()
        {
            if (Disposed)
                throw new ObjectDisposedException("Folder");

            SKPath path = new SKPath {FillType = SKPathFillType.Winding};
            foreach (ProfileElement child in Children)
                if (child is RenderProfileElement effectChild && effectChild.Path != null)
                    path.AddPath(effectChild.Path);

            Path = path;

            // Folder render properties are based on child paths and thus require an update
            if (Parent is Folder folder)
                folder.CalculateRenderProperties();

            OnRenderPropertiesUpdated();
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;

            foreach (ProfileElement profileElement in Children)
                profileElement.Dispose();
            Renderer.Dispose();

            base.Dispose(disposing);
        }

        internal override void Load()
        {
            _expandedPropertyGroups.AddRange(FolderEntity.ExpandedPropertyGroups);

            // Load child folders
            foreach (FolderEntity childFolder in Profile.ProfileEntity.Folders.Where(f => f.ParentId == EntityId))
                ChildrenList.Add(new Folder(Profile, this, childFolder));
            // Load child layers
            foreach (LayerEntity childLayer in Profile.ProfileEntity.Layers.Where(f => f.ParentId == EntityId))
                ChildrenList.Add(new Layer(Profile, this, childLayer));

            // Ensure order integrity, should be unnecessary but no one is perfect specially me
            ChildrenList = ChildrenList.OrderBy(c => c.Order).ToList();
            for (int index = 0; index < ChildrenList.Count; index++)
                ChildrenList[index].Order = index + 1;

            LoadRenderElement();
        }

        internal override void Save()
        {
            if (Disposed)
                throw new ObjectDisposedException("Folder");

            FolderEntity.Id = EntityId;
            FolderEntity.ParentId = Parent?.EntityId ?? new Guid();

            FolderEntity.Order = Order;
            FolderEntity.Name = Name;
            FolderEntity.Enabled = Enabled;

            FolderEntity.ProfileId = Profile.EntityId;
            FolderEntity.ExpandedPropertyGroups.Clear();
            FolderEntity.ExpandedPropertyGroups.AddRange(_expandedPropertyGroups);

            SaveRenderElement();
        }

        #region Rendering

        public override void Render(SKCanvas canvas)
        {
            if (Disposed)
                throw new ObjectDisposedException("Folder");

            // Ensure the folder is ready
            if (!Enabled || !Children.Any(c => c.Enabled) || Path == null)
                return;

            // No point rendering if none of the children are going to render
            if (!Children.Any(c => c is RenderProfileElement renderElement && !renderElement.Timeline.IsFinished))
                return;

            lock (Timeline)
            {

                foreach (BaseLayerEffect baseLayerEffect in LayerEffects.Where(e => e.Enabled))
                {
                    baseLayerEffect.BaseProperties?.Update(Timeline);
                    baseLayerEffect.Update(Timeline.Delta.TotalSeconds);
                }
                
                try
                {
                    canvas.Save();
                    Renderer.Open(Path, Parent as Folder);
                    
                    if (Renderer.Canvas == null || Renderer.Path == null || Renderer.Paint == null)
                        throw new ArtemisCoreException("Failed to open folder render context");

                    foreach (BaseLayerEffect baseLayerEffect in LayerEffects.Where(e => e.Enabled))
                        baseLayerEffect.PreProcess(Renderer.Canvas, Renderer.Path, Renderer.Paint);

                    // If required, apply the opacity override of the module to the root folder
                    if (IsRootFolder && Profile.Module.OpacityOverride < 1)
                    {
                        double multiplier = Easings.SineEaseInOut(Profile.Module.OpacityOverride);
                        Renderer.Paint.Color = Renderer.Paint.Color.WithAlpha((byte)(Renderer.Paint.Color.Alpha * multiplier));
                    }

                    // No point rendering if the alpha was set to zero by one of the effects
                    if (Renderer.Paint.Color.Alpha == 0)
                        return;

                    // Iterate the children in reverse because the first layer must be rendered last to end up on top
                    for (int index = Children.Count - 1; index > -1; index--) 
                        Children[index].Render(Renderer.Canvas);

                    foreach (BaseLayerEffect baseLayerEffect in LayerEffects.Where(e => e.Enabled))
                        baseLayerEffect.PostProcess(Renderer.Canvas, Renderer.Path, Renderer.Paint);

                    canvas.DrawBitmap(Renderer.Bitmap, Renderer.TargetLocation, Renderer.Paint);
                }
                finally
                {
                    canvas.Restore();
                    Renderer.Close();
                }

                Timeline.ClearDelta();
            }
        }

        #endregion

        #region Events

        public event EventHandler RenderPropertiesUpdated;

        private void OnRenderPropertiesUpdated()
        {
            RenderPropertiesUpdated?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}