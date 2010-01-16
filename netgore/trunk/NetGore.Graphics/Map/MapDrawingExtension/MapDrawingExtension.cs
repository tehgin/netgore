using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using NetGore.Graphics;

namespace DemoGame.Client
{
    /// <summary>
    /// Base class for implementing a <see cref="IMapDrawingExtension"/>.
    /// </summary>
    public abstract class MapDrawingExtension : IMapDrawingExtension
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MapDrawingExtension"/> class.
        /// </summary>
        protected MapDrawingExtension()
        {
            Enabled = true;
        }

        /// <summary>
        /// When overridden in the derived class, handles drawing to the map after the given <paramref name="layer"/> is drawn.
        /// </summary>
        /// <param name="map">The map the drawing is taking place on.</param>
        /// <param name="layer">The layer that was just drawn.</param>
        /// <param name="spriteBatch">The <see cref="SpriteBatch"/> to draw to.</param>
        protected virtual void HandleDrawAfterLayer(IDrawableMap map, MapRenderLayer layer, SpriteBatch spriteBatch)
        {
        }

        /// <summary>
        /// When overridden in the derived class, handles drawing to the map before the given <paramref name="layer"/> is drawn.
        /// </summary>
        /// <param name="map">The map the drawing is taking place on.</param>
        /// <param name="layer">The layer that is going to be drawn.</param>
        /// <param name="spriteBatch">The <see cref="SpriteBatch"/> to draw to.</param>
        protected virtual void HandleDrawBeforeLayer(IDrawableMap map, MapRenderLayer layer, SpriteBatch spriteBatch)
        {
        }

        #region IMapDrawingExtension Members

        /// <summary>
        /// Gets or sets if this <see cref="IMapDrawingExtension"/> will perform the drawing.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Handles drawing to the map before the given <paramref name="layer"/> is drawn.
        /// </summary>
        /// <param name="map">The map the drawing is taking place on.</param>
        /// <param name="layer">The layer that is going to be drawn.</param>
        /// <param name="spriteBatch">The <see cref="SpriteBatch"/> to draw to.</param>
        public void DrawBeforeLayer(IDrawableMap map, MapRenderLayer layer, SpriteBatch spriteBatch)
        {
            if (!Enabled)
                return;

            HandleDrawBeforeLayer(map, layer, spriteBatch);
        }

        /// <summary>
        /// Handles drawing to the map after the given <paramref name="layer"/> is drawn.
        /// </summary>
        /// <param name="map">The map the drawing is taking place on.</param>
        /// <param name="layer">The layer that was just drawn.</param>
        /// <param name="spriteBatch">The <see cref="SpriteBatch"/> to draw to.</param>
        public void DrawAfterLayer(IDrawableMap map, MapRenderLayer layer, SpriteBatch spriteBatch)
        {
            if (!Enabled)
                return;

            HandleDrawAfterLayer(map, layer, spriteBatch);
        }

        #endregion
    }
}