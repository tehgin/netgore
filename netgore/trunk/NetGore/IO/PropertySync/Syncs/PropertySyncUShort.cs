using System.Linq;

namespace NetGore.IO.PropertySync
{
    /// <summary>
    /// Implementation of a PropertySyncBase that handles synchronizing an unsigned 16-bit integer.
    /// </summary>
    [PropertySyncHandler(typeof(ushort))]
    public sealed class PropertySyncUShort : PropertySyncBase<ushort>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PropertySyncUShort"/> class.
        /// </summary>
        /// <param name="syncValueAttributeInfo">The <see cref="SyncValueAttributeInfo"/>.</param>
        public PropertySyncUShort(SyncValueAttributeInfo syncValueAttributeInfo) : base(syncValueAttributeInfo)
        {
        }

        /// <summary>
        /// When overridden in the derived class, reads a value with the specified name from an IValueReader.
        /// </summary>
        /// <param name="name">Name of the value.</param>
        /// <param name="reader">IValueReader to read from.</param>
        /// <returns>Value read from the IValueReader.</returns>
        protected override ushort Read(string name, IValueReader reader)
        {
            return reader.ReadUShort(name);
        }

        /// <summary>
        /// When overridden in the derived class, writes a value to an IValueWriter with the specified name.
        /// </summary>
        /// <param name="name">Name of the value.</param>
        /// <param name="writer">IValueWriter to write to.</param>
        /// <param name="value">Value to write.</param>
        protected override void Write(string name, IValueWriter writer, ushort value)
        {
            writer.Write(name, value);
        }
    }
}