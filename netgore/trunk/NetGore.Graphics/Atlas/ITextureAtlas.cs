using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NetGore;

namespace NetGore.Graphics
{
    /// <summary>
    /// Interface for an object that can be set to an atlas
    /// </summary>
    public interface ITextureAtlas
    {
        /// <summary>
        /// Gets the texture source <see cref="Rectangle"/> of the original image.
        /// </summary>
        Rectangle SourceRect { get; }

        /// <summary>
        /// Gets the original texture.
        /// </summary>
        Texture2D Texture { get; }

        /// <summary>
        /// Sets the atlas information.
        /// </summary>
        /// <param name="texture">The atlas texture.</param>
        /// <param name="atlasSourceRect">The source <see cref="Rectangle"/> for the image in the atlas texture.</param>
        void SetAtlas(Texture2D texture, Rectangle atlasSourceRect);

        /// <summary>
        /// Removes the atlas from the object and forces it to draw normally.
        /// </summary>
        void RemoveAtlas();
    }
}