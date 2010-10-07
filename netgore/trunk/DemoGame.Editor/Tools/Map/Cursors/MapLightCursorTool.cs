﻿using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using DemoGame.Client;
using DemoGame.Editor.Properties;
using log4net;
using NetGore;
using NetGore.Editor;
using NetGore.Editor.EditorTool;
using NetGore.Graphics;
using NetGore.World;
using SFML.Graphics;

namespace DemoGame.Editor
{
    public class MapLightCursorTool : MapCursorToolBase
    {
        const Keys _placeLightKey = Keys.Control;

        /// <summary>
        /// Initializes a new instance of the <see cref="MapLightCursorTool"/> class.
        /// </summary>
        /// <param name="toolManager">The <see cref="ToolManager"/>.</param>
        public MapLightCursorTool(ToolManager toolManager) : base(toolManager, CreateSettings())
        {
            ToolBarControl.ControlSettings.AsSplitButtonSettings().ClickToEnable = true;
        }

        /// <summary>
        /// Gets the selectable object currently under the cursor.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="worldPos">The world position.</param>
        /// <returns>The selectable object currently under the cursor, or null if none.</returns>
        protected override object GetObjUnderCursor(Map map, Vector2 worldPos)
        {
            var closestLight = map.Lights.MinElementOrDefault(x => worldPos.QuickDistance(x.Center));
            if (closestLight == null)
                return null;

            if (worldPos.QuickDistance(closestLight.Center) > 10)
                return null;

            return closestLight;
        }

        /// <summary>
        /// Gets the map objects to select in the given region.
        /// </summary>
        /// <param name="map">The <see cref="Map"/>.</param>
        /// <param name="selectionArea">The selection box area.</param>
        /// <returns>The objects to select.</returns>
        protected override System.Collections.Generic.IEnumerable<object> CursorSelectObjects(Map map, Rectangle selectionArea)
        {
            return map.Lights.Where(x => x.Intersects(selectionArea)).Cast<object>();
        }

        /// <summary>
        /// When overridden in the derived class, gets if this cursor can select the given object.
        /// </summary>
        /// <param name="obj">The object to try to select.</param>
        /// <returns>True if the <paramref name="obj"/> can be selected and handled by this cursor; otherwise false.</returns>
        protected override bool CanSelect(object obj)
        {
            return (obj is ILight);
        }

        /// <summary>
        /// Property to access the <see cref="SelectedObjectsManager{T}"/>. Provided purely for convenience.
        /// </summary>
        static SelectedObjectsManager<object> SOM
        {
            get { return GlobalState.Instance.Map.SelectedObjsManager; }
        }

        /// <summary>
        /// Creates the <see cref="ToolSettings"/> to use for instantiating this class.
        /// </summary>
        /// <returns>The <see cref="ToolSettings"/>.</returns>
        static ToolSettings CreateSettings()
        {
            return new ToolSettings("Map Light Cursor")
            {
                OnToolBarByDefault = true,
                ToolBarControlType = ToolBarControlType.SplitButton,
                DisabledImage = Resources.MapLightCursorTool_Disabled,
                EnabledImage = Resources.MapLightCursorTool_Enabled,
            };
        }

        /// <summary>
        /// Handles when a mouse button is pressed on a map.
        /// </summary>
        /// <param name="sender">The <see cref="IToolTargetMapContainer"/> the event came from. Cannot be null.</param>
        /// <param name="map">The <see cref="Map"/>. Cannot be null.</param>
        /// <param name="camera">The <see cref="ICamera2D"/>. Cannot be null.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data. Cannot be null.</param>
        protected override void MapContainer_MouseDown(IToolTargetMapContainer sender, Map map, ICamera2D camera, MouseEventArgs e)
        {
            base.MapContainer_MouseDown(sender, map, camera, e);

            if (IsSelecting)
                return;

            if (e.Button == MouseButtons.Left)
            {
                // Left-click

                if ((Control.ModifierKeys & _placeLightKey) != 0)
                {
                    // Place light

                    var msc = MapScreenControl.TryFindInstance(map);
                    if (msc != null)
                    {
                        var dm = msc.DrawingManager;
                        if (dm != null)
                        {
                            var pos = camera.ToWorld(e.Position());
                            var light = new Light { Center = pos, IsEnabled = true, Tag = map };

                            map.AddLight(light);
                            dm.LightManager.Add(light);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// When overridden in the derived class, handles performing drawing before the GUI for a <see cref="IDrawableMap"/> has been draw.
        /// </summary>
        /// <param name="spriteBatch">The <see cref="ISpriteBatch"/> to use to draw.</param>
        /// <param name="imap">The <see cref="IDrawableMap"/> being drawn.</param>
        protected override void HandleBeforeDrawMapGUI(ISpriteBatch spriteBatch, IDrawableMap imap)
        {
            base.HandleBeforeDrawMapGUI(spriteBatch, imap);

            if (IsSelecting)
                return;

            if ((Control.ModifierKeys & _placeLightKey) != 0)
            {
                if (imap == _mouseOverMap)
                {
                    var lightSprite = SystemSprites.Lightblub;
                    lightSprite.Draw(spriteBatch, _mousePos - (lightSprite.Size / 2f));
                }
            }
        }

        Map _mouseOverMap;
        Vector2 _mousePos;

        /// <summary>
        /// Handles when the mouse moves over a map.
        /// </summary>
        /// <param name="sender">The <see cref="IToolTargetMapContainer"/> the event came from. Cannot be null.</param>
        /// <param name="map">The <see cref="Map"/>. Cannot be null.</param>
        /// <param name="camera">The <see cref="ICamera2D"/>. Cannot be null.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data. Cannot be null.</param>
        protected override void MapContainer_MouseMove(IToolTargetMapContainer sender, Map map, ICamera2D camera, MouseEventArgs e)
        {
            base.MapContainer_MouseMove(sender, map, camera, e);

            var cursorPos = e.Position();

            _mouseOverMap = map;
            _mousePos = cursorPos;
        }

        /// <summary>
        /// Handles when a key is raised on a map.
        /// </summary>
        /// <param name="sender">The <see cref="IToolTargetMapContainer"/> the event came from. Cannot be null.</param>
        /// <param name="map">The <see cref="Map"/>. Cannot be null.</param>
        /// <param name="camera">The <see cref="ICamera2D"/>. Cannot be null.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data. Cannot be null.</param>
        protected override void MapContainer_KeyUp(IToolTargetMapContainer sender, Map map, ICamera2D camera, KeyEventArgs e)
        {
            // Handle deletes
            if (e.KeyCode == Keys.Delete)
            {
                // Only delete when it is an Entity that is on this map
                foreach (var x in SOM.SelectedObjects.OfType<ILight>())
                {
                    if (map.Lights.Contains(x) )
                        map.RemoveLight(x);
                }
            }

            base.MapContainer_KeyUp(sender, map, camera, e);
        }
    }
}